using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class AddToMealPlanViewModel : ViewModelBase
{
    private readonly MealPlanViewModel _mealPlanViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentServings))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private Product? _product;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private MealTime? _selectedMealTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentServings))]
    [NotifyPropertyChangedFor(nameof(IsWeightValid))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private double _weight = 100.0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private bool _isWeightTextValid = true;

    public ObservableCollection<MealTime> AvailableMealTimes => _mealPlanViewModel.CurrentMealPlan.MealTimes;

    public double CurrentServings => Product?.Serving > 0 ? Weight / Product.Serving : 0;

    public bool IsWeightValid => Weight >= MealPlanItem.MinWeight && Weight <= MealPlanItem.MaxWeight;

    public AddToMealPlanViewModel(MealPlanViewModel mealPlanViewModel)
    {
        _mealPlanViewModel = mealPlanViewModel;

        // Subscribe to CurrentMealPlan changes to update AvailableMealTimes
        _mealPlanViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MealPlanViewModel.CurrentMealPlan))
            {
                OnPropertyChanged(nameof(AvailableMealTimes));
                // Reset selected meal time when meal plan changes
                SelectedMealTime = AvailableMealTimes.FirstOrDefault();
            }
        };
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        Product = null;
        SelectedMealTime = null;
        Weight = 100.0;
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (Product == null || SelectedMealTime == null) return;

        var mealPlanItem = new MealPlanItem(Product, Weight);
        SelectedMealTime.Items.Add(mealPlanItem);

        Logger.Instance.Information("Added {Product} ({Weight}g) to {MealTime}",
            Product.Name, Weight, SelectedMealTime.Name);

        Close();
    }

    private bool CanAdd() => Product != null && SelectedMealTime != null && IsWeightValid && IsWeightTextValid;

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

}
