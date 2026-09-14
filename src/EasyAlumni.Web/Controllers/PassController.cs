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
    }
}
