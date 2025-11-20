using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab4.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class UserPreferencesViewModel : ViewModelBase
{
    [ObservableProperty]
    private User _user = new();

    [ObservableProperty]
    private double _proteinPercentage = 30;

    [ObservableProperty]
    private double _fatPercentage = 30;

    [ObservableProperty]
    private double _carbsPercentage = 40;

    public ObservableCollection<ActivityLevel> ActivityLevels { get; } = new()
    {
        ActivityLevel.Sedentary,
        ActivityLevel.Moderate,
        ActivityLevel.Medium,
        ActivityLevel.High
    };

    public UserPreferencesViewModel()
    {
        UpdateNutrientSplit();
    }

    partial void OnProteinPercentageChanged(double value)
    {
        UpdateNutrientSplit();
    }

    partial void OnFatPercentageChanged(double value)
    {
        UpdateNutrientSplit();
    }

    partial void OnCarbsPercentageChanged(double value)
    {
        UpdateNutrientSplit();
    }

    private void UpdateNutrientSplit()
    {
        User.NutrientsSplit = (ProteinPercentage, FatPercentage, CarbsPercentage);
        OnPropertyChanged(nameof(TotalPercentage));
        OnPropertyChanged(nameof(IsValidSplit));
    }

    public double TotalPercentage => ProteinPercentage + FatPercentage + CarbsPercentage;
    public bool IsValidSplit => Math.Abs(TotalPercentage - 100) < 0.01;

    [RelayCommand]
    private void ResetToDefaults()
    {
        User.Weight = 75;
        User.Height = 170;
        User.Age = 30;
        User.ActivityLevel = ActivityLevel.Moderate;
        ProteinPercentage = 30;
        FatPercentage = 30;
        CarbsPercentage = 40;
    }

    [RelayCommand]
    private void SavePreferences()
    {
        // TODO: Implement saving to a file or database
        Logger.Instance.Information("User preferences saved: Weight={Weight}kg, Height={Height}cm, Age={Age}, BMI={BMI:F1}, Daily Calories={Calories:F0}",
            User.Weight, User.Height, User.Age, User.BMI, User.DailyCalories);
    }
}
