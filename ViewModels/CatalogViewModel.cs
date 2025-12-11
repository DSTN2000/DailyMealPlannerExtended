using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.ViewModels.Base;
using System;
using System.Linq;

namespace DailyMealPlannerExtended.ViewModels;

public partial class CatalogViewModel : PaginatedViewModelBase
{
    private readonly DatabaseService _databaseService;
    private CancellationTokenSource? _searchDebounceCts;
    private readonly ProductDetailViewModel _productDetailViewModel;

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

    // PageSize is exposed from base class, but DatabaseService determines actual size
    public new int PageSize => _databaseService.PageSize;

    public CatalogViewModel(ProductDetailViewModel productDetailViewModel)
    {
        _databaseService = new DatabaseService();
        _productDetailViewModel = productDetailViewModel ?? throw new ArgumentNullException(nameof(productDetailViewModel));
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
            Logger.Instance.Error(ex, "Error loading types");
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
            Logger.Instance.Error(ex, "Error loading labels");
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1; // Reset to first page (1-based indexing)
        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync(CancellationToken token = default)
    {
        IsLoading = true;
        try
        {
            // DatabaseService uses 0-based indexing, so convert from 1-based
            int dbPage = CurrentPage - 1;

            var (products, totalCount) = await Task.Run(async () =>
            {
                var result = await _databaseService.SearchProductsAsync(
                    searchText: SearchText,
                    type: SelectedType,
                    labels: SelectedLabels.ToList(),
                    page: dbPage
                );
                token.ThrowIfCancellationRequested();
                return result;
            }, token);

            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }

            TotalCount = totalCount;
            TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);
        }
        catch (OperationCanceledException)
        {
            // Expected when a new search is initiated
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

    /// <summary>
    /// Called by base class when page changes. Loads products for the new page.
    /// </summary>
    protected override void OnPageChanged()
    {
        _ = LoadProductsAsync();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedType = null;
        SelectedLabels.Clear();
        _ = SearchCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void AddLabel(string label)
    {
        if (!SelectedLabels.Contains(label))
        {
            SelectedLabels.Add(label);
            _ = SearchCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void RemoveLabel(string label)
    {
        if (SelectedLabels.Contains(label))
        {
            SelectedLabels.Remove(label);
            _ = SearchCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void ShowProductDetail(Product product)
    {
        _productDetailViewModel.ShowProduct(product);
    }

    partial void OnSearchTextChanged(string value)
    {
        DebouncedSearch();
    }

    private async void DebouncedSearch()
    {
        try
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            await Task.Delay(300, token);

            CurrentPage = 1; // Reset to first page (1-based indexing)
            await LoadProductsAsync(token);
        }
        catch (OperationCanceledException)
        {
            // This is expected when the user types quickly.
        }
    }

    partial void OnSelectedTypeChanged(string? value)
    {
        _ = SearchCommand.ExecuteAsync(null);
    }
}