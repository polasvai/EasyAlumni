using EasyAlumni.Core.Entities;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Accounts")]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.GiftItems
                .Where(g => g.IsActive)
                .Include(g => g.SizeStocks)
                .ToListAsync();

            var totalProcured = items.Sum(i => i.TotalStockQuantity);
            var totalDistributed = items.Sum(i => i.DistributedQuantity);
            var remaining = totalProcured - totalDistributed;

            ViewBag.TotalProcured = totalProcured;
            ViewBag.TotalDistributed = totalDistributed;
            ViewBag.Remaining = remaining;

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditItem(int id, string itemName, string? description, int? totalStockQuantity)
        {
            var item = await _context.GiftItems.FindAsync(id);
            if (item == null || !item.IsActive)
            {
                TempData["Error"] = "Gift item not found.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                TempData["Error"] = "Item name is required.";
                return RedirectToAction(nameof(Index));
            }

            item.ItemName = itemName.Trim();
            item.Description = description?.Trim();

            if (!item.IsSizeSpecific && totalStockQuantity.HasValue)
            {
                if (totalStockQuantity.Value < item.DistributedQuantity)
                {
                    TempData["Error"] = $"Total stock cannot be less than already distributed units ({item.DistributedQuantity}).";
                    return RedirectToAction(nameof(Index));
                }
                item.TotalStockQuantity = totalStockQuantity.Value;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Gift item '{item.ItemName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.GiftItems.FindAsync(id);
            if (item == null || !item.IsActive)
            {
                TempData["Error"] = "Gift item not found.";
                return RedirectToAction(nameof(Index));
            }

            item.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Gift item '{item.ItemName}' was deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(string itemName, string? description, int initialStock, bool isSizeSpecific)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                TempData["Error"] = "Item name is required.";
                return RedirectToAction(nameof(Index));
            }

            var item = new GiftItem
            {
                ItemName = itemName.Trim(),
                Description = description?.Trim(),
                TotalStockQuantity = initialStock,
                IsSizeSpecific = isSizeSpecific
            };

            if (isSizeSpecific)
            {
                var defaultSizes = new[] { "S", "M", "L", "XL", "XXL", "XXXL" };
                var perSize = initialStock / defaultSizes.Length;
                foreach (var size in defaultSizes)
                {
                    item.SizeStocks.Add(new GiftItemSizeStock
                    {
                        SizeName = size,
                        TotalStock = perSize
                    });
                }
            }

            _context.GiftItems.Add(item);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Gift item '{itemName}' added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStock(int itemId, int quantity)
        {
            var item = await _context.GiftItems.FindAsync(itemId);
            if (item == null)
            {
                TempData["Error"] = "Item not found.";
                return RedirectToAction(nameof(Index));
            }

            item.TotalStockQuantity += quantity;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Added {quantity} units to '{item.ItemName}'. Total Stock: {item.TotalStockQuantity}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSizeStock(int sizeStockId, int totalStock)
        {
            var sizeStock = await _context.GiftItemSizeStocks
                .Include(s => s.GiftItem)
                .FirstOrDefaultAsync(s => s.Id == sizeStockId);

            if (sizeStock == null)
            {
                TempData["Error"] = "Size stock not found.";
                return RedirectToAction(nameof(Index));
            }

            sizeStock.TotalStock = totalStock;

            // Recalculate parent item total
            if (sizeStock.GiftItem != null)
            {
                var allSizes = await _context.GiftItemSizeStocks
                    .Where(s => s.GiftItemId == sizeStock.GiftItemId && s.Id != sizeStockId)
                    .SumAsync(s => s.TotalStock);

                sizeStock.GiftItem.TotalStockQuantity = allSizes + totalStock;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Stock for size {sizeStock.SizeName} updated to {totalStock}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Distributions()
        {
            var distributions = await _context.GiftDistributions
                .Include(d => d.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Include(d => d.GiftItem)
                .Include(d => d.VolunteerUser)
                .OrderByDescending(d => d.DistributedAt)
                .Take(500)
                .ToListAsync();

            return View(distributions);
        }
    }
}
