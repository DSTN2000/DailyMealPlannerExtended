using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DailyMealPlannerExtended.Models;

public partial class DailyMealPlan : ObservableObject
{
    public DateTime Date { get; set; } = DateTime.Today;

    [ObservableProperty]
    private string _name = "";

    // Properties for shared meal plans (Discover feature)
    [ObservableProperty]
    private string? _authorId;

    [ObservableProperty]
    private string? _authorEmail;

    [ObservableProperty]
    private int _likesCount;

    [ObservableProperty]
    private bool _isLikedByCurrentUser;

    // Shared meal plan ID (for tracking in Supabase)
    public long? SharedMealPlanId { get; set; }

    public ObservableCollection<MealTime> MealTimes { get; set; }

    public IEnumerable<string> MealTimeNames => MealTimes.Select((x) => x.Name);

    // Calculated daily totals
    public double TotalCalories => MealTimes.Sum(meal => meal.TotalCalories);
    public double TotalProtein => MealTimes.Sum(meal => meal.TotalProtein);
    public double TotalFat => MealTimes.Sum(meal => meal.TotalFat);
    public double TotalCarbohydrates => MealTimes.Sum(meal => meal.TotalCarbohydrates);
    public double TotalSodium => MealTimes.Sum(meal => meal.TotalSodium);
    public double TotalFiber => MealTimes.Sum(meal => meal.TotalFiber);
    public double TotalSugar => MealTimes.Sum(meal => meal.TotalSugar);

    public DailyMealPlan()
    {
        MealTimes = new ObservableCollection<MealTime>();
        MealTimes.CollectionChanged += MealTimes_CollectionChanged;
    }

    private void MealTimes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Subscribe to new meal times
        if (e.NewItems != null)
        {
            foreach (MealTime mealTime in e.NewItems)
            {
                mealTime.PropertyChanged += MealTime_PropertyChanged;
            }
        }

        // Unsubscribe from removed meal times
        if (e.OldItems != null)
        {
            foreach (MealTime mealTime in e.OldItems)
            {
                mealTime.PropertyChanged -= MealTime_PropertyChanged;
            }
        }

        NotifyTotalsChanged();
    }

    internal void MealTime_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When any meal time's totals change, update daily totals
        if (e.PropertyName?.StartsWith("Total") == true)
        {
            NotifyTotalsChanged();
        }
    }

    private void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalCalories));
        OnPropertyChanged(nameof(TotalProtein));
        OnPropertyChanged(nameof(TotalFat));
        OnPropertyChanged(nameof(TotalCarbohydrates));
        OnPropertyChanged(nameof(TotalSodium));
        OnPropertyChanged(nameof(TotalFiber));
        OnPropertyChanged(nameof(TotalSugar));
    }

    /// <summary>
    /// Creates a deep copy of this meal plan with all meal times and items.
    /// Useful for copying meal plans to different dates or creating favorites.
    /// </summary>
    /// <param name="newDate">The date for the cloned meal plan. If null, uses the current date.</param>
    /// <returns>A complete deep copy of this meal plan</returns>
    public DailyMealPlan DeepClone(DateTime? newDate = null)
    {
        var clone = new DailyMealPlan
        {
            Date = newDate ?? this.Date,
            Name = this.Name
        };

        // Clear the default collection and clone all meal times
        clone.MealTimes.Clear();
        foreach (var mealTime in this.MealTimes)
        {
            var clonedMealTime = new MealTime(mealTime.Type, mealTime.Type == MealTimeType.Custom ? mealTime.Name : null);

            foreach (var item in mealTime.Items)
            {
                var clonedItem = new MealPlanItem(item.Product, item.Weight)
                {
                    Image = item.Image,
                    Note = item.Note
                };
                clonedMealTime.Items.Add(clonedItem);
            }

            clone.MealTimes.Add(clonedMealTime);
        }

        return clone;
    }
}
