using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using EasyAlumni.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    public class DonationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IJanataPayService _janataPayService;
        private readonly IFileStorageService _fileStorage;
        private readonly ISmsService _smsService;
        private readonly IQrCodeService _qrCodeService;
        private readonly ILogger<DonationController> _logger;

        private static readonly string[] AllowedDocExt = { ".jpg", ".jpeg", ".png", ".pdf" };

        public DonationController(
            ApplicationDbContext context,
            IJanataPayService janataPayService,
            IFileStorageService fileStorage,
            ISmsService smsService,
            IQrCodeService qrCodeService,
            ILogger<DonationController> logger)
        {
            _context = context;
            _janataPayService = janataPayService;
            _fileStorage = fileStorage;
            _smsService = smsService;
            _qrCodeService = qrCodeService;
            _logger = logger;
        }

        // POST: /Donation/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(DonationSubmitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Redirect("/#donate");
            }

            // Generate unique tracking number DON-YYYY-XXXXX
            var currentYear = DateTime.UtcNow.Year;
            var currentCount = await _context.Donations.CountAsync(d => d.DonationTrackingNo.StartsWith($"DON-{currentYear}-")) + 1;
            var trackingNo = $"DON-{currentYear}-{currentCount:D5}";

            while (await _context.Donations.AnyAsync(d => d.DonationTrackingNo == trackingNo))
            {
                currentCount++;
                trackingNo = $"DON-{currentYear}-{currentCount:D5}";
            }

            // Parse payment mode
            var paymentModeEnum = PaymentMode.JanataPay;
            if (Enum.TryParse<PaymentMode>(model.PaymentMode, true, out var parsedMode))
            {
                paymentModeEnum = parsedMode;
            }

            int? batchYearParsed = null;
            if (!string.IsNullOrWhiteSpace(model.BatchYear) && int.TryParse(model.BatchYear.Trim(), out var bYear))
            {
                batchYearParsed = bYear;
            }

            // Handle optional slip attachment
            string? slipPath = null;
            if (model.SlipAttachment != null && model.SlipAttachment.Length > 0)
            {
                try
                {
                    using var stream = model.SlipAttachment.OpenReadStream();
                    slipPath = await _fileStorage.SaveFileAsync(stream, model.SlipAttachment.FileName, "PaymentSlips", AllowedDocExt, 5242880); // Max 5MB
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save slip attachment for donation {TrackingNo}", trackingNo);
                }
            }

            var donation = new Donation
            {
                DonationTrackingNo = trackingNo,
                DonorName = model.DonorName.Trim(),
                DonorEmail = model.DonorEmail?.Trim(),
                DonorPhone = model.DonorPhone.Trim(),
                BatchYear = batchYearParsed,
                Amount = model.Amount,
                DonationPurpose = string.IsNullOrWhiteSpace(model.DonationPurpose) ? "General Reunion & Campus Welfare" : model.DonationPurpose.Trim(),
                PaymentMode = paymentModeEnum,
                Status = PaymentStatus.Pending,
                TransactionId = model.TransactionId?.Trim(),
                SenderNumber = model.SenderNumber?.Trim(),
                Remarks = model.Remarks?.Trim(),
                IsAnonymous = model.IsAnonymous,
                SlipAttachmentPath = slipPath,
                CreatedAt = DateTime.UtcNow
            };

            _context.Donations.Add(donation);
            await _context.SaveChangesAsync();

            // Online JanataPay flow
            if (paymentModeEnum == PaymentMode.JanataPay)
            {
                var tokenizeResult = await _janataPayService.InitiatePaymentAsync(
                    donation.Id,
                    donation.DonationTrackingNo,
                    donation.Amount,
                    donation.DonorName,
                    donation.DonorPhone,
                    donation.DonorEmail);

                if (tokenizeResult.Success && !string.IsNullOrEmpty(tokenizeResult.CheckoutUrl))
                {
                    donation.GatewayReferenceId = tokenizeResult.ReferenceId;
                    donation.TransactionId = tokenizeResult.TransactionToken; // store token in TransactionId temporarily
                    await _context.SaveChangesAsync();

                    return Redirect(tokenizeResult.CheckoutUrl);
                }
                else
                {
                    _logger.LogError("JanataPay initiation failed for donation {TrackingNo}: {Error}", trackingNo, tokenizeResult.ErrorMessage);
                    TempData["ErrorMessage"] = $"Online gateway error: {tokenizeResult.ErrorMessage ?? "Could not initiate payment"}. Your tracking number is {trackingNo}.";
                    return RedirectToAction(nameof(Receipt), new { trackingNo });
                }
            }

            // Manual payment modes (bKash, Nagad, Rocket, Bank)
            TempData["SuccessMessage"] = "Your donation information has been submitted successfully! It will be approved once verified.";
            return RedirectToAction(nameof(Receipt), new { trackingNo });
        }

        // GET: /Donation/Receipt/{trackingNo}
        [HttpGet]
        public async Task<IActionResult> Receipt(string trackingNo)
        {
            if (string.IsNullOrWhiteSpace(trackingNo))
            {
                return NotFound("Tracking number is required.");
            }

            var donation = await _context.Donations
                .Include(d => d.ApprovedByUser)
                .FirstOrDefaultAsync(d => d.DonationTrackingNo == trackingNo.Trim());

            if (donation == null)
            {
                return NotFound("Donation record not found.");
            }

            // Generate a verification QR code for the receipt
            var verifyUrl = $"{Request.Scheme}://{Request.Host}/Donation/Receipt/{donation.DonationTrackingNo}";
            ViewBag.QrCodeBase64 = _qrCodeService.GenerateQrCodeBase64(verifyUrl);

            return View(donation);
        }
    }
}
