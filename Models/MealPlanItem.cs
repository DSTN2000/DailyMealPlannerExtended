namespace Lab4.Models;

public class MealPlanItem
{
    public Product Product { get; set; }
    public double Weight { get; set; } = 100.0; // in grams

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
        Weight = weight;
    }
}
