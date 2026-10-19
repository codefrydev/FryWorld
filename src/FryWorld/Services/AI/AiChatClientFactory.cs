namespace FryWorld.Services.AI;

using System;
using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

/// <summary>
/// Factory that instantiates configured <see cref="IChatClient"/> instances from <see cref="AiSettings"/>.
/// </summary>
public static class AiChatClientFactory
{
    /// <summary>
    /// Creates an <see cref="IChatClient"/> configured according to the given settings,
    /// with automatic function invocation enabled.
    /// </summary>
    public static IChatClient CreateChatClient(AiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var endpoint = NormalizeEndpoint(settings.EndpointUrl, settings.Provider);
        var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? "ollama" : settings.ApiKey;
        var model = string.IsNullOrWhiteSpace(settings.ModelName) ? AiSettings.DefaultModelName : settings.ModelName;

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        var chatClient = openAiClient.GetChatClient(model).AsIChatClient();

        return new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .Build();
    }

    /// <summary>
    /// Normalizes endpoint URLs, ensuring appropriate /v1 suffixes for local Ollama and custom endpoints.
    /// </summary>
    public static Uri NormalizeEndpoint(string url, AiProviderKind provider)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            url = provider switch
            {
                AiProviderKind.OpenAI => AiSettings.DefaultOpenAiEndpoint,
                AiProviderKind.LMStudio => AiSettings.DefaultLmStudioEndpoint,
                _ => AiSettings.DefaultOllamaEndpoint
            };
        }

        url = url.TrimEnd('/');

        if (provider == AiProviderKind.Ollama)
        {
            if (!url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                url += "/v1";
            }
        }
        else if (provider is AiProviderKind.OpenAiCompatible or AiProviderKind.LMStudio)
        {
            if (!url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) &&
                !url.Contains("/v1/", StringComparison.OrdinalIgnoreCase))
            {
                url += "/v1";
            }
        }

        return new Uri(url);
    }
}
