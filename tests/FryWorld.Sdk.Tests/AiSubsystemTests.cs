namespace FryWorld.Sdk.Tests;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using Microsoft.Extensions.AI;
using Xunit;

public class AiSubsystemTests
{
    private class DummyPlugin : IFryPlugin
    {
        public string Id => "com.fryworld.test.ai";
        public string Name => "Test AI Plugin";
        public Version Version => new(1, 0, 0);
        public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
        public IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema => null;

        public Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class SampleContextProvider : IAiContextProvider
    {
        public string PluginId => "com.fryworld.test.ai";

        public Task<AiContextSnapshot> GetCurrentContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiContextSnapshot
            {
                PluginId = PluginId,
                Title = "Test Context",
                ActiveDocumentName = "test.fcircuit",
                ActiveContent = "{ \"components\": [] }",
                SystemPromptAugmentation = "Focus on low noise audio."
            });
        }
    }

    [Description("Calculates impedance.")]
    private static double CalculateImpedance([Description("Resistance in ohms.")] double r, [Description("Capacitance in farads.")] double c) => r + c;

    [Fact]
    public void RegisterAiContextProvider_AddsAndRemovesWithDisposal()
    {
        var ctx = new FryPluginContext();
        var provider = new SampleContextProvider();
        bool changedFired = false;
        ctx.AiContextProvidersChanged += () => changedFired = true;

        var sub = ctx.RegisterAiContextProvider(provider);

        Assert.True(changedFired);
        var providers = ctx.GetRegisteredAiContextProviders();
        Assert.Single(providers);
        Assert.Equal("com.fryworld.test.ai", providers[0].PluginId);

        changedFired = false;
        sub.Dispose();

        Assert.True(changedFired);
        Assert.Empty(ctx.GetRegisteredAiContextProviders());
    }

    [Fact]
    public void RegisterAiTool_AddsAndRemovesWithDisposal()
    {
        var ctx = new FryPluginContext();
        var aiFunction = AIFunctionFactory.Create(CalculateImpedance, "calc_impedance", "Calculates impedance.");

        bool changedFired = false;
        ctx.AiToolsChanged += () => changedFired = true;

        var sub = ctx.RegisterAiTool(aiFunction, requiresConfirmation: false, category: "RF Engineering");

        Assert.True(changedFired);
        var tools = ctx.GetRegisteredAiTools();
        Assert.Single(tools);
        Assert.Equal("calc_impedance", tools[0].Name);
        Assert.Equal("RF Engineering", tools[0].Category);
        Assert.False(tools[0].RequiresConfirmation);

        changedFired = false;
        sub.Dispose();

        Assert.True(changedFired);
        Assert.Empty(ctx.GetRegisteredAiTools());
    }

    [Fact]
    public void RegisterAiPrompt_AddsAndRemovesWithDisposal()
    {
        var ctx = new FryPluginContext();
        var prompt = new AiPromptDescriptor
        {
            Id = "circuit.bandpass",
            Title = "Design Active Bandpass",
            Description = "Designs a second-order active bandpass filter.",
            Category = "Filter Design",
            Template = "Design a bandpass filter with center frequency {{f0}} and Q {{q}}.",
            Arguments = new[] { "f0", "q" }
        };

        bool changedFired = false;
        ctx.AiPromptsChanged += () => changedFired = true;

        var sub = ctx.RegisterAiPrompt(prompt);

        Assert.True(changedFired);
        var prompts = ctx.GetRegisteredAiPrompts();
        Assert.Single(prompts);
        Assert.Equal("circuit.bandpass", prompts[0].Id);

        changedFired = false;
        sub.Dispose();

        Assert.True(changedFired);
        Assert.Empty(ctx.GetRegisteredAiPrompts());
    }

    [Fact]
    public void ScopedPluginContext_CleansUpAiRegistrationsOnScopeDisposal()
    {
        var rootCtx = new FryPluginContext();
        var plugin = new DummyPlugin();
        var scope = new PluginScope { Plugin = plugin };
        var scopedCtx = rootCtx.CreateScopedContext(scope);

        var provider = new SampleContextProvider();
        var aiFunction = AIFunctionFactory.Create(CalculateImpedance, "calc_impedance", "Calculates impedance.");
        var prompt = new AiPromptDescriptor
        {
            Id = "test.prompt",
            Title = "Test Prompt",
            Description = "Desc",
            Template = "Template"
        };

        scopedCtx.RegisterAiContextProvider(provider);
        scopedCtx.RegisterAiTool(aiFunction);
        scopedCtx.RegisterAiPrompt(prompt);

        Assert.Single(rootCtx.GetRegisteredAiContextProviders());
        Assert.Single(rootCtx.GetRegisteredAiTools());
        Assert.Single(rootCtx.GetRegisteredAiPrompts());

        // Unmount plugin / dispose scope
        scope.Dispose();

        Assert.Empty(rootCtx.GetRegisteredAiContextProviders());
        Assert.Empty(rootCtx.GetRegisteredAiTools());
        Assert.Empty(rootCtx.GetRegisteredAiPrompts());
    }
}
