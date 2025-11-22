using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lab4.Models;

public partial class MealTime : ObservableObject
{
    public MealTimeType Type { get; set; }

    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<MealPlanItem> Items { get; set; }

    // Calculated totals for this mealtime
    public double TotalCalories => Items.Sum(item => item.Calories);
    public double TotalProtein => Items.Sum(item => item.Protein);
    public double TotalFat => Items.Sum(item => item.TotalFat);
    public double TotalCarbohydrates => Items.Sum(item => item.Carbohydrates);
    public double TotalSodium => Items.Sum(item => item.Sodium);
    public double TotalFiber => Items.Sum(item => item.Fiber);
    public double TotalSugar => Items.Sum(item => item.Sugar);

    public MealTime()
    {
        Items = new ObservableCollection<MealPlanItem>();
        Items.CollectionChanged += Items_CollectionChanged;
    }

    public MealTime(MealTimeType type, string? customName = null)
    {
        Items = new ObservableCollection<MealPlanItem>();
        Items.CollectionChanged += Items_CollectionChanged;

        Type = type;
        _name = type switch
        {
            MealTimeType.Breakfast => "Breakfast",
            MealTimeType.Lunch => "Lunch",
            MealTimeType.Dinner => "Dinner",
            MealTimeType.Custom => customName ?? "Custom Meal",
            _ => "Meal"
        };
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Subscribe to new items
        if (e.NewItems != null)
        {
            foreach (MealPlanItem item in e.NewItems)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }
        }

        // Unsubscribe from removed items
        if (e.OldItems != null)
        {
            foreach (MealPlanItem item in e.OldItems)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
        }

        NotifyTotalsChanged();
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When any item's nutritional values change, update totals
        NotifyTotalsChanged();
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

public enum MealTimeType
{
    Breakfast,
    Lunch,
    Dinner,
    Custom
}
