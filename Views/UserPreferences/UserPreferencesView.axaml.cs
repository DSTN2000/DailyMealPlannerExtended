using Avalonia.Controls;
using DailyMealPlannerExtended.ViewModels;

namespace DailyMealPlannerExtended.Views.UserPreferences;

public partial class UserPreferencesView : UserControl
{
    public UserPreferencesView()
    {
        InitializeComponent();
        DataContext = new UserPreferencesViewModel();
    }
}
