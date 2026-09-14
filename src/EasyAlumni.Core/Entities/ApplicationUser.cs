using Microsoft.AspNetCore.Identity;

namespace EasyAlumni.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? UserCode { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public AlumniProfile? AlumniProfile { get; set; }
    }
}
