using CommunityToolkit.Mvvm.ComponentModel;

namespace DailyMealPlannerExtended.Models;

public partial class MealPlanItem : ObservableObject
{
    public Product Product { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Calories), nameof(Protein), nameof(TotalFat), nameof(Carbohydrates), nameof(Sodium), nameof(Fiber), nameof(Sugar))]
    private double _weight = 100.0; // in grams

    [ObservableProperty]
    private string? _image; // Base64 encoded image

    [ObservableProperty]
    private string? _note; // User's note about this meal item

    // Calculated nutritional values (based on weight)
    public double Calories => Product.Calories * (Weight / 100.0);
    public double Protein => Product.Protein * (Weight / 100.0);
    public double TotalFat => Product.TotalFat * (Weight / 100.0);
    public double Carbohydrates => Product.Carbohydrates * (Weight / 100.0);
    public double Sodium => Product.Sodium * (Weight / 100.0);
    public double Fiber => Product.Fiber * (Weight / 100.0);
    public double Sugar => Product.Sugar * (Weight / 100.0);

    public static double MinWeight = 10.0;
    public static double MaxWeight = 5000.0;

    public MealPlanItem()
    {
        Product = new Product();
    }

    public MealPlanItem(Product product, double weight = 100.0)
    {
        Product = product;
        _weight = weight > 0 ? weight : product.Serving;
    }

    partial void OnWeightChanging(double value)
    {
        // Log warnings for out-of-bounds values
        if (value < MinWeight)
        {
            Logger.Instance.Warning("Attempted to set weight below minimum ({MinWeight}g). Value will be clamped.", MinWeight);
        }
        else if (value > MaxWeight)
        {
            Logger.Instance.Warning("Attempted to set weight above maximum ({MaxWeight}g). Value will be clamped.", MaxWeight);
        }
    }

    partial void OnWeightChanged(double value)
    {
        // Clamp to valid range if out of bounds
        #pragma warning disable MVVMTK0034
        if (value < MinWeight)
        {
            _weight = MinWeight;
            OnPropertyChanged(nameof(Weight));
        }
        else if (value > MaxWeight)
        {
            _weight = MaxWeight;
            OnPropertyChanged(nameof(Weight));
        }
        #pragma warning restore MVVMTK0034
    }
}
