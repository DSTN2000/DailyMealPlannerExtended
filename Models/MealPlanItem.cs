using CommunityToolkit.Mvvm.ComponentModel;

namespace DailyMealPlannerExtended.Models;

public partial class MealPlanItem : ObservableObject
{
    public Product Product { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Calories), nameof(Protein), nameof(TotalFat), nameof(Carbohydrates), nameof(Sodium), nameof(Fiber), nameof(Sugar))]
    private double _weight = 100.0; // in grams

    // Calculated nutritional values (based on weight)
    public double Calories => Product.Calories * (Weight / 100.0);
    public double Protein => Product.Protein * (Weight / 100.0);
    public double TotalFat => Product.TotalFat * (Weight / 100.0);
    public double Carbohydrates => Product.Carbohydrates * (Weight / 100.0);
    public double Sodium => Product.Sodium * (Weight / 100.0);
    public double Fiber => Product.Fiber * (Weight / 100.0);
    public double Sugar => Product.Sugar * (Weight / 100.0);

    public MealPlanItem()
    {
        Product = new Product();
    }

    public MealPlanItem(Product product, double weight = 100.0)
    {
        Product = product;
        _weight = weight;
    }
}
