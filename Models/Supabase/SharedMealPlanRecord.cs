using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DailyMealPlannerExtended.Models.Supabase;

[Table("shared_meal_plans")]
public class SharedMealPlanRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("user_email")]
    public string? UserEmail { get; set; }

    [Column("meal_plan_xml")]
    public string? MealPlanXml { get; set; }

    [Column("meal_plan_hash")]
    public string? MealPlanHash { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("date")]
    public string? Date { get; set; }

    [Column("total_calories")]
    public double? TotalCalories { get; set; }

    [Column("total_protein")]
    public double? TotalProtein { get; set; }

    [Column("total_fat")]
    public double? TotalFat { get; set; }

    [Column("total_carbohydrates")]
    public double? TotalCarbohydrates { get; set; }

    [Column("likes_count")]
    public int LikesCount { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
