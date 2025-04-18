using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Grapevine.Extensions.Mcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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
            // Register a no-op host lifetime so StreamableHttpHandler can resolve IHostApplicationLifetime during tests
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
            response.Content.Headers.ContentType.MediaType.ShouldBe("text/event-stream");
        }

        [Fact]
        public async Task GetMcpSse_ReturnsEventStream()
        {
            var response = await _client.GetAsync("/mcp/sse", HttpCompletionOption.ResponseHeadersRead);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            response.Content.Headers.ContentType.MediaType.ShouldBe("text/event-stream");
        }

        [Fact]
        public async Task InvalidRoute_ReturnsNotFound()
        {
            // Any path other than /mcp or /mcp/sse should return 404
            var response = await _client.GetAsync("/invalid", HttpCompletionOption.ResponseHeadersRead);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    internal class NullHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}