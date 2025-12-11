using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.Services.Utilities;

namespace DailyMealPlannerExtended.Services;

public class FavoriteMealPlansService
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public FavoriteMealPlansService()
    {
        _databasePath = AppDataPathService.GetDatabasePath("favorites.db");
        _connectionString = $"Data Source={_databasePath}";

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Favorites (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MealPlanXml TEXT NOT NULL,
                    MealPlanHash TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    Date TEXT NOT NULL,
                    TotalCalories REAL NOT NULL,
                    TotalProtein REAL NOT NULL,
                    TotalFat REAL NOT NULL,
                    TotalCarbohydrates REAL NOT NULL,
                    CreatedAt TEXT NOT NULL
                )";
            createTableCommand.ExecuteNonQuery();

            Logger.Instance.Information("Favorites database initialized at: {Path}", _databasePath);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to initialize favorites database");
            throw;
        }
    }

    public void AddToFavorites(DailyMealPlan mealPlan)
    {
        try
        {
            var xml = MealPlanService.SerializeMealPlanToXml(mealPlan);
            var hash = ComputeMealPlanHash(mealPlan);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Check if already exists
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM Favorites WHERE MealPlanHash = $hash";
            checkCommand.Parameters.AddWithValue("$hash", hash);
            var count = (long)checkCommand.ExecuteScalar()!;

            if (count > 0)
            {
                Logger.Instance.Information("Meal plan is already in favorites");
                return;
            }

            // Insert new favorite
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Favorites (MealPlanXml, MealPlanHash, Name, Date, TotalCalories, TotalProtein, TotalFat, TotalCarbohydrates, CreatedAt)
                VALUES ($xml, $hash, $name, $date, $calories, $protein, $fat, $carbs, $createdAt)";

            insertCommand.Parameters.AddWithValue("$xml", xml);
            insertCommand.Parameters.AddWithValue("$hash", hash);
            insertCommand.Parameters.AddWithValue("$name", mealPlan.Name);
            insertCommand.Parameters.AddWithValue("$date", mealPlan.Date.ToString("yyyy-MM-dd"));
            insertCommand.Parameters.AddWithValue("$calories", mealPlan.TotalCalories);
            insertCommand.Parameters.AddWithValue("$protein", mealPlan.TotalProtein);
            insertCommand.Parameters.AddWithValue("$fat", mealPlan.TotalFat);
            insertCommand.Parameters.AddWithValue("$carbs", mealPlan.TotalCarbohydrates);
            insertCommand.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("o"));

            insertCommand.ExecuteNonQuery();

            Logger.Instance.Information("Meal plan added to favorites: {Name}", mealPlan.Name);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to add meal plan to favorites");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing favorite with current state (including images and notes)
    /// </summary>
    public void UpdateFavorite(DailyMealPlan mealPlan)
    {
        try
        {
            var xml = MealPlanService.SerializeMealPlanToXml(mealPlan);
            var hash = ComputeMealPlanHash(mealPlan);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Update existing favorite (hash stays the same, XML changes with images/notes)
            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = @"
                UPDATE Favorites
                SET MealPlanXml = $xml,
                    Name = $name,
                    Date = $date,
                    TotalCalories = $calories,
                    TotalProtein = $protein,
                    TotalFat = $fat,
                    TotalCarbohydrates = $carbs
                WHERE MealPlanHash = $hash";

            updateCommand.Parameters.AddWithValue("$xml", xml);
            updateCommand.Parameters.AddWithValue("$hash", hash);
            updateCommand.Parameters.AddWithValue("$name", mealPlan.Name);
            updateCommand.Parameters.AddWithValue("$date", mealPlan.Date.ToString("yyyy-MM-dd"));
            updateCommand.Parameters.AddWithValue("$calories", mealPlan.TotalCalories);
            updateCommand.Parameters.AddWithValue("$protein", mealPlan.TotalProtein);
            updateCommand.Parameters.AddWithValue("$fat", mealPlan.TotalFat);
            updateCommand.Parameters.AddWithValue("$carbs", mealPlan.TotalCarbohydrates);

            var rowsAffected = updateCommand.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                Logger.Instance.Debug("Updated favorite meal plan: {Name} (preserved images/notes)", mealPlan.Name);
            }
            else
            {
                Logger.Instance.Warning("Tried to update favorite but meal plan not found: {Name}", mealPlan.Name);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to update favorite meal plan");
            throw;
        }
    }

    private static string ComputeHash(string xml)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(xml);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static string ComputeMealPlanHash(DailyMealPlan mealPlan)
    {
        // Create a temporary meal plan with normalized date for consistent hashing
        // This ensures meal plans with the same content but different dates match
        var normalizedPlan = new DailyMealPlan
        {
            Date = DateTime.MinValue, // Use fixed date for hashing
            Name = mealPlan.Name
        };

        // Clear default meal times
        normalizedPlan.MealTimes.Clear();

        // Copy all meal times and items
        foreach (var mealTime in mealPlan.MealTimes)
        {
            var newMealTime = new MealTime(mealTime.Type, mealTime.Type == MealTimeType.Custom ? mealTime.Name : null);

            foreach (var item in mealTime.Items)
            {
                var productCopy = new Product
                {
                    Name = item.Product.Name,
                    Calories = item.Product.Calories,
                    Protein = item.Product.Protein,
                    TotalFat = item.Product.TotalFat,
                    Carbohydrates = item.Product.Carbohydrates,
                    Sodium = item.Product.Sodium,
                    Fiber = item.Product.Fiber,
                    Sugar = item.Product.Sugar,
                    Serving = item.Product.Serving,
                    Unit = item.Product.Unit
                };

                // Exclude Image and Note from hash for performance considerations
                var newItem = new MealPlanItem(productCopy, item.Weight);
                newMealTime.Items.Add(newItem);
            }

            normalizedPlan.MealTimes.Add(newMealTime);
        }

        // Serialize normalized plan and compute hash
        var xml = MealPlanService.SerializeMealPlanToXml(normalizedPlan);
        return ComputeHash(xml);
    }

    public void RemoveFromFavorites(DailyMealPlan mealPlan)
    {
        try
        {
            var hash = ComputeMealPlanHash(mealPlan);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM Favorites WHERE MealPlanHash = $hash";
            deleteCommand.Parameters.AddWithValue("$hash", hash);

            var rowsAffected = deleteCommand.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                Logger.Instance.Information("Meal plan removed from favorites: {Name}", mealPlan.Name);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to remove meal plan from favorites");
            throw;
        }
    }

    public List<(string fileName, DailyMealPlan mealPlan)> GetFavorites()
    {
        var favorites = new List<(string, DailyMealPlan)>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id, MealPlanXml FROM Favorites ORDER BY CreatedAt DESC";

            using var reader = selectCommand.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var xml = reader.GetString(1);

                var mealPlan = MealPlanService.DeserializeMealPlanFromXml(xml);
                if (mealPlan != null)
                {
                    favorites.Add((id.ToString(), mealPlan));
                }
            }

            Logger.Instance.Information("Loaded {Count} favorite meal plans from database", favorites.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to get favorite meal plans");
        }

        return favorites;
    }

    public bool IsFavorite(DailyMealPlan mealPlan)
    {
        try
        {
            var hash = ComputeMealPlanHash(mealPlan);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM Favorites WHERE MealPlanHash = $hash";
            checkCommand.Parameters.AddWithValue("$hash", hash);

            var count = (long)checkCommand.ExecuteScalar()!;
            return count > 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to check if meal plan is favorite");
            return false;
        }
    }
}
