using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using DailyMealPlannerExtended.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class MealPlanViewModel : ViewModelBase
{
    private readonly DaySnapshotService _snapshotService;
    private readonly MealPlanService _mealPlanService;
    private readonly FavoriteMealPlansService _favoritesService;
    private readonly UserPreferencesService _preferencesService;
    private readonly SupabaseDiscoverService? _discoverService;
    private readonly AutoSyncService? _autoSyncService;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaloriesProgress))]
    [NotifyPropertyChangedFor(nameof(ProteinProgress))]
    [NotifyPropertyChangedFor(nameof(FatProgress))]
    [NotifyPropertyChangedFor(nameof(CarbsProgress))]
    [NotifyPropertyChangedFor(nameof(CaloriesProgressColor))]
    [NotifyPropertyChangedFor(nameof(ProteinProgressColor))]
    [NotifyPropertyChangedFor(nameof(FatProgressColor))]
    [NotifyPropertyChangedFor(nameof(CarbsProgressColor))]
    private DailyMealPlan _currentMealPlan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaloriesProgress))]
    [NotifyPropertyChangedFor(nameof(ProteinProgress))]
    [NotifyPropertyChangedFor(nameof(FatProgress))]
    [NotifyPropertyChangedFor(nameof(CarbsProgress))]
    [NotifyPropertyChangedFor(nameof(CaloriesProgressColor))]
    [NotifyPropertyChangedFor(nameof(ProteinProgressColor))]
    [NotifyPropertyChangedFor(nameof(FatProgressColor))]
    [NotifyPropertyChangedFor(nameof(CarbsProgressColor))]
    private User _user;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _isAuthenticated;

    /// <summary>
    /// Cached snapshot progress data for the currently visible calendar month.
    /// Key: Date, Value: Calories progress percentage
    /// </summary>
    public Dictionary<DateTime, double> MonthSnapshotProgress { get; private set; } = new();

    private int _cachedMonth = -1;
    private int _cachedYear = -1;

    public MealPlanViewModel(SupabaseAuthService? authService = null, AutoSyncService? autoSyncService = null)
    {
        _snapshotService = new();
        _mealPlanService = new();
        _favoritesService = new();
        _preferencesService = new();
        _autoSyncService = autoSyncService;
        _user = _preferencesService.LoadPreferences() ?? new();
        _currentMealPlan = GetOrCreateMealPlan(SelectedDate);

        // Set up discover service if authenticated
        IsAuthenticated = authService?.IsAuthenticated ?? false;
        if (authService != null && IsAuthenticated)
        {
            _discoverService = new SupabaseDiscoverService(authService);
        }

        // Subscribe to property changes for progress updates
        SubscribeToPropertyChanges();

        // Update favorite status
        UpdateFavoriteStatus();

        // Load initial month snapshot data for calendar coloring
        LoadMonthSnapshotData(SelectedDate.Year, SelectedDate.Month);
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        // Save current meal plan to favorites if it's favorited (preserves images/notes)
        SaveCurrentMealPlanToFavoritesIfNeeded();

        Logger.Instance.Information("Getting the meal plan for {Date}", value.ToShortDateString());
        CurrentMealPlan = GetOrCreateMealPlan(value);

        // Refresh month snapshot data if we've moved to a different month
        RefreshMonthSnapshotDataIfNeeded(value);
    }

    /// <summary>
    /// Refreshes the month snapshot data if the selected date is in a different month
    /// than the currently cached month. This is used for calendar day coloring.
    /// </summary>
    private void RefreshMonthSnapshotDataIfNeeded(DateTime date)
    {
        if (date.Month != _cachedMonth || date.Year != _cachedYear)
        {
            LoadMonthSnapshotData(date.Year, date.Month);
        }
    }

    /// <summary>
    /// Loads all snapshot data for the given month and calculates calories progress
    /// for each day. Used to color the calendar cells.
    /// </summary>
    private void LoadMonthSnapshotData(int year, int month)
    {
        try
        {
            MonthSnapshotProgress.Clear();

            // Get all snapshot dates
            var snapshotDates = _snapshotService.GetAllSnapshotDates();

            // Filter to only dates in the specified month
            foreach (var date in snapshotDates)
            {
                if (date.Year == year && date.Month == month)
                {
                    var snapshot = _snapshotService.LoadSnapshot(date);
                    if (snapshot != null && snapshot.UserPreferences != null)
                    {
                        // Calculate calories progress for this day
                        var dailyCalories = snapshot.UserPreferences.DailyCalories;
                        var actualCalories = snapshot.MealPlan.TotalCalories;

                        var progress = dailyCalories > 0
                            ? (actualCalories / dailyCalories) * 100
                            : 0;

                        MonthSnapshotProgress[date.Date] = progress;
                    }
                }
            }

            _cachedMonth = month;
            _cachedYear = year;

            Logger.Instance.Debug("Loaded {Count} snapshots for {Year}-{Month:D2}",
                MonthSnapshotProgress.Count, year, month);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load month snapshot data for {Year}-{Month}", year, month);
        }
    }

    /// <summary>
    /// Saves the current meal plan to favorites if it's already favorited.
    /// This preserves images and notes without requiring manual re-favoriting.
    /// Called automatically on date changes and app close.
    /// </summary>
    public void SaveCurrentMealPlanToFavoritesIfNeeded()
    {
        if (IsFavorite && CurrentMealPlan != null)
        {
            try
            {
                _favoritesService.UpdateFavorite(CurrentMealPlan);
                Logger.Instance.Debug("Auto-saved favorite meal plan: {Name}", CurrentMealPlan.Name);

                // Queue favorite for syncing to cloud if authenticated
                if (_autoSyncService != null)
                {
                    _autoSyncService.QueueSync(SyncOperation.SyncFavorite, CurrentMealPlan);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning(ex, "Failed to auto-save favorite meal plan");
            }
        }
    }

    partial void OnCurrentMealPlanChanged(DailyMealPlan value)
    {
        // Subscribe to the new meal plan's property changes
        if (value != null)
        {
            value.PropertyChanged += CurrentMealPlan_PropertyChanged;
        }

        // Update favorite status
        UpdateFavoriteStatus();
    }

    partial void OnUserChanged(User value)
    {
        // Subscribe to the new user's property changes
        if (value != null)
        {
            value.PropertyChanged += User_PropertyChanged;
        }
    }

    private void SubscribeToPropertyChanges()
    {
        // Subscribe to CurrentMealPlan totals changes
        CurrentMealPlan.PropertyChanged += CurrentMealPlan_PropertyChanged;

        // Subscribe to User daily goals changes
        User.PropertyChanged += User_PropertyChanged;
    }

    private void CurrentMealPlan_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName?.StartsWith("Total") == true)
        {
            NotifyProgressChanged();
        }

        // Reset favorite status when meal plan changes
        if (e.PropertyName != nameof(DailyMealPlan.Date))
        {
            UpdateFavoriteStatus();
        }
    }

    private void User_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName?.StartsWith("Daily") == true)
        {
            NotifyProgressChanged();
        }
    }

    private void NotifyProgressChanged()
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(ProteinProgress));
        OnPropertyChanged(nameof(FatProgress));
        OnPropertyChanged(nameof(CarbsProgress));
        OnPropertyChanged(nameof(CaloriesProgressColor));
        OnPropertyChanged(nameof(ProteinProgressColor));
        OnPropertyChanged(nameof(FatProgressColor));
        OnPropertyChanged(nameof(CarbsProgressColor));
    }

    private DailyMealPlan GetOrCreateMealPlan(DateTime date)
    {
        // Try to load meal plan from snapshot if it exists
        var snapshot = _snapshotService.LoadSnapshot(date);
        if (snapshot != null)
        {
            // Set read-only mode for past dates with snapshots
            IsReadOnly = date.Date < DateTime.Today;

            // Load user preferences from snapshot
            if (snapshot.UserPreferences != null)
            {
                User = snapshot.UserPreferences;
                Logger.Instance.Information("Loaded user from snapshot");
            }

            Logger.Instance.Information("Loaded meal plan from snapshot for {Date} (ReadOnly: {IsReadOnly})",
                date.ToShortDateString(), IsReadOnly);
            return snapshot.MealPlan;
        }

        // Not read-only for dates without snapshots
        IsReadOnly = false;

        User = _preferencesService.LoadPreferences() ?? new();
        Logger.Instance.Information("Loaded user from saved preferences");

        // Create a new meal plan with default meal times
        var mealPlan = new DailyMealPlan
        {
            Date = date,
            Name = $"My Meal Plan for {date:MMMM d, yyyy}"
        };

        // Add default meal times
        mealPlan.MealTimes.Add(new MealTime(MealTimeType.Breakfast));
        mealPlan.MealTimes.Add(new MealTime(MealTimeType.Lunch));
        mealPlan.MealTimes.Add(new MealTime(MealTimeType.Dinner));

        Logger.Instance.Information("Created new meal plan for {Date}", date.ToShortDateString());
        return mealPlan;
    }

    [RelayCommand]
    private void AddMealTime()
    {
        uint idx = (uint) CurrentMealPlan.MealTimes.Count + 1;
        while (CurrentMealPlan.MealTimeNames.Contains($"Meal {idx}")) idx--;
        var newMealTime = new MealTime(MealTimeType.Custom, $"Meal {idx}");
        CurrentMealPlan.MealTimes.Add(newMealTime);
        Logger.Instance.Information("Added new meal time: {Name}", newMealTime.Name);
    }

    [RelayCommand]
    private void RemoveMealTime(MealTime mealTime)
    {
        // Only allow removing custom meal times
        if (mealTime.Type == MealTimeType.Custom && CurrentMealPlan.MealTimes.Contains(mealTime))
        {
            CurrentMealPlan.MealTimes.Remove(mealTime);
            Logger.Instance.Information("Removed meal time: {Name}", mealTime.Name);
        }
    }

    [RelayCommand]
    private void MoveMealTimeUp(MealTime mealTime)
    {
        var index = CurrentMealPlan.MealTimes.IndexOf(mealTime);
        if (index > 0)
        {
            CurrentMealPlan.MealTimes.Move(index, index - 1);
            Logger.Instance.Information("Moved meal time up: {Name}", mealTime.Name);
        }
    }

    [RelayCommand]
    private void MoveMealTimeDown(MealTime mealTime)
    {
        var index = CurrentMealPlan.MealTimes.IndexOf(mealTime);
        if (index >= 0 && index < CurrentMealPlan.MealTimes.Count - 1)
        {
            CurrentMealPlan.MealTimes.Move(index, index + 1);
            Logger.Instance.Information("Moved meal time down: {Name}", mealTime.Name);
        }
    }

    [RelayCommand]
    private void RemoveFoodItem(MealPlanItem item)
    {
        foreach (var mealTime in CurrentMealPlan.MealTimes)
        {
            if (mealTime.Items.Contains(item))
            {
                mealTime.Items.Remove(item);
                Logger.Instance.Information("Removed {Product} from {MealTime}",
                    item.Product.Name, mealTime.Name);
                break;
            }
        }
    }

    // Nutritional progress properties
    public double CaloriesProgress => User.DailyCalories > 0
        ? (CurrentMealPlan.TotalCalories / User.DailyCalories) * 100
        : 0;

    public double ProteinProgress => User.DailyProtein > 0
        ? (CurrentMealPlan.TotalProtein / User.DailyProtein) * 100
        : 0;

    public double FatProgress => User.DailyFat > 0
        ? (CurrentMealPlan.TotalFat / User.DailyFat) * 100
        : 0;

    public double CarbsProgress => User.DailyCarbohydrates > 0
        ? (CurrentMealPlan.TotalCarbohydrates / User.DailyCarbohydrates) * 100
        : 0;

    // Color properties based on progress (green at 90-110%, red when far off)
    public string CaloriesProgressColor => Config.GetProgressColorHex(CaloriesProgress);
    public string ProteinProgressColor => Config.GetProgressColorHex(ProteinProgress);
    public string FatProgressColor => Config.GetProgressColorHex(FatProgress);
    public string CarbsProgressColor => Config.GetProgressColorHex(CarbsProgress);

    [RelayCommand]
    private void SaveDaySnapshot()
    {
        try
        {
            _snapshotService.SaveSnapshot(CurrentMealPlan, User);
            Logger.Instance.Information("Day snapshot saved for {Date}: Calories={Calories:F0}, Protein={Protein:F1}g, Fat={Fat:F1}g, Carbs={Carbs:F1}g",
                CurrentMealPlan.Date.ToShortDateString(),
                CurrentMealPlan.TotalCalories,
                CurrentMealPlan.TotalProtein,
                CurrentMealPlan.TotalFat,
                CurrentMealPlan.TotalCarbohydrates);

            // Queue snapshot for syncing to cloud if authenticated
            if (_autoSyncService != null)
            {
                var snapshot = new DaySnapshot
                {
                    MealPlan = CurrentMealPlan,
                    UserPreferences = User
                };
                _autoSyncService.QueueSync(SyncOperation.SyncSnapshot, snapshot);
            }

            // Force refresh month snapshot data to update calendar colors immediately
            LoadMonthSnapshotData(CurrentMealPlan.Date.Year, CurrentMealPlan.Date.Month);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to save day snapshot");
        }
    }

    [RelayCommand]
    private async Task ExportMealPlanAsync()
    {
        try
        {
            // Get the top-level window to access the storage provider
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel == null)
            {
                Logger.Instance.Warning("Cannot export: no top-level window available");
                return;
            }

            // Show save file dialog
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export Meal Plan",
                SuggestedFileName = $"MealPlan-{CurrentMealPlan.Date:yyyy-MM-dd}.xml",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("XML Files")
                    {
                        Patterns = new[] { "*.xml" }
                    }
                }
            });

            if (file != null)
            {
                var filePath = file.Path.LocalPath;
                _mealPlanService.ExportMealPlan(CurrentMealPlan, filePath);
                Logger.Instance.Information("Meal plan exported to: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to export meal plan");
        }
    }

    [RelayCommand]
    private async Task ImportMealPlanAsync()
    {
        try
        {
            // Get the top-level window to access the storage provider
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel == null)
            {
                Logger.Instance.Warning("Cannot import: no top-level window available");
                return;
            }

            // Show open file dialog
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Import Meal Plan",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("XML Files")
                    {
                        Patterns = new[] { "*.xml" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var filePath = files[0].Path.LocalPath;
                var importedMealPlan = _mealPlanService.ImportMealPlan(filePath);

                if (importedMealPlan != null)
                {
                    // Update the current meal plan date to today (or selected date)
                    importedMealPlan.Date = SelectedDate;

                    // Replace the current meal plan
                    CurrentMealPlan = importedMealPlan;

                    Logger.Instance.Information("Meal plan imported from: {Path}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to import meal plan");
        }
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        try
        {
            if (IsFavorite)
            {
                // Remove from favorites
                _favoritesService.RemoveFromFavorites(CurrentMealPlan);
                Logger.Instance.Information("Removed meal plan from favorites: {Name}", CurrentMealPlan.Name);

                // Queue favorite deletion for syncing to cloud if authenticated
                if (_autoSyncService != null)
                {
                    _autoSyncService.QueueSync(SyncOperation.DeleteFavorite, CurrentMealPlan);
                }
            }
            else
            {
                // Add to favorites
                _favoritesService.AddToFavorites(CurrentMealPlan);
                Logger.Instance.Information("Added meal plan to favorites: {Name}", CurrentMealPlan.Name);

                // Queue favorite for syncing to cloud if authenticated
                if (_autoSyncService != null)
                {
                    _autoSyncService.QueueSync(SyncOperation.SyncFavorite, CurrentMealPlan);
                }
            }

            // Update favorite status
            UpdateFavoriteStatus();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to toggle favorite");
        }
    }

    [RelayCommand]
    private async Task ShareMealPlanAsync()
    {
        if (_discoverService == null || !IsAuthenticated)
        {
            Logger.Instance.Warning("Cannot share meal plan: not authenticated");
            return;
        }

        try
        {
            var success = await _discoverService.ShareMealPlanAsync(CurrentMealPlan);
            if (success)
            {
                Logger.Instance.Information("Successfully shared meal plan: {Name}", CurrentMealPlan.Name);
            }
            else
            {
                Logger.Instance.Warning("Failed to share meal plan: {Name}", CurrentMealPlan.Name);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to share meal plan");
        }
    }

    public void UpdateFavoriteStatus()
    {
        try
        {
            IsFavorite = _favoritesService.IsFavorite(CurrentMealPlan);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to update favorite status");
            IsFavorite = false;
        }
    }

    /// <summary>
    /// Reloads user preferences from local storage (called after sync completes)
    /// </summary>
    public void ReloadUserPreferences()
    {
        try
        {
            var updatedUser = _preferencesService.LoadPreferences();
            if (updatedUser != null)
            {
                User = updatedUser;
                Logger.Instance.Information("Reloaded user preferences: Weight={Weight}kg, Height={Height}cm, Age={Age}",
                    User.Weight, User.Height, User.Age);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to reload user preferences");
        }
    }

    /// <summary>
    /// Reloads snapshots from local storage and refreshes calendar colors (called after sync completes)
    /// </summary>
    public void ReloadSnapshots()
    {
        try
        {
            // Reload the current meal plan from snapshot if it exists
            var snapshot = _snapshotService.LoadSnapshot(SelectedDate);
            if (snapshot != null)
            {
                CurrentMealPlan = GetOrCreateMealPlan(SelectedDate);
                Logger.Instance.Information("Reloaded snapshot for {Date}", SelectedDate.ToShortDateString());
            }

            // Refresh month snapshot data to update calendar colors
            LoadMonthSnapshotData(SelectedDate.Year, SelectedDate.Month);
            Logger.Instance.Information("Refreshed calendar colors after sync");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to reload snapshots");
        }
    }
}
