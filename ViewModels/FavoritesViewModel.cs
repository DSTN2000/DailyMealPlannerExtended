using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using DailyMealPlannerExtended.Models;
using System.Collections.ObjectModel;

namespace DailyMealPlannerExtended.ViewModels;

public partial class FavoritesViewModel : ViewModelBase
{
    private readonly FavoriteMealPlansService _favoritesService;
    private readonly MealPlanViewModel? _mealPlanViewModel;
    private readonly AutoSyncService? _autoSyncService;
    private List<(string fileName, DailyMealPlan mealPlan)> _allFavorites = new();
    private const int PageSize = 10;

    [ObservableProperty]
    private ObservableCollection<DailyMealPlan> _currentPageFavorites = new();

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalFavorites = 0;

    public bool IsReadOnly => _mealPlanViewModel?.IsReadOnly ?? false;

    public FavoritesViewModel()
    {
        _favoritesService = new FavoriteMealPlansService();
        LoadFavorites();
    }

    public FavoritesViewModel(MealPlanViewModel mealPlanViewModel, AutoSyncService? autoSyncService = null) : this()
    {
        _mealPlanViewModel = mealPlanViewModel;
        _autoSyncService = autoSyncService;

        // Subscribe to MealPlanViewModel's IsReadOnly changes
        if (_mealPlanViewModel != null)
        {
            _mealPlanViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MealPlanViewModel.IsReadOnly))
                {
                    OnPropertyChanged(nameof(IsReadOnly));
                }
            };
        }
    }

    private void LoadFavorites()
    {
        try
        {
            _allFavorites = _favoritesService.GetFavorites();
            TotalFavorites = _allFavorites.Count;
            TotalPages = (int)Math.Ceiling((double)TotalFavorites / PageSize);

            if (TotalPages == 0)
            {
                TotalPages = 1;
                CurrentPage = 1;
            }

            UpdateCurrentPage();
            Logger.Instance.Information("Loaded {Count} favorites, {Pages} pages", TotalFavorites, TotalPages);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load favorites");
        }
    }

    partial void OnCurrentPageChanged(int value)
    {
        UpdateCurrentPage();
    }

    private void UpdateCurrentPage()
    {
        CurrentPageFavorites.Clear();

        if (_allFavorites.Count == 0)
        {
            return;
        }

        var skip = (CurrentPage - 1) * PageSize;
        var pageFavorites = _allFavorites
            .Skip(skip)
            .Take(PageSize)
            .Select(f => f.mealPlan)
            .ToList();

        foreach (var favorite in pageFavorites)
        {
            CurrentPageFavorites.Add(favorite);
        }

        // Notify command CanExecute state
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    public bool CanGoToPreviousPage => CurrentPage > 1;
    public bool CanGoToNextPage => CurrentPage < TotalPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void GoToPreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void GoToNextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
        }
    }

    [RelayCommand]
    private void RemoveFromFavorites(DailyMealPlan mealPlan)
    {
        try
        {
            _favoritesService.RemoveFromFavorites(mealPlan);
            Logger.Instance.Information("Removed from favorites: {Name}", mealPlan.Name);

            // Queue favorite deletion for syncing to cloud if authenticated
            if (_autoSyncService != null)
            {
                _autoSyncService.QueueSync(SyncOperation.DeleteFavorite, mealPlan);
            }

            // Reload favorites and update current page
            LoadFavorites();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to remove from favorites");
        }
    }

    [RelayCommand]
    private void LoadMealPlan(DailyMealPlan mealPlan)
    {
        try
        {
            if (_mealPlanViewModel == null)
            {
                Logger.Instance.Warning("Cannot load meal plan: MealPlanViewModel not available");
                return;
            }

            // Create a deep copy of the meal plan and set it to today's date
            var newMealPlan = new DailyMealPlan
            {
                Date = _mealPlanViewModel.SelectedDate,
                Name = mealPlan.Name
            };

            // Clear default meal times
            newMealPlan.MealTimes.Clear();

            // Copy all meal times and items
            foreach (var mealTime in mealPlan.MealTimes)
            {
                var newMealTime = new MealTime(mealTime.Type, mealTime.Type == MealTimeType.Custom ? mealTime.Name : null);

                foreach (var item in mealTime.Items)
                {
                    // Create a copy with all properties including Image and Note
                    var newItem = new MealPlanItem(item.Product, item.Weight)
                    {
                        Image = item.Image,
                        Note = item.Note
                    };
                    newMealTime.Items.Add(newItem);
                }

                newMealPlan.MealTimes.Add(newMealTime);
            }

            // Replace the current meal plan
            _mealPlanViewModel.CurrentMealPlan = newMealPlan;

            Logger.Instance.Information("Loaded meal plan '{Name}' with {Calories:F0} kcal",
                mealPlan.Name, mealPlan.TotalCalories);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load meal plan");
        }
    }

    [RelayCommand]
    private void RefreshFavorites()
    {
        LoadFavorites();
    }
}
