using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class UserPreferencesViewModel : ViewModelBase
{
    private readonly UserPreferencesService _preferencesService;
    private readonly MealPlanViewModel? _mealPlanViewModel;
    private readonly SupabaseAuthService? _authService;
    private readonly SupabaseSyncService? _syncService;

    public event EventHandler? LoggedOut;

    public User User => _mealPlanViewModel?.User ?? new User();

    public bool IsReadOnly => _mealPlanViewModel?.IsReadOnly ?? false;

    // Auth properties
    public bool IsLoggedIn => _authService?.IsAuthenticated ?? false;
    public string? UserEmail => _authService?.CurrentUser?.Email;
    public string LoginStatus => IsLoggedIn ? $"Signed in as {UserEmail}" : "Not signed in";

    // Sync properties
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncNowCommand))]
    private bool _isSyncing;

    [ObservableProperty]
    private string _syncStatus = "Not synced yet";

    [ObservableProperty]
    private DateTime? _lastSyncTime;

    public string LastSyncDisplay => LastSyncTime.HasValue
        ? $"Last synced: {LastSyncTime.Value:MMM d, yyyy h:mm tt}"
        : "Never synced";

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

    public UserPreferencesViewModel(MealPlanViewModel? mealPlanViewModel = null, SupabaseAuthService? authService = null, SupabaseSyncService? syncService = null)
    {
        _mealPlanViewModel = mealPlanViewModel;
        _authService = authService;
        _syncService = syncService;
        _preferencesService = new UserPreferencesService();

        // Subscribe to sync service events
        if (_syncService != null)
        {
            _syncService.SyncStatusChanged += (s, e) =>
            {
                SyncStatus = e.Status;
            };

            _syncService.SyncCompleted += (s, e) =>
            {
                LastSyncTime = _syncService.LastSyncTime;
                OnPropertyChanged(nameof(LastSyncDisplay));
            };
        }

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
            };
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
            if (_authService != null)
            {
                await _authService.SignOutAsync();
                Logger.Instance.Information("User logged out successfully");
                LoggedOut?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to log out");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncNowAsync()
    {
        if (_syncService == null) return;

        try
        {
            IsSyncing = true;
            SyncStatus = "Syncing...";

            var success = await _syncService.SyncAllAsync();

            if (success)
            {
                SyncStatus = "Sync completed successfully";
            }
            else
            {
                SyncStatus = "Sync failed";
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Sync failed");
            SyncStatus = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private bool CanSync() => IsLoggedIn && !IsSyncing;

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
