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
    public class AccountingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountingController(
            ApplicationDbContext context,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _fileStorage = fileStorage;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var totalIncome = await _context.CashEntries
                .Include(c => c.AccountHead)
                .Where(c => c.AccountHead!.Type == AccountHeadType.Income)
                .SumAsync(c => c.Amount);

            var totalExpense = await _context.CashEntries
                .Include(c => c.AccountHead)
                .Where(c => c.AccountHead!.Type == AccountHeadType.Expense)
                .SumAsync(c => c.Amount);

            var netBalance = totalIncome - totalExpense;

            // Budget vs Actual
            var budgetHeads = await _context.BudgetHeads
                .Include(b => b.ReunionEvent)
                .ToListAsync();

            // Recent 30 Cash Entries
            var entries = await _context.CashEntries
                .Include(c => c.AccountHead)
                .Include(c => c.CreatedByUser)
                .OrderByDescending(c => c.EntryDate)
                .Take(30)
                .ToListAsync();

            ViewBag.TotalIncome = totalIncome;
            ViewBag.TotalExpense = totalExpense;
            ViewBag.NetBalance = netBalance;
            ViewBag.BudgetHeads = budgetHeads;
            ViewBag.AccountHeads = await _context.AccountHeads.Where(h => h.IsActive).OrderBy(h => h.Type).ThenBy(h => h.HeadName).ToListAsync();

            return View(entries);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVoucher(
            int accountHeadId,
            decimal amount,
            string paymentMode,
            string? description,
            IFormFile? receiptFile,
            int? budgetHeadId)
        {
            if (amount <= 0 || accountHeadId <= 0)
            {
                TempData["Error"] = "Invalid voucher amount or account head.";
                return RedirectToAction(nameof(Index));
            }

            var accountHead = await _context.AccountHeads.FindAsync(accountHeadId);
            if (accountHead == null)
            {
                TempData["Error"] = "Account head not found.";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var count = await _context.CashEntries.CountAsync() + 1;
            var prefix = accountHead.Type == AccountHeadType.Income ? "V-INC" : "V-EXP";
            var voucherNo = $"{prefix}-{DateTime.UtcNow.Year}-{count:D5}";

            string? receiptPath = null;
            if (receiptFile != null && receiptFile.Length > 0)
            {
                using var stream = receiptFile.OpenReadStream();
                receiptPath = await _fileStorage.SaveFileAsync(stream, receiptFile.FileName, "Vouchers", new[] { ".jpg", ".jpeg", ".png", ".pdf" }, 5242880);
            }

            var entry = new CashEntry
            {
                VoucherNumber = voucherNo,
                EntryDate = DateTime.UtcNow,
                AccountHeadId = accountHeadId,
                Amount = amount,
                PaymentMode = paymentMode,
                Description = description?.Trim(),
                ReceiptAttachmentPath = receiptPath,
                CreatedByUserId = currentUser?.Id
            };

            _context.CashEntries.Add(entry);

            // If expense and budget head linked, update budget actual spent
            if (accountHead.Type == AccountHeadType.Expense && budgetHeadId.HasValue && budgetHeadId.Value > 0)
            {
                var budgetHead = await _context.BudgetHeads.FindAsync(budgetHeadId.Value);
                if (budgetHead != null)
                {
                    budgetHead.ActualSpent += amount;
                    _context.BudgetEntries.Add(new BudgetEntry
                    {
                        BudgetHeadId = budgetHead.Id,
                        ExpenseAmount = amount,
                        VoucherReference = voucherNo,
                        ExpenseDate = DateTime.UtcNow,
                        Description = description
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Voucher {voucherNo} for ৳ {amount:N0} recorded successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBudgetHead(int budgetHeadId, decimal allocatedAmount)
        {
            var head = await _context.BudgetHeads.FindAsync(budgetHeadId);
            if (head == null)
            {
                TempData["Error"] = "Budget head not found.";
                return RedirectToAction(nameof(Index));
            }

            head.AllocatedAmount = allocatedAmount;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Budget allocation for '{head.HeadName}' updated to ৳ {allocatedAmount:N0}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditVoucher(
            int id,
            int accountHeadId,
            decimal amount,
            string paymentMode,
            string? description,
            IFormFile? receiptFile)
        {
            var entry = await _context.CashEntries.FindAsync(id);
            if (entry == null)
            {
                TempData["Error"] = "Voucher not found.";
                return RedirectToAction(nameof(Index));
            }

            if (amount <= 0 || accountHeadId <= 0)
            {
                TempData["Error"] = "Invalid amount or account head.";
                return RedirectToAction(nameof(Index));
            }

            var accountHead = await _context.AccountHeads.FindAsync(accountHeadId);
            if (accountHead == null)
            {
                TempData["Error"] = "Account head not found.";
                return RedirectToAction(nameof(Index));
            }

            var diff = amount - entry.Amount;

            // Adjust linked budget entry if existing
            var budgetEntry = await _context.BudgetEntries
                .Include(b => b.BudgetHead)
                .FirstOrDefaultAsync(b => b.VoucherReference == entry.VoucherNumber);

            if (budgetEntry != null && budgetEntry.BudgetHead != null)
            {
                budgetEntry.ExpenseAmount = amount;
                budgetEntry.Description = description;
                budgetEntry.BudgetHead.ActualSpent += diff;
            }

            entry.AccountHeadId = accountHeadId;
            entry.Amount = amount;
            entry.PaymentMode = paymentMode;
            entry.Description = description?.Trim();

            if (receiptFile != null && receiptFile.Length > 0)
            {
                using var stream = receiptFile.OpenReadStream();
                entry.ReceiptAttachmentPath = await _fileStorage.SaveFileAsync(stream, receiptFile.FileName, "Vouchers", new[] { ".jpg", ".jpeg", ".png", ".pdf" }, 5242880);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Voucher '{entry.VoucherNumber}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            var entry = await _context.CashEntries.FindAsync(id);
            if (entry == null)
            {
                TempData["Error"] = "Voucher not found.";
                return RedirectToAction(nameof(Index));
            }

            var vNo = entry.VoucherNumber;
            var vAmt = entry.Amount;

            // Adjust linked budget entry if any
            var budgetEntry = await _context.BudgetEntries
                .Include(b => b.BudgetHead)
                .FirstOrDefaultAsync(b => b.VoucherReference == entry.VoucherNumber);

            if (budgetEntry != null)
            {
                if (budgetEntry.BudgetHead != null)
                {
                    budgetEntry.BudgetHead.ActualSpent -= vAmt;
                    if (budgetEntry.BudgetHead.ActualSpent < 0)
                        budgetEntry.BudgetHead.ActualSpent = 0;
                }
                _context.BudgetEntries.Remove(budgetEntry);
            }

            _context.CashEntries.Remove(entry);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Voucher '{vNo}' (৳ {vAmt:N0}) has been deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
