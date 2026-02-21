using System.ComponentModel;
using System.Text.Json;
using System.Windows;

namespace LanguageSchoolERP.App;

internal static class WindowStatePersistence
{
    private static readonly object Sync = new();
    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanguageSchoolERP",
        "window-state.json");

    private static bool _isRegistered;
    private static bool _isLoaded;
    private static Dictionary<string, WindowBoundsState> _states = new(StringComparer.Ordinal);

    public static void Register()
    {
        if (_isRegistered)
            return;

        _isRegistered = true;

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.InitializedEvent, new RoutedEventHandler(OnWindowInitialized));
    }

    private static void OnWindowInitialized(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || !ReferenceEquals(window, e.OriginalSource))
            return;

        if (!ShouldPersist(window))
            return;

        window.Closing += OnWindowClosing;
        Restore(window);
    }

    private static void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (sender is not Window window)
            return;

        if (!ShouldPersist(window))
            return;

        Save(window);
    }

    private static bool ShouldPersist(Window window)
        => window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

    private static string GetWindowKey(Window window)
        => window.GetType().FullName ?? window.GetType().Name;

    private static void Restore(Window window)
    {
        var key = GetWindowKey(window);
        WindowBoundsState? state;

        lock (Sync)
        {
            EnsureLoaded();
            _states.TryGetValue(key, out state);
        }

        if (state is null || !state.IsValid())
            return;

        if (!IsWithinVirtualScreen(state.Left, state.Top, state.Width, state.Height))
            return;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = state.Left;
        window.Top = state.Top;
        window.Width = state.Width;
        window.Height = state.Height;

        if (state.WindowState == WindowState.Maximized)
            window.WindowState = WindowState.Maximized;
    }

    private static void Save(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var state = new WindowBoundsState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = window.WindowState == WindowState.Minimized ? WindowState.Normal : window.WindowState
        };

        var key = GetWindowKey(window);

        lock (Sync)
        {
            EnsureLoaded();
            _states[key] = state;
            Persist();
        }
    }

    private static bool IsWithinVirtualScreen(double left, double top, double width, double height)
    {
        var rect = new Rect(left, top, width, height);
        var virtualRect = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        return rect.IntersectsWith(virtualRect);
    }

    private static void EnsureLoaded()
    {
        if (_isLoaded)
            return;

        _isLoaded = true;
        try
        {
            if (!File.Exists(StateFilePath))
                return;

            var json = File.ReadAllText(StateFilePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, WindowBoundsState>>(json);
            if (parsed is not null)
                _states = new Dictionary<string, WindowBoundsState>(parsed, StringComparer.Ordinal);
        }
        catch
        {
            _states = new Dictionary<string, WindowBoundsState>(StringComparer.Ordinal);
        }
    }

    private static void Persist()
    {
        var directory = Path.GetDirectoryName(StateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_states, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StateFilePath, json);
    }

    private sealed class WindowBoundsState
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public WindowState WindowState { get; set; }

        public bool IsValid()
            => !double.IsNaN(Left)
               && !double.IsNaN(Top)
               && !double.IsNaN(Width)
               && !double.IsNaN(Height)
               && Width > 0
               && Height > 0;
    }
}
