using System.IO.Compression;
using System.Net.Http;
using DailyMealPlannerExtended.Services.Utilities;
using Microsoft.Data.Sqlite;

namespace DailyMealPlannerExtended.Services;

/// <summary>
/// Service responsible for downloading and initializing the OpenNutrition database
/// if it doesn't exist. Makes the application self-contained without requiring
/// manual Python script execution.
/// </summary>
public class DatabaseInitializationService
{
    private const string DatabaseFileName = "opennutrition_foods.db";
    private const string DatasetUrl = "https://downloads.opennutrition.app/opennutrition-dataset-2025.1.zip";
    private const string TsvFileName = "opennutrition_foods.tsv";

    /// <summary>
    /// Ensures the OpenNutrition database exists. Downloads and creates it if missing.
    /// </summary>
    /// <returns>True if database is ready, false if initialization failed</returns>
    public async Task<bool> EnsureDatabaseExistsAsync()
    {
        try
        {
            var dbPath = AppDataPathService.GetDatabasePath(DatabaseFileName);

            // Check if database already exists
            if (File.Exists(dbPath))
            {
                Logger.Instance.Information("OpenNutrition database already exists at: {DbPath}", dbPath);
                return true;
            }

            Logger.Instance.Information("OpenNutrition database not found. Initializing...");

            // Download and create the database
            await DownloadAndCreateDatabaseAsync(dbPath);

            Logger.Instance.Information("OpenNutrition database successfully created at: {DbPath}", dbPath);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to initialize OpenNutrition database");
            return false;
        }
    }

    private async Task DownloadAndCreateDatabaseAsync(string dbPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"opennutrition_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Step 1: Download the zip file
            var zipPath = Path.Combine(tempDir, "opennutrition-dataset.zip");
            Logger.Instance.Information("Downloading OpenNutrition dataset from {Url}...", DatasetUrl);

            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(10);
                var response = await httpClient.GetAsync(DatasetUrl);
                response.EnsureSuccessStatusCode();

                await using var fileStream = File.Create(zipPath);
                await response.Content.CopyToAsync(fileStream);
            }

            Logger.Instance.Information("Download complete. Extracting...");

            // Step 2: Extract the zip file
            var extractDir = Path.Combine(tempDir, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Step 3: Find the TSV file
            var tsvPath = FindTsvFile(extractDir);
            if (tsvPath == null)
            {
                throw new FileNotFoundException($"Could not find {TsvFileName} in the downloaded archive");
            }

            Logger.Instance.Information("Found TSV file. Converting to SQLite database...");

            // Step 4: Convert TSV to SQLite database using SQLite's .import equivalent
            await ConvertTsvToSqliteAsync(tsvPath, dbPath);

            Logger.Instance.Information("Database conversion complete");
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                    Logger.Instance.Debug("Cleaned up temporary files");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(ex, "Failed to clean up temporary directory: {TempDir}", tempDir);
            }
        }
    }

    private string? FindTsvFile(string directory)
    {
        // Search recursively for the TSV file
        var files = Directory.GetFiles(directory, TsvFileName, SearchOption.AllDirectories);
        return files.Length > 0 ? files[0] : null;
    }

    private async Task ConvertTsvToSqliteAsync(string tsvPath, string dbPath)
    {
        // Ensure the database directory exists
        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        // Delete existing database if present (shouldn't happen, but be safe)
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        // Use SQLite's CSV import feature to directly import the TSV
        // This is much simpler and faster than manual row-by-row insertion
        var connectionString = $"Data Source={dbPath}";
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Read the first line to get column names
            var firstLine = File.ReadLines(tsvPath).First();
            var columns = firstLine.Split('\t');

            // Create the table with all columns as TEXT
            var columnDefinitions = string.Join(", ", columns.Select(col => $"\"{col}\" TEXT"));
            var createTableSql = $"CREATE TABLE opennutrition_foods ({columnDefinitions})";

            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = createTableSql;
                await createCmd.ExecuteNonQueryAsync();
            }

            Logger.Instance.Debug("Created table with {ColumnCount} columns", columns.Length);

            // Now insert data line by line (skip header)
            var lines = File.ReadLines(tsvPath).Skip(1);
            var insertSql = $"INSERT INTO opennutrition_foods VALUES ({string.Join(",", Enumerable.Range(0, columns.Length).Select(i => $"@p{i}"))})";

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = insertSql;

            // Add parameters
            for (int i = 0; i < columns.Length; i++)
            {
                insertCmd.Parameters.Add(new SqliteParameter($"@p{i}", null));
            }

            int rowCount = 0;
            foreach (var line in lines)
            {
                var values = line.Split('\t');

                // Set parameter values
                for (int i = 0; i < columns.Length; i++)
                {
                    insertCmd.Parameters[i].Value = i < values.Length ? (object)values[i] : DBNull.Value;
                }

                await insertCmd.ExecuteNonQueryAsync();
                rowCount++;

                // Log progress every 5000 rows
                if (rowCount % 5000 == 0)
                {
                    Logger.Instance.Debug("Inserted {Count} rows...", rowCount);
                }
            }

            await transaction.CommitAsync();
            Logger.Instance.Information("Inserted {Count} total rows into database", rowCount);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
