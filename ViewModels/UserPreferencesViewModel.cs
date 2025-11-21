using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using Lab4.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class UserPreferencesViewModel : ViewModelBase
{
    private readonly UserPreferencesService _preferencesService;

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
        _preferencesService = new UserPreferencesService();
        LoadPreferences();
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
        try
        {
            _preferencesService.SavePreferences(User, ProteinPercentage, FatPercentage, CarbsPercentage);
            Logger.Instance.Information("User preferences saved: Weight={Weight}kg, Height={Height}cm, Age={Age}, BMI={BMI:F1}, Daily Calories={Calories:F0}",
                User.Weight, User.Height, User.Age, User.BMI, User.DailyCalories);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to save preferences");
        }
    }

    private void LoadPreferences()
    {
        try
        {
            var loaded = _preferencesService.LoadPreferences();
            if (loaded.HasValue)
            {
                var (user, proteinPct, fatPct, carbsPct) = loaded.Value;
                User = user;
                ProteinPercentage = proteinPct;
                FatPercentage = fatPct;
                CarbsPercentage = carbsPct;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load preferences");
        }
    }
}
