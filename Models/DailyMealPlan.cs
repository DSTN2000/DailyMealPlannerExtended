using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lab4.Models;

public partial class DailyMealPlan : ObservableObject
{
    public DateTime Date { get; set; } = DateTime.Today;
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

        // Initialize with default mealtimes
        MealTimes.Add(new MealTime(MealTimeType.Breakfast));
        MealTimes.Add(new MealTime(MealTimeType.Lunch));
        MealTimes.Add(new MealTime(MealTimeType.Dinner));
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

    private void MealTime_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
}
