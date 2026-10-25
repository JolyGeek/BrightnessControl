using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using BrightnessControl.Services;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace BrightnessControl;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _writeTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly NotifyIcon _trayIcon;
    private readonly Icon _ownedTrayIcon;
    private readonly IntPtr _trayIconHandle;
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private bool _updatingSlider;
    private bool _exiting;
    private AppSettings _settings = new();

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BrightnessControl", "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        _settings = LoadSettings();
        ApplyTheme(_settings.Theme, persist: false);

        _ownedTrayIcon = CreateTrayIcon(out _trayIconHandle);
        _trayIcon = new NotifyIcon
        {
            Icon = _ownedTrayIcon,
            Text = "Яркость экрана",
            Visible = true,
            ContextMenuStrip = CreateTrayMenu()
        };

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                Dispatcher.Invoke(TogglePanel);
        };
        _writeTimer.Tick += async (_, _) => await ApplyBrightnessAsync();
        DisplayPicker.SelectionChanged += DisplayPicker_SelectionChanged;
        BrightnessSlider.ValueChanged += BrightnessSlider_ValueChanged;
        ThemeButton.Click += (_, _) => ApplyTheme(_settings.Theme == "dark" ? "light" : "dark", persist: true);
        CloseButton.Click += (_, _) => Hide();
        ExitButton.Click += (_, _) => ExitApplication();
        StartupToggle.Click += StartupToggle_Click;
        Loaded += MainWindow_Loaded;
        Deactivated += (_, _) =>
        {
            if (IsVisible && !_exiting)
                Hide();
        };
        Closing += (_, e) =>
        {
            if (!_exiting)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionNearTaskbar();
        try
        {
            _monitors = await Task.Run(DisplayService.Enumerate);
            DisplayPicker.ItemsSource = _monitors;
            DisplayPicker.SelectedIndex = _monitors.Count > 0 ? 0 : -1;
            BrightnessSlider.IsEnabled = _monitors.Count > 0;
            if (_monitors.Count == 0)
                BrightnessSlider.ToolTip = "Совместимый экран не найден";
        }
        catch (Exception ex)
        {
            BrightnessSlider.IsEnabled = false;
            BrightnessSlider.ToolTip = $"Не удалось найти экраны: {ex.Message}";
        }

        try
        {
            StartupToggle.IsChecked = await StartupManager.IsEnabledAsync();
        }
        catch
        {
            StartupToggle.IsChecked = false;
            StartupToggle.ToolTip = "Не удалось прочитать настройку автозапуска";
        }
    }

    private void DisplayPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DisplayPicker.SelectedItem is not MonitorInfo monitor)
            return;

        _updatingSlider = true;
        BrightnessSlider.Value = monitor.Brightness;
        ValueText.Text = $"{monitor.Brightness}%";
        _updatingSlider = false;
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = (int)Math.Round(BrightnessSlider.Value);
        ValueText.Text = $"{value}%";
        if (_updatingSlider || DisplayPicker.SelectedItem is not MonitorInfo)
            return;

        _writeTimer.Stop();
        _writeTimer.Start();
    }

    private async Task ApplyBrightnessAsync()
    {
        _writeTimer.Stop();
        if (DisplayPicker.SelectedItem is not MonitorInfo monitor)
            return;

        var value = (int)Math.Round(BrightnessSlider.Value);
        try
        {
            await Task.Run(() => DisplayService.SetBrightness(monitor, value));
            BrightnessSlider.ToolTip = "Яркость установлена";
        }
        catch (Exception ex)
        {
            BrightnessSlider.ToolTip = $"Не удалось изменить яркость: {ex.Message}";
        }
    }

    private async void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var enabled = await StartupManager.SetEnabledAsync(StartupToggle.IsChecked == true);
            StartupToggle.IsChecked = enabled;
        }
        catch (Exception ex)
        {
            StartupToggle.IsChecked = false;
            StartupToggle.ToolTip = $"Не удалось изменить автозапуск: {ex.Message}";
        }
    }

    private void ApplyTheme(string theme, bool persist)
    {
        _settings.Theme = theme;
        var dark = theme == "dark";
        Resources["SurfaceBrush"] = Brush(dark ? "#202020" : "#F7F7F7");
        Resources["CardBrush"] = Brush(dark ? "#2B2B2B" : "#FFFFFF");
        Resources["ThemeBorderBrush"] = Brush(dark ? "#3A3A3A" : "#E6E6E6");
        Resources["TextBrush"] = Brush(dark ? "#F5F5F5" : "#202020");
        Resources["MutedBrush"] = Brush(dark ? "#B7B7B7" : "#777777");
        Resources["AccentBrush"] = Brush(dark ? "#60CDFF" : "#0078D4");
        Resources["HoverBrush"] = Brush(dark ? "#383838" : "#F0F0F0");
        ThemeButton.Content = "Тема";
        ThemeButton.ToolTip = dark ? "Переключить на светлую тему" : "Переключить на тёмную тему";
        if (persist)
            SaveSettings();
    }

    private static System.Windows.Media.SolidColorBrush Brush(string color) =>
        new((Color)System.Windows.Media.ColorConverter.ConvertFromString(color));

    private AppSettings LoadSettings()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private ContextMenuStrip CreateTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть яркость", null, (_, _) => Dispatcher.Invoke(ShowPanel));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        return menu;
    }

    private void TogglePanel()
    {
        if (IsVisible)
            Hide();
        else
            ShowPanel();
    }

    private void ShowPanel()
    {
        PositionNearTaskbar();
        Show();
        Activate();
    }

    private void PositionNearTaskbar()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 8;
        Top = area.Bottom - Height - 8;
    }

    private void ExitApplication()
    {
        _exiting = true;
        _writeTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _ownedTrayIcon.Dispose();
        DestroyIcon(_trayIconHandle);
        foreach (var monitor in _monitors)
            monitor.Dispose();
        Close();
        Application.Current.Shutdown();
    }

    private static Icon CreateTrayIcon(out IntPtr handle)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Transparent);
        using var rayPen = new Pen(System.Drawing.Color.FromArgb(255, 255, 196, 0), 2.2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4;
            graphics.DrawLine(rayPen,
                (float)(16 + Math.Cos(angle) * 9), (float)(16 + Math.Sin(angle) * 9),
                (float)(16 + Math.Cos(angle) * 13), (float)(16 + Math.Sin(angle) * 13));
        }
        using var sun = new SolidBrush(System.Drawing.Color.FromArgb(255, 255, 214, 64));
        graphics.FillEllipse(sun, 10, 10, 12, 12);
        handle = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(handle);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

public sealed class AppSettings
{
    public string Theme { get; set; } = "dark";
}
