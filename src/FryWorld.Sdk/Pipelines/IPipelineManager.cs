using System;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Pipelines;

/// <summary>
/// Manages typed execution pipelines across plugins inspired by the Cordis composability framework.
/// </summary>
public interface IPipelineManager
{
    // --- 1. Waterfall Pipeline (Around-middleware with next() delegation) ---
    /// <summary>Adds around-middleware to a waterfall pipeline; it calls <c>next()</c> to continue the chain.</summary>
    IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware);
    /// <summary>Runs a waterfall pipeline, then <paramref name="terminal"/> at the end of the chain.</summary>
    Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null);

    // --- 2. Bail Pipeline (Short-circuits on first non-null result) ---
    /// <summary>Adds a handler to a bail pipeline; it returns null to let the next handler try.</summary>
    IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class;
    /// <summary>Runs a bail pipeline, returning the first non-null result, or null.</summary>
    Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class;

    // --- 3. Parallel Pipeline (Concurrent asynchronous dispatch via Task.WhenAll) ---
    /// <summary>Adds a handler to a parallel pipeline.</summary>
    IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler);
    /// <summary>Runs every handler of a parallel pipeline at once and waits for all of them.</summary>
    Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context);

    // --- 4. Serial Pipeline (Sequentially awaited execution) ---
    /// <summary>Adds a handler to a serial pipeline.</summary>
    IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler);
    /// <summary>Runs a serial pipeline's handlers one after another, in registration order.</summary>
    Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context);
}
