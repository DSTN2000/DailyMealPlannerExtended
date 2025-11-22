using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.ViewModels;

public enum ProductDetailMode
{
    Catalog,        // Viewing from catalog - show "Add to Meal Plan" button
    EditMealItem,   // Editing current day meal item - show editable weight controls
    ViewMealItem    // Viewing past day meal item - show read-only weight data
}

public partial class ProductDetailViewModel : ViewModelBase
{
    [ObservableProperty]
    private Product? _product;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private MealPlanItem? _mealPlanItem;

    [ObservableProperty]
    private ProductDetailMode _mode = ProductDetailMode.Catalog;

    // Computed properties for view visibility
    public bool ShowAddToMealPlanButton => Mode == ProductDetailMode.Catalog;
    public bool ShowWeightControls => Mode == ProductDetailMode.EditMealItem || Mode == ProductDetailMode.ViewMealItem;
    public bool IsWeightEditable => Mode == ProductDetailMode.EditMealItem;

    public string UnitLabel => Product?.Unit switch
    {
        ServingUnit.g => "g",
        ServingUnit.ml => "ml",
        _ => "g"
    };

    public double CurrentWeight => MealPlanItem?.Weight ?? 0;

    public double CurrentServings => Product?.Serving > 0 && MealPlanItem != null
        ? MealPlanItem.Weight / Product.Serving
        : 0;

    [RelayCommand]
    private void IncrementServing()
    {
        if (Product?.Serving > 0 && MealPlanItem != null && IsWeightEditable)
        {
            var currentServings = Math.Floor(MealPlanItem.Weight / Product.Serving);
            MealPlanItem.Weight = (currentServings + 1) * Product.Serving;
        }
    }

    [RelayCommand]
    private void DecrementServing()
    {
        if (Product?.Serving > 0 && MealPlanItem != null && IsWeightEditable)
        {
            var currentServings = Math.Ceiling(MealPlanItem.Weight / Product.Serving);
            var newServings = Math.Max(0, currentServings - 1);
            MealPlanItem.Weight = newServings * Product.Serving;
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        Product = null;
        MealPlanItem = null;
        Mode = ProductDetailMode.Catalog;
    }

    public void ShowProduct(Product product)
    {
        Product = product;
        MealPlanItem = null;
        Mode = ProductDetailMode.Catalog;
        IsVisible = true;
        NotifyModeChanged();
    }

    public void ShowMealPlanItem(MealPlanItem item, bool isEditable)
    {
        MealPlanItem = item;
        Product = item.Product;
        Mode = isEditable ? ProductDetailMode.EditMealItem : ProductDetailMode.ViewMealItem;
        IsVisible = true;
        NotifyModeChanged();
    }

    partial void OnMealPlanItemChanged(MealPlanItem? value)
    {
        if (value != null)
        {
            value.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MealPlanItem.Weight))
                {
                    OnPropertyChanged(nameof(CurrentWeight));
                    OnPropertyChanged(nameof(CurrentServings));
                }
            };
        }
    }

    partial void OnModeChanged(ProductDetailMode value)
    {
        NotifyModeChanged();
    }

    private void NotifyModeChanged()
    {
        OnPropertyChanged(nameof(ShowAddToMealPlanButton));
        OnPropertyChanged(nameof(ShowWeightControls));
        OnPropertyChanged(nameof(IsWeightEditable));
        OnPropertyChanged(nameof(CurrentWeight));
        OnPropertyChanged(nameof(CurrentServings));
    }
}
