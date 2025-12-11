using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using DailyMealPlannerExtended.Models;
using Avalonia.Controls;

namespace DailyMealPlannerExtended.ViewModels;

public partial class UserPreferencesViewModel : ViewModelBase
{
    private readonly UserPreferencesService _preferencesService;
    private readonly MealPlanViewModel? _mealPlanViewModel;
    private readonly SupabaseAuthService? _authService;
    private readonly AutoSyncService? _autoSyncService;
    private readonly NetworkConnectivityService? _connectivityService;

    public event EventHandler? LoggedOut;

    public User User => _mealPlanViewModel?.User ?? new User();

    public bool IsReadOnly => _mealPlanViewModel?.IsReadOnly ?? false;

    // Auth properties
    public bool IsLoggedIn => _authService?.IsAuthenticated ?? false;
    public string? UserEmail => _authService?.CurrentUser?.Email;
    public string LoginStatus => IsLoggedIn ? $"Signed in as {UserEmail}" : "Not signed in";

    // Connection status properties
    [ObservableProperty]
    private string _connectionStatusText = "Offline";

    [ObservableProperty]
    private string _connectionStatusIcon = "WifiOff";

    [ObservableProperty]
    private string _connectionStatusColor = "#FF9800"; // Orange for offline

    public ObservableCollection<ActivityLevel> ActivityLevels { get; } = new()
    {
        ActivityLevel.Sedentary,
        ActivityLevel.Moderate,
        ActivityLevel.Medium,
        ActivityLevel.High
    };

    // Properties that bind directly to User.NutrientsSplit
    public double ProteinPercentage
    {
        get => User.NutrientsSplit.p;
        set
        {
            if (Math.Abs(User.NutrientsSplit.p - value) > 0.01)
            {
                User.NutrientsSplit = (value, User.NutrientsSplit.f, User.NutrientsSplit.c);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPercentage));
                OnPropertyChanged(nameof(IsValidSplit));
            }
        }
    }

    public double FatPercentage
    {
        get => User.NutrientsSplit.f;
        set
        {
            if (Math.Abs(User.NutrientsSplit.f - value) > 0.01)
            {
                User.NutrientsSplit = (User.NutrientsSplit.p, value, User.NutrientsSplit.c);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPercentage));
                OnPropertyChanged(nameof(IsValidSplit));
            }
        }
    }

    public double CarbsPercentage
    {
        get => User.NutrientsSplit.c;
        set
        {
            if (Math.Abs(User.NutrientsSplit.c - value) > 0.01)
            {
                User.NutrientsSplit = (User.NutrientsSplit.p, User.NutrientsSplit.f, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPercentage));
                OnPropertyChanged(nameof(IsValidSplit));
            }
        }
    }

    public double TotalPercentage => User.NutrientsSplit.p + User.NutrientsSplit.f + User.NutrientsSplit.c;
    public bool IsValidSplit => Math.Abs(TotalPercentage - 100) < 0.01;

    public UserPreferencesViewModel(
        MealPlanViewModel? mealPlanViewModel = null,
        SupabaseAuthService? authService = null,
        AutoSyncService? autoSyncService = null,
        NetworkConnectivityService? connectivityService = null)
    {
        _mealPlanViewModel = mealPlanViewModel;
        _authService = authService;
        _autoSyncService = autoSyncService;
        _connectivityService = connectivityService;
        _preferencesService = new UserPreferencesService();

        // Subscribe to MealPlanViewModel's property changes
        if (_mealPlanViewModel != null)
        {
            _mealPlanViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MealPlanViewModel.IsReadOnly))
                {
                    OnPropertyChanged(nameof(IsReadOnly));
                }
                else if (e.PropertyName == nameof(MealPlanViewModel.User))
                {
                    // When User changes in MealPlanViewModel, notify all our properties
                    OnPropertyChanged(nameof(User));
                    OnPropertyChanged(nameof(ProteinPercentage));
                    OnPropertyChanged(nameof(FatPercentage));
                    OnPropertyChanged(nameof(CarbsPercentage));
                    OnPropertyChanged(nameof(TotalPercentage));
                    OnPropertyChanged(nameof(IsValidSplit));
                }
            };
        }

        // Subscribe to auth state changes
        if (_authService != null)
        {
            _authService.AuthStateChanged += (s, isAuthenticated) =>
            {
                OnPropertyChanged(nameof(IsLoggedIn));
                OnPropertyChanged(nameof(UserEmail));
                OnPropertyChanged(nameof(LoginStatus));
                UpdateConnectionStatus();
            };

            _authService.ConnectionStatusChanged += (s, status) =>
            {
                UpdateConnectionStatus();
            };
        }

        // Subscribe to connectivity changes
        if (_connectivityService != null)
        {
            _connectivityService.ConnectionStatusChanged += (s, status) =>
            {
                UpdateConnectionStatus();
            };
        }

        // Initial status update
        UpdateConnectionStatus();
    }

    private void UpdateConnectionStatus()
    {
        if (_authService == null || _connectivityService == null)
        {
            ConnectionStatusText = "Offline mode";
            ConnectionStatusIcon = "WifiOff";
            ConnectionStatusColor = "#9E9E9E"; // Gray
            return;
        }

        var status = _authService.ConnectionStatus;

        switch (status)
        {
            case ConnectionStatus.Online:
                if (IsLoggedIn)
                {
                    ConnectionStatusText = "Online - synced";
                    ConnectionStatusIcon = "CloudCheck";
                    ConnectionStatusColor = "#4CAF50"; // Green
                }
                else
                {
                    ConnectionStatusText = "Online";
                    ConnectionStatusIcon = "Wifi";
                    ConnectionStatusColor = "#4CAF50"; // Green
                }
                break;

            case ConnectionStatus.Offline:
                ConnectionStatusText = "Offline";
                ConnectionStatusIcon = "WifiOff";
                ConnectionStatusColor = "#9E9E9E"; // Gray
                break;

            case ConnectionStatus.AuthenticatedOffline:
                var pendingCount = _autoSyncService?.PendingOperationsCount ?? 0;
                if (pendingCount > 0)
                {
                    ConnectionStatusText = $"Offline ({pendingCount} pending changes)";
                }
                else
                {
                    ConnectionStatusText = "Offline (no pending changes)";
                }
                ConnectionStatusIcon = "CloudOff";
                ConnectionStatusColor = "#FF9800"; // Orange
                break;
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        User.Weight = 75;
        User.Height = 170;
        User.Age = 30;
        User.ActivityLevel = ActivityLevel.Moderate;
        User.NutrientsSplit = (30, 30, 40);

        // Notify UI that percentages changed
        OnPropertyChanged(nameof(ProteinPercentage));
        OnPropertyChanged(nameof(FatPercentage));
        OnPropertyChanged(nameof(CarbsPercentage));
        OnPropertyChanged(nameof(TotalPercentage));
        OnPropertyChanged(nameof(IsValidSplit));
    }

    [RelayCommand]
    private void SavePreferences()
    {
        try
        {
            _preferencesService.SavePreferences(User);
            Logger.Instance.Information("User preferences saved: Weight={Weight}kg, Height={Height}cm, Age={Age}, BMI={BMI:F1}, Daily Calories={Calories:F0}",
                User.Weight, User.Height, User.Age, User.BMI, User.DailyCalories);

            // Queue automatic sync if authenticated
            if (IsLoggedIn && _autoSyncService != null)
            {
                _autoSyncService.QueueSync(SyncOperation.SyncPreferences);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to save preferences");
        }
    }

    [RelayCommand]
    private async Task LogOutAsync()
    {
        try
        {
            if (_authService == null)
                return;

            // Show confirmation dialog
            var confirmed = await ShowLogoutConfirmationAsync();
            if (!confirmed)
                return;

            // Sign out (this will sync pending changes if online and clear local data)
            await _authService.SignOutAsync(clearLocalData: true);

            Logger.Instance.Information("User logged out successfully");
            LoggedOut?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to log out");
        }
    }

    private async Task<bool> ShowLogoutConfirmationAsync()
    {
        return await Task.FromResult(true);
    }

    private void LoadPreferences()
    {
        try
        {
            var user = _preferencesService.LoadPreferences();
            if (user != null && _mealPlanViewModel != null)
            {
                _mealPlanViewModel.User = user;

                // Notify UI that all percentage properties may have changed
                OnPropertyChanged(nameof(User));
                OnPropertyChanged(nameof(ProteinPercentage));
                OnPropertyChanged(nameof(FatPercentage));
                OnPropertyChanged(nameof(CarbsPercentage));
                OnPropertyChanged(nameof(TotalPercentage));
                OnPropertyChanged(nameof(IsValidSplit));
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load preferences");
        }
    }
}
