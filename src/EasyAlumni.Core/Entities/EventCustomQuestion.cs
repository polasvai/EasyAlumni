using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class EventCustomQuestion
    {
        public int Id { get; set; }

        public int ReunionEventId { get; set; }
        public ReunionEvent? ReunionEvent { get; set; }

        [Required]
        [MaxLength(300)]
        public string QuestionText { get; set; } = string.Empty; // e.g. "Do you want to participate in the Cultural Program?"

        [MaxLength(300)]
        public string? QuestionTextBangla { get; set; }

        [Required]
        [MaxLength(50)]
        public string FieldType { get; set; } = "YesNoWithSubQuestion"; // YesNo, YesNoWithSubQuestion, Text, Dropdown, Number

        [MaxLength(300)]
        public string? SubQuestionText { get; set; } // e.g. "If yes, which part? (e.g. Singing, Drama, Recitation, Dance)"

        [MaxLength(1000)]
        public string? OptionsJson { get; set; } // For Dropdown choices

        public bool IsRequired { get; set; } = false;
        public int DisplayOrder { get; set; } = 1;
        public bool IsActive { get; set; } = true;

        public ICollection<RegistrationQuestionResponse> Responses { get; set; } = new List<RegistrationQuestionResponse>();
    }
}
