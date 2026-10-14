using System;
using System.Collections.Generic;

namespace FryWorld.Sdk.Loading;

public sealed class LoadingProgressOptions
{
    public string Title { get; set; } = "Loading...";
    public string Category { get; set; } = "INITIALIZATION";
    public string StatusMessage { get; set; } = string.Empty;
    public IReadOnlyList<string>? PipelinePhases { get; set; }
    public int ActivePhaseIndex { get; set; }
    public double ProgressPercent { get; set; }
    public bool IsCancellable { get; set; }
}

public interface ILoadingProgressHandle : IDisposable
{
    void UpdateStatus(string message, double? progressPercent = null, int? activePhaseIndex = null);
}

public interface ILoadingProgressService
{
    ILoadingProgressHandle Show(LoadingProgressOptions options);
}
