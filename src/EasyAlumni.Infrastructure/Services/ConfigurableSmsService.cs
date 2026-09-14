using System.Net;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyAlumni.Infrastructure.Services
{
    public class ConfigurableSmsService : ISmsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ConfigurableSmsService> _logger;

        public ConfigurableSmsService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<ConfigurableSmsService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<(bool Success, string Response)> SendSmsAsync(string mobileNumber, string message)
        {
            try
            {
                var settings = await _context.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

                var isLocked = settings.GetValueOrDefault("SMSLock", "0") == "1";
                if (isLocked)
                {
                    _logger.LogInformation("SMS is locked (SMSLock=1). Message to {Phone} skipped: {Msg}", mobileNumber, message);
                    return (true, "SMS Locked by Admin");
                }

                var apiUrlTemplate = settings.GetValueOrDefault("SMSAPIURL", "");
                var apiKey = settings.GetValueOrDefault("SmsAPIKey", "");
                var senderId = settings.GetValueOrDefault("SenderID", "");

                if (string.IsNullOrWhiteSpace(apiUrlTemplate) || string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("SMS API URL or Key not configured.");
                    return (false, "SMS API not configured");
                }

                var cleanNumber = CleanPhoneNumber(mobileNumber);

                var url = apiUrlTemplate
                    .Replace("$api_key", WebUtility.UrlEncode(apiKey))
                    .Replace("$sender_Id", WebUtility.UrlEncode(senderId))
                    .Replace("$senderid", WebUtility.UrlEncode(senderId))
                    .Replace("$sms_number", WebUtility.UrlEncode(cleanNumber))
                    .Replace("$contacts", WebUtility.UrlEncode(cleanNumber))
                    .Replace("$sms_text", WebUtility.UrlEncode(message))
                    .Replace("$msg", WebUtility.UrlEncode(message));

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("SMS sent to {Phone}. Response: {Response}", cleanNumber, content);
                return (response.IsSuccessStatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS to {Phone}", mobileNumber);
                return (false, ex.Message);
            }
        }

        public async Task<string> CheckBalanceAsync()
        {
            try
            {
                var balanceUrl = await _context.SystemSettings
                    .Where(s => s.SettingKey == "SMSBalanceAPI")
                    .Select(s => s.SettingValue)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(balanceUrl))
                    return "Balance API not configured";

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(8);
                var resp = await client.GetAsync(balanceUrl);
                return await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public async Task SendTemplateSmsAsync(string templateKey, string mobileNumber, Dictionary<string, string> placeholders)
        {
            var template = await _context.SystemSettings
                .Where(s => s.SettingKey == templateKey)
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(template))
            {
                _logger.LogWarning("SMS template '{Key}' not found.", templateKey);
                return;
            }

            var msg = template;
            foreach (var kv in placeholders)
            {
                msg = msg.Replace($"{{{kv.Key}}}", kv.Value);
            }

            await SendSmsAsync(mobileNumber, msg);
        }

        private static string CleanPhoneNumber(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("880"))
                return digits;
            if (digits.StartsWith("0"))
                return "88" + digits;
            return "880" + digits;
        }
    }
}
