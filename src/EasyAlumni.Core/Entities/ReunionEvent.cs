using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyAlumni.Core.Entities
{
    public class ReunionEvent
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string EventTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? TitleBangla { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public DateTime EventDate { get; set; }
        public DateTime RegistrationDeadline { get; set; }

        [MaxLength(200)]
        public string VenueName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? VenueAddress { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseAlumniFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SpouseFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ChildFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GuestFee { get; set; }

        public bool IsAllowSpouse { get; set; } = true;
        public bool IsAllowChild { get; set; } = true;
        public bool IsAllowGuest { get; set; } = true;
        public bool IsActive { get; set; } = true;

        [MaxLength(255)]
        public string? BannerImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
        public ICollection<BudgetHead> BudgetHeads { get; set; } = new List<BudgetHead>();
        public ICollection<RegistrationPackage> Packages { get; set; } = new List<RegistrationPackage>();
        public ICollection<GalleryImage> GalleryImages { get; set; } = new List<GalleryImage>();
    }
}
