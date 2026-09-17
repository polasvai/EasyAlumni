using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IJanataPayService _janataPayService;
        private readonly IQrCodeService _qrCodeService;
        private readonly ISmsService _smsService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            IJanataPayService janataPayService,
            IQrCodeService qrCodeService,
            ISmsService smsService,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _janataPayService = janataPayService;
            _qrCodeService = qrCodeService;
            _smsService = smsService;
            _logger = logger;
        }

        // GET: /Payment/JanataPayCheckout?registrationNo=...
        [HttpGet]
        public async Task<IActionResult> JanataPayCheckout(string registrationNo)
        {
            if (string.IsNullOrWhiteSpace(registrationNo))
            {
                TempData["Error"] = "Invalid registration number.";
                return RedirectToAction("Index", "Home");
            }

            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.ReunionEvent)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.RegistrationNo == registrationNo);

            if (reg == null)
            {
                TempData["Error"] = "Registration not found.";
                return RedirectToAction("Index", "Home");
            }

            if (reg.Status == RegistrationStatus.Approved)
            {
                TempData["Success"] = "Your registration has already been approved and paid.";
                return RedirectToAction("Confirmation", "Enrollment", new { registrationNo = reg.RegistrationNo });
            }

            var tokenizeResult = await _janataPayService.InitiatePaymentAsync(
                reg.Id,
                reg.RegistrationNo,
                reg.TotalAmount,
                reg.AlumniProfile?.NameEnglish ?? "Alumni Member",
                reg.AlumniProfile?.ContactNumber ?? "01700000000",
                reg.AlumniProfile?.Email);

            if (!tokenizeResult.Success || string.IsNullOrEmpty(tokenizeResult.CheckoutUrl))
            {
                _logger.LogError("JanataPay checkout initiation failed for {RegNo}: {Error}", reg.RegistrationNo, tokenizeResult.ErrorMessage);
                ViewBag.ErrorMessage = tokenizeResult.ErrorMessage ?? "Unable to initialize JanataPay checkout session.";
                ViewBag.RegistrationNo = reg.RegistrationNo;
                return View("JanataPayFailed");
            }

            // Record or update pending JanataPay payment record
            var pendingPayment = reg.Payments
                .FirstOrDefault(p => p.PaymentMode == PaymentMode.JanataPay && p.Status == PaymentStatus.Pending);

            if (pendingPayment == null)
            {
                pendingPayment = new RegistrationPayment
                {
                    EventRegistrationId = reg.Id,
                    PaymentMode = PaymentMode.JanataPay,
                    TransactionId = tokenizeResult.ReferenceId ?? $"JP-{reg.RegistrationNo}",
                    SenderNumber = reg.AlumniProfile?.ContactNumber ?? "",
                    Amount = reg.TotalAmount,
                    Status = PaymentStatus.Pending,
                    SubmittedAt = DateTime.UtcNow,
                    GatewayReferenceId = tokenizeResult.ReferenceId,
                    GatewayTransactionToken = tokenizeResult.TransactionToken,
                    GatewayStatus = "Initiated"
                };
                _context.RegistrationPayments.Add(pendingPayment);
            }
            else
            {
                pendingPayment.GatewayReferenceId = tokenizeResult.ReferenceId;
                pendingPayment.GatewayTransactionToken = tokenizeResult.TransactionToken;
                pendingPayment.GatewayStatus = "Initiated";
                pendingPayment.SubmittedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Redirect user to JanataPay Hosted Checkout Portal
            return Redirect(tokenizeResult.CheckoutUrl);
        }

        // GET: /Payment/JanataPaySuccess?refid=...
        [HttpGet]
        public async Task<IActionResult> JanataPaySuccess(string? refid, [FromQuery(Name = "referenceId")] string? referenceId, [FromQuery(Name = "token")] string? token)
        {
            var effectiveRefId = !string.IsNullOrWhiteSpace(refid) ? refid : referenceId;

            if (string.IsNullOrWhiteSpace(effectiveRefId))
            {
                ViewBag.ErrorMessage = "Payment callback missing transaction reference identifier.";
                return View("JanataPayFailed");
            }

            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.ReunionEvent)
                .FirstOrDefaultAsync(p => p.GatewayReferenceId == effectiveRefId);

            if (payment == null)
            {
                ViewBag.ErrorMessage = $"No matching payment session found for reference {effectiveRefId}.";
                return View("JanataPayFailed");
            }

            var effectiveToken = !string.IsNullOrWhiteSpace(token) ? token : payment.GatewayTransactionToken;

            // Server-side verification with JanataPay API
            var verifyResult = await _janataPayService.VerifyPaymentAsync(effectiveRefId, effectiveToken ?? "");

            payment.GatewayStatus = verifyResult.TransactionStatus ?? verifyResult.TransactionStatusCode;
            payment.GatewayFtNumber = verifyResult.FtNumber;

            if (verifyResult.Success && verifyResult.Amount >= payment.Amount)
            {
                // Approved!
                payment.Status = PaymentStatus.Approved;
                payment.ApprovedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(verifyResult.FtNumber))
                {
                    payment.TransactionId = verifyResult.FtNumber;
                }

                if (payment.EventRegistration != null)
                {
                    var reg = payment.EventRegistration;
                    reg.Status = RegistrationStatus.Approved;
                    reg.PaidAmount = verifyResult.Amount;

                    // Ensure QR code
                    if (string.IsNullOrEmpty(reg.QrCodeBase64))
                    {
                        var signedToken = _qrCodeService.GenerateSignedToken(
                            reg.RegistrationNo,
                            reg.Id,
                            reg.AlumniProfile?.UserCode ?? "ALUMNI");

                        reg.QrCodeToken = signedToken;
                        reg.QrCodeBase64 = _qrCodeService.GenerateQrCodeBase64(signedToken);
                    }

                    // Auto-Reconcile into Accounting Cash Book
                    var incomeHead = await _context.AccountHeads
                        .FirstOrDefaultAsync(h => h.HeadName.Contains("Registration") && h.Type == AccountHeadType.Income)
                        ?? await _context.AccountHeads.FirstOrDefaultAsync(h => h.Type == AccountHeadType.Income);

                    if (incomeHead != null)
                    {
                        var countVouchers = await _context.CashEntries.CountAsync() + 1;
                        _context.CashEntries.Add(new CashEntry
                        {
                            VoucherNumber = $"V-JP-{DateTime.UtcNow.Year}-{countVouchers:D5}",
                            EntryDate = DateTime.UtcNow,
                            AccountHeadId = incomeHead.Id,
                            Amount = payment.Amount,
                            PaymentMode = "JanataPay",
                            Description = $"Online JanataPay Reunion Fee for {reg.RegistrationNo} ({reg.AlumniProfile?.NameEnglish}) FT: {verifyResult.FtNumber}",
                            RelatedRegistrationId = reg.Id
                        });
                    }

                    // Dispatch Approval SMS
                    if (reg.AlumniProfile != null && !string.IsNullOrWhiteSpace(reg.AlumniProfile.ContactNumber))
                    {
                        var passUrl = $"{Request.Scheme}://{Request.Host}/Pass/ViewPass/{reg.RegistrationNo}";
                        var placeholders = new Dictionary<string, string>
                        {
                            ["Name"] = reg.AlumniProfile.NameEnglish,
                            ["TicketNo"] = reg.RegistrationNo,
                            ["Amount"] = payment.Amount.ToString("N0"),
                            ["EventName"] = reg.ReunionEvent?.EventTitle ?? "Reunion",
                            ["PassUrl"] = passUrl
                        };

                        await _smsService.SendTemplateSmsAsync("PaymentSMS", reg.AlumniProfile.ContactNumber, placeholders);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Payment completed and approved successfully via JanataPay!";
                return RedirectToAction("Confirmation", "Enrollment", new { registrationNo = payment.EventRegistration?.RegistrationNo });
            }
            else
            {
                _logger.LogWarning("JanataPay Verification failed for {RefId}. Code: {Code}, Status: {Status}", effectiveRefId, verifyResult.TransactionStatusCode, verifyResult.TransactionStatus);
                await _context.SaveChangesAsync();

                ViewBag.ErrorMessage = $"Gateway verification did not confirm successful payment. Status: {verifyResult.TransactionStatus ?? "Unknown"} ({verifyResult.TransactionStatusCode})";
                ViewBag.RegistrationNo = payment.EventRegistration?.RegistrationNo;
                return View("JanataPayFailed");
            }
        }

        // GET: /Payment/JanataPayFail?refid=...
        [HttpGet]
        public async Task<IActionResult> JanataPayFail(string? refid, [FromQuery(Name = "referenceId")] string? referenceId)
        {
            var effectiveRefId = !string.IsNullOrWhiteSpace(refid) ? refid : referenceId;
            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                .FirstOrDefaultAsync(p => p.GatewayReferenceId == effectiveRefId);

            if (payment != null)
            {
                payment.GatewayStatus = "Failed";
                await _context.SaveChangesAsync();
                ViewBag.RegistrationNo = payment.EventRegistration?.RegistrationNo;
            }

            ViewBag.ErrorMessage = "The transaction was reported as failed or declined by JanataPay.";
            return View("JanataPayFailed");
        }

        // GET: /Payment/JanataPayCancel?refid=...
        [HttpGet]
        public async Task<IActionResult> JanataPayCancel(string? refid, [FromQuery(Name = "referenceId")] string? referenceId)
        {
            var effectiveRefId = !string.IsNullOrWhiteSpace(refid) ? refid : referenceId;
            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                .FirstOrDefaultAsync(p => p.GatewayReferenceId == effectiveRefId);

            if (payment != null)
            {
                payment.GatewayStatus = "Cancelled";
                await _context.SaveChangesAsync();
                ViewBag.RegistrationNo = payment.EventRegistration?.RegistrationNo;
            }

            ViewBag.ErrorMessage = "Payment was cancelled before completion.";
            return View("JanataPayFailed");
        }
    }
}
