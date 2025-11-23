namespace DailyMealPlannerExtended.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    public MealPlanViewModel MealPlanViewModel { get; }
    public FavoritesViewModel FavoritesViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public ProductDetailViewModel ProductDetailViewModel { get; }
    public AddToMealPlanViewModel AddToMealPlanViewModel { get; }
    public UserPreferencesViewModel UserPreferencesViewModel { get; }

    public MainWindowViewModel()
    {
        // Create shared instances that need to communicate
        MealPlanViewModel = new MealPlanViewModel();

        // Create UserPreferencesViewModel with reference to MealPlanViewModel
        UserPreferencesViewModel = new UserPreferencesViewModel(MealPlanViewModel);

        // Create ProductDetailViewModel with reference to MealPlanViewModel for read-only state
        ProductDetailViewModel = new ProductDetailViewModel(MealPlanViewModel);

        FavoritesViewModel = new FavoritesViewModel(MealPlanViewModel);
        HistoryViewModel = new HistoryViewModel(MealPlanViewModel);
        AddToMealPlanViewModel = new AddToMealPlanViewModel(MealPlanViewModel);
    }
}
