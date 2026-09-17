using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class PaymentSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IJanataPayService _janataPayService;
        private readonly ILogger<PaymentSettingsController> _logger;

        public PaymentSettingsController(
            ApplicationDbContext context,
            IJanataPayService janataPayService,
            ILogger<PaymentSettingsController> logger)
        {
            _context = context;
            _janataPayService = janataPayService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.SystemSettings.OrderBy(s => s.SettingKey).ToListAsync();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentSettings(Dictionary<string, string> settings)
        {
            if (settings != null)
            {
                foreach (var s in settings)
                {
                    var existing = await _context.SystemSettings.FirstOrDefaultAsync(x => x.SettingKey == s.Key);
                    if (existing != null)
                    {
                        existing.SettingValue = s.Value ?? "";
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.SystemSettings.Add(new SystemSetting
                        {
                            SettingKey = s.Key,
                            SettingValue = s.Value ?? "",
                            Description = "Payment Configuration",
                            IsSecret = s.Key.Contains("Password") || s.Key.Contains("Key") || s.Key.Contains("Token"),
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Payment settings and method toggles updated successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestJanataPayHandshake()
        {
            var (success, message, token) = await _janataPayService.TestConnectionAsync();
            if (success)
            {
                TempData["Success"] = $"JanataPay Connection Successful: {message}";
            }
            else
            {
                TempData["Error"] = $"JanataPay Connection Failed: {message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
