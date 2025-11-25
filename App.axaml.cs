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

    public static MainWindowViewModel GetMainWindowViewModel()
    {
        return _mainWindowViewModel ?? throw new InvalidOperationException("MainWindowViewModel not initialized");
    }

    public override void Initialize()
    {
        // Initialize logger early
        _ = Logger.Instance;

        // Load Supabase configuration
        SupabaseConfig.Load();

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
                // Initialize auth service and check if user is already logged in
                _authService = new SupabaseAuthService();
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
        var loginViewModel = new LoginViewModel();

        loginViewModel.LoginSuccessful += (s, e) =>
        {
            // Login successful, show main window
            _authService = loginViewModel.GetAuthService();
            ShowMainWindow(desktop);
        };

        loginViewModel.ContinueOfflineRequested += (s, e) =>
        {
            // User chose to continue offline
            ShowMainWindow(desktop);
        };

        desktop.MainWindow = new Window
        {
            Content = new LoginView { DataContext = loginViewModel },
            Width = 900,
            Height = 650,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
            CanResize = false
        };
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _mainWindowViewModel = new MainWindowViewModel(_authService);

        var mainWindow = new MainWindow
        {
            DataContext = _mainWindowViewModel,
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