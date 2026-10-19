namespace FryWorld.Hosting.Mcp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Plugins;
using FryWorld.Services.AI;
using Microsoft.Extensions.AI;

/// <summary>
/// Core Model Context Protocol (MCP) server dispatcher.
/// Handles JSON-RPC 2.0 requests and executes tool calls, resource queries, and prompt lookups.
/// </summary>
public class McpServer
{
    private readonly IAiService _aiService;
    private readonly IFryPluginContext _context;

    public McpServer(IAiService aiService, IFryPluginContext context)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Processes a single JSON-RPC 2.0 MCP request and returns the corresponding response.
    /// </summary>
    public async Task<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            switch (request.Method)
            {
                case "initialize":
                    return JsonRpcResponse.Success(request.Id, new McpInitializeResult());

                case "notifications/initialized":
                    // Notifications do not return a response
                    return null;

                case "ping":
                    return JsonRpcResponse.Success(request.Id, new Dictionary<string, object>());

                case "tools/list":
                    return await HandleListToolsAsync(request, cancellationToken);

                case "tools/call":
                    return await HandleCallToolAsync(request, cancellationToken);

                case "resources/list":
                    return await HandleListResourcesAsync(request, cancellationToken);

                case "resources/read":
                    return await HandleReadResourceAsync(request, cancellationToken);

                case "prompts/list":
                    return HandleListPrompts(request);

                case "prompts/get":
                    return HandleGetPrompt(request);

                default:
                    return JsonRpcResponse.CreateError(request.Id, -32601, $"Method '{request.Method}' not found.");
            }
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.CreateError(request.Id, -32603, $"Internal error: {ex.Message}", ex.ToString());
        }
    }

    private Task<JsonRpcResponse?> HandleListToolsAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var tools = _aiService.Tools.GetRegisteredTools();
        var mcpTools = new List<McpToolInfo>();

        foreach (var t in tools)
        {
            mcpTools.Add(new McpToolInfo
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.Function.JsonSchema
            });
        }

        return Task.FromResult<JsonRpcResponse?>(JsonRpcResponse.Success(request.Id, new { tools = mcpTools }));
    }

    private async Task<JsonRpcResponse?> HandleCallToolAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Params == null || !request.Params.Value.TryGetProperty("name", out var nameProp))
        {
            return JsonRpcResponse.CreateError(request.Id, -32602, "Missing 'name' parameter in tools/call request.");
        }

        var toolName = nameProp.GetString();
        if (string.IsNullOrWhiteSpace(toolName) || !_aiService.Tools.TryGetTool(toolName, out var tool) || tool == null)
        {
            return JsonRpcResponse.CreateError(request.Id, -32601, $"Tool '{toolName}' is not registered.");
        }

        // Parse arguments
        var argsDict = new Dictionary<string, object?>();
        if (request.Params.Value.TryGetProperty("arguments", out var argsProp) && argsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in argsProp.EnumerateObject())
            {
                argsDict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
        }

        try
        {
            var functionArgs = new AIFunctionArguments(argsDict);
            var result = await tool.Function.InvokeAsync(functionArgs, cancellationToken);
            var resultString = result switch
            {
                null => "null",
                string str => str,
                _ => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
            };

            var callResult = new McpCallToolResult
            {
                IsError = false,
                Content = new List<McpContentItem>
                {
                    new() { Type = "text", Text = resultString }
                }
            };

            return JsonRpcResponse.Success(request.Id, callResult);
        }
        catch (Exception ex)
        {
            var callResult = new McpCallToolResult
            {
                IsError = true,
                Content = new List<McpContentItem>
                {
                    new() { Type = "text", Text = $"Error executing tool '{toolName}': {ex.Message}" }
                }
            };

            return JsonRpcResponse.Success(request.Id, callResult);
        }
    }

    private Task<JsonRpcResponse?> HandleListResourcesAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var resources = new List<McpResourceInfo>
        {
            new()
            {
                Uri = "fryworld://context/active",
                Name = "Active Workspace Context Snapshot",
                Description = "Provides the live active document, schematic or code, and state of the focused workspace.",
                MimeType = "application/json"
            },
            new()
            {
                Uri = "fryworld://plugins/catalog",
                Name = "Registered Plugins Catalog",
                Description = "List of all installed, mounted, and registered workspaces and plugins.",
                MimeType = "application/json"
            },
            new()
            {
                Uri = "fryworld://schemas/active",
                Name = "Active Workspace Schema & Guidelines",
                Description = "Domain schema and system prompt guidance for the currently active workspace.",
                MimeType = "text/plain"
            }
        };

        return Task.FromResult<JsonRpcResponse?>(JsonRpcResponse.Success(request.Id, new { resources }));
    }

    private async Task<JsonRpcResponse?> HandleReadResourceAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Params == null || !request.Params.Value.TryGetProperty("uri", out var uriProp))
        {
            return JsonRpcResponse.CreateError(request.Id, -32602, "Missing 'uri' parameter in resources/read request.");
        }

        var uri = uriProp.GetString() ?? string.Empty;
        var contents = new List<McpResourceContent>();

        if (string.Equals(uri, "fryworld://context/active", StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = await _aiService.ContextManager.GetActiveContextAsync(cancellationToken);
            contents.Add(new McpResourceContent
            {
                Uri = uri,
                MimeType = "application/json",
                Text = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true })
            });
        }
        else if (string.Equals(uri, "fryworld://plugins/catalog", StringComparison.OrdinalIgnoreCase))
        {
            var nav = _context.GetRegisteredNavigationItems().Select(n => new
            {
                id = n.Id,
                title = n.Title,
                category = n.Category,
                order = n.Order
            });

            contents.Add(new McpResourceContent
            {
                Uri = uri,
                MimeType = "application/json",
                Text = JsonSerializer.Serialize(nav, new JsonSerializerOptions { WriteIndented = true })
            });
        }
        else if (string.Equals(uri, "fryworld://schemas/active", StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = await _aiService.ContextManager.GetActiveContextAsync(cancellationToken);
            contents.Add(new McpResourceContent
            {
                Uri = uri,
                MimeType = "text/plain",
                Text = snapshot?.SystemPromptAugmentation ?? "No active schema available."
            });
        }
        else
        {
            return JsonRpcResponse.CreateError(request.Id, -32602, $"Resource URI '{uri}' not found.");
        }

        return JsonRpcResponse.Success(request.Id, new McpReadResourceResult { Contents = contents });
    }

    private JsonRpcResponse? HandleListPrompts(JsonRpcRequest request)
    {
        var prompts = _aiService.Prompts.GetRegisteredPrompts();
        var mcpPrompts = prompts.Select(p => new McpPromptInfo
        {
            Name = p.Id,
            Description = p.Description,
            Arguments = p.Arguments?.Select(a => new McpPromptArgument
            {
                Name = a,
                Description = $"Argument {a}",
                Required = true
            }).ToList()
        }).ToList();

        return JsonRpcResponse.Success(request.Id, new { prompts = mcpPrompts });
    }

    private JsonRpcResponse? HandleGetPrompt(JsonRpcRequest request)
    {
        if (request.Params == null || !request.Params.Value.TryGetProperty("name", out var nameProp))
        {
            return JsonRpcResponse.CreateError(request.Id, -32602, "Missing 'name' parameter in prompts/get request.");
        }

        var promptId = nameProp.GetString() ?? string.Empty;
        if (!_aiService.Prompts.TryGetPrompt(promptId, out var prompt) || prompt == null)
        {
            return JsonRpcResponse.CreateError(request.Id, -32601, $"Prompt '{promptId}' not found.");
        }

        var promptText = prompt.Template;
        if (request.Params.Value.TryGetProperty("arguments", out var argsProp) && argsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in argsProp.EnumerateObject())
            {
                promptText = promptText.Replace($"{{{{{prop.Name}}}}}", prop.Value.GetString() ?? string.Empty);
            }
        }

        return JsonRpcResponse.Success(request.Id, new
        {
            description = prompt.Description,
            messages = new[]
            {
                new { role = "user", content = new { type = "text", text = promptText } }
            }
        });
    }
}
