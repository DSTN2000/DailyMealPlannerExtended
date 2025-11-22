using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DailyMealPlannerExtended.ViewModels;
using DailyMealPlannerExtended.Views.Toasts;
using Lab4.Models;
using ToastNotificationAvalonia.Manager;

namespace DailyMealPlannerExtended.Views.MealPlan;

public partial class MealPlanView : UserControl
{
    public MealPlanView()
    {
        InitializeComponent();
        // DataContext will be set by MainWindow
    }

    private void MealPlanView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Clear focus from any TextBox when clicking on the background
        this.Focus();
    }

    private void FoodItemWeight_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            // Move focus away from the TextBox to commit the value
            this.Focus();
            e.Handled = true;
        }
    }

    private void FoodItemWeight_LostFocus(object? sender, RoutedEventArgs e)
    {
        // Binding should update automatically when focus is lost
    }

    private async void MealTimeName_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TextBlock textBlock) return;
        if (textBlock.DataContext is not MealTime mealTime) return;

        // Only allow renaming custom meal times
        if (mealTime.Type != MealTimeType.Custom) return;

        // Find the parent Grid
        var grid = textBlock.Parent as Grid;
        if (grid == null) return;

        // Store the original name
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

        var editCompleted = false;

        // Handle completion of editing
        async Task CompleteEdit()
        {
            if (editCompleted) return;
            editCompleted = true;

            var newName = textBox.Text?.Trim();
            var showToast = false;
            var duplicateName = "";

            if (!string.IsNullOrWhiteSpace(newName) && newName != originalName)
            {
                // Get the ViewModel to check for duplicate names
                if (DataContext is MealPlanViewModel viewModel)
                {
                    // Check if the new name already exists (excluding the current meal time)
                    var existingNames = viewModel.CurrentMealPlan.MealTimes
                        .Where(mt => mt != mealTime)
                        .Select(mt => mt.Name.ToLowerInvariant());

                    if (existingNames.Contains(newName.ToLowerInvariant()))
                    {
                        // Mark to show error toast after restoring UI
                        showToast = true;
                        duplicateName = newName;
                    }
                    else
                    {
                        mealTime.Name = newName;
                    }
                }
                else
                {
                    mealTime.Name = newName;
                }
            }

            // Remove TextBox and restore TextBlock immediately
            grid.Children.Remove(textBox);
            grid.Children.Add(textBlock);

            // Show toast after UI is restored
            if (showToast)
            {
                await ToastManager.ShowToastAsync(new ErrorToast($"A meal time with the name '{duplicateName}' already exists."));
            }
        }

        textBox.LostFocus += async (s, ev) => await CompleteEdit();
        textBox.KeyDown += async (s, ev) =>
        {
            if (ev.Key == Key.Enter)
            {
                await CompleteEdit();
                ev.Handled = true;
            }
            else if (ev.Key == Key.Escape)
            {
                if (!editCompleted)
                {
                    editCompleted = true;
                    // Cancel - restore TextBlock without saving
                    grid.Children.Remove(textBox);
                    grid.Children.Add(textBlock);
                }
                ev.Handled = true;
            }
        };
    }
}
