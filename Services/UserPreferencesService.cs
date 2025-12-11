using System.Text.Json;
using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.Services.Utilities;

namespace DailyMealPlannerExtended.Services;

public class UserPreferencesService
{
    private static readonly string PreferencesFilePath = AppDataPathService.GetFilePath("user_preferences.json");

    public UserPreferencesService()
    {
        // AppDataPathService automatically ensures the folder exists
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
            NutrientsSplit = new NutrientsSplitData
            {
                Protein = user.NutrientsSplit.p,
                Fat = user.NutrientsSplit.f,
                Carbs = user.NutrientsSplit.c
            }
        };

        return JsonSerializer.Serialize(preferences, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static User? DeserializeUserFromJson(string json)
    {
        var preferences = JsonSerializer.Deserialize<UserPreferencesData>(json);

        if (preferences == null || preferences.NutrientsSplit == null)
        {
            return null;
        }

        var user = new User
        {
            Weight = preferences.Weight,
            Height = preferences.Height,
            Age = preferences.Age,
            ActivityLevel = preferences.ActivityLevel,
            NutrientsSplit = (preferences.NutrientsSplit.Protein, preferences.NutrientsSplit.Fat, preferences.NutrientsSplit.Carbs)
        };

        return user;
    }
}

public class UserPreferencesData
{
    [System.Text.Json.Serialization.JsonPropertyName("weight")]
    public double Weight { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("height")]
    public double Height { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("age")]
    public double Age { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("activityLevel")]
    public ActivityLevel ActivityLevel { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("nutrientsSplit")]
    public NutrientsSplitData? NutrientsSplit { get; set; }
}

public class NutrientsSplitData
{
    [System.Text.Json.Serialization.JsonPropertyName("protein")]
    public double Protein { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("fat")]
    public double Fat { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("carbs")]
    public double Carbs { get; set; }
}
