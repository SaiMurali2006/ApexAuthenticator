using System.Windows;
using System.Windows.Media;

namespace ApexAuth.UI;

internal static class ResourceHelpers
{
    public static Brush Res(string key) => (Brush)Application.Current.FindResource(key);
}
