using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class SystemSetting
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string SettingKey { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string SettingValue { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Description { get; set; }

        public bool IsSecret { get; set; } = false;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class NoticePost
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Category { get; set; } = "Notice"; // Notice, News, EventUpdate

        [Required]
        public string ContentHtml { get; set; } = string.Empty;

        public DateTime PublishDate { get; set; } = DateTime.UtcNow;
        public bool IsPublished { get; set; } = true;

        [MaxLength(255)]
        public string? AttachmentPath { get; set; }
    }
}
