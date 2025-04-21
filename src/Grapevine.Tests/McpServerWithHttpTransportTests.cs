using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Grapevine.Extensions.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;

using Xunit;
using Shouldly;

namespace Grapevine.Tests
{
    public class McpServerWithHttpTransportTests : IDisposable
    {
        private readonly IRestServer _server;
        private readonly HttpClient _client;

        public McpServerWithHttpTransportTests()
        {
            var port = GetFreePort();
            var builder = RestServerBuilder.UseDefaults();
            builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithToolsFromAssembly();
            builder.Services.TryAddSingleton<IHostApplicationLifetime, NullHostApplicationLifetime>();
            _server = builder.Build();
            _server.Prefixes.Add($"http://localhost:{port}/");
            _server.MapMcp("/mcp");
            _server.Start();
            _client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
        }

        public void Dispose()
        {
            _client.Dispose();
            _server.Stop();
        }

        [Fact]
        public async Task GetMcp_ReturnsEventStream()
        {
            var response = await _client.GetAsync("/mcp", HttpCompletionOption.ResponseHeadersRead);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            response.Content.Headers.ContentType.ShouldNotBeNull();
            response.Content.Headers.ContentType.MediaType.ShouldBe("text/event-stream");
        }

        [Fact]
        public async Task GetMcpSse_ReturnsEventStream()
        {
            var response = await _client.GetAsync("/mcp/sse", HttpCompletionOption.ResponseHeadersRead);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            response.Content.Headers.ContentType.ShouldNotBeNull();
            response.Content.Headers.ContentType.MediaType.ShouldBe("text/event-stream");
        }

        [Fact]
        public async Task InvalidRoute_ReturnsNotFound()
        {
            // Any path other than /mcp or /mcp/sse should return 404
            var response = await _client.GetAsync("/invalid", HttpCompletionOption.ResponseHeadersRead);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task EchoTool_CanBeCalledOverHttp()
        {
            // Arrange: create and connect MCP client
            var mcpClient = await ConnectMcpClientAsync();

            // Act: call the Echo tool
            var echoResponse = await mcpClient.CallToolAsync(
                "Echo",
                new Dictionary<string, object> { ["message"] = "from client!" },
                cancellationToken: CancellationToken.None);

            // Assert: verify single text result
            var textContent = Assert.Single(echoResponse.Content, c => c.Type == "text");
            textContent.Text.ShouldBe("hello from client!");
        }

        private int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async Task<IMcpClient> ConnectMcpClientAsync()
        {
            Debug.Assert(_client.BaseAddress != null);
            return await McpClientFactory.CreateAsync(
                new SseClientTransport(
                    new SseClientTransportOptions
                    {
                        Endpoint = new Uri(_client.BaseAddress, "mcp/sse"),
                        Name = "Test Server"
                    },
                    _client,
                    NullLoggerFactory.Instance),
                clientOptions: null,
                loggerFactory: NullLoggerFactory.Instance,
                cancellationToken: CancellationToken.None);
        }
    }
    
    [McpServerToolType]
    public sealed class EchoTool
    {
        [McpServerTool, Description("Echoes the input back to the client.")]
        public static string Echo(string message) => $"hello {message}";
    }

    internal class NullHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}