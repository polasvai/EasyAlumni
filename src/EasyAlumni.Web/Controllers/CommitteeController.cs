using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class CommitteeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;

        public CommitteeController(ApplicationDbContext context, IFileStorageService fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        public async Task<IActionResult> Index()
        {
            var committees = await _context.Committees
                .Include(c => c.Members.OrderBy(m => m.OrderIndex))
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();

            return View(committees);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCommittee(string committeeName, string? committeeNameBangla, string? description, int orderIndex)
        {
            if (string.IsNullOrWhiteSpace(committeeName))
            {
                TempData["Error"] = "Committee name is required.";
                return RedirectToAction(nameof(Index));
            }

            _context.Committees.Add(new Committee
            {
                CommitteeName = committeeName.Trim(),
                CommitteeNameBangla = committeeNameBangla?.Trim(),
                Description = description?.Trim(),
                OrderIndex = orderIndex,
                IsActive = true
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Committee '{committeeName}' added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(
            int committeeId,
            string fullName,
            string designation,
            int? batchYear,
            string? mobile,
            string? email,
            int orderIndex,
            IFormFile? photoFile)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(designation))
            {
                TempData["Error"] = "Member name and designation are required.";
                return RedirectToAction(nameof(Index));
            }

            string? photoPath = null;
            if (photoFile != null && photoFile.Length > 0)
            {
                using var stream = photoFile.OpenReadStream();
                photoPath = await _fileStorage.SaveFileAsync(stream, photoFile.FileName, "CommitteePhotos", new[] { ".jpg", ".jpeg", ".png" });
            }

            _context.CommitteeMembers.Add(new CommitteeMember
            {
                CommitteeId = committeeId,
                FullName = fullName.Trim(),
                Designation = designation.Trim(),
                BatchYear = batchYear,
                Mobile = mobile?.Trim(),
                Email = email?.Trim(),
                OrderIndex = orderIndex,
                PhotoPath = photoPath,
                IsActive = true
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Member '{fullName}' added to committee.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCommittee(int id, string committeeName, string? committeeNameBangla, string? description, int orderIndex, bool isActive)
        {
            var committee = await _context.Committees.FindAsync(id);
            if (committee == null)
            {
                TempData["Error"] = "Committee not found.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(committeeName))
            {
                TempData["Error"] = "Committee name is required.";
                return RedirectToAction(nameof(Index));
            }

            committee.CommitteeName = committeeName.Trim();
            committee.CommitteeNameBangla = committeeNameBangla?.Trim();
            committee.Description = description?.Trim();
            committee.OrderIndex = orderIndex;
            committee.IsActive = isActive;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Committee '{committeeName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCommittee(int id)
        {
            var committee = await _context.Committees
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (committee == null)
            {
                TempData["Error"] = "Committee not found.";
                return RedirectToAction(nameof(Index));
            }

            var name = committee.CommitteeName;
            _context.CommitteeMembers.RemoveRange(committee.Members);
            _context.Committees.Remove(committee);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Committee '{name}' and all associated members deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMember(
            int id,
            string fullName,
            string designation,
            int? batchYear,
            string? mobile,
            string? email,
            int orderIndex,
            bool isActive,
            IFormFile? photoFile)
        {
            var member = await _context.CommitteeMembers.FindAsync(id);
            if (member == null)
            {
                TempData["Error"] = "Member not found.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(designation))
            {
                TempData["Error"] = "Member name and designation are required.";
                return RedirectToAction(nameof(Index));
            }

            if (photoFile != null && photoFile.Length > 0)
            {
                using var stream = photoFile.OpenReadStream();
                member.PhotoPath = await _fileStorage.SaveFileAsync(stream, photoFile.FileName, "CommitteePhotos", new[] { ".jpg", ".jpeg", ".png" });
            }

            member.FullName = fullName.Trim();
            member.Designation = designation.Trim();
            member.BatchYear = batchYear;
            member.Mobile = mobile?.Trim();
            member.Email = email?.Trim();
            member.OrderIndex = orderIndex;
            member.IsActive = isActive;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Member '{fullName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMember(int id)
        {
            var member = await _context.CommitteeMembers.FindAsync(id);
            if (member == null)
            {
                TempData["Error"] = "Member not found.";
                return RedirectToAction(nameof(Index));
            }

            var name = member.FullName;
            _context.CommitteeMembers.Remove(member);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Member '{name}' removed from committee.";
            return RedirectToAction(nameof(Index));
        }
    }
}
