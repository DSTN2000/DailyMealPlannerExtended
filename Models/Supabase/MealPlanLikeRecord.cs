using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DailyMealPlannerExtended.Models.Supabase;

[Table("meal_plan_likes")]
public class MealPlanLikeRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("meal_plan_id")]
    public long MealPlanId { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
