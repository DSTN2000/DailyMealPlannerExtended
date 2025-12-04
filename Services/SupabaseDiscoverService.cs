using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.Models.Supabase;

namespace DailyMealPlannerExtended.Services;

public class SupabaseDiscoverService
{
    private readonly SupabaseAuthService _authService;

    public SupabaseDiscoverService(SupabaseAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Shares a meal plan to the discover feed
    /// </summary>
    public async Task<bool> ShareMealPlanAsync(DailyMealPlan mealPlan)
    {
        if (!_authService.IsAuthenticated)
        {
            Logger.Instance.Warning("Cannot share meal plan: user not authenticated");
            return false;
        }

        try
        {
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;
            var userEmail = _authService.CurrentUser?.Email;

            if (string.IsNullOrEmpty(userId))
            {
                Logger.Instance.Error("User ID is null or empty");
                return false;
            }

            var hash = ComputeMealPlanHash(mealPlan);
            var xml = MealPlanService.SerializeMealPlanToXml(mealPlan);

            // Check if already shared
            var existing = await client
                .From<SharedMealPlanRecord>()
                .Where(x => x.UserId == userId)
                .Where(x => x.MealPlanHash == hash)
                .Get();

            if (existing.Models.Count > 0)
            {
                Logger.Instance.Information("Meal plan already shared");
                return true;
            }

            var newRecord = new SharedMealPlanRecord
            {
                UserId = userId,
                UserEmail = userEmail,
                MealPlanXml = xml,
                MealPlanHash = hash,
                Name = mealPlan.Name,
                Date = mealPlan.Date.ToString("yyyy-MM-dd"),
                TotalCalories = mealPlan.TotalCalories,
                TotalProtein = mealPlan.TotalProtein,
                TotalFat = mealPlan.TotalFat,
                TotalCarbohydrates = mealPlan.TotalCarbohydrates,
                LikesCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await client
                .From<SharedMealPlanRecord>()
                .Insert(newRecord);

            Logger.Instance.Information("Shared meal plan '{Name}' to discover feed", mealPlan.Name);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to share meal plan");
            return false;
        }
    }

    /// <summary>
    /// Gets shared meal plans sorted by likes count
    /// </summary>
    public async Task<List<DailyMealPlan>> GetSharedMealPlansAsync(int limit = 50)
    {
        if (!_authService.IsAuthenticated)
        {
            Logger.Instance.Warning("Cannot get shared meal plans: user not authenticated");
            return new List<DailyMealPlan>();
        }

        try
        {
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            // Get shared meal plans sorted by likes
            var response = await client
                .From<SharedMealPlanRecord>()
                .Order("likes_count", Supabase.Postgrest.Constants.Ordering.Descending)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(limit)
                .Get();

            var mealPlans = new List<DailyMealPlan>();

            // Get user's likes
            var userLikes = await GetUserLikesAsync();
            var likedMealPlanIds = userLikes.Select(l => l.MealPlanId).ToHashSet();

            foreach (var record in response.Models)
            {
                if (record.MealPlanXml != null)
                {
                    var mealPlan = MealPlanService.DeserializeMealPlanFromXml(record.MealPlanXml);
                    if (mealPlan != null)
                    {
                        mealPlan.SharedMealPlanId = record.Id;
                        mealPlan.AuthorId = record.UserId;
                        mealPlan.AuthorEmail = record.UserEmail;
                        mealPlan.LikesCount = record.LikesCount;
                        mealPlan.IsLikedByCurrentUser = likedMealPlanIds.Contains(record.Id);
                        mealPlans.Add(mealPlan);
                    }
                }
            }

            Logger.Instance.Information("Retrieved {Count} shared meal plans", mealPlans.Count);
            return mealPlans;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to get shared meal plans");
            return new List<DailyMealPlan>();
        }
    }

    /// <summary>
    /// Likes a shared meal plan
    /// </summary>
    public async Task<bool> LikeMealPlanAsync(long mealPlanId)
    {
        if (!_authService.IsAuthenticated)
        {
            Logger.Instance.Warning("Cannot like meal plan: user not authenticated");
            return false;
        }

        try
        {
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                Logger.Instance.Error("User ID is null or empty");
                return false;
            }

            // Check if already liked
            var existing = await client
                .From<MealPlanLikeRecord>()
                .Where(x => x.MealPlanId == mealPlanId)
                .Where(x => x.UserId == userId)
                .Get();

            if (existing.Models.Count > 0)
            {
                Logger.Instance.Information("Meal plan already liked");
                return true;
            }

            // Add like
            var newLike = new MealPlanLikeRecord
            {
                MealPlanId = mealPlanId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await client
                .From<MealPlanLikeRecord>()
                .Insert(newLike);

            // Update likes count
            await UpdateLikesCountAsync(mealPlanId);

            Logger.Instance.Information("Liked meal plan {MealPlanId}", mealPlanId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to like meal plan");
            return false;
        }
    }

    /// <summary>
    /// Unlikes a shared meal plan
    /// </summary>
    public async Task<bool> UnlikeMealPlanAsync(long mealPlanId)
    {
        if (!_authService.IsAuthenticated)
        {
            Logger.Instance.Warning("Cannot unlike meal plan: user not authenticated");
            return false;
        }

        try
        {
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                Logger.Instance.Error("User ID is null or empty");
                return false;
            }

            // Get the like record
            var existing = await client
                .From<MealPlanLikeRecord>()
                .Where(x => x.MealPlanId == mealPlanId)
                .Where(x => x.UserId == userId)
                .Get();

            if (existing.Models.Count == 0)
            {
                Logger.Instance.Information("Meal plan not liked");
                return true;
            }

            // Delete like using the model directly
            var likeToDelete = existing.Models[0];
            await likeToDelete.Delete<MealPlanLikeRecord>();

            // Update likes count
            await UpdateLikesCountAsync(mealPlanId);

            Logger.Instance.Information("Unliked meal plan {MealPlanId}", mealPlanId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to unlike meal plan");
            return false;
        }
    }

    /// <summary>
    /// Gets the current user's likes
    /// </summary>
    private async Task<List<MealPlanLikeRecord>> GetUserLikesAsync()
    {
        try
        {
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                return new List<MealPlanLikeRecord>();
            }

            var response = await client
                .From<MealPlanLikeRecord>()
                .Where(x => x.UserId == userId)
                .Get();

            return response.Models;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to get user likes");
            return new List<MealPlanLikeRecord>();
        }
    }

    /// <summary>
    /// Updates the likes count for a meal plan
    /// </summary>
    private async Task UpdateLikesCountAsync(long mealPlanId)
    {
        try
        {
            var client = _authService.GetClient();

            // Count likes
            var likes = await client
                .From<MealPlanLikeRecord>()
                .Where(x => x.MealPlanId == mealPlanId)
                .Get();

            var likesCount = likes.Models.Count;

            // Get the meal plan
            var mealPlan = await client
                .From<SharedMealPlanRecord>()
                .Where(x => x.Id == mealPlanId)
                .Single();

            if (mealPlan != null)
            {
                mealPlan.LikesCount = likesCount;
                mealPlan.UpdatedAt = DateTime.UtcNow;

                await client
                    .From<SharedMealPlanRecord>()
                    .Update(mealPlan);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to update likes count");
        }
    }

    /// <summary>
    /// Computes a hash for a meal plan to detect duplicates
    /// </summary>
    private static string ComputeMealPlanHash(DailyMealPlan mealPlan)
    {
        // Create a normalized meal plan for hashing
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
}
