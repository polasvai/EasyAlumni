using EasyAlumni.Core.Entities;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class DynamicSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DynamicSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string activeTab = "packages")
        {
            var activeEvent = await _context.ReunionEvents
                .FirstOrDefaultAsync(e => e.IsActive)
                ?? await _context.ReunionEvents.FirstOrDefaultAsync();

            if (activeEvent == null)
            {
                TempData["Error"] = "No active reunion event found.";
                return RedirectToAction("Index", "Admin");
            }

            ViewBag.ActiveEvent = activeEvent;
            ViewBag.ActiveTab = activeTab;

            // 1. Packages & Gift Inclusions
            var packages = await _context.RegistrationPackages
                .Where(p => p.ReunionEventId == activeEvent.Id)
                .Include(p => p.PackageGiftItems)
                    .ThenInclude(pg => pg.GiftItem)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            // 2. All Active Gift Items for Checkbox mapping
            var allGiftItems = await _context.GiftItems
                .Where(g => g.IsActive)
                .Include(g => g.SizeStocks)
                .OrderBy(g => g.ItemName)
                .ToListAsync();

            // 3. Dynamic Guest Categories
            var guestCategories = await _context.GuestCategories
                .Where(g => g.ReunionEventId == activeEvent.Id)
                .OrderBy(g => g.DisplayOrder)
                .ToListAsync();

            // 4. Dynamic Custom Questions
            var customQuestions = await _context.EventCustomQuestions
                .Where(q => q.ReunionEventId == activeEvent.Id)
                .OrderBy(q => q.DisplayOrder)
                .ToListAsync();

            ViewBag.Packages = packages;
            ViewBag.AllGiftItems = allGiftItems;
            ViewBag.GuestCategories = guestCategories;
            ViewBag.CustomQuestions = customQuestions;

            return View();
        }

        #region Package Rules & Gift Inclusions

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePackageRules(int packageId, int? minPassingYear, int? maxPassingYear, List<int>? selectedGiftItemIds)
        {
            var package = await _context.RegistrationPackages
                .Include(p => p.PackageGiftItems)
                .FirstOrDefaultAsync(p => p.Id == packageId);

            if (package == null)
            {
                TempData["Error"] = "Package not found.";
                return RedirectToAction(nameof(Index), new { activeTab = "packages" });
            }

            package.MinPassingYear = minPassingYear;
            package.MaxPassingYear = maxPassingYear;

            // Synchronize PackageGiftItems
            selectedGiftItemIds ??= new List<int>();

            // Remove unselected
            var toRemove = package.PackageGiftItems
                .Where(pg => !selectedGiftItemIds.Contains(pg.GiftItemId))
                .ToList();
            foreach (var item in toRemove)
            {
                _context.PackageGiftItems.Remove(item);
            }

            // Add newly selected
            var existingGiftIds = package.PackageGiftItems.Select(pg => pg.GiftItemId).ToHashSet();
            foreach (var giftId in selectedGiftItemIds)
            {
                if (!existingGiftIds.Contains(giftId))
                {
                    _context.PackageGiftItems.Add(new PackageGiftItem
                    {
                        RegistrationPackageId = package.Id,
                        GiftItemId = giftId
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Rules and gift items for package '{package.PackageName}' updated successfully.";
            return RedirectToAction(nameof(Index), new { activeTab = "packages" });
        }

        #endregion

        #region Guest Categories Management

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddGuestCategory(GuestCategory model)
        {
            if (string.IsNullOrWhiteSpace(model.CategoryName) || model.Fee < 0)
            {
                TempData["Error"] = "Valid category name and non-negative fee are required.";
                return RedirectToAction(nameof(Index), new { activeTab = "guests" });
            }

            var activeEvent = await _context.ReunionEvents.FirstOrDefaultAsync(e => e.IsActive)
                ?? await _context.ReunionEvents.FirstOrDefaultAsync();

            if (activeEvent == null)
            {
                TempData["Error"] = "Active event required.";
                return RedirectToAction(nameof(Index), new { activeTab = "guests" });
            }

            model.ReunionEventId = activeEvent.Id;
            model.CategoryName = model.CategoryName.Trim();
            model.CategoryNameBangla = model.CategoryNameBangla?.Trim();
            model.EligibilityRules = model.EligibilityRules?.Trim();
            model.GenderRestriction = string.IsNullOrWhiteSpace(model.GenderRestriction) ? "None" : model.GenderRestriction;

            _context.GuestCategories.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Guest category '{model.CategoryName}' added successfully.";
            return RedirectToAction(nameof(Index), new { activeTab = "guests" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGuestCategory(GuestCategory model)
        {
            var existing = await _context.GuestCategories.FindAsync(model.Id);
            if (existing == null)
            {
                TempData["Error"] = "Guest category not found.";
                return RedirectToAction(nameof(Index), new { activeTab = "guests" });
            }

            existing.CategoryName = model.CategoryName.Trim();
            existing.CategoryNameBangla = model.CategoryNameBangla?.Trim();
            existing.Fee = model.Fee;
            existing.EligibilityRules = model.EligibilityRules?.Trim();
            existing.MaxAge = model.MaxAge;
            existing.MinAge = model.MinAge;
            existing.GenderRestriction = model.GenderRestriction ?? "None";
            existing.MaxAllowed = model.MaxAllowed;
            existing.DisplayOrder = model.DisplayOrder;
            existing.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Guest category '{existing.CategoryName}' updated successfully.";
            return RedirectToAction(nameof(Index), new { activeTab = "guests" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGuestCategory(int id)
        {
            var category = await _context.GuestCategories
                .Include(c => c.Guests)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction(nameof(Index), new { activeTab = "guests" });
            }

            if (category.Guests.Any())
            {
                category.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Category '{category.CategoryName}' has registered guests and was deactivated instead of deleted.";
            }
            else
            {
                _context.GuestCategories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Category '{category.CategoryName}' deleted successfully.";
            }

            return RedirectToAction(nameof(Index), new { activeTab = "guests" });
        }

        #endregion

        #region Custom Questions Management

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCustomQuestion(EventCustomQuestion model)
        {
            if (string.IsNullOrWhiteSpace(model.QuestionText))
            {
                TempData["Error"] = "Question text is required.";
                return RedirectToAction(nameof(Index), new { activeTab = "questions" });
            }

            var activeEvent = await _context.ReunionEvents.FirstOrDefaultAsync(e => e.IsActive)
                ?? await _context.ReunionEvents.FirstOrDefaultAsync();

            if (activeEvent == null)
            {
                TempData["Error"] = "Active event required.";
                return RedirectToAction(nameof(Index), new { activeTab = "questions" });
            }

            model.ReunionEventId = activeEvent.Id;
            model.QuestionText = model.QuestionText.Trim();
            model.QuestionTextBangla = model.QuestionTextBangla?.Trim();
            model.SubQuestionText = model.SubQuestionText?.Trim();

            _context.EventCustomQuestions.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Custom question added successfully.";
            return RedirectToAction(nameof(Index), new { activeTab = "questions" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomQuestion(EventCustomQuestion model)
        {
            var existing = await _context.EventCustomQuestions.FindAsync(model.Id);
            if (existing == null)
            {
                TempData["Error"] = "Question not found.";
                return RedirectToAction(nameof(Index), new { activeTab = "questions" });
            }

            existing.QuestionText = model.QuestionText.Trim();
            existing.QuestionTextBangla = model.QuestionTextBangla?.Trim();
            existing.FieldType = model.FieldType;
            existing.SubQuestionText = model.SubQuestionText?.Trim();
            existing.IsRequired = model.IsRequired;
            existing.DisplayOrder = model.DisplayOrder;
            existing.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Custom question updated successfully.";
            return RedirectToAction(nameof(Index), new { activeTab = "questions" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomQuestion(int id)
        {
            var question = await _context.EventCustomQuestions.FindAsync(id);
            if (question == null)
            {
                TempData["Error"] = "Question not found.";
                return RedirectToAction(nameof(Index), new { activeTab = "questions" });
            }

            question.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Custom question deactivated/deleted successfully.";
            return RedirectToAction(nameof(Index), new { activeTab = "questions" });
        }

        #endregion

        #region Dynamic Gift Sizes Management

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddGiftSize(int giftItemId, string sizeName, int initialStock)
        {
            if (string.IsNullOrWhiteSpace(sizeName))
            {
                TempData["Error"] = "Size label/name is required.";
                return RedirectToAction(nameof(Index), new { activeTab = "sizes" });
            }

            var item = await _context.GiftItems
                .Include(g => g.SizeStocks)
                .FirstOrDefaultAsync(g => g.Id == giftItemId);

            if (item == null)
            {
                TempData["Error"] = "Gift item not found.";
                return RedirectToAction(nameof(Index), new { activeTab = "sizes" });
            }

            item.IsSizeSpecific = true; // Ensure size-specific is true
            var sizeUpper = sizeName.Trim().ToUpperInvariant();

            if (item.SizeStocks.Any(s => s.SizeName.Equals(sizeUpper, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = $"Size '{sizeUpper}' already exists for this item.";
                return RedirectToAction(nameof(Index), new { activeTab = "sizes" });
            }

            item.SizeStocks.Add(new GiftItemSizeStock
            {
                SizeName = sizeUpper,
                TotalStock = Math.Max(0, initialStock)
            });

            item.TotalStockQuantity = item.SizeStocks.Sum(s => s.TotalStock);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Size '{sizeUpper}' added to '{item.ItemName}'.";
            return RedirectToAction(nameof(Index), new { activeTab = "sizes" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGiftSize(int sizeStockId)
        {
            var sizeStock = await _context.GiftItemSizeStocks
                .Include(s => s.GiftItem)
                .FirstOrDefaultAsync(s => s.Id == sizeStockId);

            if (sizeStock == null)
            {
                TempData["Error"] = "Size variant not found.";
                return RedirectToAction(nameof(Index), new { activeTab = "sizes" });
            }

            if (sizeStock.DistributedStock > 0)
            {
                TempData["Error"] = $"Cannot delete size '{sizeStock.SizeName}': {sizeStock.DistributedStock} units have already been distributed at kiosk.";
                return RedirectToAction(nameof(Index), new { activeTab = "sizes" });
            }

            var parent = sizeStock.GiftItem;
            _context.GiftItemSizeStocks.Remove(sizeStock);
            await _context.SaveChangesAsync();

            if (parent != null)
            {
                parent.TotalStockQuantity = await _context.GiftItemSizeStocks
                    .Where(s => s.GiftItemId == parent.Id)
                    .SumAsync(s => s.TotalStock);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"Size '{sizeStock.SizeName}' removed successfully.";
            return RedirectToAction(nameof(Index), new { activeTab = "sizes" });
        }

        #endregion
    }
}
