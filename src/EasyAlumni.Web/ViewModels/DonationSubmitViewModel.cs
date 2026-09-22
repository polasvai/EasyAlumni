using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EasyAlumni.Web.ViewModels
{
    public class DonationSubmitViewModel
    {
        [Required(ErrorMessage = "Donor name is required")]
        [StringLength(100, ErrorMessage = "Donor name cannot exceed 100 characters")]
        [Display(Name = "Your Name / Organization")]
        public string DonorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Valid mobile number is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(20)]
        [Display(Name = "Contact Mobile Number")]
        public string DonorPhone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email address")]
        [StringLength(100)]
        [Display(Name = "Email Address (Optional)")]
        public string? DonorEmail { get; set; }

        [StringLength(20)]
        [Display(Name = "Batch / SSC Year (Optional)")]
        public string? BatchYear { get; set; }

        [Required(ErrorMessage = "Donation amount is required")]
        [Range(10, 10000000, ErrorMessage = "Donation amount must be between ৳ 10 and ৳ 10,000,000")]
        [Display(Name = "Donation Amount (BDT)")]
        public decimal Amount { get; set; }

        [StringLength(100)]
        [Display(Name = "Donation Purpose / Cause")]
        public string? DonationPurpose { get; set; }

        [Required(ErrorMessage = "Please select payment method")]
        [Display(Name = "Payment Method")]
        public string PaymentMode { get; set; } = "JanataPay"; // "JanataPay", "bKash", "Nagad", "Rocket", "Bank"

        [Display(Name = "Transaction ID (TrxID)")]
        [StringLength(100)]
        public string? TransactionId { get; set; }

        [Display(Name = "Sender Phone / Account No")]
        [StringLength(50)]
        public string? SenderNumber { get; set; }

        [Display(Name = "Remarks / Notes")]
        [StringLength(500)]
        public string? Remarks { get; set; }

        [Display(Name = "Keep my name anonymous publicly")]
        public bool IsAnonymous { get; set; } = false;

        [Display(Name = "Payment Slip / Screenshot")]
        public IFormFile? SlipAttachment { get; set; }
    }
}
