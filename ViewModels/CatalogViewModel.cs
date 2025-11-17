using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using Lab4.Models;

namespace DailyMealPlannerExtended.ViewModels;

public partial class CatalogViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService;
    private CancellationTokenSource? _searchDebounceTokenSource;
    private string? _lastSearchedText;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedType;

    [ObservableProperty]
    private ObservableCollection<string> _types = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableLabels = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedLabels = new();

    [ObservableProperty]
    private ObservableCollection<Product> _products = new();

    [ObservableProperty]
    private Product? _selectedProduct;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageDisplay))]
    private int _currentPage;

    [ObservableProperty]
    private int _totalPages;

    // Display page numbers starting from 1 instead of 0
    public int CurrentPageDisplay => CurrentPage + 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private bool _hasPreviousPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private bool _hasNextPage;

    public int PageSize => _databaseService.PageSize;

    public CatalogViewModel()
    {
        _databaseService = new DatabaseService();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await LoadTypesAsync();
            await LoadLabelsAsync();
            await SearchAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Error in InitializeAsync");
        }
    }

    private async Task LoadTypesAsync()
    {
        try
        {
            var types = await _databaseService.GetTypesAsync();
            Types.Clear();
            foreach (var type in types)
            {
                Types.Add(type);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading types: {ex.Message}");
        }
    }

    private async Task LoadLabelsAsync()
    {
        try
        {
            var labels = await _databaseService.GetLabelsAsync();
            AvailableLabels.Clear();
            foreach (var label in labels)
            {
                AvailableLabels.Add(label);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading labels: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 0;
        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        IsLoading = true;
        try
        {
            var (products, totalCount) = await _databaseService.SearchProductsAsync(
                searchText: SearchText,
                type: SelectedType,
                labels: SelectedLabels.ToList(),
                page: CurrentPage
            );

            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }

            // For search queries, the total count should reflect actual matches
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                // When searching, we fetch PageSize * 10, filter by relevance, then paginate
                // So the total is based on the pre-filtered count
                TotalCount = totalCount;
                TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);
            }
            else
            {
                TotalCount = totalCount;
                TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);
            }

            HasPreviousPage = CurrentPage > 0;
            HasNextPage = CurrentPage < TotalPages - 1;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Error searching products");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
            await LoadProductsAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages - 1)
        {
            CurrentPage++;
            await LoadProductsAsync();
        }
    }

    [RelayCommand]
    private async Task GoToPageAsync(int page)
    {
        if (page >= 0 && page < TotalPages)
        {
            CurrentPage = page;
            await LoadProductsAsync();
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedType = null;
        SelectedLabels.Clear();
        _ = SearchAsync();
    }

    [RelayCommand]
    private void AddLabel(string label)
    {
        if (!SelectedLabels.Contains(label))
        {
            SelectedLabels.Add(label);
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void RemoveLabel(string label)
    {
        if (SelectedLabels.Contains(label))
        {
            SelectedLabels.Remove(label);
            _ = SearchAsync();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Cancel previous debounce
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource = new CancellationTokenSource();
        var token = _searchDebounceTokenSource.Token;
        var searchValue = value; // Capture current value

        // Debounce: wait 300ms before searching
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested && _lastSearchedText != searchValue)
                {
                    _lastSearchedText = searchValue;
                    await SearchAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when user types again
            }
        });
    }

    partial void OnSelectedTypeChanged(string? value)
    {
        _ = SearchAsync();
    }
}
