using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Services;
using DailyMealPlannerExtended.Models;
using System.Collections.ObjectModel;

namespace DailyMealPlannerExtended.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly DaySnapshotService _snapshotService;
    private readonly MealPlanViewModel _mealPlanViewModel;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<DateTime> _datesWithSnapshots = new();

    public DateTime Today => DateTime.Today;

    /// <summary>
    /// Expose MealPlanViewModel for calendar coloring converter to access MonthSnapshotProgress
    /// </summary>
    public MealPlanViewModel MealPlanViewModel => _mealPlanViewModel;

    public HistoryViewModel(MealPlanViewModel mealPlanViewModel, AutoSyncService? autoSyncService = null)
    {
        _snapshotService = new DaySnapshotService();
        _mealPlanViewModel = mealPlanViewModel;
        // AutoSyncService stored for potential future use (e.g., syncing snapshots)
        LoadAvailableDates();
    }

    private void LoadAvailableDates()
    {
        try
        {
            var dates = _snapshotService.GetAllSnapshotDates();
            DatesWithSnapshots.Clear();
            foreach (var date in dates)
            {
                DatesWithSnapshots.Add(date);
            }
            Logger.Instance.Information("Loaded {Count} dates with snapshots", dates.Count);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load snapshot dates");
        }
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        LoadSnapshotForDate(value);
    }

    private void LoadSnapshotForDate(DateTime date)
    {
        try
        {
            var snapshot = _snapshotService.LoadSnapshot(date);
            if (snapshot != null)
            {
                // Load the meal plan into the meal plan view
                _mealPlanViewModel.SelectedDate = date;
                Logger.Instance.Information("Loaded snapshot for {Date}", date.ToShortDateString());
            }
            else
            {
                Logger.Instance.Information("No snapshot found for {Date}", date.ToShortDateString());
                // Still update the date to allow creating a new meal plan
                _mealPlanViewModel.SelectedDate = date;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to load snapshot for date");
        }
    }

    [RelayCommand]
    private void RefreshDates()
    {
        LoadAvailableDates();
    }
}
