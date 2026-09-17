using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyAlumni.Core.Enums;

namespace EasyAlumni.Core.Entities
{
    public class RegistrationPayment
    {
        public int Id { get; set; }

        public int EventRegistrationId { get; set; }
        public EventRegistration? EventRegistration { get; set; }

        public PaymentMode PaymentMode { get; set; }

        [Required]
        [MaxLength(50)]
        public string TransactionId { get; set; } = string.Empty; // TrxID

        [Required]
        [MaxLength(20)]
        public string SenderNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(255)]
        public string? SlipAttachmentPath { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public string? ApprovedByUserId { get; set; }
        public ApplicationUser? ApprovedByUser { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [MaxLength(500)]
        public string? AdminRemarks { get; set; }

        [MaxLength(128)]
        public string? GatewayTransactionToken { get; set; }

        [MaxLength(100)]
        public string? GatewayReferenceId { get; set; }

        [MaxLength(100)]
        public string? GatewayFtNumber { get; set; }

        [MaxLength(50)]
        public string? GatewayStatus { get; set; }
    }
}
