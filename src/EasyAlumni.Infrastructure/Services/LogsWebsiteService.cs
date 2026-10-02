using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EasyAlumni.Core.Entities;
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
        private static readonly object _fileLock = new object();

        public LogsWebsiteService(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<LogsWebsiteService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<AppErrorLog> RecordErrorAsync(
            string title,
            string type,
            string? controller,
            string? message,
            string? stackTrace = null,
            string? requestPath = null,
            string? requestMethod = null,
            string? userIdentifier = null,
            string? clientIp = null)
        {
            var normalizedType = (type?.ToLowerInvariant()) switch
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

            var safeTitle = string.IsNullOrWhiteSpace(title) ? "Application Error" : title.Trim();
            if (safeTitle.Length > 500)
            {
                safeTitle = safeTitle.Substring(0, 497) + "...";
            }

            var safeController = string.IsNullOrWhiteSpace(controller) ? "EasyAlumni.Web" : controller.Trim();
            if (safeController.Length > 250)
            {
                safeController = safeController.Substring(0, 250);
            }

            var errorLog = new AppErrorLog
            {
                Title = safeTitle,
                Type = normalizedType,
                Controller = safeController,
                Message = message ?? string.Empty,
                StackTrace = stackTrace,
                RequestPath = requestPath != null && requestPath.Length > 500 ? requestPath.Substring(0, 500) : requestPath,
                RequestMethod = requestMethod != null && requestMethod.Length > 20 ? requestMethod.Substring(0, 20) : requestMethod,
                UserIdentifier = userIdentifier != null && userIdentifier.Length > 256 ? userIdentifier.Substring(0, 256) : userIdentifier,
                ClientIp = clientIp != null && clientIp.Length > 100 ? clientIp.Substring(0, 100) : clientIp,
                CreatedAt = DateTime.UtcNow,
                IsSyncedToRemote = false
            };

            // 1. Always persist to local database first
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                dbContext.AppErrorLogs.Add(errorLog);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Fallback to local file logging if DB is unreachable
                _logger.LogError(ex, "Failed to persist error log to database. Writing to fallback file.");
                WriteToFallbackFile(errorLog, ex);
            }

            // 2. Dispatch to remote logs.website
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var settings = await dbContext.SystemSettings
                    .Where(s => s.SettingKey.StartsWith("LogsWebsite_"))
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

                var isEnabled = settings.GetValueOrDefault("LogsWebsite_Enabled", "1") == "1";
                var token = settings.GetValueOrDefault("LogsWebsite_ApiToken", "").Trim();
                var apiUrl = settings.GetValueOrDefault("LogsWebsite_ApiUrl", "https://logs.website/api/ingest").Trim();
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    apiUrl = "https://logs.website/api/ingest";
                }

                if (!isEnabled)
                {
                    await UpdateSyncStatusAsync(errorLog.Id, false, "Remote logging disabled in settings");
                    return errorLog;
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    await UpdateSyncStatusAsync(errorLog.Id, false, "Missing logs.website Bearer Token");
                    return errorLog;
                }

                // Send payload
                var (success, syncError) = await SendToRemoteAsync(apiUrl, token, errorLog);
                await UpdateSyncStatusAsync(errorLog.Id, success, syncError);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Remote log dispatch failed for ErrorLog #{Id}", errorLog.Id);
                await UpdateSyncStatusAsync(errorLog.Id, false, ex.Message);
            }

            return errorLog;
        }

        public async Task<bool> LogAsync(string title, string type, string? controller, string? data)
        {
            var log = await RecordErrorAsync(
                title: title,
                type: type,
                controller: controller,
                message: data ?? title,
                stackTrace: data != null && data.Contains("Exception") ? data : null);

            return log.Id > 0;
        }

        public async Task<(bool Success, string Message)> RetrySyncLogAsync(int errorLogId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var log = await dbContext.AppErrorLogs.FindAsync(errorLogId);
                if (log == null)
                {
                    return (false, "Error log entry not found.");
                }

                var settings = await dbContext.SystemSettings
                    .Where(s => s.SettingKey.StartsWith("LogsWebsite_"))
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

                var token = settings.GetValueOrDefault("LogsWebsite_ApiToken", "").Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return (false, "logs.website Bearer Token is not configured in settings.");
                }

                var apiUrl = settings.GetValueOrDefault("LogsWebsite_ApiUrl", "https://logs.website/api/ingest").Trim();
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    apiUrl = "https://logs.website/api/ingest";
                }

                var (success, errorMsg) = await SendToRemoteAsync(apiUrl, token, log);
                if (success)
                {
                    log.IsSyncedToRemote = true;
                    log.RemoteSyncedAt = DateTime.UtcNow;
                    log.RemoteSyncError = null;
                    await dbContext.SaveChangesAsync();
                    return (true, "Successfully synced log to logs.website!");
                }
                else
                {
                    log.IsSyncedToRemote = false;
                    log.RemoteSyncError = errorMsg;
                    await dbContext.SaveChangesAsync();
                    return (false, $"Failed to sync: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error while retrying sync: {ex.Message}");
            }
        }

        public async Task<(int SuccessCount, int FailCount, string Message)> SyncAllPendingLogsAsync()
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
                    return (0, 0, "logs.website API Token is not configured.");
                }

                var apiUrl = settings.GetValueOrDefault("LogsWebsite_ApiUrl", "https://logs.website/api/ingest").Trim();
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    apiUrl = "https://logs.website/api/ingest";
                }

                var pendingLogs = await dbContext.AppErrorLogs
                    .Where(l => !l.IsSyncedToRemote)
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                if (!pendingLogs.Any())
                {
                    return (0, 0, "No pending unsynced logs found.");
                }

                int successCount = 0;
                int failCount = 0;

                foreach (var log in pendingLogs)
                {
                    var (success, errorMsg) = await SendToRemoteAsync(apiUrl, token, log);
                    if (success)
                    {
                        log.IsSyncedToRemote = true;
                        log.RemoteSyncedAt = DateTime.UtcNow;
                        log.RemoteSyncError = null;
                        successCount++;
                    }
                    else
                    {
                        log.RemoteSyncError = errorMsg;
                        failCount++;
                    }
                }

                await dbContext.SaveChangesAsync();
                return (successCount, failCount, $"Sync completed: {successCount} synced successfully, {failCount} failed.");
            }
            catch (Exception ex)
            {
                return (0, 0, $"Sync process failed: {ex.Message}");
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

                // Record test log locally first
                var testLog = await RecordErrorAsync(
                    title: "EasyAlumni Test Log Verification",
                    type: "info",
                    controller: "SettingsController",
                    message: $"Connection test verification from EasyAlumni application at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.",
                    requestPath: "/Settings/TestLogsWebsite");

                if (testLog.IsSyncedToRemote)
                {
                    return (true, "Test log successfully recorded locally AND synced to logs.website!");
                }
                else
                {
                    return (true, $"Test log was recorded in local database! Remote sync status: {testLog.RemoteSyncError ?? "Pending"}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Network or connection error: {ex.Message}");
            }
        }

        private async Task<(bool Success, string? Error)> SendToRemoteAsync(string apiUrl, string token, AppErrorLog log)
        {
            try
            {
                var combinedData = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(log.RequestPath))
                {
                    combinedData.AppendLine($"Path: {log.RequestMethod} {log.RequestPath}");
                }
                if (!string.IsNullOrWhiteSpace(log.UserIdentifier))
                {
                    combinedData.AppendLine($"User: {log.UserIdentifier}");
                }
                if (!string.IsNullOrWhiteSpace(log.ClientIp))
                {
                    combinedData.AppendLine($"Client IP: {log.ClientIp}");
                }
                if (!string.IsNullOrWhiteSpace(log.Message))
                {
                    combinedData.AppendLine($"Message: {log.Message}");
                }
                if (!string.IsNullOrWhiteSpace(log.StackTrace))
                {
                    combinedData.AppendLine();
                    combinedData.AppendLine("Stack Trace:");
                    combinedData.AppendLine(log.StackTrace);
                }

                var safeData = combinedData.ToString();
                if (safeData.Length > 100000)
                {
                    safeData = safeData.Substring(0, 100000) + "\n...[truncated]";
                }

                var payload = new
                {
                    title = log.Title,
                    type = log.Type,
                    controller = log.Controller,
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
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var body = await response.Content.ReadAsStringAsync();
                var shortBody = body.Length > 200 ? body.Substring(0, 200) : body;
                return (false, $"HTTP {(int)response.StatusCode}: {shortBody}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task UpdateSyncStatusAsync(int logId, bool isSynced, string? syncError)
        {
            if (logId <= 0) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var entity = await dbContext.AppErrorLogs.FindAsync(logId);
                if (entity != null)
                {
                    entity.IsSyncedToRemote = isSynced;
                    entity.RemoteSyncedAt = isSynced ? DateTime.UtcNow : null;
                    entity.RemoteSyncError = isSynced ? null : (syncError != null && syncError.Length > 500 ? syncError.Substring(0, 500) : syncError);
                    await dbContext.SaveChangesAsync();
                }
            }
            catch
            {
                // Ignore secondary sync status update failures
            }
        }

        private void WriteToFallbackFile(AppErrorLog log, Exception? dbEx)
        {
            try
            {
                lock (_fileLock)
                {
                    var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    var filePath = Path.Combine(logDir, $"app-errors-{DateTime.UtcNow:yyyy-MM-dd}.log");
                    var sb = new StringBuilder();
                    sb.AppendLine($"================== {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ==================");
                    sb.AppendLine($"Title: {log.Title}");
                    sb.AppendLine($"Type: {log.Type}");
                    sb.AppendLine($"Controller: {log.Controller}");
                    sb.AppendLine($"Path: {log.RequestMethod} {log.RequestPath}");
                    sb.AppendLine($"User: {log.UserIdentifier} | IP: {log.ClientIp}");
                    sb.AppendLine($"Message: {log.Message}");
                    if (!string.IsNullOrWhiteSpace(log.StackTrace))
                    {
                        sb.AppendLine($"StackTrace: {log.StackTrace}");
                    }
                    if (dbEx != null)
                    {
                        sb.AppendLine($"DB Save Error: {dbEx.Message}");
                    }
                    sb.AppendLine();

                    File.AppendAllText(filePath, sb.ToString());
                }
            }
            catch
            {
                // Fallback file write should not throw
            }
        }
    }
}
