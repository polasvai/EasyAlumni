using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyAlumni.Core.Entities
{
    public class GuestCategory
    {
        public int Id { get; set; }

        public int ReunionEventId { get; set; }
        public ReunionEvent? ReunionEvent { get; set; }

        [Required]
        [MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty; // e.g. Spouse, Child, Driver, VIP

        [MaxLength(100)]
        public string? CategoryNameBangla { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Fee { get; set; } = 0;

        [MaxLength(300)]
        public string? EligibilityRules { get; set; } // e.g. "Male child under 5 yrs, Female child no age restrictions"

        public int? MaxAge { get; set; }
        public int? MinAge { get; set; }

        [MaxLength(20)]
        public string GenderRestriction { get; set; } = "None"; // None, MaleOnly, FemaleOnly

        public int MaxAllowed { get; set; } = 5;
        public int DisplayOrder { get; set; } = 1;
        public bool IsActive { get; set; } = true;

        public ICollection<RegistrationGuest> Guests { get; set; } = new List<RegistrationGuest>();
    }
}
