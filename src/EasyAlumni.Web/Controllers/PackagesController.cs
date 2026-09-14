using EasyAlumni.Core.Entities;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class PackagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PackagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var activeEvent = await _context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive)
                ?? await _context.ReunionEvents.FirstOrDefaultAsync();

            if (activeEvent == null)
            {
                TempData["Error"] = "No active reunion event configured.";
                return View(new List<RegistrationPackage>());
            }

            ViewBag.ActiveEvent = activeEvent;

            var packages = await _context.RegistrationPackages
                .Where(p => p.ReunionEventId == activeEvent.Id)
                .Include(p => p.Registrations)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            return View(packages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePackage(RegistrationPackage model)
        {
            if (string.IsNullOrWhiteSpace(model.PackageName) || model.Fee <= 0)
            {
                TempData["Error"] = "Valid package name and positive fee amount are required.";
                return RedirectToAction(nameof(Index));
            }

            var activeEvent = await _context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive)
                ?? await _context.ReunionEvents.FirstOrDefaultAsync();

            if (activeEvent == null)
            {
                TempData["Error"] = "No active event found to attach package to.";
                return RedirectToAction(nameof(Index));
            }

            model.ReunionEventId = activeEvent.Id;
            model.PackageName = model.PackageName.Trim();
            model.PackageNameBangla = model.PackageNameBangla?.Trim();
            model.Description = model.Description?.Trim();
            model.BadgeText = model.BadgeText?.Trim();
            model.CreatedAt = DateTime.UtcNow;

            _context.RegistrationPackages.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Package '{model.PackageName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPackage(RegistrationPackage model)
        {
            var existing = await _context.RegistrationPackages.FindAsync(model.Id);
            if (existing == null)
            {
                TempData["Error"] = "Package not found.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.PackageName) || model.Fee <= 0)
            {
                TempData["Error"] = "Valid package name and positive fee amount are required.";
                return RedirectToAction(nameof(Index));
            }

            existing.PackageName = model.PackageName.Trim();
            existing.PackageNameBangla = model.PackageNameBangla?.Trim();
            existing.Description = model.Description?.Trim();
            existing.Fee = model.Fee;
            existing.IncludesTShirt = model.IncludesTShirt;
            existing.IncludesKitBag = model.IncludesKitBag;
            existing.IncludesFood = model.IncludesFood;
            existing.IncludesRaffle = model.IncludesRaffle;
            existing.IncludedGuests = model.IncludedGuests;
            existing.DisplayOrder = model.DisplayOrder;
            existing.IsFeatured = model.IsFeatured;
            existing.IsActive = model.IsActive;
            existing.BadgeText = model.BadgeText?.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Package '{existing.PackageName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var pkg = await _context.RegistrationPackages
                .Include(p => p.Registrations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pkg == null)
            {
                TempData["Error"] = "Package not found.";
                return RedirectToAction(nameof(Index));
            }

            if (pkg.Registrations != null && pkg.Registrations.Any())
            {
                pkg.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Warning"] = $"Package '{pkg.PackageName}' has {pkg.Registrations.Count} existing registration(s) and was deactivated instead of permanently deleted.";
            }
            else
            {
                _context.RegistrationPackages.Remove(pkg);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Package '{pkg.PackageName}' deleted permanently.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
