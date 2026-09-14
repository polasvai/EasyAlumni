using System.Text;
using EasyAlumni.Core.Enums;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using EasyAlumni.Web.ViewModels;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Accounts")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var approvedPayments = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Where(p => p.Status == PaymentStatus.Approved)
                .ToListAsync();

            var pendingPayments = await _context.RegistrationPayments
                .Where(p => p.Status == PaymentStatus.Pending)
                .ToListAsync();

            var totalExpenses = await _context.CashEntries
                .Include(c => c.AccountHead)
                .Where(c => c.AccountHead!.Type == AccountHeadType.Expense)
                .SumAsync(c => c.Amount);

            var approvedRegistrations = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Where(r => r.Status == RegistrationStatus.Approved)
                .ToListAsync();

            var totalCollected = approvedPayments.Sum(p => p.Amount);
            var pendingAmount = pendingPayments.Sum(p => p.Amount);

            var totalAlumni = approvedRegistrations.Count;
            var totalSpouses = approvedRegistrations.Sum(r => r.SpouseCount);
            var totalChildren = approvedRegistrations.Sum(r => r.ChildCount);
            var totalGuests = approvedRegistrations.Sum(r => r.GuestCount);
            var grandTotalHeads = approvedRegistrations.Sum(r => 1 + r.SpouseCount + r.ChildCount + r.GuestCount);

            var tShirtSizes = new Dictionary<string, int>
            {
                ["S"] = approvedRegistrations.Count(r => r.TShirtSize == "S"),
                ["M"] = approvedRegistrations.Count(r => r.TShirtSize == "M"),
                ["L"] = approvedRegistrations.Count(r => r.TShirtSize == "L"),
                ["XL"] = approvedRegistrations.Count(r => r.TShirtSize == "XL"),
                ["XXL"] = approvedRegistrations.Count(r => r.TShirtSize == "XXL"),
                ["XXXL"] = approvedRegistrations.Count(r => r.TShirtSize == "XXXL")
            };

            var topBatches = approvedRegistrations
                .GroupBy(r => r.AlumniProfile?.PassingYear ?? 0)
                .Select(g => new BatchParticipationSummary
                {
                    BatchYear = g.Key,
                    RegisteredCount = g.Count(),
                    TotalCollected = g.Sum(r => r.PaidAmount),
                    TotalHeads = g.Sum(r => 1 + r.SpouseCount + r.ChildCount + r.GuestCount)
                })
                .OrderByDescending(b => b.RegisteredCount)
                .Take(5)
                .ToList();

            var paymentChannels = approvedPayments
                .GroupBy(p => p.PaymentMode.ToString())
                .Select(g => new PaymentChannelSummary
                {
                    ChannelName = g.Key,
                    TransactionCount = g.Count(),
                    TotalAmount = g.Sum(p => p.Amount)
                })
                .OrderByDescending(c => c.TotalAmount)
                .ToList();

            var recentApproved = approvedPayments
                .OrderByDescending(p => p.SubmittedAt)
                .Take(6)
                .ToList();

            var vm = new ReportsSummaryViewModel
            {
                TotalCollectedRevenue = totalCollected,
                ApprovedPaymentsCount = approvedPayments.Count,
                PendingAmount = pendingAmount,
                PendingPaymentsCount = pendingPayments.Count,
                TotalExpenses = totalExpenses,
                NetSurplus = totalCollected - totalExpenses,
                TotalAlumni = totalAlumni,
                TotalSpouses = totalSpouses,
                TotalChildren = totalChildren,
                TotalGuests = totalGuests,
                GrandTotalCateringHeads = grandTotalHeads,
                TShirtSizeCounts = tShirtSizes,
                TopBatches = topBatches,
                PaymentChannels = paymentChannels,
                RecentApprovedPayments = recentApproved
            };

            return View(vm);
        }

        public async Task<IActionResult> CollectionReport(DateTime? fromDate, DateTime? toDate, int? batch, PaymentMode? mode)
        {
            var query = _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Where(p => p.Status == PaymentStatus.Approved)
                .AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(p => p.SubmittedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.SubmittedAt <= toDate.Value.AddDays(1).AddTicks(-1));

            if (batch.HasValue)
                query = query.Where(p => p.EventRegistration!.AlumniProfile!.PassingYear == batch.Value);

            if (mode.HasValue)
                query = query.Where(p => p.PaymentMode == mode.Value);

            var payments = await query.OrderByDescending(p => p.SubmittedAt).ToListAsync();
            var totalAmount = payments.Sum(p => p.Amount);

            var distinctBatches = await _context.AlumniProfiles
                .Select(a => a.PassingYear)
                .Distinct()
                .OrderBy(b => b)
                .ToListAsync();

            ViewBag.TotalAmount = totalAmount;
            ViewBag.Batches = distinctBatches;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.SelectedBatch = batch;
            ViewBag.SelectedMode = mode;

            return View(payments);
        }

        public async Task<IActionResult> CateringReport()
        {
            var approvedRegistrations = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Where(r => r.Status == RegistrationStatus.Approved)
                .ToListAsync();

            var batchSummaries = approvedRegistrations
                .GroupBy(r => r.AlumniProfile?.PassingYear ?? 0)
                .Select(g => new
                {
                    Batch = g.Key,
                    AlumniCount = g.Count(),
                    SpouseCount = g.Sum(r => r.SpouseCount),
                    ChildCount = g.Sum(r => r.ChildCount),
                    GuestCount = g.Sum(r => r.GuestCount),
                    TotalHeads = g.Sum(r => 1 + r.SpouseCount + r.ChildCount + r.GuestCount)
                })
                .OrderBy(b => b.Batch)
                .ToList();

            ViewBag.GrandTotalAlumni = approvedRegistrations.Count;
            ViewBag.GrandTotalSpouses = approvedRegistrations.Sum(r => r.SpouseCount);
            ViewBag.GrandTotalChildren = approvedRegistrations.Sum(r => r.ChildCount);
            ViewBag.GrandTotalGuests = approvedRegistrations.Sum(r => r.GuestCount);
            ViewBag.GrandTotalHeads = approvedRegistrations.Sum(r => 1 + r.SpouseCount + r.ChildCount + r.GuestCount);

            return View(batchSummaries);
        }

        public async Task<IActionResult> TShirtReport()
        {
            var tShirtItem = await _context.GiftItems
                .Include(g => g.SizeStocks)
                .FirstOrDefaultAsync(g => g.IsSizeSpecific && g.ItemName.Contains("T-Shirt"));

            var actualRegistrations = await _context.EventRegistrations
                .Where(r => r.Status == RegistrationStatus.Approved)
                .GroupBy(r => r.TShirtSize)
                .Select(g => new { Size = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Size, x => x.Count);

            ViewBag.RegisteredDemands = actualRegistrations;
            return View(tShirtItem);
        }

        public async Task<IActionResult> FinancialStatement()
        {
            var incomes = await _context.CashEntries
                .Include(c => c.AccountHead)
                .Where(c => c.AccountHead!.Type == AccountHeadType.Income)
                .GroupBy(c => c.AccountHead!.HeadName)
                .Select(g => new { Head = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var expenses = await _context.CashEntries
                .Include(c => c.AccountHead)
                .Where(c => c.AccountHead!.Type == AccountHeadType.Expense)
                .GroupBy(c => c.AccountHead!.HeadName)
                .Select(g => new { Head = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var totalIncome = incomes.Sum(i => i.Total);
            var totalExpense = expenses.Sum(e => e.Total);
            var netBalance = totalIncome - totalExpense;

            ViewBag.TotalIncome = totalIncome;
            ViewBag.TotalExpense = totalExpense;
            ViewBag.NetBalance = netBalance;
            ViewBag.Incomes = incomes;
            ViewBag.Expenses = expenses;

            return View();
        }

        public async Task<IActionResult> ExportCollectionCsv()
        {
            var payments = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Where(p => p.Status == PaymentStatus.Approved)
                .OrderByDescending(p => p.SubmittedAt)
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("TicketNo,UserCode,AlumnusName,Batch,Mobile,PaymentMethod,TrxID,SenderNumber,AmountBDT,Date");

            foreach (var p in payments)
            {
                var reg = p.EventRegistration;
                var prof = reg?.AlumniProfile;
                builder.AppendLine($"\"{reg?.RegistrationNo}\",\"{prof?.UserCode}\",\"{prof?.NameEnglish}\",\"{prof?.PassingYear}\",\"{prof?.ContactNumber}\",\"{p.PaymentMode}\",\"{p.TransactionId}\",\"{p.SenderNumber}\",\"{p.Amount:F2}\",\"{p.SubmittedAt:yyyy-MM-dd}\"");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"CollectionReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
    }
}
