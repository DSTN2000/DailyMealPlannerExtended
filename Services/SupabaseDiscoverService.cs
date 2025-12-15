using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.Models.Supabase;
using DailyMealPlannerExtended.Services.Utilities;

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

            // Strip images and notes before sharing (privacy & size optimization)
            var cleanMealPlan = MealPlanService.StripImagesAndNotes(mealPlan);

            var hash = MealPlanHashUtility.ComputeHash(cleanMealPlan);
            var xml = MealPlanService.SerializeMealPlanToXml(cleanMealPlan);

            // Check if already shared
            var existing = await client
                .From<SharedMealPlanRecord>()
                .Where(x => x.UserId == userId)
                .Where(x => x.MealPlanHash == hash)
                .Get();

            if (existing.Models.Count > 0)
            {
                Logger.Instance.Information("Meal plan already shared - duplicate detected");
                throw new InvalidOperationException("This meal plan has already been shared to Discover.");
            }

            var newRecord = new SharedMealPlanRecord
            {
                UserId = userId,
                UserEmail = userEmail,
                MealPlanXml = xml,
                MealPlanHash = hash,
                Name = cleanMealPlan.Name,
                Date = cleanMealPlan.Date.ToString("yyyy-MM-dd"),
                TotalCalories = cleanMealPlan.TotalCalories,
                TotalProtein = cleanMealPlan.TotalProtein,
                TotalFat = cleanMealPlan.TotalFat,
                TotalCarbohydrates = cleanMealPlan.TotalCarbohydrates,
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
        catch (InvalidOperationException)
        {
            // Re-throw InvalidOperationException so it can be handled by the caller (ViewModel)
            throw;
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

            // Get ALL likes for all meal plans
            var allLikes = await client
                .From<MealPlanLikeRecord>()
                .Get();

            // Group likes by meal plan ID to get counts
            var likesCountByMealPlan = allLikes.Models
                .GroupBy(l => l.MealPlanId)
                .ToDictionary(g => g.Key, g => g.Count());

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
                        // Get the actual likes count from the likes table
                        mealPlan.LikesCount = likesCountByMealPlan.GetValueOrDefault(record.Id, 0);
                        mealPlan.IsLikedByCurrentUser = likedMealPlanIds.Contains(record.Id);
                        mealPlans.Add(mealPlan);
                    }
                }
            }

            // Sort by actual likes count (since we got fresh data)
            mealPlans = mealPlans.OrderByDescending(m => m.LikesCount).ThenByDescending(m => m.SharedMealPlanId).ToList();

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
    /// Likes a shared meal plan and returns the updated likes count
    /// </summary>
    public async Task<(bool success, int likesCount)> LikeMealPlanAsync(long mealPlanId)
    {
        if (!_authService.IsAuthenticated)
        {
            Logger.Instance.Warning("Cannot like meal plan: user not authenticated");
            return (false, 0);
        }

        try
        {
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                Logger.Instance.Error("User ID is null or empty");
                return (false, 0);
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
                var currentCount = await GetLikesCountAsync(mealPlanId);
                return (true, currentCount);
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

            // Update likes count and return the new count
            var updatedCount = await UpdateLikesCountAsync(mealPlanId);

            Logger.Instance.Information("Liked meal plan {MealPlanId}, new count: {Count}", mealPlanId, updatedCount);
            return (true, updatedCount);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to like meal plan");
            return (false, 0);
        }
    }

    /// <summary>
    /// Unlikes a shared meal plan and returns the updated likes count
    /// </summary>
    public async Task<(bool success, int likesCount)> UnlikeMealPlanAsync(long mealPlanId)
    {
        if (!_authService.IsAuthenticated)
        {
            Logger.Instance.Warning("Cannot unlike meal plan: user not authenticated");
            return (false, 0);
        }

        try
        {
            var client = _authService.GetClient();
            var userId = _authService.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                Logger.Instance.Error("User ID is null or empty");
                return (false, 0);
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
                var currentCount = await GetLikesCountAsync(mealPlanId);
                return (true, currentCount);
            }

            // Delete like using the model directly
            var likeToDelete = existing.Models[0];
            await likeToDelete.Delete<MealPlanLikeRecord>();

            // Update likes count and return the new count
            var updatedCount = await UpdateLikesCountAsync(mealPlanId);

            Logger.Instance.Information("Unliked meal plan {MealPlanId}, new count: {Count}", mealPlanId, updatedCount);
            return (true, updatedCount);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to unlike meal plan");
            return (false, 0);
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
    /// Gets the current likes count for a meal plan from the database
    /// </summary>
    private async Task<int> GetLikesCountAsync(long mealPlanId)
    {
        try
        {
            var client = _authService.GetClient();

            // Count likes
            var likes = await client
                .From<MealPlanLikeRecord>()
                .Where(x => x.MealPlanId == mealPlanId)
                .Get();

            return likes.Models.Count;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to get likes count");
            return 0;
        }
    }

    /// <summary>
    /// Updates the likes count for a meal plan and returns the new count
    /// </summary>
    private async Task<int> UpdateLikesCountAsync(long mealPlanId)
    {
        try
        {
            var client = _authService.GetClient();

            // Count likes
            var likesCount = await GetLikesCountAsync(mealPlanId);

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

            return likesCount;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to update likes count");
            return 0;
        }
    }

}
