using System.Collections.Concurrent;
using System.Text.Json;
using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.Models.Supabase;
using DailyMealPlannerExtended.Services.Utilities;

namespace DailyMealPlannerExtended.Services;

public enum SyncOperation
{
    SyncPreferences,
    SyncSnapshot,
    SyncFavorite,
    DeleteFavorite,
    DownloadAllData
}

public class PendingSyncOperation
{
    public SyncOperation Operation { get; set; }
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AutoSyncService
{
    private readonly SupabaseAuthService _authService;
    private readonly NetworkConnectivityService _connectivityService;
    private readonly UserPreferencesService _preferencesService;
    private readonly DaySnapshotService _snapshotService;
    private readonly FavoriteMealPlansService _favoritesService;

    private readonly ConcurrentQueue<PendingSyncOperation> _syncQueue = new();
    private readonly Timer _syncTimer;
    private bool _isSyncing;

    public event EventHandler? SyncCompleted;
    public DateTime? LastSyncTime { get; private set; }
    public int PendingOperationsCount => _syncQueue.Count;

    public AutoSyncService(
        SupabaseAuthService authService,
        NetworkConnectivityService connectivityService,
        UserPreferencesService preferencesService,
        DaySnapshotService snapshotService,
        FavoriteMealPlansService favoritesService)
    {
        _authService = authService;
        _connectivityService = connectivityService;
        _preferencesService = preferencesService;
        _snapshotService = snapshotService;
        _favoritesService = favoritesService;

        // Process sync queue every 5 seconds
        _syncTimer = new Timer(ProcessSyncQueue, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        // Subscribe to connectivity changes
        _connectivityService.ConnectionStatusChanged += OnConnectionStatusChanged;
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatus status)
    {
        if (status == ConnectionStatus.Online && _authService.IsAuthenticated)
        {
            Logger.Instance.Information("Connection restored - processing queued sync operations");
            _ = ProcessSyncQueueAsync();
        }
    }

    /// <summary>
    /// Queue a sync operation to be processed when online
    /// </summary>
    public void QueueSync(SyncOperation operation, object? data = null)
    {
        _syncQueue.Enqueue(new PendingSyncOperation
        {
            Operation = operation,
            Data = data,
            Timestamp = DateTime.UtcNow
        });

        Logger.Instance.Debug("Queued sync operation: {Operation}", operation);

        // Try to process immediately if online
        if (_connectivityService.IsOnline && _authService.IsAuthenticated)
        {
            _ = ProcessSyncQueueAsync();
        }
    }

    private void ProcessSyncQueue(object? state)
    {
        _ = ProcessSyncQueueAsync();
    }

    private async Task ProcessSyncQueueAsync()
    {
        if (_isSyncing || !_connectivityService.IsOnline || !_authService.IsAuthenticated)
        {
            return;
        }

        if (_syncQueue.IsEmpty)
        {
            return;
        }

        try
        {
            _isSyncing = true;
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                Logger.Instance.Warning("Cannot sync: user ID is null");
                return;
            }

            while (_syncQueue.TryDequeue(out var operation))
            {
                try
                {
                    await ProcessOperationAsync(client, userId, operation);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(ex, "Failed to process sync operation: {Operation}", operation.Operation);
                    // Re-queue failed operation (put it back)
                    _syncQueue.Enqueue(operation);
                    break; // Stop processing if an error occurs
                }
            }

            LastSyncTime = DateTime.Now;
            SyncCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Error in sync queue processing");
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task ProcessOperationAsync(Supabase.Client client, string userId, PendingSyncOperation operation)
    {
        switch (operation.Operation)
        {
            case SyncOperation.SyncPreferences:
                await SyncUserPreferencesAsync(client, userId);
                break;

            case SyncOperation.SyncSnapshot:
                if (operation.Data is DaySnapshot snapshot)
                {
                    await SyncSnapshotAsync(client, userId, snapshot);
                }
                break;

            case SyncOperation.SyncFavorite:
                if (operation.Data is DailyMealPlan mealPlan)
                {
                    await SyncFavoriteAsync(client, userId, mealPlan);
                }
                break;

            case SyncOperation.DeleteFavorite:
                if (operation.Data is DailyMealPlan deletedMealPlan)
                {
                    await DeleteFavoriteAsync(client, userId, deletedMealPlan);
                }
                break;

            case SyncOperation.DownloadAllData:
                await DownloadAllDataAsync(client, userId);
                break;
        }

        Logger.Instance.Debug("Processed sync operation: {Operation}", operation.Operation);
    }

    /// <summary>
    /// Download all user data from Supabase and merge with local data (used on login)
    /// </summary>
    public async Task InitialSyncAsync()
    {
        if (!_authService.IsAuthenticated || !_connectivityService.IsOnline)
        {
            Logger.Instance.Warning("Cannot perform initial sync: not authenticated or offline");
            return;
        }

        try
        {
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                Logger.Instance.Warning("Cannot sync: user ID is null");
                return;
            }

            Logger.Instance.Information("Starting initial sync...");

            await DownloadAllDataAsync(client, userId);

            LastSyncTime = DateTime.Now;
            Logger.Instance.Information("Initial sync completed");

            // Fire sync completed event so ViewModels can reload data
            SyncCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Initial sync failed");
        }
    }

    private async Task DownloadAllDataAsync(Supabase.Client client, string userId)
    {
        // Download preferences
        await DownloadPreferencesAsync(client, userId);

        // Download snapshots
        await DownloadSnapshotsAsync(client, userId);

        // Download favorites
        await DownloadFavoritesAsync(client, userId);
    }

    private async Task DownloadPreferencesAsync(Supabase.Client client, string userId)
    {
        try
        {
            var remotePrefs = await client
                .From<UserPreferencesRecord>()
                .Where(x => x.UserId == userId)
                .Get();

            if (remotePrefs.Models.Count > 0)
            {
                var record = remotePrefs.Models[0];
                if (!string.IsNullOrEmpty(record.PreferencesJson))
                {
                    // Parse JSON preferences
                    using var doc = JsonDocument.Parse(record.PreferencesJson);
                    var root = doc.RootElement;

                    var user = new User
                    {
                        Weight = root.GetProperty("weight").GetDouble(),
                        Height = root.GetProperty("height").GetDouble(),
                        Age = root.GetProperty("age").GetInt32(),
                        ActivityLevel = Enum.Parse<ActivityLevel>(root.GetProperty("activityLevel").GetString()!)
                    };

                    // Parse nutrients split
                    var nutrientsSplit = root.GetProperty("nutrientsSplit");
                    user.NutrientsSplit = (
                        p: nutrientsSplit.GetProperty("protein").GetDouble(),
                        f: nutrientsSplit.GetProperty("fat").GetDouble(),
                        c: nutrientsSplit.GetProperty("carbs").GetDouble()
                    );

                    // Save preferences locally
                    _preferencesService.SavePreferences(user);
                    Logger.Instance.Information("Downloaded and saved user preferences");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to download preferences");
        }
    }

    private async Task DownloadSnapshotsAsync(Supabase.Client client, string userId)
    {
        try
        {
            var remoteSnapshots = await client
                .From<UserSnapshotRecord>()
                .Where(x => x.UserId == userId)
                .Get();

            foreach (var record in remoteSnapshots.Models)
            {
                if (!string.IsNullOrEmpty(record.MealPlanXml) && !string.IsNullOrEmpty(record.UserPreferencesJson))
                {
                    var mealPlan = MealPlanService.DeserializeMealPlanFromXml(record.MealPlanXml!);
                    if (mealPlan != null)
                    {
                        // Parse user preferences from JSON
                        using var doc = JsonDocument.Parse(record.UserPreferencesJson);
                        var root = doc.RootElement;

                        var userPrefs = new User
                        {
                            Weight = root.GetProperty("weight").GetDouble(),
                            Height = root.GetProperty("height").GetDouble(),
                            Age = root.GetProperty("age").GetInt32(),
                            ActivityLevel = Enum.Parse<ActivityLevel>(root.GetProperty("activityLevel").GetString()!)
                        };

                        var nutrientsSplit = root.GetProperty("nutrientsSplit");
                        userPrefs.NutrientsSplit = (
                            p: nutrientsSplit.GetProperty("protein").GetDouble(),
                            f: nutrientsSplit.GetProperty("fat").GetDouble(),
                            c: nutrientsSplit.GetProperty("carbs").GetDouble()
                        );

                        // Create snapshot and save locally
                        var snapshot = new DaySnapshot
                        {
                            MealPlan = mealPlan,
                            UserPreferences = userPrefs
                        };

                        _snapshotService.SaveSnapshot(mealPlan, userPrefs);
                        Logger.Instance.Debug("Downloaded and saved snapshot for date: {Date}", record.Date);
                    }
                }
            }

            Logger.Instance.Information("Downloaded and saved {Count} snapshots", remoteSnapshots.Models.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to download snapshots");
        }
    }

    private async Task DownloadFavoritesAsync(Supabase.Client client, string userId)
    {
        try
        {
            var remoteFavorites = await client
                .From<UserFavoriteRecord>()
                .Where(x => x.UserId == userId)
                .Get();

            var localFavorites = _favoritesService.GetFavorites();
            var localHashes = localFavorites.Select(x => ComputeMealPlanHash(x.mealPlan)).ToHashSet();

            foreach (var record in remoteFavorites.Models)
            {
                // Only add if not already in local favorites
                if (record.MealPlanHash != null && !localHashes.Contains(record.MealPlanHash))
                {
                    if (!string.IsNullOrEmpty(record.MealPlanXml))
                    {
                        var mealPlan = MealPlanService.DeserializeMealPlanFromXml(record.MealPlanXml!);
                        if (mealPlan != null)
                        {
                            _favoritesService.AddToFavorites(mealPlan);
                        }
                    }
                }
            }

            Logger.Instance.Information("Downloaded favorites (merged {RemoteCount} remote with {LocalCount} local)",
                remoteFavorites.Models.Count, localFavorites.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to download favorites");
        }
    }

    private async Task SyncUserPreferencesAsync(Supabase.Client client, string userId)
    {
        try
        {
            var localPrefs = _preferencesService.LoadPreferences();
            if (localPrefs == null)
            {
                return;
            }

            var prefsJson = UserPreferencesService.SerializeUserToJson(localPrefs);

            var existingPrefs = await client
                .From<UserPreferencesRecord>()
                .Where(x => x.UserId == userId)
                .Get();

            if (existingPrefs.Models.Count > 0)
            {
                var record = existingPrefs.Models[0];
                record.PreferencesJson = prefsJson;
                record.UpdatedAt = DateTime.UtcNow;

                await client.From<UserPreferencesRecord>().Update(record);
            }
            else
            {
                var newRecord = new UserPreferencesRecord
                {
                    UserId = userId,
                    PreferencesJson = prefsJson,
                    UpdatedAt = DateTime.UtcNow
                };

                await client.From<UserPreferencesRecord>().Insert(newRecord);
            }

            Logger.Instance.Debug("Synced user preferences");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sync user preferences");
            throw;
        }
    }

    private async Task SyncSnapshotAsync(Supabase.Client client, string userId, DaySnapshot snapshot)
    {
        try
        {
            var dateStr = snapshot.MealPlan.Date.ToString("yyyy-MM-dd");

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

            var mealPlanXml = MealPlanService.SerializeMealPlanToXml(snapshot.MealPlan);

            var existingSnapshot = await client
                .From<UserSnapshotRecord>()
                .Where(x => x.UserId == userId)
                .Where(x => x.Date == dateStr)
                .Get();

            if (existingSnapshot.Models.Count > 0)
            {
                var record = existingSnapshot.Models[0];
                record.UserPreferencesJson = userPrefsJson;
                record.MealPlanXml = mealPlanXml;
                record.UpdatedAt = DateTime.UtcNow;

                await client.From<UserSnapshotRecord>().Update(record);
            }
            else
            {
                var newRecord = new UserSnapshotRecord
                {
                    UserId = userId,
                    Date = dateStr,
                    UserPreferencesJson = userPrefsJson,
                    MealPlanXml = mealPlanXml,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await client.From<UserSnapshotRecord>().Insert(newRecord);
            }

            Logger.Instance.Debug("Synced snapshot for date: {Date}", dateStr);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sync snapshot");
            throw;
        }
    }

    private async Task SyncFavoriteAsync(Supabase.Client client, string userId, DailyMealPlan mealPlan)
    {
        try
        {
            var hash = ComputeMealPlanHash(mealPlan);
            var xml = MealPlanService.SerializeMealPlanToXml(mealPlan);

            // Check if already exists
            var existing = await client
                .From<UserFavoriteRecord>()
                .Where(x => x.UserId == userId)
                .Where(x => x.MealPlanHash == hash)
                .Get();

            if (existing.Models.Count == 0)
            {
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

                await client.From<UserFavoriteRecord>().Insert(newRecord);
                Logger.Instance.Debug("Synced favorite: {Name}", mealPlan.Name);
            }
            else
            {
                // Update existing favorite (for images/notes changes)
                var record = existing.Models[0];
                record.MealPlanXml = xml;
                record.Name = mealPlan.Name;
                record.Date = mealPlan.Date.ToString("yyyy-MM-dd");
                record.TotalCalories = mealPlan.TotalCalories;
                record.TotalProtein = mealPlan.TotalProtein;
                record.TotalFat = mealPlan.TotalFat;
                record.TotalCarbohydrates = mealPlan.TotalCarbohydrates;
                record.UpdatedAt = DateTime.UtcNow;

                await client.From<UserFavoriteRecord>().Update(record);
                Logger.Instance.Debug("Updated favorite: {Name}", mealPlan.Name);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to sync favorite");
            throw;
        }
    }

    private async Task DeleteFavoriteAsync(Supabase.Client client, string userId, DailyMealPlan mealPlan)
    {
        try
        {
            var hash = ComputeMealPlanHash(mealPlan);

            // Delete the favorite from cloud
            await client
                .From<UserFavoriteRecord>()
                .Where(x => x.UserId == userId)
                .Where(x => x.MealPlanHash == hash)
                .Delete();

            Logger.Instance.Debug("Deleted favorite from cloud: {Name}", mealPlan.Name);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to delete favorite from cloud");
            throw;
        }
    }

    private static string ComputeMealPlanHash(DailyMealPlan mealPlan)
    {
        return MealPlanHashUtility.ComputeHash(mealPlan);
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        _connectivityService.ConnectionStatusChanged -= OnConnectionStatusChanged;
    }
}
