namespace FryWorld.Tests;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using FryWorld.Hosting.Mcp;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Plugins;
using FryWorld.Services.AI;
using Microsoft.Extensions.AI;
using Xunit;

public class McpServerTests
{
    private class MockCircuitContextProvider : IAiContextProvider
    {
        public string PluginId => "com.fryworld.circuit";

        public Task<AiContextSnapshot> GetCurrentContextAsync(System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult(new AiContextSnapshot
            {
                PluginId = PluginId,
                Title = "RC Low-Pass Filter",
                ActiveDocumentName = "Filter.fcircuit",
                ActiveContent = "{\"components\":[{\"id\":\"R1\",\"type\":\"resistor\"}]}",
                SystemPromptAugmentation = "Focus on passive filter design."
            });
        }
    }

    [Description("Adds two numbers.")]
    private static int AddNumbers([Description("First number")] int a, [Description("Second number")] int b) => a + b;

    private (McpServer server, FryPluginContext ctx, AiService ai) CreateTestServer(string? activePluginId = "com.fryworld.circuit")
    {
        var ctx = new FryPluginContext();
        var settings = new AiSettings();
        var contextManager = new AiContextManager(ctx, () => activePluginId);
        var toolRegistry = new AiToolRegistry(ctx, () => activePluginId);
        var promptRegistry = new AiPromptRegistry(ctx);
        var aiService = new AiService(settings, contextManager, toolRegistry, promptRegistry);

        ctx.RegisterService<IAiService>(aiService);
        var server = new McpServer(aiService, ctx);
        return (server, ctx, aiService);
    }

    [Fact]
    public async Task HandleRequest_Initialize_ReturnsMcpCapabilities()
    {
        var (server, _, _) = CreateTestServer();
        var req = new JsonRpcRequest
        {
            Id = 1,
            Method = "initialize"
        };

        var res = await server.HandleRequestAsync(req);

        Assert.NotNull(res);
        Assert.Equal(1, Convert.ToInt32(res.Id));
        Assert.Null(res.Error);
        Assert.NotNull(res.Result);

        var initResult = Assert.IsType<McpInitializeResult>(res.Result);
        Assert.Equal("2024-11-05", initResult.ProtocolVersion);
        Assert.Equal("FryWorld MCP Server", initResult.ServerInfo.Name);
        Assert.True(initResult.Capabilities.Tools.ListChanged);
        Assert.True(initResult.Capabilities.Resources.ListChanged);
        Assert.True(initResult.Capabilities.Prompts.ListChanged);
    }

    [Fact]
    public async Task HandleRequest_ToolsList_ReturnsBuiltInAndPluginTools()
    {
        var (server, ctx, _) = CreateTestServer();

        // Register custom plugin tool
        var customFunc = AIFunctionFactory.Create(AddNumbers, "math_add", "Adds two numbers.");
        ctx.RegisterAiTool(customFunc, false, "Math");

        var req = new JsonRpcRequest
        {
            Id = 2,
            Method = "tools/list"
        };

        var res = await server.HandleRequestAsync(req);

        Assert.NotNull(res);
        Assert.Null(res.Error);

        var json = JsonSerializer.Serialize(res.Result);
        Assert.Contains("fryworld_list_plugins", json);
        Assert.Contains("fryworld_navigate", json);
        Assert.Contains("fryworld_scaffold_plugin", json);
        Assert.Contains("math_add", json);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_InvokesFunctionAndReturnsResult()
    {
        var (server, ctx, _) = CreateTestServer();

        var customFunc = AIFunctionFactory.Create(AddNumbers, "math_add", "Adds two numbers.");
        ctx.RegisterAiTool(customFunc);

        var argsJson = JsonSerializer.SerializeToElement(new
        {
            name = "math_add",
            arguments = new { a = 17, b = 25 }
        });

        var req = new JsonRpcRequest
        {
            Id = 3,
            Method = "tools/call",
            Params = argsJson
        };

        var res = await server.HandleRequestAsync(req);

        Assert.NotNull(res);
        Assert.Null(res.Error);

        var callResult = Assert.IsType<McpCallToolResult>(res.Result);
        Assert.False(callResult.IsError);
        Assert.Single(callResult.Content);
        Assert.Equal("42", callResult.Content[0].Text);
    }

    [Fact]
    public async Task HandleRequest_ResourcesRead_ActiveContext_ReturnsLiveSnapshot()
    {
        var (server, ctx, _) = CreateTestServer("com.fryworld.circuit");
        ctx.RegisterAiContextProvider(new MockCircuitContextProvider());

        var paramsJson = JsonSerializer.SerializeToElement(new
        {
            uri = "fryworld://context/active"
        });

        var req = new JsonRpcRequest
        {
            Id = 4,
            Method = "resources/read",
            Params = paramsJson
        };

        var res = await server.HandleRequestAsync(req);

        Assert.NotNull(res);
        Assert.Null(res.Error);

        var readResult = Assert.IsType<McpReadResourceResult>(res.Result);
        Assert.Single(readResult.Contents);
        Assert.Contains("RC Low-Pass Filter", readResult.Contents[0].Text);
        Assert.Contains("Filter.fcircuit", readResult.Contents[0].Text);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_CircuitLoadAndSimulate_ReturnsSuccess()
    {
        var (server, ctx, _) = CreateTestServer("com.fryworld.circuit");

        // Register mock circuit tools
        string loadedSchematic = "";
        string LoadSchematic(string fcircuitJson)
        {
            loadedSchematic = fcircuitJson;
            return "Loaded successfully";
        }

        string StepSim(int steps = 10) => $"Simulated {steps} steps.";

        ctx.RegisterAiTool(AIFunctionFactory.Create(LoadSchematic, "circuit_load_schematic", "Loads circuit"));
        ctx.RegisterAiTool(AIFunctionFactory.Create(StepSim, "circuit_step_simulation", "Steps simulation"));

        // Call circuit_load_schematic
        var loadReq = new JsonRpcRequest
        {
            Id = 5,
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new
            {
                name = "circuit_load_schematic",
                arguments = new { fcircuitJson = "{\"name\":\"Test 555 Timer\"}" }
            })
        };

        var loadRes = await server.HandleRequestAsync(loadReq);
        Assert.NotNull(loadRes);
        Assert.Null(loadRes.Error);
        var loadResult = Assert.IsType<McpCallToolResult>(loadRes.Result);
        Assert.Contains("Loaded successfully", loadResult.Content[0].Text);
        Assert.Equal("{\"name\":\"Test 555 Timer\"}", loadedSchematic);

        // Call circuit_step_simulation
        var stepReq = new JsonRpcRequest
        {
            Id = 6,
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new
            {
                name = "circuit_step_simulation",
                arguments = new { steps = 50 }
            })
        };

        var stepRes = await server.HandleRequestAsync(stepReq);
        Assert.NotNull(stepRes);
        Assert.Null(stepRes.Error);
        var stepResult = Assert.IsType<McpCallToolResult>(stepRes.Result);
        Assert.Contains("Simulated 50 steps.", stepResult.Content[0].Text);
    }
}
