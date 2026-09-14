using EasyAlumni.Core.Entities;
using EasyAlumni.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class VolunteerManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public VolunteerManagementController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var volunteers = await _context.Volunteers
                .Include(v => v.ApplicationUser)
                .OrderBy(v => v.AssignedBooth)
                .ThenBy(v => v.FullName)
                .ToListAsync();

            return View(volunteers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVolunteer(
            string fullName,
            string mobile,
            int? batchYear,
            string assignedBooth,
            string dutyShift,
            string? email,
            string? password)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(mobile))
            {
                TempData["Error"] = "Full name and mobile number are required.";
                return RedirectToAction(nameof(Index));
            }

            string? userId = null;

            // If email & password provided, create Volunteer login account
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                var existingUser = await _userManager.FindByEmailAsync(email.Trim());
                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = email.Trim(),
                        Email = email.Trim(),
                        FullName = fullName.Trim(),
                        EmailConfirmed = true,
                        IsActive = true
                    };

                    var result = await _userManager.CreateAsync(user, password.Trim());
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Volunteer");
                        userId = user.Id;
                    }
                }
                else
                {
                    userId = existingUser.Id;
                }
            }

            var vol = new Volunteer
            {
                FullName = fullName.Trim(),
                Mobile = mobile.Trim(),
                BatchYear = batchYear,
                AssignedBooth = assignedBooth,
                DutyShift = dutyShift,
                ApplicationUserId = userId,
                IsActive = true
            };

            _context.Volunteers.Add(vol);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Volunteer '{fullName}' registered and assigned to '{assignedBooth}'.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBooth(int volunteerId, string assignedBooth, string dutyShift)
        {
            var vol = await _context.Volunteers.FindAsync(volunteerId);
            if (vol == null)
            {
                TempData["Error"] = "Volunteer not found.";
                return RedirectToAction(nameof(Index));
            }

            vol.AssignedBooth = assignedBooth;
            vol.DutyShift = dutyShift;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Updated booth assignment for {vol.FullName}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
