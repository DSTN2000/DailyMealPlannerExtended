using System.Xml.Serialization;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.Services;

public class DaySnapshotService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DailyMealPlanner",
        "DailyMealPlannerExtended"
    );

    private static readonly string SnapshotsFolder = Path.Combine(AppDataFolder, "Snapshots");

    public DaySnapshotService()
    {
        // Ensure the snapshots folder exists
        if (!Directory.Exists(SnapshotsFolder))
        {
            Directory.CreateDirectory(SnapshotsFolder);
            Logger.Instance.Information("Created snapshots folder: {Folder}", SnapshotsFolder);
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

            // Create the wrapper snapshot
            var snapshot = new DaySnapshotXml
            {
                Date = mealPlan.Date,
                UserPreferencesJson = userJson,
                MealPlanXml = mealPlanXml
            };

            var fileName = $"{mealPlan.Date:yyyy-MM-dd}-snapshot.xml";
            var filePath = Path.Combine(SnapshotsFolder, fileName);

            var serializer = new XmlSerializer(typeof(DaySnapshotXml));
            using var writer = new StreamWriter(filePath);
            serializer.Serialize(writer, snapshot);

            Logger.Instance.Information("Day snapshot saved to: {Path}", filePath);
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
            var fileName = $"{date:yyyy-MM-dd}-snapshot.xml";
            var filePath = Path.Combine(SnapshotsFolder, fileName);

            if (!File.Exists(filePath))
            {
                Logger.Instance.Information("No snapshot found for date: {Date}", date.ToShortDateString());
                return null;
            }

            var serializer = new XmlSerializer(typeof(DaySnapshotXml));
            using var reader = new StreamReader(filePath);
            var snapshotXml = (DaySnapshotXml?)serializer.Deserialize(reader);

            if (snapshotXml == null)
            {
                Logger.Instance.Warning("Failed to deserialize snapshot XML");
                return null;
            }

            // Reuse UserPreferencesService deserialization for JSON -> User
            var user = UserPreferencesService.DeserializeUserFromJson(snapshotXml.UserPreferencesJson);
            if (user == null)
            {
                Logger.Instance.Warning("Failed to deserialize user preferences from snapshot");
                return null;
            }

            // Reuse MealPlanService deserialization for XML -> MealPlan
            var mealPlan = MealPlanService.DeserializeMealPlanFromXml(snapshotXml.MealPlanXml);
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

            Logger.Instance.Information("Day snapshot loaded from: {Path}", filePath);
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
        var fileName = $"{date:yyyy-MM-dd}-snapshot.xml";
        var filePath = Path.Combine(SnapshotsFolder, fileName);
        return File.Exists(filePath);
    }

    public List<DateTime> GetAllSnapshotDates()
    {
        var dates = new List<DateTime>();

        try
        {
            if (!Directory.Exists(SnapshotsFolder))
            {
                return dates;
            }

            var files = Directory.GetFiles(SnapshotsFolder, "*-snapshot.xml");

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                // Remove "-snapshot" suffix
                var dateStr = fileName.Replace("-snapshot", "");

                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
                {
                    dates.Add(date);
                }
            }

            dates.Sort();
            Logger.Instance.Information("Found {Count} snapshot dates", dates.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to get snapshot dates");
        }

        return dates;
    }
}

// XML wrapper that contains JSON user preferences and XML meal plan
public class DaySnapshotXml
{
    public DateTime Date { get; set; }
    public string UserPreferencesJson { get; set; } = string.Empty;
    public string MealPlanXml { get; set; } = string.Empty;
}
