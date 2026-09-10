using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RasidAccountingSystem.Helpers
{
    /// <summary>
    /// محول لإظهار/إخفاء علامة الوحدة (Unit1, Unit2, Unit3)
    /// يستخدم في عرض تفاصيل التسويات الجردية
    /// </summary>
    public class UnitTagVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string unitTag = value as string ?? "Unit3";

            // إظهار العلامة فقط إذا كانت الوحدة مختلفة عن الافتراضية (Unit3)
            if (unitTag == "Unit3" || string.IsNullOrEmpty(unitTag))
                return Visibility.Collapsed;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}