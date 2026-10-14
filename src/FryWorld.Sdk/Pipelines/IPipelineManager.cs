using System;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Pipelines;

/// <summary>
/// Manages typed execution pipelines across plugins inspired by the Cordis composability framework.
/// </summary>
public interface IPipelineManager
{
    // --- 1. Waterfall Pipeline (Around-middleware with next() delegation) ---
    IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware);
    Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null);

    // --- 2. Bail Pipeline (Short-circuits on first non-null result) ---
    IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class;
    Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class;

    // --- 3. Parallel Pipeline (Concurrent asynchronous dispatch via Task.WhenAll) ---
    IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler);
    Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context);

    // --- 4. Serial Pipeline (Sequentially awaited execution) ---
    IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler);
    Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context);
}
