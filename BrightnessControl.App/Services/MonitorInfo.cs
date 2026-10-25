namespace BrightnessControl.Services;

public sealed class MonitorInfo : IDisposable
{
    internal MonitorInfo(string name, string kind, string? instanceName, IntPtr handle,
        uint minimum, uint maximum, int brightness)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Монитор" : name.Trim();
        Kind = kind;
        InstanceName = instanceName;
        Handle = handle;
        Minimum = minimum;
        Maximum = maximum;
        Brightness = Math.Clamp(brightness, 0, 100);
    }

    public string Name { get; }
    public string Kind { get; }
    public string Label => $"{Name} — {Kind}";
    public string? InstanceName { get; }
    public int Brightness { get; }
    internal IntPtr Handle { get; }
    internal uint Minimum { get; }
    internal uint Maximum { get; }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
            DisplayService.DestroyPhysicalMonitor(Handle);
    }
}