using System;
using FryWorld.Sdk.Loading;
using Xunit;

namespace FryWorld.Sdk.Tests;

public class PackageLoaderTests
{
    [Theory]
    [InlineData("../sneaky")]
    [InlineData("..\\sneaky")]
    [InlineData("/etc/passwd")]
    [InlineData("id with spaces")]
    [InlineData("id*with#symbols")]
    public void PluginIdValidator_RejectsPathTraversalAndInvalidChars(string unsafeId)
    {
        Assert.Throws<ArgumentException>(() => PluginIdValidator.ValidateIdentifier(unsafeId));
    }

    [Theory]
    [InlineData("com.fryworld.tool")]
    [InlineData("valid-plugin-id_123")]
    [InlineData("my.tool.v1")]
    public void PluginIdValidator_AcceptsValidIdentifiers(string validId)
    {
        PluginIdValidator.ValidateIdentifier(validId);
    }

    [Fact]
    public async Task FryPluginPackageLoader_LoadsAndMountsCSharpEditorPlugin()
    {
        var packagePath = "/Users/codefrydev/Desktop/SourceCode/FryWorld/FrySharp/bin/Release/net10.0/CSharpEditor.fryplugin";
        if (!System.IO.File.Exists(packagePath))
        {
            return; // Skip if release package has not been built
        }

        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FryWorldTestPlugins_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var result = FryPluginPackageLoader.UnpackAndLoad(packagePath, tempDir);
            Assert.NotNull(result);
            Assert.Equal("com.fryworld.csharpstudio", result.Manifest.Id);
            Assert.Equal("FrySharp", result.Manifest.Name);
            Assert.NotEmpty(result.AssemblyPackage.Plugins);

            var plugin = result.AssemblyPackage.Plugins[0];
            Assert.Equal("com.fryworld.csharpstudio", plugin.Id);
            Assert.Equal("FrySharp", plugin.Name);

            Assert.False(result.AssemblyPackage.IsCollectible);
            Assert.False(result.AssemblyPackage.Context.IsCollectible);

            var host = new FryWorld.Sdk.Plugins.PluginHost();
            host.RegisterPlugin(plugin);
            await host.StartAsync();

            var registeredNavItems = host.Context.GetRegisteredNavigationItems();
            Assert.Contains(registeredNavItems, nav => nav.Id == "com.fryworld.csharpstudio" && nav.Title == "FrySharp");

            var registeredShortcuts = host.Context.GetRegisteredShortcuts();
            Assert.NotEmpty(registeredShortcuts);

            var registeredCommands = host.Context.GetRegisteredCommands();
            Assert.Contains(registeredCommands, cmd => cmd.Id == "cmd.studio.csharpeditor");

            await host.UnmountPluginAsync(plugin.Id);
            Assert.Empty(host.Context.GetRegisteredNavigationItems());
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                try { System.IO.Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task DiscoverAndLoadDirectory_LoadsCSharpEditorPlugin_WithNonCollectibleContextAndExecutesRoslyn()
    {
        var installedDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "FryWorld", "plugins");

        if (!System.IO.Directory.Exists(installedDir)) return;

        var packages = PluginAssemblyLoader.DiscoverAndLoadDirectory(installedDir);
        var csharpPackage = packages.FirstOrDefault(p => p.Plugins.Any(pl => pl.Id == "com.fryworld.csharpstudio"));
        if (csharpPackage == null) return;

        Assert.False(csharpPackage.IsCollectible);
        Assert.False(csharpPackage.Context.IsCollectible);

        var asm = csharpPackage.Context.Assemblies.FirstOrDefault(a => a.GetName().Name == "CSharpEditorPlugin");
        Assert.NotNull(asm);

        var kernelType = csharpPackage.Context.Assemblies
            .Select(a => a.GetType("PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.NotebookExecutionKernel"))
            .FirstOrDefault(t => t != null)
            ?? csharpPackage.Context.LoadFromAssemblyName(new System.Reflection.AssemblyName("FrySharp.Core"))
                .GetType("PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.NotebookExecutionKernel");
        Assert.NotNull(kernelType);

        dynamic kernel = Activator.CreateInstance(kernelType)!;
        var twoSumCode = @"
using System;
using System.Collections.Generic;

public class Solution
{
    public int[] TwoSum(int[] nums, int target)
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < nums.Length; i++)
        {
            int complement = target - nums[i];
            if (map.TryGetValue(complement, out int index))
            {
                return new int[] { index, i };
            }
            map[nums[i]] = i;
        }
        return Array.Empty<int>();
    }
}

new Solution().TwoSum(new int[] { 2, 7, 11, 15 }, 9).Dump();
";

        var task = (Task)kernel.ExecuteCellAsync(
            twoSumCode,
            onLiveConsole: null,
            onRichOutput: null,
            ct: System.Threading.CancellationToken.None
        );
        await task;
        dynamic execResult = ((dynamic)task).Result;
        Assert.True(execResult.Success, (string)execResult.ErrorMessage);

        // Verify FryServer cell execution with ambient globals inside isolated ALC without InvalidCastException
        var serverCompilerType = csharpPackage.Context.Assemblies
            .Select(a => a.GetType("PdfEditorApp.Plugins.CSharpEditor.Services.Server.RoslynServerCompilationService"))
            .FirstOrDefault(t => t != null);
        Assert.NotNull(serverCompilerType);

        dynamic serverCompiler = Activator.CreateInstance(serverCompilerType)!;
        var cellItemType = csharpPackage.Context.Assemblies
            .Select(a => a.GetType("PdfEditorApp.Plugins.CSharpEditor.Models.Server.FryServerCellItem"))
            .FirstOrDefault(t => t != null);
        Assert.NotNull(cellItemType);

        dynamic cell = Activator.CreateInstance(cellItemType)!;
        cell.Id = "test-cell-1";
        cell.Source = "return Ok(new { message = \"pong\" });";

        var contextType = csharpPackage.Context.Assemblies
            .Select(a => a.GetType("PdfEditorApp.Plugins.CSharpEditor.Services.Server.FryServerContext"))
            .FirstOrDefault(t => t != null);
        Assert.NotNull(contextType);

        dynamic serverCtx = Activator.CreateInstance(
            contextType,
            "GET",
            "/ping",
            null,
            null,
            null,
            string.Empty,
            null,
            new Dictionary<string, object>()
        )!;

        var serverTask = (Task)serverCompiler.ExecuteCellAsync(cell, serverCtx, System.Threading.CancellationToken.None);
        await serverTask;
        dynamic serverResult = ((dynamic)serverTask).Result;
        Assert.NotNull(serverResult);
        Assert.Equal(200, (int)serverResult.StatusCode);
    }
}
