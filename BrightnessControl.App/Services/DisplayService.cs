using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace BrightnessControl.Services;

public static class DisplayService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitor
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] physicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr monitor, out uint minimum, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr monitor, uint brightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    internal static extern bool DestroyPhysicalMonitor(IntPtr monitor);

    public static IReadOnlyList<MonitorInfo> Enumerate()
    {
        var displays = new List<MonitorInfo>();
        MonitorEnumProc callback = (IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
        {
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0)
                return true;

            var physical = new PhysicalMonitor[checked((int)count)];
            if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physical))
                return true;

            foreach (var item in physical)
            {
                if (GetMonitorBrightness(item.Handle, out var minimum, out var current, out var maximum))
                {
                    var percent = maximum <= minimum ? 0 :
                        (int)Math.Round(100.0 * (current - minimum) / (maximum - minimum));
                    displays.Add(new MonitorInfo(item.Description, "DDC/CI", null, item.Handle,
                        minimum, maximum, percent));
                }
                else
                {
                    DestroyPhysicalMonitor(item.Handle);
                }
            }
            return true;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        AddWmiDisplays(displays);
        return displays.OrderBy(display => display.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static void AddWmiDisplays(List<MonitorInfo> displays)
    {
        object? locator = null;
        object? services = null;
        try
        {
            var locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (locatorType is null)
                return;

            locator = Activator.CreateInstance(locatorType);
            if (locator is null)
                return;

            dynamic comLocator = locator;
            services = comLocator.ConnectServer(".", @"root\wmi");
            dynamic comServices = services;
            dynamic monitors = comServices.ExecQuery("SELECT * FROM WmiMonitorBrightness");
            foreach (dynamic monitor in monitors)
            {
                string instance = Convert.ToString(monitor.InstanceName) ?? "";
                string name = instance.Split('\\').LastOrDefault() ?? "Встроенный экран";
                var brightness = Convert.ToInt32(monitor.CurrentBrightness);
                displays.Add(new MonitorInfo(name, "Windows", instance, IntPtr.Zero, 0, 100, brightness));
            }
        }
        catch
        {
            // WMI is optional; DDC/CI displays remain available if WMI is unavailable.
        }
        finally
        {
            if (services is not null && Marshal.IsComObject(services))
                Marshal.FinalReleaseComObject(services);
            if (locator is not null && Marshal.IsComObject(locator))
                Marshal.FinalReleaseComObject(locator);
        }
    }

    public static void SetBrightness(MonitorInfo display, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (display.Kind == "DDC/CI")
        {
            var raw = display.Minimum + (uint)Math.Round(
                (display.Maximum - display.Minimum) * percent / 100.0);
            if (!SetMonitorBrightness(display.Handle, raw))
                throw new InvalidOperationException("Монитор не ответил на DDC/CI. Проверьте, включён ли DDC/CI в меню монитора.");
            return;
        }

        if (display.Kind == "Windows")
        {
            SetWmiBrightness(display.InstanceName!, (byte)percent);
            return;
        }

        throw new NotSupportedException("Для этого экрана нет интерфейса управления яркостью.");
    }

    private static void SetWmiBrightness(string instanceName, byte brightness)
    {
        object? locator = null;
        object? services = null;
        try
        {
            var locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (locatorType is null)
                throw new InvalidOperationException("Интерфейс WMI недоступен.");

            locator = Activator.CreateInstance(locatorType);
            if (locator is null)
                throw new InvalidOperationException("Не удалось подключиться к WMI.");

            dynamic comLocator = locator;
            services = comLocator.ConnectServer(".", @"root\wmi");
            dynamic comServices = services;
            dynamic methods = comServices.ExecQuery("SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (dynamic method in methods)
            {
                if (!string.Equals(Convert.ToString(method.InstanceName), instanceName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var result = Convert.ToUInt32(method.WmiSetBrightness((uint)1, brightness));
                if (result != 0)
                    throw new InvalidOperationException($"WMI вернул код {result} при изменении яркости.");
                return;
            }
            throw new InvalidOperationException("Windows не нашла интерфейс управления яркостью для этого экрана.");
        }
        finally
        {
            if (services is not null && Marshal.IsComObject(services))
                Marshal.FinalReleaseComObject(services);
            if (locator is not null && Marshal.IsComObject(locator))
                Marshal.FinalReleaseComObject(locator);
        }
    }
}