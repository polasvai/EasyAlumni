using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyAlumni.Core.Entities
{
    public class RegistrationGuest
    {
        public int Id { get; set; }

        public int EventRegistrationId { get; set; }
        public EventRegistration? EventRegistration { get; set; }

        public int GuestCategoryId { get; set; }
        public GuestCategory? GuestCategory { get; set; }

        [MaxLength(150)]
        public string? GuestName { get; set; }

        [MaxLength(15)]
        public string? Gender { get; set; } // Male, Female, Other

        public int? Age { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FeeCharged { get; set; } = 0;
    }
}
