using DailyMealPlannerExtended.Services;

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

    public SupabaseAuthService? AuthService { get; }
    public bool IsAuthenticated => AuthService?.IsAuthenticated ?? false;
    public string? UserEmail => AuthService?.CurrentUser?.Email;

    public MainWindowViewModel(SupabaseAuthService? authService = null)
    {
        AuthService = authService;

        // Create shared instances that need to communicate
        MealPlanViewModel = new MealPlanViewModel();

        // Create UserPreferencesViewModel with reference to MealPlanViewModel
        UserPreferencesViewModel = new UserPreferencesViewModel(MealPlanViewModel);

        // Create ProductDetailViewModel with reference to MealPlanViewModel for read-only state
        ProductDetailViewModel = new ProductDetailViewModel(MealPlanViewModel);

        FavoritesViewModel = new FavoritesViewModel(MealPlanViewModel);
        HistoryViewModel = new HistoryViewModel(MealPlanViewModel);
        AddToMealPlanViewModel = new AddToMealPlanViewModel(MealPlanViewModel);

        if (IsAuthenticated)
        {
            Logger.Instance.Information("Main window initialized with authenticated user: {Email}", UserEmail);
        }
        else
        {
            Logger.Instance.Information("Main window initialized in offline mode");
        }
    }
}
