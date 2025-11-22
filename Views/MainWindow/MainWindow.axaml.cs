using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DailyMealPlannerExtended.ViewModels;
using ToastNotificationAvalonia.Manager;

namespace DailyMealPlannerExtended.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Initialize ToastManager with the container
        var toastContainer = this.FindControl<Canvas>("ToastContainer");
        if (toastContainer != null)
        {
            ToastManager.Initialize(toastContainer);
        }
    }

    private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabControl) return;
        if (DataContext is not MainWindowViewModel viewModel) return;

        // Check if Favorites tab is selected (index 2)
        if (tabControl.SelectedIndex == 2)
        {
            viewModel.FavoritesViewModel.RefreshFavoritesCommand.Execute(null);
        }
    }
}