namespace DailyMealPlannerExtended.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    public MealPlanViewModel MealPlanViewModel { get; }
    public FavoritesViewModel FavoritesViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public ProductDetailViewModel ProductDetailViewModel { get; } = new();
    public AddToMealPlanViewModel AddToMealPlanViewModel { get; }
    public UserPreferencesViewModel UserPreferencesViewModel { get; }

    public MainWindowViewModel()
    {
        // Create shared instances that need to communicate
        // First create MealPlanViewModel with default user
        var defaultUser = new Models.User();
        MealPlanViewModel = new MealPlanViewModel(defaultUser);

        // Create UserPreferencesViewModel with reference to MealPlanViewModel for read-only state
        UserPreferencesViewModel = new UserPreferencesViewModel(MealPlanViewModel);

        // Load user preferences and update MealPlanViewModel
        MealPlanViewModel.User = UserPreferencesViewModel.User;

        FavoritesViewModel = new FavoritesViewModel(MealPlanViewModel);
        HistoryViewModel = new HistoryViewModel(MealPlanViewModel);
        AddToMealPlanViewModel = new AddToMealPlanViewModel(MealPlanViewModel);
    }
}
