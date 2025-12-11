using Microsoft.Data.Sqlite;
using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.Services.Utilities;

namespace DailyMealPlannerExtended.Services;

public class DaySnapshotService
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public DaySnapshotService()
    {
        _databasePath = AppDataPathService.GetDatabasePath("snapshots.db");
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
                CREATE TABLE IF NOT EXISTS Snapshots (
                    Date TEXT PRIMARY KEY,
                    UserPreferencesJson TEXT NOT NULL,
                    MealPlanXml TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                )";
            createTableCommand.ExecuteNonQuery();

            Logger.Instance.Information("Snapshots database initialized at: {Path}", _databasePath);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to initialize snapshots database");
            throw;
        }
    }

    public void SaveSnapshot(DailyMealPlan mealPlan, User user)
    {
        try
        {
            // Reuse UserPreferencesService serialization for User -> JSON
            var userJson = UserPreferencesService.SerializeUserToJson(user);

            // Reuse MealPlanService serialization for MealPlan -> XML
            var mealPlanXml = MealPlanService.SerializeMealPlanToXml(mealPlan);

            var dateStr = mealPlan.Date.ToString("yyyy-MM-dd");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Use INSERT OR REPLACE to update if exists, insert if not
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Snapshots (Date, UserPreferencesJson, MealPlanXml, CreatedAt)
                VALUES ($date, $userJson, $mealPlanXml, $createdAt)";

            command.Parameters.AddWithValue("$date", dateStr);
            command.Parameters.AddWithValue("$userJson", userJson);
            command.Parameters.AddWithValue("$mealPlanXml", mealPlanXml);
            command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("o"));

            command.ExecuteNonQuery();

            Logger.Instance.Information("Day snapshot saved to database for date: {Date}", dateStr);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to save day snapshot");
            throw;
        }
    }

    public DaySnapshot? LoadSnapshot(DateTime date)
    {
        try
        {
            var dateStr = date.ToString("yyyy-MM-dd");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT UserPreferencesJson, MealPlanXml FROM Snapshots WHERE Date = $date";
            command.Parameters.AddWithValue("$date", dateStr);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                Logger.Instance.Information("No snapshot found for date: {Date}", date.ToShortDateString());
                return null;
            }

            var userJson = reader.GetString(0);
            var mealPlanXml = reader.GetString(1);

            // Reuse UserPreferencesService deserialization for JSON -> User
            var user = UserPreferencesService.DeserializeUserFromJson(userJson);
            if (user == null)
            {
                Logger.Instance.Warning("Failed to deserialize user preferences from snapshot");
                return null;
            }

            // Reuse MealPlanService deserialization for XML -> MealPlan
            var mealPlan = MealPlanService.DeserializeMealPlanFromXml(mealPlanXml);
            if (mealPlan == null)
            {
                Logger.Instance.Warning("Failed to deserialize meal plan from snapshot");
                return null;
            }

            var daySnapshot = new DaySnapshot
            {
                MealPlan = mealPlan,
                UserPreferences = user
            };

            Logger.Instance.Information("Day snapshot loaded from database for date: {Date}", dateStr);
            return daySnapshot;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load day snapshot for date: {Date}", date.ToShortDateString());
            return null;
        }
    }

    public bool HasSnapshot(DateTime date)
    {
        try
        {
            var dateStr = date.ToString("yyyy-MM-dd");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Snapshots WHERE Date = $date";
            command.Parameters.AddWithValue("$date", dateStr);

            var count = (long)command.ExecuteScalar()!;
            return count > 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to check snapshot existence for date: {Date}", date.ToShortDateString());
            return false;
        }
    }

    public List<DateTime> GetAllSnapshotDates()
    {
        var dates = new List<DateTime>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Date FROM Snapshots ORDER BY Date";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var dateStr = reader.GetString(0);
                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
                {
                    dates.Add(date);
                }
            }

            Logger.Instance.Information("Found {Count} snapshot dates", dates.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to get snapshot dates");
        }

        return dates;
    }
}
