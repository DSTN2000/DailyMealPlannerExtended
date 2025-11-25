using System.Text.Json;
using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.Models.Supabase;

namespace DailyMealPlannerExtended.Services;

public class SupabaseSyncService
{
    private readonly SupabaseAuthService _authService;
    private readonly UserPreferencesService _preferencesService;
    private readonly DaySnapshotService _snapshotService;
    private readonly FavoriteMealPlansService _favoritesService;

    public event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;
    public event EventHandler? SyncCompleted;

    public DateTime? LastSyncTime { get; private set; }
    public bool IsSyncing { get; private set; }

    public SupabaseSyncService(
        SupabaseAuthService authService,
        UserPreferencesService preferencesService,
        DaySnapshotService snapshotService,
        FavoriteMealPlansService favoritesService)
    {
        _authService = authService;
        _preferencesService = preferencesService;
        _snapshotService = snapshotService;
        _favoritesService = favoritesService;
    }

    public async Task<bool> SyncAllAsync()
    {
        if (!_authService.IsAuthenticated)
        {
            Logger.Instance.Warning("Cannot sync: user not authenticated");
            return false;
        }

        if (IsSyncing)
        {
            Logger.Instance.Information("Sync already in progress");
            return false;
        }

        try
        {
            IsSyncing = true;
            OnSyncStatusChanged("Starting sync...");

            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                Logger.Instance.Error("User ID is null or empty");
                return false;
            }

            // Sync user preferences
            OnSyncStatusChanged("Syncing user preferences...");
            await SyncUserPreferencesAsync(client, userId);

            // Sync snapshots
            OnSyncStatusChanged("Syncing meal plan history...");
            await SyncSnapshotsAsync(client, userId);

            // Sync favorites
            OnSyncStatusChanged("Syncing favorites...");
            await SyncFavoritesAsync(client, userId);

            LastSyncTime = DateTime.Now;
            OnSyncStatusChanged("Sync completed successfully");
            SyncCompleted?.Invoke(this, EventArgs.Empty);

            Logger.Instance.Information("Sync completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Sync failed");
            OnSyncStatusChanged($"Sync failed: {ex.Message}");
            return false;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private async Task SyncUserPreferencesAsync(Supabase.Client client, string userId)
    {
        try
        {
            var localPrefs = _preferencesService.LoadPreferences();
            if (localPrefs == null)
            {
                Logger.Instance.Warning("No local preferences to sync");
                return;
            }

            // Serialize preferences to JSON
            var prefsJson = JsonSerializer.Serialize(new
            {
                weight = localPrefs.Weight,
                height = localPrefs.Height,
                age = localPrefs.Age,
                activityLevel = localPrefs.ActivityLevel.ToString(),
                nutrientsSplit = new
                {
                    protein = localPrefs.NutrientsSplit.p,
                    fat = localPrefs.NutrientsSplit.f,
                    carbs = localPrefs.NutrientsSplit.c
                }
            });

            // Check if preferences exist
            var existingPrefs = await client
                .From<UserPreferencesRecord>()
                .Where(x => x.UserId == userId)
                .Get();

            if (existingPrefs.Models.Count > 0)
            {
                // Update existing
                var record = existingPrefs.Models[0];
                record.PreferencesJson = prefsJson;
                record.UpdatedAt = DateTime.UtcNow;

                await client
                    .From<UserPreferencesRecord>()
                    .Update(record);

                Logger.Instance.Information("User preferences updated in Supabase");
            }
            else
            {
                // Insert new
                var newRecord = new UserPreferencesRecord
                {
                    UserId = userId,
                    PreferencesJson = prefsJson,
                    UpdatedAt = DateTime.UtcNow
                };

                await client
                    .From<UserPreferencesRecord>()
                    .Insert(newRecord);

                Logger.Instance.Information("User preferences inserted to Supabase");
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sync user preferences");
            throw;
        }
    }

    private async Task SyncSnapshotsAsync(Supabase.Client client, string userId)
    {
        try
        {
            var snapshotDates = _snapshotService.GetAllSnapshotDates();

            foreach (var date in snapshotDates)
            {
                var snapshot = _snapshotService.LoadSnapshot(date);
                if (snapshot == null) continue;

                var dateStr = snapshot.MealPlan.Date.ToString("yyyy-MM-dd");

                // Serialize user preferences
                var userPrefsJson = JsonSerializer.Serialize(new
                {
                    weight = snapshot.UserPreferences.Weight,
                    height = snapshot.UserPreferences.Height,
                    age = snapshot.UserPreferences.Age,
                    activityLevel = snapshot.UserPreferences.ActivityLevel.ToString(),
                    nutrientsSplit = new
                    {
                        protein = snapshot.UserPreferences.NutrientsSplit.p,
                        fat = snapshot.UserPreferences.NutrientsSplit.f,
                        carbs = snapshot.UserPreferences.NutrientsSplit.c
                    }
                });

                // Serialize meal plan
                var mealPlanXml = MealPlanService.SerializeMealPlanToXml(snapshot.MealPlan);

                // Check if snapshot exists
                var existingSnapshot = await client
                    .From<UserSnapshotRecord>()
                    .Where(x => x.UserId == userId)
                    .Where(x => x.Date == dateStr)
                    .Get();

                if (existingSnapshot.Models.Count > 0)
                {
                    // Update existing
                    var record = existingSnapshot.Models[0];
                    record.UserPreferencesJson = userPrefsJson;
                    record.MealPlanXml = mealPlanXml;
                    record.UpdatedAt = DateTime.UtcNow;

                    await client
                        .From<UserSnapshotRecord>()
                        .Update(record);
                }
                else
                {
                    // Insert new
                    var newRecord = new UserSnapshotRecord
                    {
                        UserId = userId,
                        Date = dateStr,
                        UserPreferencesJson = userPrefsJson,
                        MealPlanXml = mealPlanXml,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await client
                        .From<UserSnapshotRecord>()
                        .Insert(newRecord);
                }
            }

            Logger.Instance.Information("Synced {Count} snapshots to Supabase", snapshotDates.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sync snapshots");
            throw;
        }
    }

    private async Task SyncFavoritesAsync(Supabase.Client client, string userId)
    {
        try
        {
            var localFavorites = _favoritesService.GetFavorites();

            // Get existing favorites from Supabase
            var remoteFavorites = await client
                .From<UserFavoriteRecord>()
                .Where(x => x.UserId == userId)
                .Get();

            var remoteFavoriteHashes = remoteFavorites.Models
                .Select(x => x.MealPlanHash)
                .ToHashSet();

            // Upload local favorites that don't exist remotely
            foreach (var (_, mealPlan) in localFavorites)
            {
                var hash = ComputeMealPlanHash(mealPlan);

                if (!remoteFavoriteHashes.Contains(hash))
                {
                    var xml = MealPlanService.SerializeMealPlanToXml(mealPlan);

                    var newRecord = new UserFavoriteRecord
                    {
                        UserId = userId,
                        MealPlanXml = xml,
                        MealPlanHash = hash,
                        Name = mealPlan.Name,
                        Date = mealPlan.Date.ToString("yyyy-MM-dd"),
                        TotalCalories = mealPlan.TotalCalories,
                        TotalProtein = mealPlan.TotalProtein,
                        TotalFat = mealPlan.TotalFat,
                        TotalCarbohydrates = mealPlan.TotalCarbohydrates,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await client
                        .From<UserFavoriteRecord>()
                        .Insert(newRecord);
                }
            }

            // Download remote favorites that don't exist locally
            var localFavoriteHashes = localFavorites
                .Select(x => ComputeMealPlanHash(x.mealPlan))
                .ToHashSet();

            foreach (var remoteFavorite in remoteFavorites.Models)
            {
                if (remoteFavorite.MealPlanHash != null && !localFavoriteHashes.Contains(remoteFavorite.MealPlanHash))
                {
                    if (remoteFavorite.MealPlanXml != null)
                    {
                        var mealPlan = MealPlanService.DeserializeMealPlanFromXml(remoteFavorite.MealPlanXml);
                        if (mealPlan != null)
                        {
                            _favoritesService.AddToFavorites(mealPlan);
                        }
                    }
                }
            }

            Logger.Instance.Information("Synced favorites: {LocalCount} local, {RemoteCount} remote",
                localFavorites.Count, remoteFavorites.Models.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sync favorites");
            throw;
        }
    }

    private static string ComputeMealPlanHash(DailyMealPlan mealPlan)
    {
        // Create a temporary meal plan with normalized date for consistent hashing
        var normalizedPlan = new DailyMealPlan
        {
            Date = DateTime.MinValue,
            Name = mealPlan.Name
        };

        normalizedPlan.MealTimes.Clear();

        foreach (var mealTime in mealPlan.MealTimes)
        {
            var newMealTime = new MealTime(mealTime.Type, mealTime.Type == MealTimeType.Custom ? mealTime.Name : null);

            foreach (var item in mealTime.Items)
            {
                var productCopy = new Product
                {
                    Name = item.Product.Name,
                    Calories = item.Product.Calories,
                    Protein = item.Product.Protein,
                    TotalFat = item.Product.TotalFat,
                    Carbohydrates = item.Product.Carbohydrates,
                    Sodium = item.Product.Sodium,
                    Fiber = item.Product.Fiber,
                    Sugar = item.Product.Sugar,
                    Serving = item.Product.Serving,
                    Unit = item.Product.Unit
                };

                var newItem = new MealPlanItem(productCopy, item.Weight);
                newMealTime.Items.Add(newItem);
            }

            normalizedPlan.MealTimes.Add(newMealTime);
        }

        var xml = MealPlanService.SerializeMealPlanToXml(normalizedPlan);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(xml);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    private void OnSyncStatusChanged(string status)
    {
        SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs(status));
    }
}

public class SyncStatusEventArgs : EventArgs
{
    public string Status { get; }
    public DateTime Timestamp { get; }

    public SyncStatusEventArgs(string status)
    {
        Status = status;
        Timestamp = DateTime.Now;
    }
}
