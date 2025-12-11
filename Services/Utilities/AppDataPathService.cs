namespace DailyMealPlannerExtended.Services.Utilities;

/// <summary>
/// Centralized service for managing application data directory paths.
/// Provides platform-specific application data folder locations.
/// </summary>
public static class AppDataPathService
{
    private static readonly Lazy<string> _dataDirectory = new(() =>
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DailyMealPlanner",
            "DailyMealPlannerExtended"
        );

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return path;
    });

    /// <summary>
    /// Gets the application's data directory path.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    /// <returns>Absolute path to the application data directory</returns>
    public static string DataDirectory => _dataDirectory.Value;

    /// <summary>
    /// Gets the full path for a database file in the application data directory.
    /// </summary>
    /// <param name="databaseName">Name of the database file (e.g., "snapshots.db")</param>
    /// <returns>Full path to the database file</returns>
    public static string GetDatabasePath(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name cannot be null or empty", nameof(databaseName));
        }

        return Path.Combine(DataDirectory, databaseName);
    }

    /// <summary>
    /// Gets the full path for a file in the application data directory.
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <returns>Full path to the file</returns>
    public static string GetFilePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
        }

        return Path.Combine(DataDirectory, fileName);
    }
}
