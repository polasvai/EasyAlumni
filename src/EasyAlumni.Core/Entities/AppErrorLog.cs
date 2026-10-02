using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class AppErrorLog
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "error"; // critical, error, warning, info, debug

        [MaxLength(250)]
        public string Controller { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? StackTrace { get; set; }

        [MaxLength(500)]
        public string? RequestPath { get; set; }

        [MaxLength(20)]
        public string? RequestMethod { get; set; }

        [MaxLength(256)]
        public string? UserIdentifier { get; set; }

        [MaxLength(100)]
        public string? ClientIp { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsSyncedToRemote { get; set; } = false;

        [MaxLength(500)]
        public string? RemoteSyncError { get; set; }

        public DateTime? RemoteSyncedAt { get; set; }
    }
}
