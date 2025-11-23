using System.Xml.Serialization;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.Services;

public class MealPlanService
{
    // Reusable meal plan XML serialization (no user preferences, just the meal plan)
    public static string SerializeMealPlanToXml(DailyMealPlan mealPlan)
    {
        var serializer = new XmlSerializer(typeof(DailyMealPlan));
        using var stringWriter = new StringWriter();
        serializer.Serialize(stringWriter, mealPlan);
        return stringWriter.ToString();
    }

    public static DailyMealPlan? DeserializeMealPlanFromXml(string xml)
    {
        var serializer = new XmlSerializer(typeof(DailyMealPlan));
        using var stringReader = new StringReader(xml);
        var mealPlan = (DailyMealPlan?)serializer.Deserialize(stringReader);

        // Re-subscribe to property changes after deserialization
        if (mealPlan != null)
        {
            // The CollectionChanged event handler is already set up in the constructor
            // But we need to manually trigger subscription for existing items
            foreach (var mealTime in mealPlan.MealTimes)
            {
                mealTime.PropertyChanged += mealPlan.MealTime_PropertyChanged;
            }
        }

        return mealPlan;
    }

    public void ExportMealPlan(DailyMealPlan mealPlan, string filePath)
    {
        try
        {
            var xml = SerializeMealPlanToXml(mealPlan);
            File.WriteAllText(filePath, xml);
            Logger.Instance.Information("Meal plan exported to: {Path}", filePath);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to export meal plan");
            throw;
        }
    }

    public DailyMealPlan? ImportMealPlan(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Logger.Instance.Warning("Meal plan file not found: {Path}", filePath);
                return null;
            }

            var xml = File.ReadAllText(filePath);
            var mealPlan = DeserializeMealPlanFromXml(xml);

            Logger.Instance.Information("Meal plan imported from: {Path}", filePath);
            return mealPlan;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to import meal plan");
            throw;
        }
    }
}
