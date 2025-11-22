using DailyMealPlannerExtended.Models;
using Microsoft.Data.Sqlite;
using Fastenshtein;
using System.Text.Json;

namespace DailyMealPlannerExtended.Services;

public class DatabaseService : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public int PageSize { get; set; } = 50;

    public DatabaseService()
    {
        // Get platform-agnostic app data location
        var appDataDir = GetAppDataDirectory();
        var dbPath = Path.Combine(appDataDir, "opennutrition_foods.db");
        _connectionString = $"Data Source={dbPath}";

        if (!File.Exists(dbPath))
        {
            Logger.Instance.Error("Database file not found at: {DbPath}", dbPath);
        }
    }

    private static string GetAppDataDirectory()
    {
        // Match the Python platformdirs pattern
        var appName = "DailyMealPlannerExtended";
        var appAuthor = "DailyMealPlanner";

        string baseDir;
        if (OperatingSystem.IsWindows())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support"
            );
        }
        else // Linux
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = string.IsNullOrEmpty(xdgDataHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : xdgDataHome;
        }

        return Path.Combine(baseDir, appAuthor, appName);
    }

    private async Task<SqliteConnection> GetConnectionAsync()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync();
        }
        else if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }
        return _connection;
    }

    public async Task<(List<Product> Products, int TotalCount)> SearchProductsAsync(
        string searchText = "",
        string? type = null,
        List<string>? labels = null,
        int page = 0)
    {
        var connection = await GetConnectionAsync();
        var products = new List<Product>();

        // Build the WHERE clause
        var whereClauses = new List<string> { "1=1" };
        var parameters = new List<SqliteParameter>();

        // Add search filter - only match name or ingredients
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            whereClauses.Add("(name LIKE @search OR ingredients LIKE @search OR alternate_names LIKE @search)");
            parameters.Add(new SqliteParameter("@search", $"%{searchText}%"));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            whereClauses.Add("type = @type");
            parameters.Add(new SqliteParameter("@type", type));
        }

        if (labels != null && labels.Count > 0)
        {
            var labelConditions = new List<string>();
            for (int i = 0; i < labels.Count; i++)
            {
                labelConditions.Add($"labels LIKE @label{i}");
                parameters.Add(new SqliteParameter($"@label{i}", $"%{labels[i]}%"));
            }
            whereClauses.Add($"({string.Join(" OR ", labelConditions)})");
        }

        var whereClause = string.Join(" AND ", whereClauses);

        // Get total count
        var countQuery = $"SELECT COUNT(*) FROM opennutrition_foods WHERE {whereClause}";
        using var countCommand = new SqliteCommand(countQuery, connection);
        countCommand.Parameters.AddRange(parameters.ToArray());
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        // When searching, fetch ALL matching products for relevance sorting
        // Otherwise, use pagination normally
        var query = string.IsNullOrWhiteSpace(searchText)
            ? $@"SELECT id, name, alternate_names, description, type, serving, nutrition_100g, labels, ingredients
                 FROM opennutrition_foods
                 WHERE {whereClause}
                 LIMIT {PageSize} OFFSET {page * PageSize}"
            : $@"SELECT id, name, alternate_names, description, type, serving, nutrition_100g, labels, ingredients
                 FROM opennutrition_foods
                 WHERE {whereClause}";

        using var command = new SqliteCommand(query, connection);
        // Re-create parameters for the second query
        var queryParameters = new List<SqliteParameter>();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            queryParameters.Add(new SqliteParameter("@search", $"%{searchText}%"));
        }
        if (!string.IsNullOrWhiteSpace(type))
        {
            queryParameters.Add(new SqliteParameter("@type", type));
        }
        if (labels != null && labels.Count > 0)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                queryParameters.Add(new SqliteParameter($"@label{i}", $"%{labels[i]}%"));
            }
        }
        command.Parameters.AddRange(queryParameters.ToArray());

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var product = ParseProduct(reader);
            if (product != null)
            {
                products.Add(product);
            }
        }

        // Sort by relevance if search text provided
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            products = SortByRelevance(products, searchText);
            // When searching, return the count of matched products, not total DB count
            var searchResultCount = products.Count;
            // Apply pagination after sorting
            products = products.Skip(page * PageSize).Take(PageSize).ToList();
            return (products, searchResultCount);
        }

        return (products, totalCount);
    }

    private Product? ParseProduct(SqliteDataReader reader)
    {
        try
        {
            var nutritionJson = reader.IsDBNull(6) ? "{}" : reader.GetString(6);
            var nutrition = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(nutritionJson)
                ?? new Dictionary<string, JsonElement>();

            var (q, u) = ParseServing(reader.IsDBNull(5) ? null : reader.GetString(5));
            var product = new Product
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                AltNames = ParseJsonStringList(reader.IsDBNull(2) ? null : reader.GetString(2)),
                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Category = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Serving = q,
                Unit = u,
                Labels = ParseJsonStringList(reader.IsDBNull(7) ? null : reader.GetString(7)),
                Ingredients = ParseIngredients(reader.IsDBNull(8) ? null : reader.GetString(8)),

                // Parse nutritional values
                Calories = GetNutritionValue(nutrition, "calories"),
                Protein = GetNutritionValue(nutrition, "protein"),
                TotalFat = GetNutritionValue(nutrition, "total_fat"),
                Carbohydrates = GetNutritionValue(nutrition, "carbohydrates"),
                Sodium = GetNutritionValue(nutrition, "sodium"),
                Fiber = GetNutritionValue(nutrition, "dietary_fiber"),
                Sugar = GetNutritionValue(nutrition, "total_sugars")
            };

            return product;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Error parsing product");
            return null;
        }
    }

    private double GetNutritionValue(Dictionary<string, JsonElement> nutrition, string key)
    {
        if (nutrition.TryGetValue(key, out var element))
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetDouble();
            }
        }
        return 0.0;
    }

    private (double q, ServingUnit u) ParseServing(string? servingJson)
    {
        if (string.IsNullOrWhiteSpace(servingJson))
            return (100.0, ServingUnit.g);

        try
        {
            var serving = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(servingJson);
            double q = 0.0;
            if (serving != null && serving.TryGetValue("metric", out var metric))
            {
                if (metric.ValueKind == JsonValueKind.Object)
                {
                    var metricObj = metric.Deserialize<Dictionary<string, JsonElement>>();
                    if (metricObj != null && metricObj.TryGetValue("quantity", out var quantity))
                    {
                        if (quantity.ValueKind == JsonValueKind.Number)
                        {
                            q = quantity.GetDouble();
                        }
                    }

                    if (metricObj != null && metricObj.TryGetValue("unit", out var unit))
                    {
                        if (unit.ValueKind == JsonValueKind.String)
                        {
                            return unit.GetString() == "g" ? (q, ServingUnit.g) : (q, ServingUnit.ml);
                        }
                    }
                }
            }
        }
        catch
        {
            // Fall back to default
        }

        return (100.0, ServingUnit.g);
    }

    private List<string> ParseJsonStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            var preview = json != null ? json.Substring(0, Math.Min(100, json.Length)) : "";
            Logger.Instance.Error(ex, "Error parsing JSON string list: {Json}", preview);
            return new List<string>();
        }
    }

    private List<string> ParseIngredients(string? ingredients)
    {
        if (string.IsNullOrWhiteSpace(ingredients))
            return new List<string>();

        // Remove section labels (e.g., "Filling:", "Crust:", "Contains:")
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            ingredients,
            @"\b[A-Z][a-z]+:",
            "",
            System.Text.RegularExpressions.RegexOptions.None
        );

        // Split by comma or period, but not if inside parentheses
        var pattern = @",(?![^()]*\))|\.(?![^()]*\))";
        var parts = System.Text.RegularExpressions.Regex.Split(cleaned, pattern);

        return parts
            .Select(p => p.Trim().TrimEnd('.', ','))
            .Where(p => !string.IsNullOrWhiteSpace(p) && p.Length > 2)
            .Select(p => char.ToUpper(p[0]) + p.Substring(1))
            .ToList();
    }

    private List<Product> SortByRelevance(List<Product> products, string searchText)
    {
        var searchLower = searchText.ToLower();
        var levenshtein = new Levenshtein(searchLower);

        Logger.Instance.Information("Search: '{SearchText}'", searchText);

        var scored = products.Select(p =>
        {
            // Calculate distance based on product name only
            var distance = levenshtein.DistanceFrom(p.Name.ToLower());

            // Boost exact matches and prefix matches
            if (p.Name.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                distance = -1000;
            else if (p.Name.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
                distance -= 100;
            else if (p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                distance -= 50;

            return new { Product = p, Distance = distance };
        })
        .OrderBy(x => x.Distance)
        .ToList();

        // Log top 5 results with scores
        foreach (var item in scored.Take(5))
        {
            Logger.Instance.Information("  [{Score,4}] {Name}", item.Distance, item.Product.Name);
        }

        return scored.Select(x => x.Product).ToList();
    }

    public async Task<List<string>> GetTypesAsync()
    {
        var connection = await GetConnectionAsync();
        var types = new List<string>();

        var query = "SELECT DISTINCT type FROM opennutrition_foods WHERE type IS NOT NULL AND type != '' ORDER BY type";

        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            types.Add(reader.GetString(0));
        }

        return types;
    }

    public async Task<List<string>> GetLabelsAsync()
    {
        var connection = await GetConnectionAsync();
        var labelsSet = new HashSet<string>();

        var query = "SELECT DISTINCT labels FROM opennutrition_foods WHERE labels IS NOT NULL AND labels != ''";

        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var labelsStr = reader.GetString(0);
            var labels = ParseJsonStringList(labelsStr);
            foreach (var label in labels)
            {
                labelsSet.Add(label);
            }
        }

        return labelsSet.OrderBy(x => x).ToList();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
