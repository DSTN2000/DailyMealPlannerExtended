using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DailyMealPlannerExtended.ViewModels;

namespace DailyMealPlannerExtended.Converters;

/// <summary>
/// Converter to color calendar day cells based on snapshot calories progress
/// Uses the same color scheme as the main progress bars for consistency
/// </summary>
public class CalendarDaySnapshotColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0] = DateTime (from CalendarDayButton's implicit binding)
        // values[1] = MealPlanViewModel (DataContext from parent UserControl)

        try
        {
            Services.Logger.Instance.Debug("CalendarDaySnapshotColorConverter called with {Count} values", values.Count);

            if (values.Count < 2)
            {
                Services.Logger.Instance.Debug("Not enough values");
                return Config.ProgressColors.TransparentBrush;
            }

            Services.Logger.Instance.Debug("Value[0] type: {Type}, Value[1] type: {Type2}",
                values[0]?.GetType().Name ?? "null",
                values[1]?.GetType().Name ?? "null");

            // The first value should be DateTime directly from CalendarDayButton
            if (values[0] is DateTime date && values[1] is MealPlanViewModel viewModel)
            {
                Services.Logger.Instance.Debug("Checking date {Date}, month cache has {Count} entries",
                    date.Date, viewModel.MonthSnapshotProgress.Count);

                // Get the calories progress for this date from the cached month data
                if (viewModel.MonthSnapshotProgress.TryGetValue(date.Date, out var caloriesProgress))
                {
                    Services.Logger.Instance.Debug("Found progress {Progress}% for {Date}", caloriesProgress, date.Date);
                    // Use the same color logic as the progress bars
                    return Config.GetProgressColorBrush(caloriesProgress);
                }
                else
                {
                    Services.Logger.Instance.Debug("No progress data for {Date}", date.Date);
                }
            }
            else
            {
                Services.Logger.Instance.Warning("Unexpected types in converter - Value[0]: {Type0}, Value[1]: {Type1}",
                    values[0]?.GetType().FullName ?? "null",
                    values[1]?.GetType().FullName ?? "null");
            }

            // No snapshot for this date - return transparent
            return Config.ProgressColors.TransparentBrush;
        }
        catch (Exception ex)
        {
            Services.Logger.Instance.Error(ex, "Error in CalendarDaySnapshotColorConverter");
            return Config.ProgressColors.TransparentBrush;
        }
    }
}
