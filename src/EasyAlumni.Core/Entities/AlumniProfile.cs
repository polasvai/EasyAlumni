using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class AlumniProfile
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string UserCode { get; set; } = string.Empty; // e.g. 3C6C150B97

        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        [Required]
        [MaxLength(150)]
        public string NameBangla { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string NameEnglish { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string NickName { get; set; } = string.Empty;

        [Required]
        public int PassingYear { get; set; }

        [MaxLength(10)]
        public string? BloodGroup { get; set; }

        [Required]
        [MaxLength(20)]
        public string ContactNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? AlternativeNumber { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        // Educational info
        [MaxLength(250)]
        public string? LastInstitute { get; set; }

        [MaxLength(100)]
        public string? LastDegree { get; set; }

        [MaxLength(150)]
        public string? LastDegreeSubject { get; set; }

        [MaxLength(255)]
        public string? TestimonialPath { get; set; }

        // Employment info
        [MaxLength(200)]
        public string? CompanyName { get; set; }

        [MaxLength(300)]
        public string? CurrentWorkingAddress { get; set; }

        [MaxLength(150)]
        public string? Designation { get; set; }

        // Addresses
        [MaxLength(300)]
        public string? PresentAddress { get; set; }

        [MaxLength(300)]
        public string? PermanentAddress { get; set; }

        // Photos
        [MaxLength(255)]
        public string? OldPhotoPath { get; set; }

        [MaxLength(255)]
        public string? RecentPhotoPath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
    }
}
