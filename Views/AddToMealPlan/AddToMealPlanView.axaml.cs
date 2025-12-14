using Avalonia;
using Avalonia.Controls;
using DailyMealPlannerExtended.ViewModels;

namespace DailyMealPlannerExtended.Views.AddToMealPlan;

public partial class AddToMealPlanView : UserControl
{
    public AddToMealPlanView()
    {
        InitializeComponent();
    }

    private void WeightTextBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(TextBox.Text) && DataContext is AddToMealPlanViewModel viewModel)
        {
            // Check if the text is a valid number
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                var text = textBox.Text ?? string.Empty;
                var isNumeric = double.TryParse(text, out _);

                // Update a property that the button can check
                viewModel.IsWeightTextValid = isNumeric;
            }
        }
    }
}
