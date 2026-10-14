using System;
using System.Threading;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Common;

/// <summary>
/// Asynchronous event handler delegate returning a <see cref="Task"/>.
/// </summary>
public delegate Task AsyncEventHandler<in TEventArgs>(object? sender, TEventArgs e);
