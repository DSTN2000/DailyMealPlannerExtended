using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using DailyMealPlannerExtended.Models;
using System.Collections.ObjectModel;

namespace DailyMealPlannerExtended.ViewModels;

public partial class FavoritesViewModel : ViewModelBase
{
    private readonly FavoriteMealPlansService _favoritesService;
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

    public FavoritesViewModel()
    {
        _favoritesService = new FavoriteMealPlansService();
        LoadFavorites();
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
            // TODO: Implement loading meal plan into current day
            Logger.Instance.Information("Load meal plan requested: {Name}", mealPlan.Name);
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
