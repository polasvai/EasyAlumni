using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class Committee
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string CommitteeName { get; set; } = string.Empty; // e.g. Executive Committee, Advisory Council, Food Sub-Committee

        [MaxLength(150)]
        public string? CommitteeNameBangla { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public int OrderIndex { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public ICollection<CommitteeMember> Members { get; set; } = new List<CommitteeMember>();
    }

    public class CommitteeMember
    {
        public int Id { get; set; }

        public int CommitteeId { get; set; }
        public Committee? Committee { get; set; }

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Designation { get; set; } = string.Empty; // Convener, Member Secretary, Member, Coordinator

        public int? BatchYear { get; set; }

        [MaxLength(20)]
        public string? Mobile { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(255)]
        public string? PhotoPath { get; set; }

        public int OrderIndex { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class Volunteer
    {
        public int Id { get; set; }

        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Mobile { get; set; } = string.Empty;

        public int? BatchYear { get; set; }

        [MaxLength(100)]
        public string AssignedBooth { get; set; } = "Registration Desk"; // Gate 1, T-Shirt Counter, Food Token, Guest Escort

        [MaxLength(50)]
        public string? DutyShift { get; set; } // Morning, Afternoon, Full Day

        public bool IsActive { get; set; } = true;
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    }
}
