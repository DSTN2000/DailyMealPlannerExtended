namespace Lab4.Models;

public class User
{
    // Input values
    public double Weight { get; set; } // in kg
    public double Height { get; set; } // in cm
    public double Age { get; set; } // in years
    public ActivityLevel ActivityLevel { get; set; }
    public (double p, double f, double c) NutrientsSplit {get; set;} // in %

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
    public double DailyFat => DailyCalories * (NutrientsSplit.p / 100) / 9; // in grams, 9 cal/g
    public double DailyCarbohydrates => DailyCalories * (NutrientsSplit.p / 100) / 4; // in grams, 4 cal/g

    public User()
    {
        Weight = 75;
        Height = 170;
        Age = 30;
        ActivityLevel = ActivityLevel.Moderate;
    }
}

public enum ActivityLevel
{
    Sedentary,
    Moderate,
    Medium,
    High
}
