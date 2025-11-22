using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DailyMealPlannerExtended.ViewModels;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.Views;

public partial class Catalog : UserControl
{
    public Catalog()
    {
        InitializeComponent();
        DataContext = new CatalogViewModel();
    }

    private void OnLabelSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is string label)
        {
            if (DataContext is CatalogViewModel viewModel)
            {
                viewModel.AddLabelCommand.Execute(label);
            }
            // Clear selection so the same label can be selected again if removed
            comboBox.SelectedItem = null;
        }
    }

    private void ProductCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is Product product)
        {
            if (DataContext is CatalogViewModel viewModel)
            {
                viewModel.ShowProductDetailCommand.Execute(product);
            }
        }
    }
}
