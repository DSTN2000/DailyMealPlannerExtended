using CommunityToolkit.Mvvm.ComponentModel;

namespace DailyMealPlannerExtended.Models;

public partial class User : ObservableObject
{
    // Input values
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BMI), nameof(BMR), nameof(DailyCalories), nameof(DailyProtein), nameof(DailyFat), nameof(DailyCarbohydrates))]
    private double _weight = 75; // in kg

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BMI), nameof(BMR), nameof(DailyCalories), nameof(DailyProtein), nameof(DailyFat), nameof(DailyCarbohydrates))]
    private double _height = 170; // in cm

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BMR), nameof(DailyCalories), nameof(DailyProtein), nameof(DailyFat), nameof(DailyCarbohydrates))]
    private double _age = 30; // in years

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ARM), nameof(DailyCalories), nameof(DailyProtein), nameof(DailyFat), nameof(DailyCarbohydrates))]
    private ActivityLevel _activityLevel = ActivityLevel.Moderate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DailyProtein), nameof(DailyFat), nameof(DailyCarbohydrates))]
    private (double p, double f, double c) _nutrientsSplit = (30, 30, 40); // in %

    // Calculated values
    public double BMI => Weight / Math.Pow(Height / 100.0, 2);
    public double ARM => ActivityLevel switch
    {
        ActivityLevel.Sedentary => 1.2,
        ActivityLevel.Moderate => 1.375,
        ActivityLevel.Medium => 1.55,
        ActivityLevel.High => 1.725,
        _ => 1.2
    };
    public double BMR => 447.593 + 9.247 * Weight + 3.098 * Height - 4.330 * Age;
    public double DailyCalories => BMR * ARM;
    public double DailyProtein => DailyCalories * (NutrientsSplit.p / 100) / 4; // in grams, 4 cal/g
    public double DailyFat => DailyCalories * (NutrientsSplit.f / 100) / 9; // in grams, 9 cal/g
    public double DailyCarbohydrates => DailyCalories * (NutrientsSplit.c / 100) / 4; // in grams, 4 cal/g
}

public enum ActivityLevel
{
    Sedentary,
    Moderate,
    Medium,
    High
}
