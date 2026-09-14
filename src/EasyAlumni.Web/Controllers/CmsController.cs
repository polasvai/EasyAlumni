using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using EasyAlumni.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class CmsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;

        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageSize = 5 * 1024 * 1024; // 5 MB

        public CmsController(ApplicationDbContext context, IFileStorageService fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
            var activeEvent = await _context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive);

            DateTime? parsedDate = null;
            if (settings.TryGetValue("ReunionDate", out var dateStr) && DateTime.TryParse(dateStr, out var d))
            {
                parsedDate = d;
            }
            else if (activeEvent != null)
            {
                parsedDate = activeEvent.EventDate;
            }

            var vm = new CmsSettingsViewModel
            {
                LandingHeroTitle = settings.GetValueOrDefault("LandingHeroTitle", activeEvent?.EventTitle ?? "Grand Alumni Reunion 2026"),
                LandingHeroSubtitle = settings.GetValueOrDefault("LandingHeroSubtitle", activeEvent?.TitleBangla ?? ""),
                LandingHeroTagline = settings.GetValueOrDefault("LandingHeroTagline", "Celebrating Lifelong Bonds & Brotherhood"),
                LandingHeroBgImage = settings.GetValueOrDefault("LandingHeroBgImage", "https://images.unsplash.com/photo-1523580494863-6f3031224c94?auto=format&fit=crop&w=1920&q=80"),

                AboutTitle = settings.GetValueOrDefault("AboutTitle", "About the Grand Reunion"),
                AboutSubtitle = settings.GetValueOrDefault("AboutSubtitle", "A historic day where old classmates, mentors, and friends reunite under one grand roof."),
                AboutStory = settings.GetValueOrDefault("AboutStory", activeEvent?.Description ?? ""),
                AboutImageUrl = settings.GetValueOrDefault("AboutImageUrl", "https://images.unsplash.com/photo-1511578314322-379afb476865?auto=format&fit=crop&w=800&q=80"),

                EventVenue = settings.GetValueOrDefault("EventVenue", activeEvent?.VenueName ?? ""),
                VenueAddress = settings.GetValueOrDefault("VenueAddress", activeEvent?.VenueAddress ?? ""),
                GoogleMapEmbedUrl = settings.GetValueOrDefault("GoogleMapEmbedUrl", "https://maps.google.com/maps?q=Shahid+Nazmul+Huq+Girls+High+School+Rajshahi&hl=en&z=16&output=embed"),
                ReunionDate = parsedDate,
                RegistrationDeadline = activeEvent?.RegistrationDeadline,

                ManualBkashNumber = settings.GetValueOrDefault("ManualBkashNumber", "01700000000"),
                ManualNagadNumber = settings.GetValueOrDefault("ManualNagadNumber", "01800000000"),
                ManualRocketNumber = settings.GetValueOrDefault("ManualRocketNumber", "01900000000-8"),
                PaymentBankDetails = settings.GetValueOrDefault("PaymentBankDetails", ""),

                ContactHotline = settings.GetValueOrDefault("ContactHotline", "+880 1711-000000"),
                ContactEmail = settings.GetValueOrDefault("ContactEmail", "alumni@snhghs.edu.bd"),

                FooterAboutText = settings.GetValueOrDefault("FooterAboutText", "The ultimate event and alumni community management platform. Connecting alumni, celebrating lifelong friendships, and organizing grand reunions seamlessly."),
                FooterFacebookUrl = settings.GetValueOrDefault("FooterFacebookUrl", "#"),
                FooterYoutubeUrl = settings.GetValueOrDefault("FooterYoutubeUrl", "#"),
                FooterWhatsappUrl = settings.GetValueOrDefault("FooterWhatsappUrl", "#"),
                FooterCopyright = settings.GetValueOrDefault("FooterCopyright", "© 2026 EasyAlumni Association. All rights reserved."),
                FooterTagline = settings.GetValueOrDefault("FooterTagline", "Designed for School, College & University Grand Reunions"),
                FooterMadeWith = settings.GetValueOrDefault("FooterMadeWith", "Made with ❤️"),

                ActiveTab = TempData["ActiveTab"]?.ToString() ?? "hero"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveHero(CmsSettingsViewModel vm)
        {
            if (vm.HeroBgFile != null && vm.HeroBgFile.Length > 0)
            {
                using var stream = vm.HeroBgFile.OpenReadStream();
                vm.LandingHeroBgImage = await _fileStorage.SaveFileAsync(
                    stream,
                    vm.HeroBgFile.FileName,
                    "CMS",
                    AllowedImageExtensions,
                    MaxImageSize);
            }

            await UpsertSettingAsync("LandingHeroTitle", vm.LandingHeroTitle, "Main heading on landing page");
            await UpsertSettingAsync("LandingHeroSubtitle", vm.LandingHeroSubtitle, "Bengali subheading on landing page");
            await UpsertSettingAsync("LandingHeroTagline", vm.LandingHeroTagline, "Tagline text displayed under hero title");
            if (!string.IsNullOrEmpty(vm.LandingHeroBgImage))
            {
                await UpsertSettingAsync("LandingHeroBgImage", vm.LandingHeroBgImage, "Hero banner background image URL");
            }

            // Sync with active event title if present
            var activeEvent = await _context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive);
            if (activeEvent != null)
            {
                if (!string.IsNullOrWhiteSpace(vm.LandingHeroTitle))
                    activeEvent.EventTitle = vm.LandingHeroTitle;
                if (!string.IsNullOrWhiteSpace(vm.LandingHeroSubtitle))
                    activeEvent.TitleBangla = vm.LandingHeroSubtitle;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Hero Banner section updated successfully!";
            TempData["ActiveTab"] = "hero";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAbout(CmsSettingsViewModel vm)
        {
            if (vm.AboutImageFile != null && vm.AboutImageFile.Length > 0)
            {
                using var stream = vm.AboutImageFile.OpenReadStream();
                vm.AboutImageUrl = await _fileStorage.SaveFileAsync(
                    stream,
                    vm.AboutImageFile.FileName,
                    "CMS",
                    AllowedImageExtensions,
                    MaxImageSize);
            }

            await UpsertSettingAsync("AboutTitle", vm.AboutTitle, "Title for About section on home page");
            await UpsertSettingAsync("AboutSubtitle", vm.AboutSubtitle, "Subtitle for About section");
            await UpsertSettingAsync("AboutStory", vm.AboutStory, "Full story / text for About section");
            if (!string.IsNullOrEmpty(vm.AboutImageUrl))
            {
                await UpsertSettingAsync("AboutImageUrl", vm.AboutImageUrl, "Main photo displayed in About section");
            }

            var activeEvent = await _context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive);
            if (activeEvent != null && !string.IsNullOrWhiteSpace(vm.AboutStory))
            {
                activeEvent.Description = vm.AboutStory;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "About section updated successfully!";
            TempData["ActiveTab"] = "about";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveVenue(CmsSettingsViewModel vm)
        {
            await UpsertSettingAsync("EventVenue", vm.EventVenue, "Location venue for reunion");
            await UpsertSettingAsync("VenueAddress", vm.VenueAddress, "Full street address of the event venue");
            await UpsertSettingAsync("GoogleMapEmbedUrl", vm.GoogleMapEmbedUrl, "Google Maps iframe embed URL for venue");
            if (vm.ReunionDate.HasValue)
            {
                await UpsertSettingAsync("ReunionDate", vm.ReunionDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"), "Date and time of the reunion event");
            }

            var activeEvent = await _context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive);
            if (activeEvent != null)
            {
                if (!string.IsNullOrWhiteSpace(vm.EventVenue))
                    activeEvent.VenueName = vm.EventVenue;
                if (!string.IsNullOrWhiteSpace(vm.VenueAddress))
                    activeEvent.VenueAddress = vm.VenueAddress;
                if (vm.ReunionDate.HasValue)
                    activeEvent.EventDate = vm.ReunionDate.Value;
                if (vm.RegistrationDeadline.HasValue)
                    activeEvent.RegistrationDeadline = vm.RegistrationDeadline.Value;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Event Venue, Schedule & Google Map updated successfully!";
            TempData["ActiveTab"] = "venue";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePayments(CmsSettingsViewModel vm)
        {
            await UpsertSettingAsync("ManualBkashNumber", vm.ManualBkashNumber, "bKash number displayed to attendees");
            await UpsertSettingAsync("ManualNagadNumber", vm.ManualNagadNumber, "Nagad number displayed to attendees");
            await UpsertSettingAsync("ManualRocketNumber", vm.ManualRocketNumber, "Rocket number displayed to attendees");
            await UpsertSettingAsync("PaymentBankDetails", vm.PaymentBankDetails, "Bank transfer account information");

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Payment accounts & instructions updated successfully!";
            TempData["ActiveTab"] = "payments";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveContact(CmsSettingsViewModel vm)
        {
            await UpsertSettingAsync("ContactHotline", vm.ContactHotline, "Hotline phone numbers for queries");
            await UpsertSettingAsync("ContactEmail", vm.ContactEmail, "Official inquiry email address");

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Helpdesk hotlines & contact info updated successfully!";
            TempData["ActiveTab"] = "contact";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFooter(CmsSettingsViewModel vm)
        {
            await UpsertSettingAsync("FooterAboutText", vm.FooterAboutText, "Brief narrative displayed under logo in footer");
            await UpsertSettingAsync("FooterFacebookUrl", vm.FooterFacebookUrl, "Facebook page or group link");
            await UpsertSettingAsync("FooterYoutubeUrl", vm.FooterYoutubeUrl, "YouTube channel link");
            await UpsertSettingAsync("FooterWhatsappUrl", vm.FooterWhatsappUrl, "WhatsApp group or contact link");
            await UpsertSettingAsync("FooterCopyright", vm.FooterCopyright, "Copyright statement in footer");
            await UpsertSettingAsync("FooterTagline", vm.FooterTagline, "Bottom tagline text in footer");
            await UpsertSettingAsync("FooterMadeWith", vm.FooterMadeWith, "Sub-badge text under footer about story");

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Footer content and social links updated successfully!";
            TempData["ActiveTab"] = "footer";
            return RedirectToAction(nameof(Index));
        }

        private async Task UpsertSettingAsync(string key, string? value, string description = "")
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = value ?? string.Empty,
                    Description = description
                });
            }
            else
            {
                setting.SettingValue = value ?? string.Empty;
                setting.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
