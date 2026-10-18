using System.ComponentModel.DataAnnotations;
using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace EasyAlumni.Web.ViewModels
{
    public class AdminEditRegistrationViewModel
    {
        // ==========================================
        // IMMUTABLE / READ-ONLY IDENTIFIERS
        // ==========================================
        public int RegistrationId { get; set; }

        public string RegistrationNo { get; set; } = string.Empty;

        public string UserCode { get; set; } = string.Empty;

        public int ProfileId { get; set; }

        public string? TransactionId { get; set; }

        public string? GatewayFtNumber { get; set; }

        public string? GatewayStatus { get; set; }

        public DateTime RegisteredAt { get; set; }

        // ==========================================
        // SECTION 1: PERSONAL INFORMATION (EDITABLE)
        // ==========================================
        [Required(ErrorMessage = "বাংলায় নাম আবশ্যক")]
        [Display(Name = "Name (Bangla)")]
        [MaxLength(150)]
        public string NameBangla { get; set; } = string.Empty;

        [Required(ErrorMessage = "English Name is required")]
        [Display(Name = "Name (English)")]
        [MaxLength(150)]
        public string NameEnglish { get; set; } = string.Empty;

        [Display(Name = "Nick Name")]
        [MaxLength(100)]
        public string? NickName { get; set; }

        [Required(ErrorMessage = "Passing Year is required")]
        [Display(Name = "Batch (Passing Year)")]
        [Range(1950, 2035, ErrorMessage = "Please enter a valid batch year")]
        public int PassingYear { get; set; } = 2010;

        [Display(Name = "Blood Group")]
        [MaxLength(10)]
        public string? BloodGroup { get; set; }

        public string? CurrentRecentPhotoPath { get; set; }

        [Display(Name = "Replace Photo (Optional)")]
        public IFormFile? NewPhotoFile { get; set; }

        // ==========================================
        // SECTION 2: CONTACT & ADDRESSES (EDITABLE)
        // ==========================================
        [Required(ErrorMessage = "Contact number is required")]
        [RegularExpression(@"^(01[3-9]\d{8})$", ErrorMessage = "Please enter a valid 11-digit mobile number (01XXXXXXXXX)")]
        [Display(Name = "Primary Contact Number")]
        [MaxLength(20)]
        public string ContactNumber { get; set; } = string.Empty;

        [Display(Name = "Alternative Contact Number")]
        [MaxLength(20)]
        public string? AlternativeNumber { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email Address")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Present Address")]
        [MaxLength(500)]
        public string? PresentAddress { get; set; }

        [Display(Name = "Permanent Address")]
        [MaxLength(500)]
        public string? PermanentAddress { get; set; }

        // ==========================================
        // SECTION 3: EDUCATION & CAREER (EDITABLE)
        // ==========================================
        [Display(Name = "Last Educational Institute")]
        [MaxLength(200)]
        public string? LastInstitute { get; set; }

        [Display(Name = "Last Degree (e.g. BSc, MBBS, MBA)")]
        [MaxLength(100)]
        public string? LastDegree { get; set; }

        [Display(Name = "Subject / Major")]
        [MaxLength(100)]
        public string? LastDegreeSubject { get; set; }

        [Display(Name = "Current Company / Organization")]
        [MaxLength(200)]
        public string? CompanyName { get; set; }

        [Display(Name = "Job Designation")]
        [MaxLength(150)]
        public string? Designation { get; set; }

        [Display(Name = "Office / Working Address")]
        [MaxLength(500)]
        public string? CurrentWorkingAddress { get; set; }

        // ==========================================
        // SECTION 4: PACKAGE, MERCHANDISE & GUESTS
        // ==========================================
        [Display(Name = "Registration Package")]
        public int? RegistrationPackageId { get; set; }

        public List<RegistrationPackage> AvailablePackages { get; set; } = new();

        [Required(ErrorMessage = "T-Shirt Size is required")]
        [Display(Name = "T-Shirt Size")]
        [MaxLength(20)]
        public string TShirtSize { get; set; } = "L";

        [Range(0, 10, ErrorMessage = "Spouse count cannot be negative")]
        [Display(Name = "Spouse Count")]
        public int SpouseCount { get; set; }

        [Range(0, 20, ErrorMessage = "Child count cannot be negative")]
        [Display(Name = "Child Count")]
        public int ChildCount { get; set; }

        [Range(0, 50, ErrorMessage = "Guest count cannot be negative")]
        [Display(Name = "Other Guest Count")]
        public int GuestCount { get; set; }

        [Display(Name = "Kit / Souvenir Handed Over")]
        public bool IsKitDistributed { get; set; }

        public DateTime? KitDistributedAt { get; set; }

        [Display(Name = "Registration Status")]
        public RegistrationStatus RegistrationStatus { get; set; } = RegistrationStatus.Pending;

        // ==========================================
        // SECTION 5: PAYMENT & FINANCIAL DETAILS
        // ==========================================
        [Range(0, 1000000, ErrorMessage = "Total Amount must be a valid non-negative number")]
        [Display(Name = "Total Fee / Payable (৳)")]
        public decimal TotalAmount { get; set; }

        [Range(0, 1000000, ErrorMessage = "Paid Amount must be a valid non-negative number")]
        [Display(Name = "Paid / Claimed Amount (৳)")]
        public decimal PaidAmount { get; set; }

        [Display(Name = "Payment Mode")]
        public PaymentMode PaymentMode { get; set; } = PaymentMode.JanataPay;

        [Display(Name = "Payment Status")]
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        [Display(Name = "Sender Mobile / Account Number")]
        [MaxLength(50)]
        public string? SenderNumber { get; set; }

        [Display(Name = "Admin Remarks")]
        [MaxLength(500)]
        public string? AdminRemarks { get; set; }
    }
}
