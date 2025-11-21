using System.Collections.ObjectModel;

namespace Lab4.Models;

public class MealTime
{
    public MealTimeType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<MealPlanItem> Items { get; set; } = new();

    // Calculated totals for this mealtime
    public double TotalCalories => Items.Sum(item => item.Calories);
    public double TotalProtein => Items.Sum(item => item.Protein);
    public double TotalFat => Items.Sum(item => item.TotalFat);
    public double TotalCarbohydrates => Items.Sum(item => item.Carbohydrates);
    public double TotalSodium => Items.Sum(item => item.Sodium);
    public double TotalFiber => Items.Sum(item => item.Fiber);
    public double TotalSugar => Items.Sum(item => item.Sugar);

    public MealTime()
    {
    }

    public MealTime(MealTimeType type, string? customName = null)
    {
        Type = type;
        Name = type switch
        {
            MealTimeType.Breakfast => "Breakfast",
            MealTimeType.Lunch => "Lunch",
            MealTimeType.Dinner => "Dinner",
            MealTimeType.Custom => customName ?? "Custom Meal",
            _ => "Meal"
        };
    }
}

public enum MealTimeType
{
    Breakfast,
    Lunch,
    Dinner,
    Custom
}
