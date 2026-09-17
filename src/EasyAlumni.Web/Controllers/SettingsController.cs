using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IFileStorageService _fileStorage;
        private readonly IJanataPayService _janataPayService;

        public SettingsController(
            ApplicationDbContext context,
            ISmsService smsService,
            IFileStorageService fileStorage,
            IJanataPayService janataPayService)
        {
            _context = context;
            _smsService = smsService;
            _fileStorage = fileStorage;
            _janataPayService = janataPayService;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _context.SystemSettings.OrderBy(s => s.SettingKey).ToListAsync();
            var notices = await _context.NoticePosts.OrderByDescending(n => n.PublishDate).ToListAsync();

            ViewBag.Notices = notices;
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSetting(string key, string value)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting != null)
            {
                setting.SettingValue = value;
                setting.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Setting '{key}' updated successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAllSettings(Dictionary<string, string> settings)
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
                        Description = "Custom System Configuration",
                        IsSecret = s.Key.Contains("Password") || s.Key.Contains("Key") || s.Key.Contains("Token"),
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "System settings updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestJanataPay()
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestSms(string testMobile, string testMessage)
        {
            if (string.IsNullOrWhiteSpace(testMobile) || string.IsNullOrWhiteSpace(testMessage))
            {
                TempData["Error"] = "Mobile number and test message are required.";
                return RedirectToAction(nameof(Index));
            }

            var (success, response) = await _smsService.SendSmsAsync(testMobile, testMessage);
            if (success)
            {
                TempData["Success"] = $"Test SMS sent to {testMobile}! Gateway Response: {response}";
            }
            else
            {
                TempData["Error"] = $"Failed to send test SMS: {response}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> CheckBalance()
        {
            var balance = await _smsService.CheckBalanceAsync();
            return Json(new { balance });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNotice(string title, string category, string contentHtml, IFormFile? attachmentFile)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(contentHtml))
            {
                TempData["Error"] = "Title and Content are required.";
                return RedirectToAction(nameof(Index));
            }

            string? attachPath = null;
            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                using var stream = attachmentFile.OpenReadStream();
                attachPath = await _fileStorage.SaveFileAsync(stream, attachmentFile.FileName, "Notices", new[] { ".pdf", ".jpg", ".png" }, 5242880);
            }

            _context.NoticePosts.Add(new NoticePost
            {
                Title = title.Trim(),
                Category = category ?? "Notice",
                ContentHtml = contentHtml.Trim(),
                PublishDate = DateTime.UtcNow,
                IsPublished = true,
                AttachmentPath = attachPath
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Notice '{title}' published.";
            return RedirectToAction(nameof(Index));
        }
    }
}
