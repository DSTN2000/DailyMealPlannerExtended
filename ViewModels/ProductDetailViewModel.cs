using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lab4.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class ProductDetailViewModel : ViewModelBase
{
    [ObservableProperty]
    private Product? _product;

    [ObservableProperty]
    private bool _isVisible;

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        Product = null;
    }

    public void ShowProduct(Product product)
    {
        Product = product;
        IsVisible = true;
    }
}
