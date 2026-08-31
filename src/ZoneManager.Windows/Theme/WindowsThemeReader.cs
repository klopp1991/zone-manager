using Microsoft.Win32;

namespace ZoneManager.Windows.Theme;

public static class WindowsThemeReader
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsSystemDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }
}

