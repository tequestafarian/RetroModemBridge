using System.Drawing;

namespace RetroModemBridge;

internal static class AppIconHelper
{
    private static Icon? _cachedIcon;

    public static Icon? LoadAppIcon()
    {
        if (_cachedIcon is not null)
            return _cachedIcon;

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "retromodem-bridge.ico");
            if (File.Exists(iconPath))
            {
                _cachedIcon = new Icon(iconPath);
                return _cachedIcon;
            }
        }
        catch
        {
        }

        return null;
    }
}
