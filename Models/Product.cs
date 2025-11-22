namespace DailyMealPlannerExtended.Models;

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new(); // subcategories
    public List<string> Ingredients { get; set; } = new();
    public List<string>? AltNames { get; set; } = null;


    public double Serving { get; set; } // in g/ml
    public ServingUnit Unit { get; set; }

    // Nutritional data per 100g
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double TotalFat { get; set; }
    public double Carbohydrates { get; set; }

    // Additional nutritional info
    public double Sodium { get; set; }
    public double Fiber { get; set; }
    public double Sugar { get; set; }

    public Product() { }
}

public enum ServingUnit
{
    g,
    ml
}
