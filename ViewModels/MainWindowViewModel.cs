namespace DailyMealPlannerExtended.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    public MealPlanViewModel MealPlanViewModel { get; }
    public FavoritesViewModel FavoritesViewModel { get; }
    public ProductDetailViewModel ProductDetailViewModel { get; } = new();
    public AddToMealPlanViewModel AddToMealPlanViewModel { get; }
    public UserPreferencesViewModel UserPreferencesViewModel { get; }

    public MainWindowViewModel()
    {
        // Create shared instances that need to communicate
        UserPreferencesViewModel = new UserPreferencesViewModel();
        MealPlanViewModel = new MealPlanViewModel(UserPreferencesViewModel.User);
        FavoritesViewModel = new FavoritesViewModel();
        AddToMealPlanViewModel = new AddToMealPlanViewModel(MealPlanViewModel);
    }
}
