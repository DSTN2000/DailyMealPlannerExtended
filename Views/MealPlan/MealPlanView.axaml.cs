using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DailyMealPlannerExtended.ViewModels;
using Lab4.Models;

namespace DailyMealPlannerExtended.Views.MealPlan;

public partial class MealPlanView : UserControl
{
    public MealPlanView()
    {
        InitializeComponent();
        DataContext = new MealPlanViewModel();
    }

    private void MealTimeName_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TextBlock textBlock) return;
        if (textBlock.DataContext is not MealTime mealTime) return;

        // Only allow renaming custom meal times
        if (mealTime.Type != MealTimeType.Custom) return;

        // Find the parent Grid
        var grid = textBlock.Parent as Grid;
        if (grid == null) return;

        // Store the original name for the TextBlock
        var originalName = mealTime.Name;

        // Create TextBox for editing
        var textBox = new TextBox
        {
            Text = mealTime.Name,
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            [Grid.ColumnProperty] = 0
        };

        // Remove TextBlock and add TextBox
        grid.Children.Remove(textBlock);
        grid.Children.Add(textBox);

        // Focus and select all text
        textBox.Focus();
        textBox.SelectAll();

        // Handle completion of editing
        void CompleteEdit()
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                mealTime.Name = textBox.Text.Trim();
            }

            // Remove TextBox and recreate TextBlock
            grid.Children.Remove(textBox);

            var newTextBlock = new TextBlock
            {
                Name = "MealTimeNameText",
                Text = mealTime.Name,
                FontSize = 18,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                DataContext = mealTime,
                [Grid.ColumnProperty] = 0
            };
            newTextBlock.DoubleTapped += MealTimeName_DoubleTapped;

            grid.Children.Insert(0, newTextBlock);
        }

        void CancelEdit()
        {
            // Remove TextBox and recreate TextBlock without saving
            grid.Children.Remove(textBox);

            var newTextBlock = new TextBlock
            {
                Name = "MealTimeNameText",
                Text = originalName,
                FontSize = 18,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                DataContext = mealTime,
                [Grid.ColumnProperty] = 0
            };
            newTextBlock.DoubleTapped += MealTimeName_DoubleTapped;

            grid.Children.Insert(0, newTextBlock);
        }

        textBox.LostFocus += (s, ev) => CompleteEdit();
        textBox.KeyDown += (s, ev) =>
        {
            if (ev.Key == Key.Enter)
            {
                CompleteEdit();
                ev.Handled = true;
            }
            else if (ev.Key == Key.Escape)
            {
                CancelEdit();
                ev.Handled = true;
            }
        };
    }
}
