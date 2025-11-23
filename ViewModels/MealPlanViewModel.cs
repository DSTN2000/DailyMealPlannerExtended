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

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private DailyMealPlan _currentMealPlan;

    [ObservableProperty]
    private User _user;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isReadOnly;

    public MealPlanViewModel()
    {
        _snapshotService = new();
        _mealPlanService = new();
        _favoritesService = new();
        _preferencesService = new();
        _user = _preferencesService.LoadPreferences() ?? new();
        _currentMealPlan = GetOrCreateMealPlan(SelectedDate);

        // Subscribe to property changes for progress updates
        SubscribeToPropertyChanges();

        // Update favorite status
        UpdateFavoriteStatus();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        Logger.Instance.Information("Getting the meal plan for {Date}", value.ToShortDateString());
        CurrentMealPlan = GetOrCreateMealPlan(value);
    }

    partial void OnCurrentMealPlanChanged(DailyMealPlan value)
    {
        // Subscribe to the new meal plan's property changes
        if (value != null)
        {
            value.PropertyChanged += CurrentMealPlan_PropertyChanged;
        }

        // Update all progress properties
        NotifyProgressChanged();

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

        // Update all progress properties
        NotifyProgressChanged();
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
    public string CaloriesProgressColor => GetProgressColor(CaloriesProgress);
    public string ProteinProgressColor => GetProgressColor(ProteinProgress);
    public string FatProgressColor => GetProgressColor(FatProgress);
    public string CarbsProgressColor => GetProgressColor(CarbsProgress);

    private static string GetProgressColor(double progress)
    {
        // Color constants
        const string Red = "#F44336";
        const string Orange = "#FF9800";
        const string Yellow = "#FFC107";
        const string LightGreen = "#8BC34A";
        const string Green = "#4CAF50";

        // Progress thresholds
        const double VeryLow = 40;
        const double Low = 70;
        const double Approaching = 85;
        const double OptimalMin = 90;
        const double OptimalMax = 110;
        const double SlightlyOver = 115;
        const double High = 130;
        const double VeryHigh = 150;

        return progress switch
        {
            < VeryLow => Red,
            < Low => Orange,
            < Approaching => Yellow,
            < OptimalMin => LightGreen,
            <= OptimalMax => Green,
            < SlightlyOver => LightGreen,
            < High => Yellow,
            < VeryHigh => Orange,
            _ => Red
        };
    }

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
            }
            else
            {
                // Add to favorites
                _favoritesService.AddToFavorites(CurrentMealPlan);
                Logger.Instance.Information("Added meal plan to favorites: {Name}", CurrentMealPlan.Name);
            }

            // Update favorite status
            UpdateFavoriteStatus();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to toggle favorite");
        }
    }

    private void UpdateFavoriteStatus()
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
}
