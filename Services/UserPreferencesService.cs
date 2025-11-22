using System.Text.Json;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.Services;

public class UserPreferencesService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DailyMealPlanner",
        "DailyMealPlannerExtended"
    );

    private static readonly string PreferencesFilePath = Path.Combine(AppDataFolder, "user_preferences.json");

    public UserPreferencesService()
    {
        // Ensure the app data folder exists
        if (!Directory.Exists(AppDataFolder))
        {
            Directory.CreateDirectory(AppDataFolder);
            Logger.Instance.Information("Created app data folder: {Folder}", AppDataFolder);
        }
    }

    public void SavePreferences(User user)
    {
        try
        {
            var json = SerializeUserToJson(user);
            File.WriteAllText(PreferencesFilePath, json);
            Logger.Instance.Information("User preferences saved to: {Path}", PreferencesFilePath);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to save user preferences");
            throw;
        }
    }

    public User? LoadPreferences()
    {
        try
        {
            if (!File.Exists(PreferencesFilePath))
            {
                Logger.Instance.Information("No preferences file found, using defaults");
                return null;
            }

            var json = File.ReadAllText(PreferencesFilePath);
            var user = DeserializeUserFromJson(json);

            if (user == null)
            {
                Logger.Instance.Warning("Failed to deserialize preferences, using defaults");
                return null;
            }

            Logger.Instance.Information("User preferences loaded from: {Path}", PreferencesFilePath);
            return user;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load user preferences, using defaults");
            return null;
        }
    }

    // Reusable serialization methods
    public static string SerializeUserToJson(User user)
    {
        var preferences = new UserPreferencesData
        {
            Weight = user.Weight,
            Height = user.Height,
            Age = user.Age,
            ActivityLevel = user.ActivityLevel,
            ProteinPercentage = user.NutrientsSplit.p,
            FatPercentage = user.NutrientsSplit.f,
            CarbsPercentage = user.NutrientsSplit.c
        };

        return JsonSerializer.Serialize(preferences, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static User? DeserializeUserFromJson(string json)
    {
        var preferences = JsonSerializer.Deserialize<UserPreferencesData>(json);

        if (preferences == null)
        {
            return null;
        }

        var user = new User
        {
            Weight = preferences.Weight,
            Height = preferences.Height,
            Age = preferences.Age,
            ActivityLevel = preferences.ActivityLevel,
            NutrientsSplit = (preferences.ProteinPercentage, preferences.FatPercentage, preferences.CarbsPercentage)
        };

        return user;
    }
}

public class UserPreferencesData
{
    public double Weight { get; set; }
    public double Height { get; set; }
    public double Age { get; set; }
    public ActivityLevel ActivityLevel { get; set; }
    public double ProteinPercentage { get; set; }
    public double FatPercentage { get; set; }
    public double CarbsPercentage { get; set; }
}
