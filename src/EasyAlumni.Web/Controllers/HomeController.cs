using EasyAlumni.Infrastructure.Data;
using EasyAlumni.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var reunionEvent = await _context.ReunionEvents
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.EventDate)
                .FirstOrDefaultAsync();

            var settings = await _context.SystemSettings
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

            var committees = await _context.Committees
                .Include(c => c.Members.Where(m => m.IsActive).OrderBy(m => m.OrderIndex))
                .Where(c => c.IsActive)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();

            var notices = await _context.NoticePosts
                .Where(n => n.IsPublished)
                .OrderByDescending(n => n.PublishDate)
                .Take(5)
                .ToListAsync();

            var totalReg = await _context.EventRegistrations.CountAsync();
            var totalApproved = await _context.EventRegistrations
                .CountAsync(r => r.Status == Core.Enums.RegistrationStatus.Approved);

            var packages = await _context.RegistrationPackages
                .Where(p => p.IsActive && (reunionEvent == null || p.ReunionEventId == reunionEvent.Id))
                .Include(p => p.PackageGiftItems)
                    .ThenInclude(pg => pg.GiftItem)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            var guestCategories = await _context.GuestCategories
                .Where(g => g.IsActive && (reunionEvent == null || g.ReunionEventId == reunionEvent.Id))
                .OrderBy(g => g.DisplayOrder)
                .ToListAsync();

            var galleryImages = await _context.GalleryImages
                .Where(g => g.IsActive)
                .OrderBy(g => g.DisplayOrder)
                .ToListAsync();

            var vm = new LandingPageViewModel
            {
                Event = reunionEvent,
                Settings = settings,
                Committees = committees,
                Notices = notices,
                Packages = packages,
                GuestCategories = guestCategories,
                GalleryImages = galleryImages,
                TotalRegisteredCount = totalReg,
                TotalApprovedCount = totalApproved
            };

            return View(vm);
        }

        public async Task<IActionResult> Gallery()
        {
            var images = await _context.GalleryImages
                .Where(g => g.IsActive)
                .OrderBy(g => g.DisplayOrder)
                .ToListAsync();
            return View(images);
        }

        public async Task<IActionResult> Committee()
        {
            var committees = await _context.Committees
                .Include(c => c.Members.Where(m => m.IsActive).OrderBy(m => m.OrderIndex))
                .Where(c => c.IsActive)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();

            return View(committees);
        }

        public async Task<IActionResult> Notice(int id)
        {
            var notice = await _context.NoticePosts.FindAsync(id);
            if (notice == null || !notice.IsPublished)
                return NotFound();

            return View(notice);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
