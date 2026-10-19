namespace FryWorld.Services.AI;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Supported AI provider backends.
/// </summary>
public enum AiProviderKind
{
    Ollama,
    OpenAI,
    OpenAiCompatible,
    LMStudio,
    Custom
}

/// <summary>
/// Persistent settings configuration for the FryWorld AI and MCP subsystems.
/// </summary>
public class AiSettings
{
    public const string DefaultOllamaEndpoint = "http://localhost:11434/v1";
    public const string DefaultOpenAiEndpoint = "https://api.openai.com/v1";
    public const string DefaultLmStudioEndpoint = "http://localhost:1234/v1";
    public const string DefaultModelName = "llama3.2";

    public AiProviderKind Provider { get; set; } = AiProviderKind.Ollama;

    public string EndpointUrl { get; set; } = DefaultOllamaEndpoint;

    public string ModelName { get; set; } = DefaultModelName;

    public string ApiKey { get; set; } = string.Empty;

    public float Temperature { get; set; } = 0.2f;

    public int MaxOutputTokens { get; set; } = 4096;

    public bool AutoApproveEdits { get; set; } = false;

    public bool McpEnabled { get; set; } = true;

    public int McpPort { get; set; } = 5242;

    public string CustomSystemPrompt { get; set; } =
        "You are the intelligent copilot of FryWorld, a high-performance modular desktop operating environment. " +
        "You assist the user in designing circuits, writing scripts, managing workflows, and using active plugins.";

    public AiSettings Clone()
    {
        return new AiSettings
        {
            Provider = Provider,
            EndpointUrl = EndpointUrl,
            ModelName = ModelName,
            ApiKey = ApiKey,
            Temperature = Temperature,
            MaxOutputTokens = MaxOutputTokens,
            AutoApproveEdits = AutoApproveEdits,
            McpEnabled = McpEnabled,
            McpPort = McpPort,
            CustomSystemPrompt = CustomSystemPrompt
        };
    }
}
