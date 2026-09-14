using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Web.ViewModels
{
    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? UserCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsCurrentLoggedInUser { get; set; }
    }

    public class UserListIndexViewModel
    {
        public List<UserListItemViewModel> Users { get; set; } = new();
        public int TotalUsers { get; set; }
        public int AdminCount { get; set; }
        public int AccountsCount { get; set; }
        public int VolunteerCount { get; set; }
        public int AlumniCount { get; set; }
        public List<string> AvailableRoles { get; set; } = new();
        public string? SelectedRoleFilter { get; set; }
        public string? SearchQuery { get; set; }
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Mobile / Phone")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Assigned Roles")]
        public List<string> SelectedRoles { get; set; } = new();

        [Display(Name = "Active Account")]
        public bool IsActive { get; set; } = true;
    }

    public class EditUserViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Mobile / Phone")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Assigned Roles")]
        public List<string> SelectedRoles { get; set; } = new();

        [Display(Name = "Active Account")]
        public bool IsActive { get; set; } = true;
    }

    public class ResetUserPasswordViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string? UserEmail { get; set; }
        public string? FullName { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class AccessMatrixViewModel
    {
        public List<string> Roles { get; set; } = new();
        public List<ModulePermissionItem> Permissions { get; set; } = new();
    }

    public class ModulePermissionItem
    {
        public string Category { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, bool> RoleAccess { get; set; } = new();
    }
}
