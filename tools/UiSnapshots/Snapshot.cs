using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

namespace FryWorld.Tools.UiSnapshots;

/// <summary>
/// Headless Avalonia session using Skia renderer with no screen and no message loop.
/// Renders FryWorld views, windows, overlays, and plugins off-screen to audit layout and save PNG snapshots.
/// </summary>
public static class Snapshot
{
    private static bool _isInitialized;

    public static string OutputFolder { get; private set; } = string.Empty;

    public static void Start(string? outputDir = null, bool lightTheme = false)
    {
        if (!_isInitialized)
        {
            AppBuilder.Configure<FryWorld.App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .WithInterFont()
                .SetupWithoutStarting();

            _isInitialized = true;
        }

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = lightTheme ? ThemeVariant.Light : ThemeVariant.Dark;
        }

        OutputFolder = Path.GetFullPath(outputDir ?? Path.Combine(Directory.GetCurrentDirectory(), "test-artifacts", "snapshots"));
        Directory.CreateDirectory(OutputFolder);
    }

    public static Window Show(Control content, double width = 1280, double height = 820)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = content
        };

        window.Show();
        Settle(10);
        return window;
    }

    public static void Settle(int frames = 10)
    {
        for (int i = 0; i < frames; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(25);
        }
    }

    public static bool WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    public static string Save(Window window, string name)
    {
        Settle(5);
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"The window rendered nothing for '{name}'.");

        string fileName = name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? name : name + ".png";
        string path = Path.Combine(OutputFolder, fileName);
        frame.Save(path, PngBitmapEncoderOptions.Default);
        Console.WriteLine($"[UiSnapshots] Saved: {path}");
        return path;
    }

    public static string RenderAndSave(Control control, double width, double height, string name)
    {
        var window = Show(control, width, height);
        try
        {
            return Save(window, name);
        }
        finally
        {
            window.Close();
            Settle(2);
        }
    }
}
