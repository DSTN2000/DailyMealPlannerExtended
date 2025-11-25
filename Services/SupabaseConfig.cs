using DotNetEnv;

namespace DailyMealPlannerExtended.Services;

public static class SupabaseConfig
{
    public static string Url { get; private set; } = string.Empty;
    public static string PublishableKey { get; private set; } = string.Empty;

    public static void Load()
    {
        try
        {
            // Load .env file from project root
            var projectRoot = Directory.GetCurrentDirectory();
            var envPath = Path.Combine(projectRoot, ".env");

            if (!File.Exists(envPath))
            {
                Logger.Instance.Warning(".env file not found at: {Path}", envPath);
                return;
            }

            Env.Load(envPath);

            Url = Environment.GetEnvironmentVariable("SUPABASE_PROJECT_URL") ?? string.Empty;
            PublishableKey = Environment.GetEnvironmentVariable("SUPABASE_PUBLISHABLE_KEY") ?? string.Empty;

            if (string.IsNullOrEmpty(Url) || string.IsNullOrEmpty(PublishableKey))
            {
                Logger.Instance.Warning("Supabase credentials not found in .env file");
            }
            else
            {
                Logger.Instance.Information("Supabase configuration loaded successfully");
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load Supabase configuration");
        }
    }

    public static bool IsConfigured()
    {
        return !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(PublishableKey);
    }
}
