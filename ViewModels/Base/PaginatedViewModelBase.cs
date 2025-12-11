using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DailyMealPlannerExtended.ViewModels.Base;

/// <summary>
/// Base class for ViewModels that implement pagination functionality.
/// Provides common pagination properties and commands to avoid code duplication.
/// </summary>
public abstract partial class PaginatedViewModelBase : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToPreviousPage), nameof(CanGoToNextPage))]
    [NotifyCanExecuteChangedFor(nameof(GoToPreviousPageCommand), nameof(GoToNextPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToPreviousPage), nameof(CanGoToNextPage))]
    [NotifyCanExecuteChangedFor(nameof(GoToPreviousPageCommand), nameof(GoToNextPageCommand))]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _pageSize = 12;

    /// <summary>
    /// Determines if navigating to the previous page is possible
    /// </summary>
    public bool CanGoToPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Determines if navigating to the next page is possible
    /// </summary>
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

    partial void OnCurrentPageChanged(int value)
    {
        OnPageChanged();
    }

    partial void OnPageSizeChanged(int value)
    {
        // Reset to page 1 when page size changes
        CurrentPage = 1;
    }

    /// <summary>
    /// Called when the current page changes. Derived classes should override this
    /// to update their displayed items for the new page.
    /// </summary>
    protected abstract void OnPageChanged();
}
