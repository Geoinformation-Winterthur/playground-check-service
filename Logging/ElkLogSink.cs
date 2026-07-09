// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Serilog.Core;
using Serilog.Events;

namespace playground_check_service.Logging
{
    /// <summary>
    /// Serilog sink that sends log events as JSON to the GDIW ELK HTTP endpoint.
    /// The payload intentionally follows the shape used by existing GDIW Python jobs.
    /// </summary>
    public sealed class ElkLogSink : ILogEventSink, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _elkUri;
        private readonly string _environment;
        private readonly string _directory;
        private readonly string _service;
        private readonly string _hostname;
        private readonly BlockingCollection<ElkLogPayload> _queue = new BlockingCollection<ElkLogPayload>(1024);
        private readonly Task _workerTask;

        public ElkLogSink(string elkUrl, bool verifySsl, string environment,
                    string directory, string service, string hostname)
        {
            _elkUri = new Uri(elkUrl);
            _environment = environment;
            _directory = directory;
            _service = service;
            _hostname = hostname;

            HttpClientHandler handler = new HttpClientHandler();
            if (!verifySsl)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            _workerTask = Task.Run(ProcessQueueAsync);
        }

        public void Emit(LogEvent logEvent)
        {
            List<string> details = new List<string>();

            if (logEvent.Exception != null)
            {
                details.Add(logEvent.Exception.ToString());
            }

            foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
            {
                details.Add($"{property.Key}={property.Value}");
            }

            ElkLogPayload payload = new ElkLogPayload
            {
                Timestamp = logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Host = _hostname,
                Environment = _environment,
                Level = MapLevel(logEvent.Level),
                Message = logEvent.RenderMessage(),
                Directory = _directory,
                Service = _service,
                Details = details.Count > 0 ? details : null
            };

            // Logging must never block or break the application request flow.
            _queue.TryAdd(payload);
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            try
            {
                _workerTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore logging shutdown errors deliberately.
            }

            _queue.Dispose();
            _httpClient.Dispose();
        }

        private async Task ProcessQueueAsync()
        {
            foreach (ElkLogPayload payload in _queue.GetConsumingEnumerable())
            {
                try
                {
                    using HttpResponseMessage response = await _httpClient
                        .PostAsJsonAsync(_elkUri, payload)
                        .ConfigureAwait(false);

                    // Do not throw into the application if ELK is temporarily unavailable.
                    _ = response.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warnung: Log-Sendung an ELK fehlgeschlagen: {ex.Message}");
                }
            }
        }

        private static string MapLevel(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => "VERBOSE",
                LogEventLevel.Debug => "DEBUG",
                LogEventLevel.Information => "INFO",
                LogEventLevel.Warning => "WARNING",
                LogEventLevel.Error => "ERROR",
                LogEventLevel.Fatal => "CRITICAL",
                _ => level.ToString().ToUpperInvariant()
            };
        }

        private sealed class ElkLogPayload
        {
            [JsonPropertyName("@timestamp")]
            public string Timestamp { get; set; } = string.Empty;

            [JsonPropertyName("host")]
            public string Host { get; set; } = string.Empty;

            [JsonPropertyName("environment")]
            public string Environment { get; set; } = string.Empty;

            [JsonPropertyName("level")]
            public string Level { get; set; } = string.Empty;

            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;

            [JsonPropertyName("directory")]
            public string Directory { get; set; } = string.Empty;

            [JsonPropertyName("service")]
            public string Service { get; set; } = string.Empty;

            [JsonPropertyName("details")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<string>? Details { get; set; }
        }
    }
}
