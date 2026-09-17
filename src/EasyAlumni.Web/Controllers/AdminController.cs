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
    [Authorize(Roles = "SuperAdmin,Admin,Accounts")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IQrCodeService _qrCodeService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJanataPayService _janataPayService;

        public AdminController(
            ApplicationDbContext context,
            ISmsService smsService,
            IQrCodeService qrCodeService,
            UserManager<ApplicationUser> userManager,
            IJanataPayService janataPayService)
        {
            _context = context;
            _smsService = smsService;
            _qrCodeService = qrCodeService;
            _userManager = userManager;
            _janataPayService = janataPayService;
        }

        public async Task<IActionResult> Index()
        {
            var totalReg = await _context.EventRegistrations.CountAsync();
            var approvedReg = await _context.EventRegistrations.CountAsync(r => r.Status == RegistrationStatus.Approved);
            var pendingReg = await _context.EventRegistrations.CountAsync(r => r.Status == RegistrationStatus.Pending);
            var rejectedReg = await _context.EventRegistrations.CountAsync(r => r.Status == RegistrationStatus.Rejected);

            var totalCollected = await _context.RegistrationPayments
                .Where(p => p.Status == PaymentStatus.Approved)
                .SumAsync(p => p.Amount);

            var totalPendingAmount = await _context.RegistrationPayments
                .Where(p => p.Status == PaymentStatus.Pending)
                .SumAsync(p => p.Amount);

            var spouses = await _context.EventRegistrations.Where(r => r.Status == RegistrationStatus.Approved).SumAsync(r => r.SpouseCount);
            var children = await _context.EventRegistrations.Where(r => r.Status == RegistrationStatus.Approved).SumAsync(r => r.ChildCount);
            var guests = await _context.EventRegistrations.Where(r => r.Status == RegistrationStatus.Approved).SumAsync(r => r.GuestCount);
            var totalHeads = approvedReg + spouses + children + guests;

            // Batch distribution
            var batchData = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Where(r => r.AlumniProfile != null)
                .GroupBy(r => r.AlumniProfile!.PassingYear)
                .Select(g => new { Batch = g.Key, Count = g.Count() })
                .OrderBy(x => x.Batch)
                .ToListAsync();

            // T-Shirt size demand
            var tShirtData = await _context.EventRegistrations
                .GroupBy(r => r.TShirtSize)
                .Select(g => new { Size = g.Key, Count = g.Count() })
                .ToListAsync();

            // Recent 10 registrations
            var recentRegistrations = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .OrderByDescending(r => r.RegisteredAt)
                .Take(10)
                .ToListAsync();

            ViewBag.TotalReg = totalReg;
            ViewBag.ApprovedReg = approvedReg;
            ViewBag.PendingReg = pendingReg;
            ViewBag.RejectedReg = rejectedReg;
            ViewBag.TotalCollected = totalCollected;
            ViewBag.TotalPendingAmount = totalPendingAmount;
            ViewBag.TotalHeads = totalHeads;
            ViewBag.Spouses = spouses;
            ViewBag.Children = children;
            ViewBag.Guests = guests;

            ViewBag.BatchLabels = batchData.Select(b => $"Batch {b.Batch}").ToList();
            ViewBag.BatchCounts = batchData.Select(b => b.Count).ToList();

            ViewBag.TShirtLabels = tShirtData.Select(t => t.Size).ToList();
            ViewBag.TShirtCounts = tShirtData.Select(t => t.Count).ToList();

            return View(recentRegistrations);
        }

        public async Task<IActionResult> Registrations(int? batch, RegistrationStatus? status)
        {
            var query = _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .Include(r => r.ReunionEvent)
                .AsQueryable();

            if (batch.HasValue)
            {
                query = query.Where(r => r.AlumniProfile!.PassingYear == batch.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            var list = await query.OrderByDescending(r => r.RegisteredAt).ToListAsync();

            var distinctBatches = await _context.AlumniProfiles
                .Select(a => a.PassingYear)
                .Distinct()
                .OrderBy(b => b)
                .ToListAsync();

            ViewBag.Batches = distinctBatches;
            ViewBag.SelectedBatch = batch;
            ViewBag.SelectedStatus = status;

            return View(list);
        }

        public async Task<IActionResult> PaymentQueue(PaymentStatus? status = PaymentStatus.Pending)
        {
            var query = _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.ReunionEvent)
                .Include(p => p.ApprovedByUser)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            var payments = await query.OrderByDescending(p => p.SubmittedAt).ToListAsync();
            ViewBag.SelectedStatus = status;

            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.ReunionEvent)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(PaymentQueue));
            }

            var user = await _userManager.GetUserAsync(User);

            payment.Status = PaymentStatus.Approved;
            payment.ApprovedByUserId = user?.Id;
            payment.ApprovedAt = DateTime.UtcNow;

            if (payment.EventRegistration != null)
            {
                payment.EventRegistration.Status = RegistrationStatus.Approved;

                // Ensure signed QR code exists
                if (string.IsNullOrEmpty(payment.EventRegistration.QrCodeBase64))
                {
                    var token = _qrCodeService.GenerateSignedToken(
                        payment.EventRegistration.RegistrationNo,
                        payment.EventRegistration.Id,
                        payment.EventRegistration.AlumniProfile?.UserCode ?? "ALUMNI");

                    payment.EventRegistration.QrCodeToken = token;
                    payment.EventRegistration.QrCodeBase64 = _qrCodeService.GenerateQrCodeBase64(token);
                }

                // 1. Auto-Reconcile into Accounting Cash Book
                var incomeHead = await _context.AccountHeads
                    .FirstOrDefaultAsync(h => h.HeadName.Contains("Registration") && h.Type == AccountHeadType.Income)
                    ?? await _context.AccountHeads.FirstOrDefaultAsync(h => h.Type == AccountHeadType.Income);

                if (incomeHead != null)
                {
                    var countVouchers = await _context.CashEntries.CountAsync() + 1;
                    _context.CashEntries.Add(new CashEntry
                    {
                        VoucherNumber = $"V-INC-{DateTime.UtcNow.Year}-{countVouchers:D5}",
                        EntryDate = DateTime.UtcNow,
                        AccountHeadId = incomeHead.Id,
                        Amount = payment.Amount,
                        PaymentMode = payment.PaymentMode.ToString(),
                        Description = $"Auto-Reconciled Reunion Fee for {payment.EventRegistration.RegistrationNo} ({payment.EventRegistration.AlumniProfile?.NameEnglish}) TrxID: {payment.TransactionId}",
                        RelatedRegistrationId = payment.EventRegistration.Id,
                        CreatedByUserId = user?.Id
                    });
                }

                // 2. Dispatch Approval SMS
                if (payment.EventRegistration.AlumniProfile != null)
                {
                    var phone = payment.EventRegistration.AlumniProfile.ContactNumber;
                    var passUrl = $"{Request.Scheme}://{Request.Host}/Pass/ViewPass/{payment.EventRegistration.RegistrationNo}";

                    var placeholders = new Dictionary<string, string>
                    {
                        ["Name"] = payment.EventRegistration.AlumniProfile.NameEnglish,
                        ["TicketNo"] = payment.EventRegistration.RegistrationNo,
                        ["Amount"] = payment.Amount.ToString("N0"),
                        ["EventName"] = payment.EventRegistration.ReunionEvent?.EventTitle ?? "Reunion",
                        ["PassUrl"] = passUrl
                    };

                    await _smsService.SendTemplateSmsAsync("PaymentSMS", phone, placeholders);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Payment for {payment.EventRegistration?.RegistrationNo} approved successfully!";
            return RedirectToAction(nameof(PaymentQueue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment(int paymentId, string? remarks)
        {
            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(PaymentQueue));
            }

            var user = await _userManager.GetUserAsync(User);

            payment.Status = PaymentStatus.Rejected;
            payment.AdminRemarks = remarks ?? "Transaction ID could not be verified.";
            payment.ApprovedByUserId = user?.Id;
            payment.ApprovedAt = DateTime.UtcNow;

            if (payment.EventRegistration != null)
            {
                payment.EventRegistration.Status = RegistrationStatus.Rejected;
            }

            await _context.SaveChangesAsync();
            TempData["Warning"] = $"Payment for {payment.EventRegistration?.RegistrationNo} was rejected.";
            return RedirectToAction(nameof(PaymentQueue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReverifyJanataPayment(int paymentId)
        {
            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.ReunionEvent)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(PaymentQueue));
            }

            if (string.IsNullOrEmpty(payment.GatewayReferenceId))
            {
                TempData["Error"] = "This payment has no gateway reference ID recorded.";
                return RedirectToAction(nameof(PaymentQueue));
            }

            var verifyResult = await _janataPayService.VerifyPaymentAsync(payment.GatewayReferenceId, payment.GatewayTransactionToken ?? "");

            payment.GatewayStatus = verifyResult.TransactionStatus ?? verifyResult.TransactionStatusCode;
            payment.GatewayFtNumber = verifyResult.FtNumber;

            if (verifyResult.Success && verifyResult.Amount >= payment.Amount)
            {
                var user = await _userManager.GetUserAsync(User);
                payment.Status = PaymentStatus.Approved;
                payment.ApprovedByUserId = user?.Id;
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

                    if (string.IsNullOrEmpty(reg.QrCodeBase64))
                    {
                        var signedToken = _qrCodeService.GenerateSignedToken(
                            reg.RegistrationNo,
                            reg.Id,
                            reg.AlumniProfile?.UserCode ?? "ALUMNI");

                        reg.QrCodeToken = signedToken;
                        reg.QrCodeBase64 = _qrCodeService.GenerateQrCodeBase64(signedToken);
                    }

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
                            Description = $"Reconciled JanataPay Online for {reg.RegistrationNo} ({reg.AlumniProfile?.NameEnglish}) FT: {verifyResult.FtNumber}",
                            RelatedRegistrationId = reg.Id,
                            CreatedByUserId = user?.Id
                        });
                    }

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
                TempData["Success"] = $"Payment for {payment.EventRegistration?.RegistrationNo} was successfully verified and approved with JanataPay! (FT: {verifyResult.FtNumber})";
            }
            else
            {
                await _context.SaveChangesAsync();
                TempData["Warning"] = $"Gateway returned status '{verifyResult.TransactionStatus ?? "Incomplete"}' (Code: {verifyResult.TransactionStatusCode}). Not approved.";
            }

            return RedirectToAction(nameof(PaymentQueue));
        }
    }
}
