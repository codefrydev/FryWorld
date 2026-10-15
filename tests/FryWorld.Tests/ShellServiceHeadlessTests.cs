using System;
using System.Linq;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.Tests.TestSupport;
using FryWorld.ViewModels;
using Xunit;

namespace FryWorld.Tests;

/// <summary>
/// The FryWorld shell's <see cref="IShellService"/>, as plugins reach it through their context, and the Plugins
/// Manager's Problems list.
/// </summary>
[Collection("HeadlessTests")]
public class ShellServiceHeadlessTests
{
    public ShellServiceHeadlessTests()
    {
        TestHeadlessSession.EnsureInitialized();
    }

    [Fact]
    public void ShellService_FromAPluginsContext_OpensAndClosesOverlaysAndNavigates()
    {
        TestHeadlessSession.Run(() =>
        {
            var host = App.Services.GetRequiredService<PluginHost>();
            var mainVm = App.Services.GetRequiredService<MainViewModel>();

            // A plugin gets the shell from its context; the host provides it through the service container.
            var shell = host.Context.GetService<IShellService>();
            Assert.IsType<FryWorld.Services.ShellService>(shell);

            using var overlay = host.Context.RegisterOverlay(new OverlayDescriptor
            {
                Id = "test.shell.overlay",
                Title = "Shell Overlay",
                ViewFactory = () => new TextBlock { Text = "overlay" }
            });
            using var page = host.Context.RegisterNavigationItem(new NavigationItemDescriptor
            {
                Id = "test.shell.page",
                Title = "Shell Page",
                ViewFactory = () => new TextBlock { Text = "page" }
            });
            TestHeadlessSession.Settle(4);

            Assert.True(shell.ShowOverlay("test.shell.overlay"));
            TestHeadlessSession.Settle(2);
            Assert.True(shell.IsOverlayVisible("test.shell.overlay"));
            Assert.Contains(mainVm.ActiveOverlays, o => o.Descriptor.Id == "test.shell.overlay");

            Assert.True(shell.ToggleOverlay("test.shell.overlay"));
            TestHeadlessSession.Settle(2);
            Assert.False(shell.IsOverlayVisible("test.shell.overlay"));

            Assert.False(shell.ShowOverlay("no.such.overlay"));

            Assert.True(shell.NavigateTo("test.shell.page"));
            TestHeadlessSession.Settle(6);
            Assert.Equal("dyn.test.shell.page", mainVm.SelectedNavItem?.Id);

            var launcher = mainVm.NavigationItems.First(n => n.Id == "nav.launcher");
            mainVm.SelectNavigationItem(launcher);
            TestHeadlessSession.Settle(2);
        });
    }

    [Fact]
    public void PluginsManager_ShowsReportedProblems()
    {
        TestHeadlessSession.Run(() =>
        {
            var host = App.Services.GetRequiredService<PluginHost>();
            var manager = App.Services.GetRequiredService<MainViewModel>().PluginsManagerVm;

            host.ReportDiagnostic(new PluginLoadDiagnostic(PluginLoadStage.Mount, PluginDiagnosticSeverity.Error,
                "Test problem from the headless suite.", PluginId: "test.problem.plugin"));
            host.ReportDiagnostic(new PluginLoadDiagnostic(PluginLoadStage.Uninstall, PluginDiagnosticSeverity.Info,
                "Informational, not a problem."));
            TestHeadlessSession.Settle(4);

            Assert.True(manager.HasProblems);
            var problem = Assert.Single(manager.Problems, p => p.Diagnostic.PluginId == "test.problem.plugin");
            Assert.True(problem.IsError);
            Assert.Equal("test.problem.plugin", problem.Title);
            Assert.DoesNotContain(manager.Problems, p => p.Message.StartsWith("Informational"));

            manager.ClearProblemsCommand.Execute(null);
            Assert.False(manager.HasProblems);
        });
    }
}
