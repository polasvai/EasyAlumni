using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using EasyAlumni.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class GalleryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

        public GalleryController(ApplicationDbContext context, IFileStorageService fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var images = await _context.GalleryImages
                .Include(g => g.ReunionEvent)
                .OrderBy(g => g.DisplayOrder)
                .ThenByDescending(g => g.Id)
                .ToListAsync();

            return View(images);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(GalleryImageUploadViewModel vm)
        {
            if (vm.ImageFile == null && string.IsNullOrWhiteSpace(vm.ImageUrl))
            {
                ModelState.AddModelError("", "Please provide an image file to upload or enter a direct image URL.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Validation failed. Please provide a title and select a valid photo.";
                return RedirectToAction(nameof(Index));
            }

            string imagePath = string.Empty;

            if (vm.ImageFile != null && vm.ImageFile.Length > 0)
            {
                using var stream = vm.ImageFile.OpenReadStream();
                imagePath = await _fileStorage.SaveFileAsync(
                    stream,
                    vm.ImageFile.FileName,
                    "Gallery",
                    AllowedExtensions,
                    MaxFileSize);
            }
            else if (!string.IsNullOrWhiteSpace(vm.ImageUrl))
            {
                imagePath = vm.ImageUrl.Trim();
            }

            var activeReunion = await _context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive);
            var reunionId = activeReunion?.Id ?? 1;

            var galleryImage = new GalleryImage
            {
                ReunionEventId = reunionId,
                Title = vm.Title.Trim(),
                Caption = vm.Caption?.Trim(),
                Category = string.IsNullOrWhiteSpace(vm.Category) ? "Campus Life" : vm.Category.Trim(),
                ImagePath = imagePath,
                DisplayOrder = vm.DisplayOrder,
                IsActive = vm.IsActive
            };

            _context.GalleryImages.Add(galleryImage);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Photo '{galleryImage.Title}' uploaded successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditImage(GalleryImageEditViewModel vm)
        {
            var image = await _context.GalleryImages.FindAsync(vm.Id);
            if (image == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please provide valid information for the photo.";
                return RedirectToAction(nameof(Index));
            }

            if (vm.NewImageFile != null && vm.NewImageFile.Length > 0)
            {
                // Delete previous file if local
                if (!string.IsNullOrEmpty(image.ImagePath) && image.ImagePath.StartsWith("/Uploads/"))
                {
                    _fileStorage.DeleteFile(image.ImagePath);
                }

                using var stream = vm.NewImageFile.OpenReadStream();
                image.ImagePath = await _fileStorage.SaveFileAsync(
                    stream,
                    vm.NewImageFile.FileName,
                    "Gallery",
                    AllowedExtensions,
                    MaxFileSize);
            }

            image.Title = vm.Title.Trim();
            image.Caption = vm.Caption?.Trim();
            image.Category = string.IsNullOrWhiteSpace(vm.Category) ? "Campus Life" : vm.Category.Trim();
            image.DisplayOrder = vm.DisplayOrder;
            image.IsActive = vm.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Photo '{image.Title}' updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _context.GalleryImages.FindAsync(id);
            if (image == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(image.ImagePath) && image.ImagePath.StartsWith("/Uploads/"))
            {
                _fileStorage.DeleteFile(image.ImagePath);
            }

            _context.GalleryImages.Remove(image);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Photo '{image.Title}' deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}
