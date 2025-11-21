using Avalonia.Controls;
using DailyMealPlannerExtended.ViewModels;

namespace DailyMealPlannerExtended.Views.MealPlan;

public partial class MealPlanView : UserControl
{
    public MealPlanView()
    {
        InitializeComponent();
        DataContext = new MealPlanViewModel();
    }
}
