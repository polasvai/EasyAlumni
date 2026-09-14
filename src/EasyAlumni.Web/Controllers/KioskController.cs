using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize]
    public class KioskController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IQrCodeService _qrCodeService;
        private readonly UserManager<ApplicationUser> _userManager;

        public KioskController(
            ApplicationDbContext context,
            IQrCodeService qrCodeService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _qrCodeService = qrCodeService;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> LookupAttendee(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(new { success = false, message = "Please provide a QR code token, ticket number, or mobile number." });
            }

            query = query.Trim();

            // Try validating token
            string searchTicket = query;
            if (_qrCodeService.ValidateToken(query, out var validatedRegNo, out _))
            {
                searchTicket = validatedRegNo;
            }

            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.ReunionEvent)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.RegistrationNo == searchTicket ||
                                          r.RegistrationNo == query ||
                                          r.AlumniProfile!.ContactNumber == query ||
                                          r.AlumniProfile.UserCode == query);

            if (reg == null)
            {
                return Json(new { success = false, message = "No attendee record found matching your query." });
            }

            var profile = reg.AlumniProfile;
            var totalHeads = 1 + reg.SpouseCount + reg.ChildCount + reg.GuestCount;

            string? volunteerName = null;
            if (!string.IsNullOrEmpty(reg.KitDistributedByVolunteerId))
            {
                var volUser = await _userManager.FindByIdAsync(reg.KitDistributedByVolunteerId);
                volunteerName = volUser?.FullName ?? volUser?.UserName;
            }

            return Json(new
            {
                success = true,
                registrationId = reg.Id,
                ticketNo = reg.RegistrationNo,
                userCode = profile?.UserCode,
                nameEnglish = profile?.NameEnglish,
                nameBangla = profile?.NameBangla,
                nickname = profile?.NickName,
                batchYear = profile?.PassingYear,
                mobile = profile?.ContactNumber,
                bloodGroup = profile?.BloodGroup,
                photoUrl = string.IsNullOrEmpty(profile?.RecentPhotoPath)
                    ? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(profile?.NameEnglish ?? "A")}&background=006a4e&color=fff&size=200"
                    : profile.RecentPhotoPath,
                status = reg.Status.ToString(),
                isApproved = reg.Status == RegistrationStatus.Approved,
                tShirtSize = reg.TShirtSize,
                spouseCount = reg.SpouseCount,
                childCount = reg.ChildCount,
                guestCount = reg.GuestCount,
                totalHeads = totalHeads,
                isKitDistributed = reg.IsKitDistributed,
                kitDistributedAt = reg.KitDistributedAt?.ToString("hh:mm tt, dd MMM yyyy"),
                kitDistributedBy = volunteerName ?? "Desk Volunteer"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDistribution([FromBody] DistributionRequest request)
        {
            if (request == null || request.RegistrationId <= 0)
            {
                return Json(new { success = false, message = "Invalid registration reference." });
            }

            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .FirstOrDefaultAsync(r => r.Id == request.RegistrationId);

            if (reg == null)
            {
                return Json(new { success = false, message = "Registration not found." });
            }

            if (reg.Status != RegistrationStatus.Approved)
            {
                return Json(new { success = false, message = "Cannot distribute kit: Attendee payment has not been approved yet." });
            }

            if (reg.IsKitDistributed)
            {
                return Json(new
                {
                    success = false,
                    isDuplicate = true,
                    message = $"ALREADY CLAIMED! Kit was already distributed at {reg.KitDistributedAt:hh:mm tt} by Volunteer: {reg.KitDistributedByVolunteerId}."
                });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var now = DateTime.UtcNow;

            reg.IsKitDistributed = true;
            reg.KitDistributedAt = now;
            reg.KitDistributedByVolunteerId = currentUser?.Id ?? "Desk Volunteer";

            // Record Gift Distributions and update stock
            var giftItems = await _context.GiftItems
                .Where(g => g.IsActive)
                .Include(g => g.SizeStocks)
                .ToListAsync();

            foreach (var item in giftItems)
            {
                string? sizeGiven = null;
                if (item.IsSizeSpecific)
                {
                    sizeGiven = reg.TShirtSize;
                    var sizeStock = item.SizeStocks.FirstOrDefault(s => s.SizeName == reg.TShirtSize);
                    if (sizeStock != null)
                    {
                        sizeStock.DistributedStock += 1;
                    }
                }

                item.DistributedQuantity += 1;

                _context.GiftDistributions.Add(new GiftDistribution
                {
                    EventRegistrationId = reg.Id,
                    GiftItemId = item.Id,
                    SizeGiven = sizeGiven,
                    Quantity = 1,
                    VolunteerUserId = currentUser?.Id,
                    DistributedAt = now,
                    Notes = request.Notes
                });
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Kit & T-Shirt ({reg.TShirtSize}) successfully marked as HANDED OVER!",
                distributedAt = now.ToString("hh:mm tt")
            });
        }

        public class DistributionRequest
        {
            public int RegistrationId { get; set; }
            public string? Notes { get; set; }
        }
    }
}
