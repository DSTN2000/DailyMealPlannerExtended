using Avalonia.Controls;
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
}