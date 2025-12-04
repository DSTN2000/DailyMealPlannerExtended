using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using DailyMealPlannerExtended.Models;
using System.Collections.ObjectModel;

namespace DailyMealPlannerExtended.ViewModels;

public partial class DiscoverViewModel : ViewModelBase
{
    private readonly SupabaseDiscoverService? _discoverService;
    private readonly FavoriteMealPlansService _favoritesService;
    private readonly MealPlanViewModel? _mealPlanViewModel;
    private const int PageSize = 10;

    [ObservableProperty]
    private ObservableCollection<DailyMealPlan> _currentPageMealPlans = new();

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalMealPlans = 0;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _isAuthenticated = false;

    [ObservableProperty]
    private string _statusMessage = "";

    private List<DailyMealPlan> _allMealPlans = new();

    public bool IsReadOnly => _mealPlanViewModel?.IsReadOnly ?? false;

    public DiscoverViewModel()
    {
        _favoritesService = new FavoriteMealPlansService();
    }

    public DiscoverViewModel(MealPlanViewModel mealPlanViewModel, SupabaseAuthService? authService) : this()
    {
        _mealPlanViewModel = mealPlanViewModel;
        IsAuthenticated = authService?.IsAuthenticated ?? false;

        if (authService != null && IsAuthenticated)
        {
            _discoverService = new SupabaseDiscoverService(authService);
            _ = LoadSharedMealPlansAsync();
        }
        else
        {
            StatusMessage = "Please log in to discover shared meal plans";
        }

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

    private async Task LoadSharedMealPlansAsync()
    {
        if (_discoverService == null || !IsAuthenticated)
        {
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Loading shared meal plans...";

            _allMealPlans = await _discoverService.GetSharedMealPlansAsync(100);
            TotalMealPlans = _allMealPlans.Count;
            TotalPages = (int)Math.Ceiling((double)TotalMealPlans / PageSize);

            if (TotalPages == 0)
            {
                TotalPages = 1;
                CurrentPage = 1;
            }

            UpdateCurrentPage();

            StatusMessage = TotalMealPlans > 0
                ? $"Loaded {TotalMealPlans} shared meal plans"
                : "No shared meal plans found";

            Logger.Instance.Information("Loaded {Count} shared meal plans", TotalMealPlans);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load shared meal plans");
            StatusMessage = "Failed to load shared meal plans";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnCurrentPageChanged(int value)
    {
        UpdateCurrentPage();
    }

    private void UpdateCurrentPage()
    {
        CurrentPageMealPlans.Clear();

        if (_allMealPlans.Count == 0)
        {
            return;
        }

        var skip = (CurrentPage - 1) * PageSize;
        var pageMealPlans = _allMealPlans
            .Skip(skip)
            .Take(PageSize)
            .ToList();

        foreach (var mealPlan in pageMealPlans)
        {
            CurrentPageMealPlans.Add(mealPlan);
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
    private async Task ToggleLikeAsync(DailyMealPlan mealPlan)
    {
        if (_discoverService == null || !IsAuthenticated || mealPlan.SharedMealPlanId == null)
        {
            Logger.Instance.Warning("Cannot like meal plan: not authenticated or invalid meal plan");
            return;
        }

        try
        {
            bool success;
            if (mealPlan.IsLikedByCurrentUser)
            {
                success = await _discoverService.UnlikeMealPlanAsync(mealPlan.SharedMealPlanId.Value);
                if (success)
                {
                    mealPlan.IsLikedByCurrentUser = false;
                    mealPlan.LikesCount--;
                }
            }
            else
            {
                success = await _discoverService.LikeMealPlanAsync(mealPlan.SharedMealPlanId.Value);
                if (success)
                {
                    mealPlan.IsLikedByCurrentUser = true;
                    mealPlan.LikesCount++;
                }
            }

            if (success)
            {
                // Re-sort the list by likes count
                _allMealPlans = _allMealPlans.OrderByDescending(m => m.LikesCount).ToList();
                UpdateCurrentPage();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to toggle like");
        }
    }

    [RelayCommand]
    private void AddToFavorites(DailyMealPlan mealPlan)
    {
        try
        {
            // Create a copy without shared meal plan metadata
            var favoriteMealPlan = new DailyMealPlan
            {
                Date = DateTime.Today,
                Name = mealPlan.Name
            };

            favoriteMealPlan.MealTimes.Clear();

            foreach (var mealTime in mealPlan.MealTimes)
            {
                var newMealTime = new MealTime(mealTime.Type, mealTime.Type == MealTimeType.Custom ? mealTime.Name : null);

                foreach (var item in mealTime.Items)
                {
                    var newItem = new MealPlanItem(item.Product, item.Weight)
                    {
                        Image = item.Image,
                        Note = item.Note
                    };
                    newMealTime.Items.Add(newItem);
                }

                favoriteMealPlan.MealTimes.Add(newMealTime);
            }

            _favoritesService.AddToFavorites(favoriteMealPlan);
            Logger.Instance.Information("Added shared meal plan to favorites: {Name}", mealPlan.Name);
            StatusMessage = $"Added '{mealPlan.Name}' to favorites";
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to add to favorites");
            StatusMessage = "Failed to add to favorites";
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

            newMealPlan.MealTimes.Clear();

            foreach (var mealTime in mealPlan.MealTimes)
            {
                var newMealTime = new MealTime(mealTime.Type, mealTime.Type == MealTimeType.Custom ? mealTime.Name : null);

                foreach (var item in mealTime.Items)
                {
                    var newItem = new MealPlanItem(item.Product, item.Weight)
                    {
                        Image = item.Image,
                        Note = item.Note
                    };
                    newMealTime.Items.Add(newItem);
                }

                newMealPlan.MealTimes.Add(newMealTime);
            }

            _mealPlanViewModel.CurrentMealPlan = newMealPlan;

            Logger.Instance.Information("Loaded shared meal plan '{Name}' with {Calories:F0} kcal",
                mealPlan.Name, mealPlan.TotalCalories);
            StatusMessage = $"Loaded '{mealPlan.Name}'";
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load meal plan");
            StatusMessage = "Failed to load meal plan";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadSharedMealPlansAsync();
    }
}
