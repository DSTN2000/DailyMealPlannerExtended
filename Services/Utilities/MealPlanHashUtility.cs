using DailyMealPlannerExtended.Models;
using System;
using System.Security.Cryptography;
using System.Text;

namespace DailyMealPlannerExtended.Services.Utilities;

/// <summary>
/// Utility class for computing deterministic hashes of meal plans.
/// Used for comparing meal plan content across sync operations.
/// </summary>
public static class MealPlanHashUtility
{
    /// <summary>
    /// Computes a SHA256 hash of a meal plan based on its content.
    /// The hash is deterministic and ignores the Date property to allow
    /// comparison of meal plans across different dates.
    /// </summary>
    /// <param name="mealPlan">The meal plan to hash</param>
    /// <returns>Base64-encoded SHA256 hash string</returns>
    public static string ComputeHash(DailyMealPlan mealPlan)
    {
        // Create a normalized copy with Date set to MinValue
        // This ensures the hash is based only on content, not the date
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
                // Copy only the nutritional properties, ignoring UI-specific data like images and notes
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

        // Serialize to XML for consistent string representation
        var xml = MealPlanService.SerializeMealPlanToXml(normalizedPlan);

        // Compute SHA256 hash
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(xml);
        var hashBytes = sha256.ComputeHash(bytes);

        return Convert.ToBase64String(hashBytes);
    }
}
