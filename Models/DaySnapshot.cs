namespace DailyMealPlannerExtended.Models;

using DailyMealPlannerExtended.Models;

public class DaySnapshot
{
    public required DailyMealPlan MealPlan {get; set;}
    public required User UserPreferences {get; set;}
}
