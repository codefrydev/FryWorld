using System;
using System.Collections.Generic;

namespace FryWorld.Sdk.Loading;

/// <summary>What a loading indicator shows when it opens.</summary>
public sealed class LoadingProgressOptions
{
    /// <summary>Headline, e.g. "Opening project".</summary>
    public string Title { get; set; } = "Loading...";
    /// <summary>Small caption above the title.</summary>
    public string Category { get; set; } = "INITIALIZATION";
    /// <summary>What is happening right now.</summary>
    public string StatusMessage { get; set; } = string.Empty;
    /// <summary>Steps to show as a checklist, if any.</summary>
    public IReadOnlyList<string>? PipelinePhases { get; set; }
    /// <summary>Which step is running.</summary>
    public int ActivePhaseIndex { get; set; }
    /// <summary>Progress from 0 to 100.</summary>
    public double ProgressPercent { get; set; }
    /// <summary>Whether the user can cancel.</summary>
    public bool IsCancellable { get; set; }
}

/// <summary>An open loading indicator; dispose it to close the indicator.</summary>
public interface ILoadingProgressHandle : IDisposable
{
    /// <summary>Updates the message, and optionally the progress and current step.</summary>
    void UpdateStatus(string message, double? progressPercent = null, int? activePhaseIndex = null);
}

/// <summary>Shows a loading indicator for long operations. Hosts may provide it.</summary>
public interface ILoadingProgressService
{
    /// <summary>Opens a loading indicator.</summary>
    ILoadingProgressHandle Show(LoadingProgressOptions options);
}
