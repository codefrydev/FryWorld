namespace FryWorld.Sdk.AI;

/// <summary>
/// Configuration options for creating customized <see cref="Microsoft.Extensions.AI.IChatClient"/> instances.
/// </summary>
public record AiClientOptions
{
    /// <summary>
    /// Gets the provider identifier (e.g. "Ollama", "OpenAI", "LMStudio", "Claude").
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Gets the custom API endpoint URL.
    /// </summary>
    public string? EndpointUrl { get; init; }

    /// <summary>
    /// Gets the model identifier (e.g. "llama3.2", "qwen2.5-coder", "gpt-4o").
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Gets the API key or authentication token, if required.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the sampling temperature (e.g. 0.2 for deterministic code/schematic generation).
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Gets the maximum number of completion tokens to generate.
    /// </summary>
    public int? MaxOutputTokens { get; init; }
}
