using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;

namespace FryWorld.Tests.TestSupport;

[CollectionDefinition("HeadlessTests", DisableParallelization = true)]
public class HeadlessTestCollectionDefinition
{
}

public static class TestHeadlessSession
{
    private static readonly object _sync = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_sync)
        {
            if (_initialized) return;

            AppBuilder.Configure<FryWorld.App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .WithInterFont()
                .SetupWithoutStarting();

            _initialized = true;
        }
    }

    public static void Settle(int frames = 8)
    {
        for (int i = 0; i < frames; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(15);
        }
    }

    public static void Run(Action action)
    {
        EnsureInitialized();
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            Settle(4);
        }
        else
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                action();
                Settle(4);
            });
        }
    }

    public static T Run<T>(Func<T> func)
    {
        EnsureInitialized();
        if (Dispatcher.UIThread.CheckAccess())
        {
            var res = func();
            Settle(4);
            return res;
        }
        else
        {
            return Dispatcher.UIThread.Invoke(() =>
            {
                var res = func();
                Settle(4);
                return res;
            });
        }
    }

    public static Window ShowOffscreen(Control content, double width = 1280, double height = 820)
    {
        return Run(() =>
        {
            var window = new Window
            {
                Width = width,
                Height = height,
                Content = content
            };
            window.Show();
            Settle(6);
            return window;
        });
    }

    public static WriteableBitmap Capture(Window window)
    {
        return Run(() =>
        {
            Settle(4);
            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("Headless window failed to capture rendered frame.");
            return frame;
        });
    }

    public static void Close(Window window)
    {
        Run(() =>
        {
            window.Close();
            Settle(2);
        });
    }
}
