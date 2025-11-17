namespace Lab4.Models;

public class DailyMealPlan
{
    public DateTime Date { get; set; } = DateTime.Today;
    public List<MealTime> MealTimes { get; set; } = new();

    // Calculated daily totals
    public double TotalCalories => MealTimes.Sum(meal => meal.TotalCalories);
    public double TotalProtein => MealTimes.Sum(meal => meal.TotalProtein);
    public double TotalFat => MealTimes.Sum(meal => meal.TotalFat);
    public double TotalCarbohydrates => MealTimes.Sum(meal => meal.TotalCarbohydrates);
    public double TotalSodium => MealTimes.Sum(meal => meal.TotalSodium);
    public double TotalFiber => MealTimes.Sum(meal => meal.TotalFiber);
    public double TotalSugar => MealTimes.Sum(meal => meal.TotalSugar);

    public DailyMealPlan()
    {
        // Initialize with default mealtimes
        MealTimes.Add(new MealTime(MealTimeType.Breakfast));
        MealTimes.Add(new MealTime(MealTimeType.Lunch));
        MealTimes.Add(new MealTime(MealTimeType.Dinner));
    }
}
