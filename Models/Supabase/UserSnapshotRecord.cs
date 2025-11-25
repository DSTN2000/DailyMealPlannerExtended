using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DailyMealPlannerExtended.Models.Supabase;

[Table("user_snapshots")]
public class UserSnapshotRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("date")]
    public string? Date { get; set; }

    [Column("user_preferences_json")]
    public string? UserPreferencesJson { get; set; }

    [Column("meal_plan_xml")]
    public string? MealPlanXml { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
