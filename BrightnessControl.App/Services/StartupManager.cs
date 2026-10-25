using Microsoft.Win32;

namespace BrightnessControl.Services;

public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BrightnessControl";

    public static Task<bool> IsEnabledAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return Task.FromResult(key?.GetValue(ValueName) is string);
    }

    public static Task<bool> SetEnabledAsync(bool enabled)
    {
        if (enabled)
        {
            var executable = Environment.ProcessPath ??
                throw new InvalidOperationException("Не удалось определить путь приложения.");
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            key.SetValue(ValueName, $"\"{executable}\" --startup", RegistryValueKind.String);
            return Task.FromResult(true);
        }

        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        return Task.FromResult(false);
    }
}
