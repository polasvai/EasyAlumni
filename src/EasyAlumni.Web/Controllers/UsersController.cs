using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EasyAlumni.Core.Entities;
using EasyAlumni.Infrastructure.Data;
using EasyAlumni.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index(string? role, string? search)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isCallerSuperAdmin = User.IsInRole("SuperAdmin");

            var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            if (!allRoles.Any())
            {
                allRoles = new List<string> { "SuperAdmin", "Admin", "Accounts", "Volunteer", "Alumni" };
            }

            var query = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(s) ||
                                         (u.Email != null && u.Email.ToLower().Contains(s)) ||
                                         (u.PhoneNumber != null && u.PhoneNumber.Contains(s)) ||
                                         (u.UserCode != null && u.UserCode.ToLower().Contains(s)));
            }

            var rawUsers = await query.OrderByDescending(u => u.CreatedAt).Take(200).ToListAsync();

            // Fetch user roles efficiently
            var userRolesQuery = await (from ur in _context.UserRoles
                                        join r in _context.Roles on ur.RoleId equals r.Id
                                        select new { ur.UserId, RoleName = r.Name })
                                        .ToListAsync();

            var rolesByUser = userRolesQuery
                .GroupBy(ur => ur.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName ?? string.Empty).ToList());

            var userItems = new List<UserListItemViewModel>();
            foreach (var u in rawUsers)
            {
                var assignedRoles = rolesByUser.GetValueOrDefault(u.Id, new List<string>());
                userItems.Add(new UserListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? string.Empty,
                    PhoneNumber = u.PhoneNumber,
                    UserCode = u.UserCode,
                    CreatedAt = u.CreatedAt,
                    IsActive = u.IsActive,
                    Roles = assignedRoles,
                    IsCurrentLoggedInUser = u.Id == currentUserId
                });
            }

            // Apply role filter in memory
            if (!string.IsNullOrWhiteSpace(role) && role != "All")
            {
                userItems = userItems.Where(u => u.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            // Aggregate counts
            var totalUsers = await _context.Users.CountAsync();
            var adminCount = userRolesQuery.Count(ur => ur.RoleName == "SuperAdmin" || ur.RoleName == "Admin");
            var accountsCount = userRolesQuery.Count(ur => ur.RoleName == "Accounts");
            var volunteerCount = userRolesQuery.Count(ur => ur.RoleName == "Volunteer");
            var alumniCount = userRolesQuery.Count(ur => ur.RoleName == "Alumni");

            var viewModel = new UserListIndexViewModel
            {
                Users = userItems,
                TotalUsers = totalUsers,
                AdminCount = adminCount,
                AccountsCount = accountsCount,
                VolunteerCount = volunteerCount,
                AlumniCount = alumniCount,
                AvailableRoles = isCallerSuperAdmin ? allRoles : allRoles.Where(r => r != "SuperAdmin").ToList(),
                SelectedRoleFilter = role ?? "All",
                SearchQuery = search
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            var isCallerSuperAdmin = User.IsInRole("SuperAdmin");

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (existingUser != null)
            {
                TempData["ErrorMessage"] = $"A user with email '{model.Email}' already exists.";
                return RedirectToAction(nameof(Index));
            }

            // Disallow non-SuperAdmin from creating SuperAdmin
            if (!isCallerSuperAdmin && model.SelectedRoles.Contains("SuperAdmin"))
            {
                model.SelectedRoles.Remove("SuperAdmin");
            }

            var newUser = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                UserCode = "STAFF-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
                EmailConfirmed = true,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(newUser, model.Password);
            if (!createResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join("; ", createResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            if (model.SelectedRoles != null && model.SelectedRoles.Any())
            {
                foreach (var role in model.SelectedRoles)
                {
                    if (await _roleManager.RoleExistsAsync(role))
                    {
                        await _userManager.AddToRoleAsync(newUser, role);
                    }
                }
            }
            else
            {
                // Default to Volunteer if no role chosen
                await _userManager.AddToRoleAsync(newUser, "Volunteer");
            }

            TempData["SuccessMessage"] = $"Staff user '{newUser.FullName}' created successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetUserJson(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            return Json(new
            {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                isActive = user.IsActive,
                roles = roles
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            var isCallerSuperAdmin = User.IsInRole("SuperAdmin");
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // Self-safeguard: can't deactivate self
            if (user.Id == currentUserId && !model.IsActive)
            {
                TempData["ErrorMessage"] = "Security alert: You cannot deactivate your own active account.";
                return RedirectToAction(nameof(Index));
            }

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            user.IsActive = model.IsActive;

            // Email change validation
            if (!string.Equals(user.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var emailOwner = await _userManager.FindByEmailAsync(model.Email.Trim());
                if (emailOwner != null && emailOwner.Id != user.Id)
                {
                    TempData["ErrorMessage"] = $"Email '{model.Email}' is already in use by another user.";
                    return RedirectToAction(nameof(Index));
                }
                user.Email = model.Email.Trim();
                user.UserName = model.Email.Trim();
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join("; ", updateResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            // Role Synchronization
            var currentRoles = await _userManager.GetRolesAsync(user);
            var desiredRoles = model.SelectedRoles ?? new List<string>();

            // SuperAdmin safety
            if (!isCallerSuperAdmin)
            {
                if (currentRoles.Contains("SuperAdmin") && !desiredRoles.Contains("SuperAdmin"))
                    desiredRoles.Add("SuperAdmin"); // preserve
                if (!currentRoles.Contains("SuperAdmin") && desiredRoles.Contains("SuperAdmin"))
                    desiredRoles.Remove("SuperAdmin"); // block
            }

            var rolesToAdd = desiredRoles.Except(currentRoles).ToList();
            var rolesToRemove = currentRoles.Except(desiredRoles).ToList();

            // Self-safeguard: can't remove own Admin or SuperAdmin role
            if (user.Id == currentUserId)
            {
                rolesToRemove.Remove("SuperAdmin");
                rolesToRemove.Remove("Admin");
            }

            if (rolesToAdd.Any()) await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (rolesToRemove.Any()) await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

            TempData["SuccessMessage"] = $"User '{user.FullName}' updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetUserPasswordViewModel model)
        {
            var isCallerSuperAdmin = User.IsInRole("SuperAdmin");

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // Disallow non-SuperAdmin from resetting a SuperAdmin password
            var targetRoles = await _userManager.GetRolesAsync(user);
            if (targetRoles.Contains("SuperAdmin") && !isCallerSuperAdmin)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Super Administrators can reset passwords for SuperAdmin accounts.";
                return RedirectToAction(nameof(Index));
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Password for '{user.FullName}' was reset successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isCallerSuperAdmin = User.IsInRole("SuperAdmin");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            if (user.Id == currentUserId)
            {
                TempData["ErrorMessage"] = "Security alert: You cannot deactivate your own logged-in account.";
                return RedirectToAction(nameof(Index));
            }

            var targetRoles = await _userManager.GetRolesAsync(user);
            if (targetRoles.Contains("SuperAdmin") && !isCallerSuperAdmin)
            {
                TempData["ErrorMessage"] = "Access Denied: Only Super Administrators can alter SuperAdmin accounts.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = user.IsActive
                ? $"User '{user.FullName}' has been ACTIVATED."
                : $"User '{user.FullName}' has been DEACTIVATED.";

            return RedirectToAction(nameof(Index));
        }

        public IActionResult AccessMatrix()
        {
            var roles = new List<string> { "SuperAdmin", "Admin", "Accounts", "Volunteer", "Alumni" };

            var permissions = new List<ModulePermissionItem>
            {
                new ModulePermissionItem
                {
                    Category = "System & Core Configuration",
                    ModuleName = "SMS Gateway & System Settings",
                    Description = "Configure SMS provider credentials, WhatsApp API, and global application parameters.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = false, ["Accounts"] = false, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "System & Core Configuration",
                    ModuleName = "User & Access Management",
                    Description = "Create admin/staff users, assign roles, reset credentials, and suspend access.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = false, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Public Portal & CMS",
                    ModuleName = "Website CMS Editor",
                    Description = "Customize hero banner, about story, venue date, Google Map, payment numbers, and footer.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = false, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Public Portal & CMS",
                    ModuleName = "Photo Gallery Showcase",
                    Description = "Upload reunion photos, organize categories, manage captions, and publish gallery pictures.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = false, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Registrations & Event Management",
                    ModuleName = "Registration Packages (Tiers)",
                    Description = "Create, edit, price, and configure ticket packages, souvenir inclusions, and badges.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = false, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Registrations & Event Management",
                    ModuleName = "Registration & Payment Approvals",
                    Description = "Review submitted payments, verify bKash/Nagad transactions, and approve digital passes.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = true, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Financials & Accounting",
                    ModuleName = "Cash Book & Expense Vouchers",
                    Description = "Record revenue receipts, log vendor expense vouchers, and maintain balanced ledger.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = true, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Financials & Accounting",
                    ModuleName = "Financial Statements & Summary",
                    Description = "Audit income vs expense statements, cash balance, and budget head allocations.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = true, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Gate & Logistics",
                    ModuleName = "Kiosk / QR Code Check-in",
                    Description = "Scan attendee QR code passes at event gate, check in participants, and issue kits.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = true, ["Volunteer"] = true, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Gate & Logistics",
                    ModuleName = "Apparel Stock & Kit Distribution",
                    Description = "Manage T-Shirt inventory by size (S, M, L, XL, XXL) and monitor distribution logs.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = true, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Executive Governance",
                    ModuleName = "Organizing Committee & Volunteers",
                    Description = "Manage executive committee members, designate conveners, and schedule volunteer rosters.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = false, ["Volunteer"] = false, ["Alumni"] = false }
                },
                new ModulePermissionItem
                {
                    Category = "Executive Governance",
                    ModuleName = "Analytical Reports Center",
                    Description = "Generate comprehensive batch distributions, catering headcounts, and export CSV/PDF reports.",
                    RoleAccess = new Dictionary<string, bool> { ["SuperAdmin"] = true, ["Admin"] = true, ["Accounts"] = true, ["Volunteer"] = false, ["Alumni"] = false }
                }
            };

            var vm = new AccessMatrixViewModel
            {
                Roles = roles,
                Permissions = permissions
            };

            return View(vm);
        }
    }
}
