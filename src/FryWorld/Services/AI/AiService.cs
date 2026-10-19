namespace FryWorld.Services.AI;

using System;
using System.Threading;
using FryWorld.Sdk.AI;
using Microsoft.Extensions.AI;

/// <summary>
/// Host implementation of the universal <see cref="IAiService"/>.
/// </summary>
public class AiService : IAiService
{
    private readonly AiSettings _settings;
    private readonly AiContextManager _contextManager;
    private readonly AiToolRegistry _toolRegistry;
    private readonly AiPromptRegistry _promptRegistry;
    private IChatClient? _cachedClient;
    private readonly object _clientLock = new();

    public IChatClient ChatClient
    {
        get
        {
            lock (_clientLock)
            {
                _cachedClient ??= AiChatClientFactory.CreateChatClient(_settings);
                return _cachedClient;
            }
        }
    }

    public IAiContextManager ContextManager => _contextManager;

    public IAiToolRegistry Tools => _toolRegistry;

    public IAiPromptRegistry Prompts => _promptRegistry;

    public AiSettings Settings => _settings;

    public AiService(
        AiSettings settings,
        AiContextManager contextManager,
        AiToolRegistry toolRegistry,
        AiPromptRegistry promptRegistry)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _promptRegistry = promptRegistry ?? throw new ArgumentNullException(nameof(promptRegistry));
    }

    /// <summary>
    /// Resets cached client instances when settings (provider, model, endpoint) are updated.
    /// </summary>
    public void InvalidateClient()
    {
        lock (_clientLock)
        {
            (_cachedClient as IDisposable)?.Dispose();
            _cachedClient = null;
        }
    }

    public IChatClient CreateChatClient(AiClientOptions? options = null)
    {
        if (options == null) return ChatClient;

        var mergedSettings = _settings.Clone();
        if (!string.IsNullOrWhiteSpace(options.Provider) && Enum.TryParse<AiProviderKind>(options.Provider, true, out var p))
        {
            mergedSettings.Provider = p;
        }
        if (!string.IsNullOrWhiteSpace(options.EndpointUrl)) mergedSettings.EndpointUrl = options.EndpointUrl;
        if (!string.IsNullOrWhiteSpace(options.ModelName)) mergedSettings.ModelName = options.ModelName;
        if (!string.IsNullOrWhiteSpace(options.ApiKey)) mergedSettings.ApiKey = options.ApiKey;
        if (options.Temperature.HasValue) mergedSettings.Temperature = options.Temperature.Value;
        if (options.MaxOutputTokens.HasValue) mergedSettings.MaxOutputTokens = options.MaxOutputTokens.Value;

        return AiChatClientFactory.CreateChatClient(mergedSettings);
    }
}
