using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SolidFireGateway
{
    public class SolidFireClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly Microsoft.Extensions.Logging.ILogger<SolidFireClient> _logger;

        private readonly string? _username;
        private readonly string? _password;

        /// <summary>
        /// Initializes a new SolidFireClient using a default null logger.
        /// </summary>
        public SolidFireClient(HttpClient httpClient, IConfiguration config)
            : this(httpClient, config, Microsoft.Extensions.Logging.Abstractions.NullLogger<SolidFireClient>.Instance)
        {
        }

        /// <summary>
        /// Initializes a new SolidFireClient with a provided logger.
        /// </summary>
        public SolidFireClient(HttpClient httpClient, IConfiguration config, Microsoft.Extensions.Logging.ILogger<SolidFireClient> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;

            // Try to get credentials for the current cluster from config
            // Assumes config path: SolidFireClusters:{clusterName}:Username/Password
            // Try to extract cluster name from HttpClient name (if available)
            var clusterSection = config.GetSection($"SolidFireClusters");
            foreach (var cluster in clusterSection.GetChildren())
            {
                var endpoint = cluster.GetValue<string>("Endpoint");
                if (!string.IsNullOrEmpty(endpoint) && _httpClient.BaseAddress != null && endpoint == _httpClient.BaseAddress.ToString())
                {
                    _username = cluster.GetValue<string>("Username");
                    _password = cluster.GetValue<string>("Password");
                    break;
                }
            }
        }

        public async Task<TResponse> SendRequestAsync<TResponse>(string method, object parameters)
        {
            var request = new
            {
                method,
                @params = parameters,
                id = 1
            };

            // Add Basic Auth header if credentials are available
            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                var byteArray = System.Text.Encoding.ASCII.GetBytes($"{_username}:{_password}");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            // Use serializer settings to omit null properties (so 'qos' is not sent when null)
            var serializeOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            // Log outgoing JSON-RPC request for diagnostics
            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                try
                {
                    var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
                    var reqLog = Path.Combine(logsDir, "rpc_request.log");
                    var requestJson = JsonSerializer.Serialize(request, serializeOptions);
                    Directory.CreateDirectory(logsDir);
                    File.AppendAllText(reqLog, requestJson + Environment.NewLine);
                }
                catch { /* best-effort logging */ }
            }
            var response = await _httpClient.PostAsJsonAsync("", request, serializeOptions);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            // Persist raw JSON-RPC response to log file for offline inspection
            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                try
                {
                    var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
                    var rpcLog = Path.Combine(logsDir, "rpc.log");
                    Directory.CreateDirectory(logsDir);
                    File.AppendAllText(rpcLog, json + Environment.NewLine);
                }
                catch { /* best-effort logging */ }
            }
            // Log the raw JSON-RPC response for diagnostics
            _logger.LogDebug("[SolidFireClient] Raw JSON-RPC response: {Json}", json);
            // Log the raw JSON-RPC response for debugging
            _logger.LogInformation("SolidFire RPC raw response: {Json}", json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // Handle JSON-RPC error
            if (root.TryGetProperty("error", out var errorElement))
            {
                var errorJson = errorElement.GetRawText();
                _logger.LogError("SolidFire RPC returned error: {Error}", errorJson);
                throw new HttpRequestException($"JSON-RPC error: {errorJson}");
            }
            // Handle JSON-RPC result
            if (root.TryGetProperty("result", out var resultElement))
            {
                return JsonSerializer.Deserialize<TResponse>(resultElement.GetRawText())!;
            }
            // Unexpected payload
            _logger.LogError("Unexpected JSON-RPC payload: {Json}", json);
            throw new HttpRequestException($"Invalid JSON-RPC response: {json}");
        }
    }
}
