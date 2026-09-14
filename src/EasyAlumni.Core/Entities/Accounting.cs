using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyAlumni.Core.Enums;

namespace EasyAlumni.Core.Entities
{
    public class AccountHead
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string HeadName { get; set; } = string.Empty; // e.g. Registration Fees, Sponsorship, Catering & Lunch, Stage & Sound

        public AccountHeadType Type { get; set; } // Income or Expense

        [MaxLength(20)]
        public string? Code { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<CashEntry> CashEntries { get; set; } = new List<CashEntry>();
    }

    public class CashEntry
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string VoucherNumber { get; set; } = string.Empty; // e.g. V-2026-001

        public DateTime EntryDate { get; set; } = DateTime.UtcNow;

        public int AccountHeadId { get; set; }
        public AccountHead? AccountHead { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(50)]
        public string PaymentMode { get; set; } = "Cash"; // Cash, Bank, bKash, Nagad

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? ReceiptAttachmentPath { get; set; }

        public int? RelatedRegistrationId { get; set; }
        public EventRegistration? RelatedRegistration { get; set; }

        public string? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }
    }

    public class BudgetHead
    {
        public int Id { get; set; }

        public int ReunionEventId { get; set; }
        public ReunionEvent? ReunionEvent { get; set; }

        [Required]
        [MaxLength(100)]
        public string HeadName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AllocatedAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualSpent { get; set; } = 0;

        public ICollection<BudgetEntry> Entries { get; set; } = new List<BudgetEntry>();
    }

    public class BudgetEntry
    {
        public int Id { get; set; }

        public int BudgetHeadId { get; set; }
        public BudgetHead? BudgetHead { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpenseAmount { get; set; }

        [MaxLength(50)]
        public string? VoucherReference { get; set; }

        public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

        [MaxLength(250)]
        public string? Description { get; set; }
    }
}
