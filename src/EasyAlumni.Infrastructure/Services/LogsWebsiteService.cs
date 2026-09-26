using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyAlumni.Infrastructure.Services
{
    public class LogsWebsiteService : ILogsWebsiteService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LogsWebsiteService> _logger;

        public LogsWebsiteService(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<LogsWebsiteService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> LogAsync(string title, string type, string? controller, string? data)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var settings = await dbContext.SystemSettings
                    .Where(s => s.SettingKey.StartsWith("LogsWebsite_"))
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

                var isEnabled = settings.GetValueOrDefault("LogsWebsite_Enabled", "1") == "1";
                if (!isEnabled)
                {
                    return false;
                }

                var token = settings.GetValueOrDefault("LogsWebsite_ApiToken", "").Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return false;
                }

                var apiUrl = settings.GetValueOrDefault("LogsWebsite_ApiUrl", "https://logs.website/api/ingest").Trim();
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    apiUrl = "https://logs.website/api/ingest";
                }

                // Severity mapping: critical, error, warning, info, debug
                var normalizedType = type?.ToLowerInvariant() switch
                {
                    "critical" => "critical",
                    "error" => "error",
                    "warning" => "warning",
                    "warn" => "warning",
                    "info" => "info",
                    "information" => "info",
                    "debug" => "debug",
                    _ => "error"
                };

                // Truncate fields per logs.website docs:
                // title: max 512 chars
                // controller: max 256 chars
                // request body: max 256 KB
                var safeTitle = string.IsNullOrWhiteSpace(title) ? "Application Error" : title.Trim();
                if (safeTitle.Length > 500)
                {
                    safeTitle = safeTitle.Substring(0, 500) + "...";
                }

                var safeController = string.IsNullOrWhiteSpace(controller) ? "EasyAlumni.Web" : controller.Trim();
                if (safeController.Length > 250)
                {
                    safeController = safeController.Substring(0, 250);
                }

                var safeData = data ?? "";
                if (safeData.Length > 100000) // Keep well within 256 KB limit
                {
                    safeData = safeData.Substring(0, 100000) + "\n...[truncated]";
                }

                var payload = new
                {
                    title = safeTitle,
                    type = normalizedType,
                    controller = safeController,
                    data = safeData
                };

                var json = JsonSerializer.Serialize(payload);
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(8);

                using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch log to logs.website");
                return false;
            }
        }

        public async Task<(bool Success, string Message)> TestLogAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var settings = await dbContext.SystemSettings
                    .Where(s => s.SettingKey.StartsWith("LogsWebsite_"))
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

                var token = settings.GetValueOrDefault("LogsWebsite_ApiToken", "").Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return (false, "API Key Token is not configured. Please enter your logs.website Bearer Token first.");
                }

                var apiUrl = settings.GetValueOrDefault("LogsWebsite_ApiUrl", "https://logs.website/api/ingest").Trim();
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    apiUrl = "https://logs.website/api/ingest";
                }

                var payload = new
                {
                    title = "EasyAlumni Test Log Connection",
                    type = "info",
                    controller = "SettingsController",
                    data = $"Connection test verification from EasyAlumni application at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC."
                };

                var json = JsonSerializer.Serialize(payload);
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return (true, $"Log line successfully sent to logs.website! (Status: {(int)response.StatusCode})");
                }
                else
                {
                    return (false, $"logs.website returned status {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Network or connection error: {ex.Message}");
            }
        }
    }
}
