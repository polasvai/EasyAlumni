using System.ComponentModel.DataAnnotations;
using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace EasyAlumni.Web.ViewModels
{
    public class AlumniRegistrationViewModel
    {
        // Event Context
        public int ReunionEventId { get; set; }
        public ReunionEvent? ReunionEvent { get; set; }

        public Dictionary<string, string> PaymentSettings { get; set; } = new();

        // Package Selection
        [Display(Name = "Selected Registration Package")]
        public int? RegistrationPackageId { get; set; }
        public List<RegistrationPackage> AvailablePackages { get; set; } = new();

        // Dynamic Configuration Collections
        public List<GuestCategory> AvailableGuestCategories { get; set; } = new();
        public List<GuestInputModel> Guests { get; set; } = new();

        public List<EventCustomQuestion> AvailableCustomQuestions { get; set; } = new();
        public List<QuestionResponseInputModel> QuestionResponses { get; set; } = new();

        public List<GiftSizeChoiceInputModel> GiftSizeChoices { get; set; } = new();

        // 1. Personal Information
        [Required(ErrorMessage = "বাংলায় নাম আবশ্যক")]
        [Display(Name = "Name (Bangla)")]
        [MaxLength(150)]
        public string NameBangla { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name in English is required")]
        [Display(Name = "Name (English)")]
        [MaxLength(150)]
        public string NameEnglish { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nick Name is required")]
        [Display(Name = "Nick Name")]
        [MaxLength(100)]
        public string NickName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Passing Year is required")]
        [Display(Name = "Passing Year (SSC/HSC)")]
        [Range(1950, 2030, ErrorMessage = "Please select a valid passing year")]
        public int PassingYear { get; set; } = 2010;

        [Display(Name = "Blood Group")]
        public string? BloodGroup { get; set; }

        [Required(ErrorMessage = "Contact number is required")]
        [RegularExpression(@"^(01[3-9]\d{8})$", ErrorMessage = "Please enter a valid 11-digit Bangladeshi mobile number (01XXXXXXXXX)")]
        [Display(Name = "Contact Number")]
        public string ContactNumber { get; set; } = string.Empty;

        [Display(Name = "Alternative Number")]
        public string? AlternativeNumber { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        // 2. Education & Professional Information
        [Display(Name = "Last Educational Institute")]
        public string? LastInstitute { get; set; }

        [Display(Name = "Last Degree (e.g. BSc, MBBS, MBA)")]
        public string? LastDegree { get; set; }

        [Display(Name = "Subject / Major")]
        public string? LastDegreeSubject { get; set; }

        [Display(Name = "Current Company / Organization")]
        public string? CompanyName { get; set; }

        [Display(Name = "Job Designation")]
        public string? Designation { get; set; }

        [Display(Name = "Office / Working Address")]
        public string? CurrentWorkingAddress { get; set; }

        [Display(Name = "Present Address")]
        public string? PresentAddress { get; set; }

        [Display(Name = "Permanent Address")]
        public string? PermanentAddress { get; set; }

        // 3. Document & Photo Uploads
        [Display(Name = "Old School Photo")]
        public IFormFile? OldPhotoFile { get; set; }

        [Display(Name = "Recent Photo")]
        public IFormFile? RecentPhotoFile { get; set; }

        [Display(Name = "Testimonial / ID Certificate (Max 200KB)")]
        public IFormFile? TestimonialFile { get; set; }

        // 4. Merchandise & Guests
        [Display(Name = "T-Shirt Size")]
        public string? TShirtSize { get; set; } = "L";

        [Range(0, 1, ErrorMessage = "Spouse count can be 0 or 1")]
        [Display(Name = "Spouse Attendance")]
        public int SpouseCount { get; set; } = 0;

        [Range(0, 5, ErrorMessage = "Child count must be between 0 and 5")]
        [Display(Name = "Child Attendance (above 5 yrs)")]
        public int ChildCount { get; set; } = 0;

        [Range(0, 3, ErrorMessage = "Driver / Guest count must be between 0 and 3")]
        [Display(Name = "Driver / Guest Attendance")]
        public int GuestCount { get; set; } = 0;

        // 5. Payment Information
        [Required(ErrorMessage = "Payment mode is required")]
        [Display(Name = "Payment Mode")]
        public PaymentMode PaymentMode { get; set; } = PaymentMode.bKashManual;

        [Required(ErrorMessage = "Transaction ID (TrxID) is required")]
        [Display(Name = "Transaction ID (TrxID)")]
        [MaxLength(50)]
        public string TransactionId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sender Mobile Number is required")]
        [Display(Name = "Sender Mobile Number")]
        [RegularExpression(@"^(01[3-9]\d{8})$", ErrorMessage = "Please enter a valid 11-digit sender mobile number")]
        public string SenderNumber { get; set; } = string.Empty;

        [Display(Name = "Payment Slip Screenshot")]
        public IFormFile? SlipAttachmentFile { get; set; }
    }

    public class GuestInputModel
    {
        public int GuestCategoryId { get; set; }
        public string? GuestName { get; set; }
        public string? Gender { get; set; } // Male, Female
        public int? Age { get; set; }
    }

    public class QuestionResponseInputModel
    {
        public int QuestionId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string? SubAnswer { get; set; }
    }

    public class GiftSizeChoiceInputModel
    {
        public int GiftItemId { get; set; }
        public string? SelectedSize { get; set; }
    }
}
