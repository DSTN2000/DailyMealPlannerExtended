using Avalonia.Media;

namespace DailyMealPlannerExtended;

/// <summary>
/// Application-wide configuration constants
/// </summary>
public static class Config
{
    /// <summary>
    /// Progress indicator colors based on target achievement percentage
    /// </summary>
    public static class ProgressColors
    {
        /// <summary>
        /// Red - Very far from target
        /// </summary>
        public const string Red = "#F44336";

        /// <summary>
        /// Orange - Far from target
        /// </summary>
        public const string Orange = "#FF9800";

        /// <summary>
        /// Yellow - Moderately close to target
        /// </summary>
        public const string Yellow = "#FFC107";

        /// <summary>
        /// Light Green - Close to target
        /// </summary>
        public const string LightGreen = "#8BC34A";

        /// <summary>
        /// Green - On target (optimal)
        /// </summary>
        public const string Green = "#4CAF50";

        /// <summary>
        /// Light gray - No data or no snapshot
        /// </summary>
        public const string NoData = "#E0E0E0";

        // Brush versions for use in converters and code-behind
        public static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.Parse(Red));
        public static readonly SolidColorBrush OrangeBrush = new SolidColorBrush(Color.Parse(Orange));
        public static readonly SolidColorBrush YellowBrush = new SolidColorBrush(Color.Parse(Yellow));
        public static readonly SolidColorBrush LightGreenBrush = new SolidColorBrush(Color.Parse(LightGreen));
        public static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.Parse(Green));
        public static readonly SolidColorBrush NoDataBrush = new SolidColorBrush(Color.Parse(NoData));
        public static readonly SolidColorBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);
    }

    /// <summary>
    /// Progress percentage thresholds for color determination
    /// </summary>
    public static class ProgressThresholds
    {
        public const double VeryLow = 40;
        public const double Low = 70;
        public const double Approaching = 85;
        public const double OptimalMin = 90;
        public const double OptimalMax = 110;
        public const double SlightlyOver = 115;
        public const double High = 130;
        public const double VeryHigh = 150;
    }

    /// <summary>
    /// Helper method to get the appropriate progress color hex string based on percentage
    /// </summary>
    /// <param name="progress">Progress percentage (0-infinity)</param>
    /// <returns>Hex color string representing the progress level</returns>
    public static string GetProgressColorHex(double progress)
    {
        return progress switch
        {
            < ProgressThresholds.VeryLow => ProgressColors.Red,
            < ProgressThresholds.Low => ProgressColors.Orange,
            < ProgressThresholds.Approaching => ProgressColors.Yellow,
            < ProgressThresholds.OptimalMin => ProgressColors.LightGreen,
            <= ProgressThresholds.OptimalMax => ProgressColors.Green,
            < ProgressThresholds.SlightlyOver => ProgressColors.LightGreen,
            < ProgressThresholds.High => ProgressColors.Yellow,
            < ProgressThresholds.VeryHigh => ProgressColors.Orange,
            _ => ProgressColors.Red
        };
    }

    /// <summary>
    /// Helper method to get the appropriate progress color brush based on percentage
    /// </summary>
    /// <param name="progress">Progress percentage (0-infinity)</param>
    /// <returns>SolidColorBrush representing the progress level</returns>
    public static SolidColorBrush GetProgressColorBrush(double progress)
    {
        return progress switch
        {
            < ProgressThresholds.VeryLow => ProgressColors.RedBrush,
            < ProgressThresholds.Low => ProgressColors.OrangeBrush,
            < ProgressThresholds.Approaching => ProgressColors.YellowBrush,
            < ProgressThresholds.OptimalMin => ProgressColors.LightGreenBrush,
            <= ProgressThresholds.OptimalMax => ProgressColors.GreenBrush,
            < ProgressThresholds.SlightlyOver => ProgressColors.LightGreenBrush,
            < ProgressThresholds.High => ProgressColors.YellowBrush,
            < ProgressThresholds.VeryHigh => ProgressColors.OrangeBrush,
            _ => ProgressColors.RedBrush
        };
    }
}
