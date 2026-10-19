namespace FryWorld.Hosting.Mcp;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Services;

/// <summary>
/// HTTP and Server-Sent Events (SSE) transport for Model Context Protocol (MCP) clients.
/// </summary>
public class McpHttpSseTransport : IDisposable
{
    private readonly McpServer _server;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private int _actualPort;
    private readonly ConcurrentDictionary<string, StreamWriter> _sseClients = new();

    public int Port => _actualPort;
    public bool IsRunning => _listener?.IsListening ?? false;

    public McpHttpSseTransport(McpServer server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <summary>
    /// Starts the HTTP listener, attempting the preferred port and auto-incrementing if occupied.
    /// </summary>
    public bool Start(int preferredPort = 5242, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            int testPort = preferredPort + i;
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{testPort}/");
                try { listener.Prefixes.Add($"http://localhost:{testPort}/"); } catch { }
                listener.Start();

                _listener = listener;
                _actualPort = testPort;
                _cts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));

                WriteDiscoveryManifest(_actualPort);
                AppLogService.Instance.LogInfo("McpTransport", $"MCP Server listening on http://127.0.0.1:{_actualPort}/sse");
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Instance.LogWarning("McpTransport", $"Port {testPort} unavailable ({ex.Message}), trying next port...");
            }
        }

        AppLogService.Instance.LogError("McpTransport", "Failed to bind MCP HTTP/SSE transport to any port.");
        return false;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessHttpRequestAsync(context, ct), ct);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                AppLogService.Instance.LogError("McpTransport", "Error accepting HTTP request", ex);
            }
        }
    }

    private async Task ProcessHttpRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var req = context.Request;
        var res = context.Response;

        // Apply CORS
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

        if (string.Equals(req.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            res.StatusCode = 204;
            res.Close();
            return;
        }

        var path = req.Url?.AbsolutePath ?? "/";

        if (path.StartsWith("/sse", StringComparison.OrdinalIgnoreCase))
        {
            await HandleSseConnectionAsync(context, ct);
        }
        else if (path.StartsWith("/message", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await HandleJsonRpcPostAsync(context, ct);
        }
        else
        {
            res.StatusCode = 404;
            using var writer = new StreamWriter(res.OutputStream);
            await writer.WriteAsync("Not Found. Available endpoints: /sse, /message");
            res.Close();
        }
    }

    private async Task HandleSseConnectionAsync(HttpListenerContext context, CancellationToken ct)
    {
        var res = context.Response;
        res.ContentType = "text/event-stream";
        res.Headers.Add("Cache-Control", "no-cache");
        res.Headers.Add("Connection", "keep-alive");

        var sessionId = Guid.NewGuid().ToString("N");
        var writer = new StreamWriter(res.OutputStream, new UTF8Encoding(false)) { AutoFlush = true };
        _sseClients[sessionId] = writer;

        try
        {
            // Initial endpoint event
            await writer.WriteLineAsync($"event: endpoint");
            await writer.WriteLineAsync($"data: /message?sessionId={sessionId}");
            await writer.WriteLineAsync();

            // Keep connection alive with periodic heartbeats
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(15000, ct);
                await writer.WriteLineAsync(": ping");
                await writer.WriteLineAsync();
            }
        }
        catch
        {
            // Client disconnected
        }
        finally
        {
            _sseClients.TryRemove(sessionId, out _);
            try { res.Close(); } catch { }
        }
    }

    private async Task HandleJsonRpcPostAsync(HttpListenerContext context, CancellationToken ct)
    {
        var req = context.Request;
        var res = context.Response;

        if (!string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            res.StatusCode = 405;
            res.Close();
            return;
        }

        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var body = await reader.ReadToEndAsync(ct);

        JsonRpcRequest? rpcRequest = null;
        try
        {
            rpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body);
        }
        catch (Exception ex)
        {
            res.StatusCode = 400;
            var errorResponse = JsonRpcResponse.CreateError(null, -32700, $"Parse error: {ex.Message}");
            res.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(res.OutputStream, errorResponse, cancellationToken: ct);
            res.Close();
            return;
        }

        if (rpcRequest == null)
        {
            res.StatusCode = 400;
            res.Close();
            return;
        }

        var rpcResponse = await _server.HandleRequestAsync(rpcRequest, ct);

        // Check for session ID to send via SSE if connected
        var sessionId = req.QueryString["sessionId"];
        if (!string.IsNullOrWhiteSpace(sessionId) && _sseClients.TryGetValue(sessionId, out var sseWriter) && rpcResponse != null)
        {
            var json = JsonSerializer.Serialize(rpcResponse);
            await sseWriter.WriteLineAsync("event: message");
            await sseWriter.WriteLineAsync($"data: {json}");
            await sseWriter.WriteLineAsync();
            res.StatusCode = 202; // Accepted
            res.Close();
            return;
        }

        // Otherwise reply directly via HTTP POST response
        res.StatusCode = 200;
        res.ContentType = "application/json";
        if (rpcResponse != null)
        {
            await JsonSerializer.SerializeAsync(res.OutputStream, rpcResponse, cancellationToken: ct);
        }
        res.Close();
    }

    private void WriteDiscoveryManifest(int port)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fryworld");
            Directory.CreateDirectory(dir);
            var manifestPath = Path.Combine(dir, "mcp.json");

            var manifest = new
            {
                port,
                url = $"http://127.0.0.1:{port}/sse",
                postUrl = $"http://127.0.0.1:{port}/message",
                status = "running",
                processId = Environment.ProcessId,
                timestamp = DateTime.UtcNow
            };

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLogService.Instance.LogWarning("McpTransport", $"Failed to write MCP discovery file: {ex.Message}");
        }
    }

    private void RemoveDiscoveryManifest()
    {
        try
        {
            var manifestPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fryworld", "mcp.json");
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }

        RemoveDiscoveryManifest();
    }
}
