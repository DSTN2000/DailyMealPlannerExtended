using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab4.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class MealPlanViewModel : ViewModelBase
{
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private DailyMealPlan _currentMealPlan;

    [ObservableProperty]
    private User _user;

    public MealPlanViewModel(User user)
    {
        _user = user;
        _currentMealPlan = GetOrCreateMealPlan(SelectedDate);

        // Subscribe to property changes for progress updates
        SubscribeToPropertyChanges();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        CurrentMealPlan = GetOrCreateMealPlan(value);
    }

    partial void OnCurrentMealPlanChanged(DailyMealPlan value)
    {
        // Subscribe to the new meal plan's property changes
        if (value != null)
        {
            value.PropertyChanged += CurrentMealPlan_PropertyChanged;
        }

        // Update all progress properties
        NotifyProgressChanged();
    }

    partial void OnUserChanged(User value)
    {
        // Subscribe to the new user's property changes
        if (value != null)
        {
            value.PropertyChanged += User_PropertyChanged;
        }

        // Update all progress properties
        NotifyProgressChanged();
    }

    private void SubscribeToPropertyChanges()
    {
        // Subscribe to CurrentMealPlan totals changes
        CurrentMealPlan.PropertyChanged += CurrentMealPlan_PropertyChanged;

        // Subscribe to User daily goals changes
        User.PropertyChanged += User_PropertyChanged;
    }

    private void CurrentMealPlan_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName?.StartsWith("Total") == true)
        {
            NotifyProgressChanged();
        }
    }

    private void User_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName?.StartsWith("Daily") == true)
        {
            NotifyProgressChanged();
        }
    }

    private void NotifyProgressChanged()
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(ProteinProgress));
        OnPropertyChanged(nameof(FatProgress));
        OnPropertyChanged(nameof(CarbsProgress));
    }

    private DailyMealPlan GetOrCreateMealPlan(DateTime date)
    {
        // TODO: Load from database if exists
        // For now, create a new one with default meal times
        // Note: DailyMealPlan constructor already initializes MealTimes with default meals
        var mealPlan = new DailyMealPlan
        {
            Date = date
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
    private void MoveMealTimeUp(MealTime mealTime)
    {
        var index = CurrentMealPlan.MealTimes.IndexOf(mealTime);
        if (index > 0)
        {
            CurrentMealPlan.MealTimes.Move(index, index - 1);
            Logger.Instance.Information("Moved meal time up: {Name}", mealTime.Name);
        }
    }

    [RelayCommand]
    private void MoveMealTimeDown(MealTime mealTime)
    {
        var index = CurrentMealPlan.MealTimes.IndexOf(mealTime);
        if (index >= 0 && index < CurrentMealPlan.MealTimes.Count - 1)
        {
            CurrentMealPlan.MealTimes.Move(index, index + 1);
            Logger.Instance.Information("Moved meal time down: {Name}", mealTime.Name);
        }
    }

    [RelayCommand]
    private void RemoveFoodItem(MealPlanItem item)
    {
        foreach (var mealTime in CurrentMealPlan.MealTimes)
        {
            if (mealTime.Items.Contains(item))
            {
                mealTime.Items.Remove(item);
                Logger.Instance.Information("Removed {Product} from {MealTime}",
                    item.Product.Name, mealTime.Name);
                break;
            }
        }
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
