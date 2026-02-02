using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace DriveRPC.Shared.UWP.Converters
{
    public class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Enum)
            {
                return (int)value;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is int intValue)
            {
                return Enum.ToObject(targetType, intValue);
            }
            return 0;
        }
    }
}