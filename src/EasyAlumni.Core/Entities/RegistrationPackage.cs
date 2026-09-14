using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyAlumni.Core.Entities
{
    public class RegistrationPackage
    {
        public int Id { get; set; }

        public int ReunionEventId { get; set; }
        public ReunionEvent? ReunionEvent { get; set; }

        [Required]
        [MaxLength(150)]
        public string PackageName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? PackageNameBangla { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Fee { get; set; }

        public bool IncludesTShirt { get; set; } = true;
        public bool IncludesKitBag { get; set; } = true;
        public bool IncludesFood { get; set; } = true;
        public bool IncludesRaffle { get; set; } = true;
        public int IncludedGuests { get; set; } = 0;

        public int DisplayOrder { get; set; } = 1;
        public bool IsFeatured { get; set; } = false;
        public bool IsActive { get; set; } = true;

        [MaxLength(50)]
        public string? BadgeText { get; set; } // e.g. "Popular", "VIP", "Recommended"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
    }
}
