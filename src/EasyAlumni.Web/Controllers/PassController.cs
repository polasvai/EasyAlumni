using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    public class PassController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IQrCodeService _qrCodeService;

        public PassController(ApplicationDbContext context, IQrCodeService qrCodeService)
        {
            _context = context;
            _qrCodeService = qrCodeService;
        }

        [HttpGet]
        public async Task<IActionResult> ViewPass(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound("Invalid Ticket / Registration ID");
            }

            int? regId = int.TryParse(id, out var parsed) ? parsed : null;

            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.ReunionEvent)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.RegistrationNo == id.Trim() || (regId.HasValue && r.Id == regId.Value));

            if (reg == null)
            {
                return NotFound("Registration pass not found.");
            }

            // Ensure QR Code is generated
            if (string.IsNullOrEmpty(reg.QrCodeBase64))
            {
                var userCode = reg.AlumniProfile?.UserCode ?? "ALUMNI";
                var token = _qrCodeService.GenerateSignedToken(reg.RegistrationNo, reg.Id, userCode);
                reg.QrCodeToken = token;
                reg.QrCodeBase64 = _qrCodeService.GenerateQrCodeBase64(token);
                await _context.SaveChangesAsync();
            }

            return View(reg);
        }

        // GET: /Pass/DownloadPass
        [HttpGet]
        public IActionResult DownloadPass()
        {
            return View();
        }

        // POST: /Pass/FindPass
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FindPass(string phoneNumber, string? identifier)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                TempData["PassLookupError"] = "Please enter your registered mobile number.";
                return RedirectToAction(nameof(DownloadPass));
            }

            var cleanPhone = phoneNumber.Trim();
            var cleanId = identifier?.Trim();

            // Find matching registration
            var query = _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .Where(r => r.AlumniProfile != null && 
                           (r.AlumniProfile.ContactNumber == cleanPhone || 
                            r.AlumniProfile.AlternativeNumber == cleanPhone));

            if (!string.IsNullOrWhiteSpace(cleanId))
            {
                query = query.Where(r => r.RegistrationNo == cleanId || 
                                         r.AlumniProfile!.UserCode == cleanId || 
                                         r.Payments.Any(p => p.TransactionId == cleanId || p.GatewayFtNumber == cleanId || p.GatewayReferenceId == cleanId));
            }

            var matches = await query.OrderByDescending(r => r.RegisteredAt).ToListAsync();

            if (!matches.Any())
            {
                TempData["PassLookupError"] = "No registration found matching the entered phone number and transaction/ticket ID. Please verify your inputs.";
                return RedirectToAction(nameof(DownloadPass));
            }

            var registration = matches.First();

            if (registration.Status == EasyAlumni.Core.Enums.RegistrationStatus.Approved)
            {
                return RedirectToAction(nameof(ViewPass), new { id = registration.RegistrationNo });
            }
            else
            {
                TempData["PassLookupWarning"] = $"Your registration ({registration.RegistrationNo}) was found, but payment approval is currently pending. You will be able to print your Digital Pass as soon as payment is confirmed.";
                return RedirectToAction(nameof(DownloadPass));
            }
        }
    }
}
