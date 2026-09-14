using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyAlumni.Core.Enums;

namespace EasyAlumni.Core.Entities
{
    public class EventRegistration
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string RegistrationNo { get; set; } = string.Empty; // e.g. RE-2026-00101

        public int ReunionEventId { get; set; }
        public ReunionEvent? ReunionEvent { get; set; }

        public int AlumniProfileId { get; set; }
        public AlumniProfile? AlumniProfile { get; set; }

        public int? RegistrationPackageId { get; set; }
        public RegistrationPackage? RegistrationPackage { get; set; }

        [Required]
        [MaxLength(10)]
        public string TShirtSize { get; set; } = "L";

        public int SpouseCount { get; set; } = 0;
        public int ChildCount { get; set; } = 0;
        public int GuestCount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; } = 0;

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        public string? QrCodeToken { get; set; }
        public string? QrCodeBase64 { get; set; }

        public bool IsKitDistributed { get; set; } = false;
        public DateTime? KitDistributedAt { get; set; }
        public string? KitDistributedByVolunteerId { get; set; }

        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        public ICollection<RegistrationPayment> Payments { get; set; } = new List<RegistrationPayment>();
        public ICollection<GiftDistribution> GiftDistributions { get; set; } = new List<GiftDistribution>();
        public ICollection<RegistrationGuest> Guests { get; set; } = new List<RegistrationGuest>();
        public ICollection<RegistrationQuestionResponse> QuestionResponses { get; set; } = new List<RegistrationQuestionResponse>();
        public ICollection<RegistrationGiftChoice> GiftChoices { get; set; } = new List<RegistrationGiftChoice>();
    }
}
