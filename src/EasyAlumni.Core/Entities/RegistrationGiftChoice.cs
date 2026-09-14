using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class RegistrationGiftChoice
    {
        public int Id { get; set; }

        public int EventRegistrationId { get; set; }
        public EventRegistration? EventRegistration { get; set; }

        public int GiftItemId { get; set; }
        public GiftItem? GiftItem { get; set; }

        [MaxLength(50)]
        public string? SelectedSize { get; set; } // e.g. "XL", "Regular", "Free Size"
    }
}
