using System;
using System.Globalization;
using System.Windows.Data;

namespace RasidAccountingSystem.Helpers
{
    /// <summary>
    /// محول لعرض الفرق مع علامة + أو - ونص (زيادة / عجز) في التسويات الجردية
    /// يستخدم MultiBinding لاستقبال DifferenceQuantity و AdjustmentType
    /// </summary>
    public class DifferenceConverter : IMultiValueConverter
    {
        /// <summary>
        /// تحويل قيمتي الفرق ونوع التعديل إلى نص مع علامة + أو - ونص توضيحي
        /// </summary>
        /// <param name="values">[0] = DifferenceQuantity, [1] = AdjustmentType</param>
        /// <param name="targetType">النوع المستهدف</param>
        /// <param name="parameter">معامل إضافي</param>
        /// <param name="culture">الثقافة</param>
        /// <returns>نص الفرق مع العلامة والنص التوضيحي</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // التحقق من وجود قيم
                if (values == null || values.Length < 2)
                {
                    return "0.00 (مطابق)";
                }

                // استخراج DifferenceQuantity
                if (!decimal.TryParse(values[0]?.ToString(), out decimal difference))
                {
                    return "0.00 (مطابق)";
                }

                // استخراج AdjustmentType
                string adjustmentType = values[1]?.ToString() ?? "";

                if (adjustmentType == "Increase")
                {
                    // ✅ زيادة: الكمية الفعلية أكبر من كمية النظام
                    return $"+ {difference:N2} (زيادة)";
                }
                else if (adjustmentType == "Decrease")
                {
                    // ✅ عجز: الكمية الفعلية أصغر من كمية النظام
                    return $"- {difference:N2} (عجز)";
                }
                else if (difference == 0)
                {
                    // ✅ لا فرق
                    return "0.00 (مطابق)";
                }
                else
                {
                    // حالة افتراضية
                    return $"{difference:N2}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DifferenceConverter Error: {ex.Message}");
                return "0.00 (مطابق)";
            }
        }

        /// <summary>
        /// تحويل عكسي (غير مستخدم)
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}