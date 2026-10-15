using System;
using Avalonia;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Runner;

/// <summary>
/// Runs a single plugin as a standalone desktop app, with the same services a FryWorld host provides
/// (<see cref="Services.IShellService"/>, settings, notifications, theme), for developing and demoing a plugin
/// without the full host.
/// </summary>
/// <example>
/// A runner project's whole <c>Program.cs</c>; the project references Avalonia.Desktop and a theme package:
/// <code>
/// [STAThread]
/// public static void Main(string[] args) =>
///     FryRunner.Configure&lt;MyPlugin&gt;(app => app.Styles.Add(new FluentTheme()))
///         .UsePlatformDetect()
///         .WithInterFont()
///         .StartWithClassicDesktopLifetime(args);
/// </code>
/// </example>
public static class FryRunner
{
    /// <summary>
    /// Creates the Avalonia app builder for a runner hosting <typeparamref name="TPlugin"/>. Chain the platform
    /// setup (<c>UsePlatformDetect()</c>) and the lifetime as usual.
    /// </summary>
    /// <param name="configureApp">Adds styles and resources to the app; a theme such as <c>FluentTheme</c> is required for controls to render.</param>
    /// <param name="configure">Runner options: window title and size, settings file, extra services.</param>
    public static AppBuilder Configure<TPlugin>(Action<Application>? configureApp = null, Action<FryRunnerOptions>? configure = null)
        where TPlugin : IFryPlugin, new()
    {
        var options = new FryRunnerOptions();
        configure?.Invoke(options);
        return AppBuilder.Configure(() => new FryRunnerApp(() => new TPlugin(), configureApp, options));
    }
}

/// <summary>
/// Settings for <see cref="FryRunner.Configure{TPlugin}"/>.
/// </summary>
public sealed class FryRunnerOptions
{
    /// <summary>Main window title; defaults to "&lt;plugin name&gt; — FryWorld Runner".</summary>
    public string? Title { get; set; }

    /// <summary>Main window width; defaults to the plugin's first overlay's width for overlay-only plugins, else 1100.</summary>
    public double? Width { get; set; }

    /// <summary>Main window height; defaults to the plugin's first overlay's height for overlay-only plugins, else 720.</summary>
    public double? Height { get; set; }

    /// <summary>
    /// Where the plugin's settings are saved; defaults to <c>&lt;plugin id&gt;.runner.settings.json</c> next to the runner.
    /// </summary>
    public string? SettingsFilePath { get; set; }

    /// <summary>Registers extra services the plugin requires, before it mounts.</summary>
    public Action<IFryPluginContext>? ConfigureServices { get; set; }
}
