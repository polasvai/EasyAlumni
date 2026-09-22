using System.Text;
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
    public class AdminDonationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISmsService _smsService;
        private readonly IJanataPayService _janataPayService;
        private readonly ILogger<AdminDonationsController> _logger;

        public AdminDonationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ISmsService smsService,
            IJanataPayService janataPayService,
            ILogger<AdminDonationsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _smsService = smsService;
            _janataPayService = janataPayService;
            _logger = logger;
        }

        // GET: /AdminDonations
        public async Task<IActionResult> Index(string? search, string? status, string? paymentMode, int page = 1)
        {
            var query = _context.Donations
                .Include(d => d.ApprovedByUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(d => d.DonationTrackingNo.Contains(s)
                                      || d.DonorName.Contains(s)
                                      || d.DonorPhone.Contains(s)
                                      || (d.TransactionId != null && d.TransactionId.Contains(s))
                                      || (d.GatewayFtNumber != null && d.GatewayFtNumber.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PaymentStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(d => d.Status == parsedStatus);
            }

            if (!string.IsNullOrWhiteSpace(paymentMode) && Enum.TryParse<PaymentMode>(paymentMode, true, out var parsedMode))
            {
                query = query.Where(d => d.PaymentMode == parsedMode);
            }

            // Summary metrics
            ViewBag.TotalDonationsCount = await _context.Donations.CountAsync();
            ViewBag.TotalApprovedAmount = await _context.Donations
                .Where(d => d.Status == PaymentStatus.Approved)
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;
            ViewBag.TotalPendingCount = await _context.Donations
                .CountAsync(d => d.Status == PaymentStatus.Pending);
            ViewBag.TotalPendingAmount = await _context.Donations
                .Where(d => d.Status == PaymentStatus.Pending)
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;

            const int pageSize = 20;
            var totalItems = await query.CountAsync();
            var donations = await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.PaymentMode = paymentMode;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.TotalItems = totalItems;

            return View(donations);
        }

        // POST: /AdminDonations/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var donation = await _context.Donations.FirstOrDefaultAsync(d => d.Id == id);
            if (donation == null)
            {
                TempData["Error"] = "Donation record not found.";
                return RedirectToAction(nameof(Index));
            }

            if (donation.Status == PaymentStatus.Approved)
            {
                TempData["Warning"] = $"Donation {donation.DonationTrackingNo} is already approved.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);

            donation.Status = PaymentStatus.Approved;
            donation.ApprovedAt = DateTime.UtcNow;
            donation.ApprovedByUserId = user?.Id;

            // Auto-Reconcile into Accounting Cash Book under INC-DON
            var donHead = await _context.AccountHeads
                .FirstOrDefaultAsync(h => h.Code == "INC-DON" || h.HeadName.Contains("Donation") || h.HeadName.Contains("Donor"))
                ?? await _context.AccountHeads.FirstOrDefaultAsync(h => h.Type == AccountHeadType.Income);

            if (donHead != null)
            {
                var countVouchers = await _context.CashEntries.CountAsync() + 1;
                var entryDesc = $"Manual Donation Approval {donation.DonationTrackingNo} from {donation.DonorName} TrxID: {donation.TransactionId}";
                if (!string.IsNullOrWhiteSpace(donation.Remarks))
                {
                    entryDesc += $" - {donation.Remarks}";
                }

                _context.CashEntries.Add(new CashEntry
                {
                    VoucherNumber = $"V-DON-{DateTime.UtcNow.Year}-{countVouchers:D5}",
                    EntryDate = DateTime.UtcNow,
                    AccountHeadId = donHead.Id,
                    Amount = donation.Amount,
                    PaymentMode = donation.PaymentMode.ToString(),
                    Description = entryDesc,
                    CreatedByUserId = user?.Id
                });
            }

            // Dispatch Approval SMS
            if (!string.IsNullOrWhiteSpace(donation.DonorPhone))
            {
                var receiptUrl = $"{Request.Scheme}://{Request.Host}/Donation/Receipt/{donation.DonationTrackingNo}";
                var smsMsg = $"Dear {donation.DonorName}, your donation of BDT {donation.Amount:N0} to SNHGHS Alumni has been verified & approved. Digital Receipt: {receiptUrl}";
                try
                {
                    await _smsService.SendSmsAsync(donation.DonorPhone, smsMsg);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send donation approval SMS to {Phone}", donation.DonorPhone);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Donation {donation.DonationTrackingNo} approved successfully and recorded in Cash Book!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminDonations/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? remarks)
        {
            var donation = await _context.Donations.FirstOrDefaultAsync(d => d.Id == id);
            if (donation == null)
            {
                TempData["Error"] = "Donation record not found.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            donation.Status = PaymentStatus.Rejected;
            donation.Remarks = string.IsNullOrWhiteSpace(remarks) ? donation.Remarks : $"{donation.Remarks} [Rejected: {remarks}]";
            donation.ApprovedByUserId = user?.Id;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Donation {donation.DonationTrackingNo} has been marked as Rejected.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminDonations/VerifyJanataPay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyJanataPay(int id)
        {
            var donation = await _context.Donations.FirstOrDefaultAsync(d => d.Id == id);
            if (donation == null)
            {
                TempData["Error"] = "Donation record not found.";
                return RedirectToAction(nameof(Index));
            }

            var refId = donation.GatewayReferenceId ?? donation.DonationTrackingNo;
            var token = donation.TransactionId ?? "";

            var verifyResult = await _janataPayService.VerifyPaymentAsync(refId, token);

            donation.GatewayFtNumber = verifyResult.FtNumber;

            if (verifyResult.Success && verifyResult.Amount >= donation.Amount)
            {
                var user = await _userManager.GetUserAsync(User);
                donation.Status = PaymentStatus.Approved;
                donation.ApprovedAt = DateTime.UtcNow;
                donation.ApprovedByUserId = user?.Id;
                if (!string.IsNullOrWhiteSpace(verifyResult.FtNumber))
                {
                    donation.TransactionId = verifyResult.FtNumber;
                }

                // Add to Cash Book
                var donHead = await _context.AccountHeads
                    .FirstOrDefaultAsync(h => h.Code == "INC-DON" || h.HeadName.Contains("Donation") || h.HeadName.Contains("Donor"))
                    ?? await _context.AccountHeads.FirstOrDefaultAsync(h => h.Type == AccountHeadType.Income);

                if (donHead != null)
                {
                    var countVouchers = await _context.CashEntries.CountAsync() + 1;
                    _context.CashEntries.Add(new CashEntry
                    {
                        VoucherNumber = $"V-DON-{DateTime.UtcNow.Year}-{countVouchers:D5}",
                        EntryDate = DateTime.UtcNow,
                        AccountHeadId = donHead.Id,
                        Amount = donation.Amount,
                        PaymentMode = "JanataPay",
                        Description = $"Online Donation {donation.DonationTrackingNo} verified from {donation.DonorName} (FT: {verifyResult.FtNumber})",
                        CreatedByUserId = user?.Id
                    });
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"JanataPay Payment verified successfully! Status: {verifyResult.TransactionStatus}, FT: {verifyResult.FtNumber}";
            }
            else
            {
                await _context.SaveChangesAsync();
                TempData["Error"] = $"JanataPay Gateway did not confirm payment. Status: {verifyResult.TransactionStatus ?? "Unknown"} ({verifyResult.TransactionStatusCode})";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /AdminDonations/ExportCsv
        public async Task<IActionResult> ExportCsv()
        {
            var donations = await _context.Donations
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("TrackingNo,DonorName,Phone,Email,BatchYear,Amount,Purpose,PaymentMode,Status,TransactionId,GatewayFt,Anonymous,CreatedAt,ApprovedAt");

            foreach (var d in donations)
            {
                sb.AppendLine($"\"{d.DonationTrackingNo}\",\"{d.DonorName.Replace("\"", "\"\"")}\",\"{d.DonorPhone}\",\"{d.DonorEmail}\",\"{d.BatchYear}\",{d.Amount},\"{d.DonationPurpose.Replace("\"", "\"\"")}\",\"{d.PaymentMode}\",\"{d.Status}\",\"{d.TransactionId}\",\"{d.GatewayFtNumber}\",{d.IsAnonymous},\"{d.CreatedAt:yyyy-MM-dd HH:mm}\",\"{d.ApprovedAt:yyyy-MM-dd HH:mm}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var output = Encoding.UTF8.GetPreamble().Concat(bytes).ToArray(); // Include BOM for Excel Bangla/English compatibility
            return File(output, "text/csv", $"Donations_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
        }
    }
}
