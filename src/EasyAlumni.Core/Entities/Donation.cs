using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyAlumni.Core.Enums;

namespace EasyAlumni.Core.Entities
{
    public class Donation
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string DonationTrackingNo { get; set; } = string.Empty; // e.g. DON-2026-00001

        public int? ReunionEventId { get; set; }
        public ReunionEvent? ReunionEvent { get; set; }

        [Required]
        [MaxLength(150)]
        public string DonorName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? DonorEmail { get; set; }

        [Required]
        [MaxLength(20)]
        public string DonorPhone { get; set; } = string.Empty;

        public int? BatchYear { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(150)]
        public string DonationPurpose { get; set; } = "General Reunion Support";

        public PaymentMode PaymentMode { get; set; } = PaymentMode.JanataPay;

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        [MaxLength(100)]
        public string? TransactionId { get; set; } // TrxID or JP token

        [MaxLength(25)]
        public string? SenderNumber { get; set; }

        [MaxLength(100)]
        public string? GatewayFtNumber { get; set; }

        [MaxLength(100)]
        public string? GatewayReferenceId { get; set; }

        [MaxLength(255)]
        public string? SlipAttachmentPath { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public bool IsAnonymous { get; set; } = false;

        public DateTime? ApprovedAt { get; set; }

        public string? ApprovedByUserId { get; set; }
        public ApplicationUser? ApprovedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
