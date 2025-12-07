using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using DailyMealPlannerExtended.ViewModels;
using DailyMealPlannerExtended.Views;
using DailyMealPlannerExtended.Services;

namespace DailyMealPlannerExtended;

public partial class App : Application
{
    private static MainWindowViewModel? _mainWindowViewModel;
    private static SupabaseAuthService? _authService;
    private static NetworkConnectivityService? _connectivityService;

    public static MainWindowViewModel GetMainWindowViewModel()
    {
        return _mainWindowViewModel ?? throw new InvalidOperationException("MainWindowViewModel not initialized");
    }

    public override void Initialize()
    {
        // Initialize logger early
        _ = Logger.Instance;

        // Initialize connectivity service
        _connectivityService = new NetworkConnectivityService();

        // Load Supabase configuration
        SupabaseConfig.Load();

        // Load Cloudflare configuration
        CloudflareConfig.Load();

        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Check if Supabase is configured
            if (SupabaseConfig.IsConfigured())
            {
                try
                {
                    // Initialize auth service and check if user is already logged in
                    _authService = new SupabaseAuthService(_connectivityService!);
                    await _authService.InitializeAsync();

                    if (!_authService.IsAuthenticated)
                    {
                        // Show login screen
                        ShowLoginScreen(desktop);
                    }
                    else
                    {
                        // User is already authenticated, show main window
                        ShowMainWindow(desktop);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(ex, "Failed to initialize auth service");
                    // Show login screen on error
                    ShowLoginScreen(desktop);
                }
            }
            else
            {
                // Supabase not configured, continue offline
                Logger.Instance.Warning("Supabase not configured. Running in offline mode.");
                ShowMainWindow(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowLoginScreen(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Initialize new auth service for fresh login
        _authService = new SupabaseAuthService(_connectivityService!);

        var loginViewModel = new LoginViewModel(_authService, _connectivityService!);

        loginViewModel.LoginSuccessful += (s, e) =>
        {
            // Login successful, show main window
            ShowMainWindow(desktop);
        };

        loginViewModel.ContinueOfflineRequested += (s, e) =>
        {
            // User chose to continue offline
            _authService = null;
            ShowMainWindow(desktop);
        };

        var loginWindow = new Window
        {
            Content = new LoginView { DataContext = loginViewModel },
            Width = 900,
            Height = 650,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
            CanResize = false
        };

        // If main window exists, close it
        if (desktop.MainWindow != null && desktop.MainWindow is MainWindow)
        {
            var mainWindow = desktop.MainWindow;
            desktop.MainWindow = loginWindow;
            loginWindow.Show();
            mainWindow.Close();
        }
        else
        {
            desktop.MainWindow = loginWindow;
        }
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _mainWindowViewModel = new MainWindowViewModel(_authService, _connectivityService);

        // Subscribe to logout event
        _mainWindowViewModel.UserLoggedOut += (s, e) =>
        {
            Logger.Instance.Information("User logged out, showing login screen");
            ShowLoginScreen(desktop);
        };

        var mainWindow = new MainWindow
        {
            DataContext = _mainWindowViewModel,
        };

        // Subscribe to window closing event to save favorites before exit
        mainWindow.Closing += (s, e) =>
        {
            try
            {
                // Save current meal plan to favorites if it's favorited (preserves images/notes)
                _mainWindowViewModel?.MealPlanViewModel?.SaveCurrentMealPlanToFavoritesIfNeeded();
                Logger.Instance.Information("Saved current meal plan to favorites before closing");
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(ex, "Failed to save current meal plan on window close");
            }
        };

        // If login window exists, close it
        if (desktop.MainWindow != null && desktop.MainWindow is not MainWindow)
        {
            var loginWindow = desktop.MainWindow;
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            loginWindow.Close();
        }
        else
        {
            desktop.MainWindow = mainWindow;
            mainWindow.Show(); // Must explicitly show the window
        }

        Logger.Instance.Information("Main window displayed");
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}