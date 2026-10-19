namespace FryWorld.Services.AI;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Plugins;

/// <summary>
/// Central registry of turnkey AI prompt templates.
/// </summary>
public class AiPromptRegistry : IAiPromptRegistry
{
    private readonly IFryPluginContext _context;
    private readonly List<AiPromptDescriptor> _builtInPrompts = new();

    public event Action? PromptsChanged;

    public AiPromptRegistry(IFryPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        RegisterBuiltInPrompts();
        _context.AiPromptsChanged += () => PromptsChanged?.Invoke();
    }

    private void RegisterBuiltInPrompts()
    {
        _builtInPrompts.Add(new AiPromptDescriptor
        {
            Id = "fryworld.workspace_summary",
            Title = "Summarize Current Workspace",
            Description = "Asks the AI to analyze the active workspace document and report its key structure and metrics.",
            Category = "Workspace",
            Template = "Please analyze the active document in the current workspace, highlight any anomalies or errors, and provide a concise technical summary."
        });

        _builtInPrompts.Add(new AiPromptDescriptor
        {
            Id = "fryworld.scaffold_plugin",
            Title = "Scaffold New FryWorld Plugin",
            Description = "Guides the assistant to design and scaffold a new custom plugin for FryWorld.",
            Category = "Developer Tools",
            Template = "Design a new FryWorld plugin for {{purpose}}. Include UI layout, registered tools, and settings schema."
        });
    }

    public IReadOnlyList<AiPromptDescriptor> GetRegisteredPrompts()
    {
        var pluginPrompts = _context.GetRegisteredAiPrompts();
        var all = new List<AiPromptDescriptor>(_builtInPrompts.Count + pluginPrompts.Count);
        all.AddRange(_builtInPrompts);
        all.AddRange(pluginPrompts);
        return all;
    }

    public bool TryGetPrompt(string promptId, [NotNullWhen(true)] out AiPromptDescriptor? prompt)
    {
        prompt = GetRegisteredPrompts().FirstOrDefault(p => string.Equals(p.Id, promptId, StringComparison.OrdinalIgnoreCase));
        return prompt != null;
    }
}
