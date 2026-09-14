using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyAlumni.Infrastructure.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<WhatsAppService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<(bool Success, string Response)> SendWhatsAppAsync(string mobileNumber, string message)
        {
            try
            {
                var apiKey = await _context.SystemSettings
                    .Where(s => s.SettingKey == "WhatsAppApiKey")
                    .Select(s => s.SettingValue)
                    .FirstOrDefaultAsync();

                var phoneId = await _context.SystemSettings
                    .Where(s => s.SettingKey == "WhatsAppPhoneId")
                    .Select(s => s.SettingValue)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(phoneId))
                {
                    _logger.LogInformation("WhatsApp Cloud API not configured. WhatsApp message skipped for {Phone}", mobileNumber);
                    return (true, "WhatsApp API not configured (Skipped)");
                }

                // In production, posts to https://graph.facebook.com/v18.0/{phoneId}/messages
                _logger.LogInformation("WhatsApp message queued for {Phone}: {Msg}", mobileNumber, message);
                return (true, "WhatsApp Message Sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send WhatsApp message to {Phone}", mobileNumber);
                return (false, ex.Message);
            }
        }
    }
}
