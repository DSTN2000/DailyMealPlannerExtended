using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class AddToMealPlanViewModel : ViewModelBase
{
    private readonly MealPlanViewModel _mealPlanViewModel;

    [ObservableProperty]
    private Product? _product;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private MealTime? _selectedMealTime;

    [ObservableProperty]
    private double _weight = 100.0;

    public ObservableCollection<MealTime> AvailableMealTimes => _mealPlanViewModel.CurrentMealPlan.MealTimes;

    public string UnitLabel => Product?.Unit switch
    {
        ServingUnit.g => "g",
        ServingUnit.ml => "ml",
        _ => "g"
    };

    public double CurrentServings => Product?.Serving > 0 ? Weight / Product.Serving : 0;

    public AddToMealPlanViewModel(MealPlanViewModel mealPlanViewModel)
    {
        _mealPlanViewModel = mealPlanViewModel;
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        Product = null;
        SelectedMealTime = null;
        Weight = 100.0;
    }

    [RelayCommand]
    private void Add()
    {
        if (Product == null || SelectedMealTime == null) return;

        var mealPlanItem = new MealPlanItem(Product, Weight);
        SelectedMealTime.Items.Add(mealPlanItem);

        Logger.Instance.Information("Added {Product} ({Weight}g) to {MealTime}",
            Product.Name, Weight, SelectedMealTime.Name);

        Close();
    }

    [RelayCommand]
    private void IncrementServing()
    {
        if (Product?.Serving > 0)
        {
            var currentServings = Math.Floor(Weight / Product.Serving);
            Weight = (currentServings + 1) * Product.Serving;
        }
    }

    [RelayCommand]
    private void DecrementServing()
    {
        if (Product?.Serving > 0)
        {
            var currentServings = Math.Ceiling(Weight / Product.Serving);
            var newServings = Math.Max(0, currentServings - 1);
            Weight = newServings * Product.Serving;
        }
    }

    [RelayCommand]
    private void ShowForProduct(Product product)
    {
        Product = product;
        Weight = product.Serving > 0 ? product.Serving : 100.0;
        SelectedMealTime = AvailableMealTimes.FirstOrDefault();
        IsVisible = true;
    }

    partial void OnWeightChanged(double value)
    {
        OnPropertyChanged(nameof(CurrentServings));
    }

    partial void OnProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(UnitLabel));
        OnPropertyChanged(nameof(CurrentServings));
    }
}
