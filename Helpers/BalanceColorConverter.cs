using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RasidAccountingSystem.Helpers
{
    public class BalanceColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal balance)
            {
                // أخضر للرصيد الموجب (دائن)، أحمر للرصيد السالب (مدين)
                return balance >= 0
                    ? new SolidColorBrush(Color.FromRgb(39, 174, 96))   // أخضر
                    : new SolidColorBrush(Color.FromRgb(231, 76, 60));  // أحمر
            }
            return new SolidColorBrush(Color.FromRgb(39, 174, 96));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}