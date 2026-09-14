using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class RegistrationQuestionResponse
    {
        public int Id { get; set; }

        public int EventRegistrationId { get; set; }
        public EventRegistration? EventRegistration { get; set; }

        public int EventCustomQuestionId { get; set; }
        public EventCustomQuestion? CustomQuestion { get; set; }

        [Required]
        [MaxLength(500)]
        public string AnswerValue { get; set; } = string.Empty; // "Yes", "No", etc.

        [MaxLength(500)]
        public string? SubAnswerValue { get; set; } // "Singing, Recitation"
    }
}
