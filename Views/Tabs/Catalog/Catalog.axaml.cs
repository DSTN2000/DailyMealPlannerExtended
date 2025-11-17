using Avalonia.Controls;
using Avalonia.Interactivity;
using DailyMealPlannerExtended.ViewModels;

namespace DailyMealPlannerExtended.Views;

public partial class Catalog : UserControl
{
    public Catalog()
    {
        InitializeComponent();
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
}
