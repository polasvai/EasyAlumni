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
        public async Task<IActionResult> UpdatePaymentSettings()
        {
            // Extract settings prefix settings[...] directly from Request.Form
            var form = Request.Form;
            var settingsDict = new Dictionary<string, string>();

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("settings[") && key.EndsWith("]"))
                {
                    var settingKey = key.Substring(9, key.Length - 10);
                    var values = form[key];
                    // If multiple values exist (e.g. hidden "0" and checkbox "1"), the checked value "1" is the last element
                    var effectiveValue = values.Count > 1 ? values[values.Count - 1] : values.ToString();
                    settingsDict[settingKey] = effectiveValue ?? "";
                }
            }

            // Explicitly handle standard payment toggles to guarantee state even if unchecked
            var toggleKeys = new[]
            {
                "Payment_Enable_JanataPay",
                "Payment_Enable_bKashManual",
                "Payment_Enable_NagadManual",
                "Payment_Enable_RocketManual",
                "Payment_Enable_BankTransfer",
                "Payment_Enable_Cash",
                "Donation_JanataPayUseDedicated"
            };

            foreach (var tKey in toggleKeys)
            {
                var formKey = $"settings[{tKey}]";
                if (form.ContainsKey(formKey))
                {
                    var values = form[formKey];
                    // Check if '1' is present anywhere in submitted values for this toggle
                    bool isChecked = values.Any(v => v == "1");
                    settingsDict[tKey] = isChecked ? "1" : "0";
                }
            }

            foreach (var kvp in settingsDict)
            {
                var existing = await _context.SystemSettings.FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);
                if (existing != null)
                {
                    existing.SettingValue = kvp.Value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.SystemSettings.Add(new SystemSetting
                    {
                        SettingKey = kvp.Key,
                        SettingValue = kvp.Value,
                        Description = "Payment Configuration",
                        IsSecret = kvp.Key.Contains("Password") || kvp.Key.Contains("Key") || kvp.Key.Contains("Token"),
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Payment settings and method toggles updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestJanataPayHandshake(EasyAlumni.Core.Models.JanataPayAccountType accountType = EasyAlumni.Core.Models.JanataPayAccountType.Registration)
        {
            var (success, message, token) = await _janataPayService.TestConnectionAsync(accountType);
            if (success)
            {
                TempData["Success"] = $"JanataPay [{accountType}] Connection Successful: {message}";
            }
            else
            {
                TempData["Error"] = $"JanataPay [{accountType}] Connection Failed: {message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
