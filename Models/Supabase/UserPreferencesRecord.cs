using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DailyMealPlannerExtended.Models.Supabase;

[Table("user_preferences")]
public class UserPreferencesRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("preferences_json")]
    public string? PreferencesJson { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
