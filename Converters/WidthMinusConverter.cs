using System;
using System.Globalization;
using System.Windows.Data;

namespace Cloudless
{
    // Converter: takes a width (double) and subtracts an optional pixel offset passed in ConverterParameter
    public class WidthMinusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                double sub = 0;
                if (parameter != null)
                {
                    double.TryParse(parameter.ToString(), out sub);
                }
                double result = d - sub;
                if (result < 0) result = 0;
                return result;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
