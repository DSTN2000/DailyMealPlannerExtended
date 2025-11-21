using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using Lab4.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class MealPlanViewModel : ViewModelBase
{
    private readonly UserPreferencesService _preferencesService;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private DailyMealPlan _currentMealPlan;

    [ObservableProperty]
    private User _user = new();

    public MealPlanViewModel()
    {
        _preferencesService = new UserPreferencesService();
        LoadUserPreferences();
        _currentMealPlan = GetOrCreateMealPlan(SelectedDate);
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        CurrentMealPlan = GetOrCreateMealPlan(value);
    }

    private void LoadUserPreferences()
    {
        try
        {
            var loaded = _preferencesService.LoadPreferences();
            if (loaded.HasValue)
            {
                var (user, proteinPct, fatPct, carbsPct) = loaded.Value;
                User = user;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load user preferences in MealPlanViewModel");
        }
    }

    private DailyMealPlan GetOrCreateMealPlan(DateTime date)
    {
        // TODO: Load from database if exists
        // For now, create a new one with default meal times
        var mealPlan = new DailyMealPlan
        {
            Date = date,
            MealTimes = new ObservableCollection<MealTime>
            {
                new MealTime(MealTimeType.Breakfast),
                new MealTime(MealTimeType.Lunch),
                new MealTime(MealTimeType.Dinner)
            }
        };

        Logger.Instance.Information("Created meal plan for {Date}", date.ToShortDateString());
        return mealPlan;
    }

    [RelayCommand]
    private void AddMealTime()
    {
        uint idx = (uint) CurrentMealPlan.MealTimes.Count + 1;
        while (CurrentMealPlan.MealTimeNames.Contains($"Meal {idx}")) idx--;
        var newMealTime = new MealTime(MealTimeType.Custom, $"Meal {idx}");
        CurrentMealPlan.MealTimes.Add(newMealTime);
        Logger.Instance.Information("Added new meal time: {Name}", newMealTime.Name);
    }

    [RelayCommand]
    private void RemoveMealTime(MealTime mealTime)
    {
        // Only allow removing custom meal times
        if (mealTime.Type == MealTimeType.Custom && CurrentMealPlan.MealTimes.Contains(mealTime))
        {
            CurrentMealPlan.MealTimes.Remove(mealTime);
            Logger.Instance.Information("Removed meal time: {Name}", mealTime.Name);
        }
    }

    [RelayCommand]
    private void AddFoodItem(MealTime mealTime)
    {
        // This will be triggered from the UI to show product selection
        // For now, we'll handle this through events or callbacks
        Logger.Instance.Information("Add food item requested for {MealTime}", mealTime.Name);
    }

    // Nutritional progress properties
    public double CaloriesProgress => User.DailyCalories > 0
        ? (CurrentMealPlan.TotalCalories / User.DailyCalories) * 100
        : 0;

    public double ProteinProgress => User.DailyProtein > 0
        ? (CurrentMealPlan.TotalProtein / User.DailyProtein) * 100
        : 0;

    public double FatProgress => User.DailyFat > 0
        ? (CurrentMealPlan.TotalFat / User.DailyFat) * 100
        : 0;

    public double CarbsProgress => User.DailyCarbohydrates > 0
        ? (CurrentMealPlan.TotalCarbohydrates / User.DailyCarbohydrates) * 100
        : 0;

    [RelayCommand]
    private void SaveMealPlan()
    {
        // TODO: Implement saving to database
        Logger.Instance.Information("Save meal plan for {Date}: Calories={Calories:F0}, Protein={Protein:F1}g, Fat={Fat:F1}g, Carbs={Carbs:F1}g",
            CurrentMealPlan.Date.ToShortDateString(),
            CurrentMealPlan.TotalCalories,
            CurrentMealPlan.TotalProtein,
            CurrentMealPlan.TotalFat,
            CurrentMealPlan.TotalCarbohydrates);
    }
}
