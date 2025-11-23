using System.Xml.Serialization;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.Services;

public class MealPlanService
{
    // Reusable meal plan XML serialization (no user preferences, just the meal plan)
    public static string SerializeMealPlanToXml(DailyMealPlan mealPlan)
    {
        var mealPlanData = new MealPlanData
        {
            Date = mealPlan.Date,
            Name = mealPlan.Name,
            MealTimes = mealPlan.MealTimes.Select(mt => new MealTimeData
            {
                Type = mt.Type,
                Name = mt.Name,
                Items = mt.Items.Select(item => new MealPlanItemData
                {
                    Product = item.Product,
                    Weight = item.Weight
                }).ToList()
            }).ToList()
        };

        var serializer = new XmlSerializer(typeof(MealPlanData));
        using var stringWriter = new StringWriter();
        serializer.Serialize(stringWriter, mealPlanData);
        return stringWriter.ToString();
    }

    public static DailyMealPlan? DeserializeMealPlanFromXml(string xml)
    {
        var serializer = new XmlSerializer(typeof(MealPlanData));
        using var stringReader = new StringReader(xml);
        var mealPlanData = (MealPlanData?)serializer.Deserialize(stringReader);

        if (mealPlanData == null)
        {
            return null;
        }

        var mealPlan = new DailyMealPlan
        {
            Date = mealPlanData.Date,
            Name = mealPlanData.Name
        };

        // Clear default meal times before reconstructing
        mealPlan.MealTimes.Clear();

        // Reconstruct meal times and items
        foreach (var mtData in mealPlanData.MealTimes)
        {
            var mealTime = new MealTime(mtData.Type, mtData.Type == MealTimeType.Custom ? mtData.Name : null);

            foreach (var itemData in mtData.Items)
            {
                var mealPlanItem = new MealPlanItem(itemData.Product, itemData.Weight);
                mealTime.Items.Add(mealPlanItem);
            }

            mealPlan.MealTimes.Add(mealTime);
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

// XML-serializable meal plan structure
public class MealPlanData
{
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<MealTimeData> MealTimes { get; set; } = new();
}

public class MealTimeData
{
    public MealTimeType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<MealPlanItemData> Items { get; set; } = new();
}

public class MealPlanItemData
{
    public Product Product { get; set; } = new();
    public double Weight { get; set; }
}
