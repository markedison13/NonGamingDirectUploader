using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Simple bool -> Visibility converter (true = Visible, false = Collapsed).
    /// Used on the Online Gaming Uploader tabs to hide the derived Payout row
    /// on category boxes that don't have one (only FUNaloMAX does).
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}