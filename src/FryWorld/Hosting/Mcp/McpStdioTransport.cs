namespace FryWorld.Hosting.Mcp;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Stdio transport for Model Context Protocol (MCP) clients such as Claude Code.
/// </summary>
public class McpStdioTransport
{
    private readonly McpServer _server;

    public McpStdioTransport(McpServer server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <summary>
    /// Runs the stdio loop, reading JSON-RPC lines from standard input and writing responses to standard output.
    /// </summary>
    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken);
            if (line == null) break;

            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
                if (request != null)
                {
                    var response = await _server.HandleRequestAsync(request, cancellationToken);
                    if (response != null)
                    {
                        var json = JsonSerializer.Serialize(response);
                        await output.WriteLineAsync(json.AsMemory(), cancellationToken);
                        await output.FlushAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                var errorResponse = JsonRpcResponse.CreateError(null, -32700, $"Parse error: {ex.Message}");
                var json = JsonSerializer.Serialize(errorResponse);
                await output.WriteLineAsync(json.AsMemory(), cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
        }
    }
}
