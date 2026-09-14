using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class GalleryImage
    {
        public int Id { get; set; }

        public int ReunionEventId { get; set; }
        public ReunionEvent? ReunionEvent { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Caption { get; set; }

        [Required]
        [MaxLength(350)]
        public string ImagePath { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Category { get; set; } = "Campus Memories"; // Campus Memories, Reunion, Sports, Cultural, Teachers

        public int DisplayOrder { get; set; } = 1;
        public bool IsActive { get; set; } = true;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
