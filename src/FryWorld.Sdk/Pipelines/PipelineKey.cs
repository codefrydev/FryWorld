using System;
using System.Threading.Tasks;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Pipelines;

/// <summary>
/// A pipeline's name together with the context type it carries. Declare one per pipeline, in a contract
/// assembly shared by the plugins that use it, and register and execute through it: the compiler then
/// rejects a handler written for the wrong context type, which the string-named overloads cannot catch.
/// </summary>
/// <example>
/// <code>
/// public static class ExportPipelines
/// {
///     public static readonly PipelineKey&lt;ExportContext&gt; BeforeExport = new("export.before");
/// }
///
/// ctx.RegisterSerial(ExportPipelines.BeforeExport, async e =&gt; { ... });
/// await ctx.ExecuteSerialAsync(ExportPipelines.BeforeExport, exportContext);
/// </code>
/// </example>
/// <typeparam name="TContext">The type handlers receive.</typeparam>
public sealed class PipelineKey<TContext>
{
    /// <summary>Creates a key for the pipeline called <paramref name="name"/>.</summary>
    public PipelineKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The pipeline's name, as used by the string-named overloads.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({typeof(TContext).Name})";
}

/// <summary>
/// A bail pipeline's name together with its context and result types. See <see cref="PipelineKey{TContext}"/>.
/// </summary>
/// <typeparam name="TContext">The type handlers receive.</typeparam>
/// <typeparam name="TResult">The type the first handler to handle the context returns.</typeparam>
public sealed class PipelineKey<TContext, TResult> where TResult : class
{
    /// <summary>Creates a key for the bail pipeline called <paramref name="name"/>.</summary>
    public PipelineKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The pipeline's name, as used by the string-named overloads.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({typeof(TContext).Name} → {typeof(TResult).Name})";
}

/// <summary>
/// Typed overloads of the pipeline methods on <see cref="IPipelineManager"/> and <see cref="IFryPluginContext"/>.
/// </summary>
public static class PipelineKeyExtensions
{
    /// <summary>Registers middleware in the waterfall pipeline <paramref name="key"/>.</summary>
    public static IDisposable RegisterWaterfall<TContext>(this IPipelineManager pipelines, PipelineKey<TContext> key, Func<TContext, Func<Task>, Task> middleware) =>
        pipelines.RegisterWaterfall(key.Name, middleware);

    /// <summary>Runs the waterfall pipeline <paramref name="key"/>.</summary>
    public static Task ExecuteWaterfallAsync<TContext>(this IPipelineManager pipelines, PipelineKey<TContext> key, TContext context, Func<Task>? terminal = null) =>
        pipelines.ExecuteWaterfallAsync(key.Name, context, terminal);

    /// <summary>Registers a handler in the bail pipeline <paramref name="key"/>.</summary>
    public static IDisposable RegisterBail<TContext, TResult>(this IPipelineManager pipelines, PipelineKey<TContext, TResult> key, Func<TContext, Task<TResult?>> handler) where TResult : class =>
        pipelines.RegisterBail(key.Name, handler);

    /// <summary>Runs the bail pipeline <paramref name="key"/>, returning the first non-null result.</summary>
    public static Task<TResult?> ExecuteBailAsync<TContext, TResult>(this IPipelineManager pipelines, PipelineKey<TContext, TResult> key, TContext context) where TResult : class =>
        pipelines.ExecuteBailAsync<TContext, TResult>(key.Name, context);

    /// <summary>Registers a handler in the parallel pipeline <paramref name="key"/>.</summary>
    public static IDisposable RegisterParallel<TContext>(this IPipelineManager pipelines, PipelineKey<TContext> key, Func<TContext, Task> handler) =>
        pipelines.RegisterParallel(key.Name, handler);

    /// <summary>Runs the parallel pipeline <paramref name="key"/>.</summary>
    public static Task ExecuteParallelAsync<TContext>(this IPipelineManager pipelines, PipelineKey<TContext> key, TContext context) =>
        pipelines.ExecuteParallelAsync(key.Name, context);

    /// <summary>Registers a handler in the serial pipeline <paramref name="key"/>.</summary>
    public static IDisposable RegisterSerial<TContext>(this IPipelineManager pipelines, PipelineKey<TContext> key, Func<TContext, Task> handler) =>
        pipelines.RegisterSerial(key.Name, handler);

    /// <summary>Runs the serial pipeline <paramref name="key"/>.</summary>
    public static Task ExecuteSerialAsync<TContext>(this IPipelineManager pipelines, PipelineKey<TContext> key, TContext context) =>
        pipelines.ExecuteSerialAsync(key.Name, context);

    // On the plugin context, registrations go through the context so they are tied to the plugin's scope
    // and removed when it unmounts.

    /// <summary>Registers middleware in the waterfall pipeline <paramref name="key"/>, removed when the plugin unmounts.</summary>
    public static IDisposable RegisterWaterfall<TContext>(this IFryPluginContext ctx, PipelineKey<TContext> key, Func<TContext, Func<Task>, Task> middleware) =>
        ctx.RegisterWaterfall(key.Name, middleware);

    /// <summary>Runs the waterfall pipeline <paramref name="key"/>.</summary>
    public static Task ExecuteWaterfallAsync<TContext>(this IFryPluginContext ctx, PipelineKey<TContext> key, TContext context, Func<Task>? terminal = null) =>
        ctx.ExecuteWaterfallAsync(key.Name, context, terminal);

    /// <summary>Registers a handler in the bail pipeline <paramref name="key"/>, removed when the plugin unmounts.</summary>
    public static IDisposable RegisterBail<TContext, TResult>(this IFryPluginContext ctx, PipelineKey<TContext, TResult> key, Func<TContext, Task<TResult?>> handler) where TResult : class =>
        ctx.RegisterBail(key.Name, handler);

    /// <summary>Runs the bail pipeline <paramref name="key"/>, returning the first non-null result.</summary>
    public static Task<TResult?> ExecuteBailAsync<TContext, TResult>(this IFryPluginContext ctx, PipelineKey<TContext, TResult> key, TContext context) where TResult : class =>
        ctx.ExecuteBailAsync<TContext, TResult>(key.Name, context);

    /// <summary>Registers a handler in the parallel pipeline <paramref name="key"/>, removed when the plugin unmounts.</summary>
    public static IDisposable RegisterParallel<TContext>(this IFryPluginContext ctx, PipelineKey<TContext> key, Func<TContext, Task> handler) =>
        ctx.RegisterParallel(key.Name, handler);

    /// <summary>Runs the parallel pipeline <paramref name="key"/>.</summary>
    public static Task ExecuteParallelAsync<TContext>(this IFryPluginContext ctx, PipelineKey<TContext> key, TContext context) =>
        ctx.ExecuteParallelAsync(key.Name, context);

    /// <summary>Registers a handler in the serial pipeline <paramref name="key"/>, removed when the plugin unmounts.</summary>
    public static IDisposable RegisterSerial<TContext>(this IFryPluginContext ctx, PipelineKey<TContext> key, Func<TContext, Task> handler) =>
        ctx.RegisterSerial(key.Name, handler);

    /// <summary>Runs the serial pipeline <paramref name="key"/>.</summary>
    public static Task ExecuteSerialAsync<TContext>(this IFryPluginContext ctx, PipelineKey<TContext> key, TContext context) =>
        ctx.ExecuteSerialAsync(key.Name, context);
}
