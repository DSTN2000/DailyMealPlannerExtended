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

    public ObservableCollection<ActivityLevel> ActivityLevels { get; } = new()
    {
        ActivityLevel.Sedentary,
        ActivityLevel.Moderate,
        ActivityLevel.Medium,
        ActivityLevel.High
    };

    // Properties that bind directly to User.NutrientsSplit
    public double ProteinPercentage
    {
        get => User.NutrientsSplit.p;
        set
        {
            if (Math.Abs(User.NutrientsSplit.p - value) > 0.01)
            {
                User.NutrientsSplit = (value, User.NutrientsSplit.f, User.NutrientsSplit.c);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPercentage));
                OnPropertyChanged(nameof(IsValidSplit));
            }
        }
    }

    public double FatPercentage
    {
        get => User.NutrientsSplit.f;
        set
        {
            if (Math.Abs(User.NutrientsSplit.f - value) > 0.01)
            {
                User.NutrientsSplit = (User.NutrientsSplit.p, value, User.NutrientsSplit.c);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPercentage));
                OnPropertyChanged(nameof(IsValidSplit));
            }
        }
    }

    public double CarbsPercentage
    {
        get => User.NutrientsSplit.c;
        set
        {
            if (Math.Abs(User.NutrientsSplit.c - value) > 0.01)
            {
                User.NutrientsSplit = (User.NutrientsSplit.p, User.NutrientsSplit.f, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPercentage));
                OnPropertyChanged(nameof(IsValidSplit));
            }
        }
    }

    public double TotalPercentage => User.NutrientsSplit.p + User.NutrientsSplit.f + User.NutrientsSplit.c;
    public bool IsValidSplit => Math.Abs(TotalPercentage - 100) < 0.01;

    public UserPreferencesViewModel()
    {
        _preferencesService = new UserPreferencesService();
        LoadPreferences();
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        User.Weight = 75;
        User.Height = 170;
        User.Age = 30;
        User.ActivityLevel = ActivityLevel.Moderate;
        User.NutrientsSplit = (30, 30, 40);

        // Notify UI that percentages changed
        OnPropertyChanged(nameof(ProteinPercentage));
        OnPropertyChanged(nameof(FatPercentage));
        OnPropertyChanged(nameof(CarbsPercentage));
        OnPropertyChanged(nameof(TotalPercentage));
        OnPropertyChanged(nameof(IsValidSplit));
    }

    [RelayCommand]
    private void SavePreferences()
    {
        try
        {
            _preferencesService.SavePreferences(User);
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
            var user = _preferencesService.LoadPreferences();
            if (user != null)
            {
                User = user;

                // Notify UI that all percentage properties may have changed
                OnPropertyChanged(nameof(ProteinPercentage));
                OnPropertyChanged(nameof(FatPercentage));
                OnPropertyChanged(nameof(CarbsPercentage));
                OnPropertyChanged(nameof(TotalPercentage));
                OnPropertyChanged(nameof(IsValidSplit));
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load preferences");
        }
    }
}
