using DailyMealPlannerExtended.Services;

namespace DailyMealPlannerExtended.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    public MealPlanViewModel MealPlanViewModel { get; }
    public FavoritesViewModel FavoritesViewModel { get; }
    public DiscoverViewModel DiscoverViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public ProductDetailViewModel ProductDetailViewModel { get; }
    public AddToMealPlanViewModel AddToMealPlanViewModel { get; }
    public UserPreferencesViewModel UserPreferencesViewModel { get; }

    public SupabaseAuthService? AuthService { get; }
    public AutoSyncService? AutoSyncService { get; }
    public NetworkConnectivityService? ConnectivityService { get; }
    public bool IsAuthenticated => AuthService?.IsAuthenticated ?? false;
    public string? UserEmail => AuthService?.CurrentUser?.Email;

    public event EventHandler? UserLoggedOut;

    public MainWindowViewModel(
        SupabaseAuthService? authService = null,
        NetworkConnectivityService? connectivityService = null)
    {
        AuthService = authService;
        ConnectivityService = connectivityService;

        // Create auto-sync service if authenticated
        if (authService != null && connectivityService != null)
        {
            AutoSyncService = new AutoSyncService(
                authService,
                connectivityService,
                new UserPreferencesService(),
                new DaySnapshotService(),
                new FavoriteMealPlansService()
            );
        }

        // Create shared instances that need to communicate
        MealPlanViewModel = new MealPlanViewModel(authService, AutoSyncService);

        // Create UserPreferencesViewModel with reference to services
        UserPreferencesViewModel = new UserPreferencesViewModel(
            MealPlanViewModel,
            AuthService,
            AutoSyncService,
            ConnectivityService);

        // Create ProductDetailViewModel with reference to MealPlanViewModel for read-only state
        ProductDetailViewModel = new ProductDetailViewModel(MealPlanViewModel);

        FavoritesViewModel = new FavoritesViewModel(MealPlanViewModel, AutoSyncService);
        DiscoverViewModel = new DiscoverViewModel(MealPlanViewModel, AuthService);
        HistoryViewModel = new HistoryViewModel(MealPlanViewModel, AutoSyncService);
        AddToMealPlanViewModel = new AddToMealPlanViewModel(MealPlanViewModel);

        // Subscribe to logout event
        UserPreferencesViewModel.LoggedOut += (s, e) =>
        {
            Logger.Instance.Information("User logged out, triggering app-level logout event");
            UserLoggedOut?.Invoke(this, EventArgs.Empty);
        };

        if (IsAuthenticated)
        {
            Logger.Instance.Information("Main window initialized with authenticated user: {Email}", UserEmail);

            // Trigger initial sync in background
            if (AutoSyncService != null)
            {
                // Subscribe to sync completion to reload preferences
                AutoSyncService.SyncCompleted += (s, e) =>
                {
                    // Reload user preferences after sync completes
                    MealPlanViewModel.ReloadUserPreferences();
                    Logger.Instance.Information("Reloaded user preferences after initial sync");
                };

                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Short delay to let UI load first
                    await AutoSyncService.InitialSyncAsync();
                });
            }
        }
        else
        {
            Logger.Instance.Information("Main window initialized in offline mode");
        }
    }
}
