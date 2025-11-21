using Avalonia.Controls;
using ToastNotificationAvalonia.UserControls;

namespace DailyMealPlannerExtended.Views.Toasts;

public partial class ErrorToast : ToastControl
{
    public ErrorToast() : this("An error occurred")
    {
    }

    public ErrorToast(string message)
    {
        InitializeComponent();

        var messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock");
        if (messageTextBlock != null)
        {
            messageTextBlock.Text = message;
        }
    }
}
