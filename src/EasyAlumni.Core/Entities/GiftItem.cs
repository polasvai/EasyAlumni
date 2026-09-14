using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Core.Entities
{
    public class GiftItem
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string ItemName { get; set; } = string.Empty; // e.g. T-Shirt, Gift Hamper, Backpack, Crest

        [MaxLength(500)]
        public string? Description { get; set; }

        public int TotalStockQuantity { get; set; } = 0;
        public int AllocatedQuantity { get; set; } = 0;
        public int DistributedQuantity { get; set; } = 0;

        public bool IsSizeSpecific { get; set; } = false; // true for T-Shirts
        public bool IsActive { get; set; } = true;

        public ICollection<GiftItemSizeStock> SizeStocks { get; set; } = new List<GiftItemSizeStock>();
        public ICollection<GiftDistribution> Distributions { get; set; } = new List<GiftDistribution>();
    }

    public class GiftItemSizeStock
    {
        public int Id { get; set; }

        public int GiftItemId { get; set; }
        public GiftItem? GiftItem { get; set; }

        [Required]
        [MaxLength(10)]
        public string SizeName { get; set; } = "L"; // S, M, L, XL, XXL, XXXL

        public int TotalStock { get; set; } = 0;
        public int AllocatedStock { get; set; } = 0;
        public int DistributedStock { get; set; } = 0;
    }

    public class GiftDistribution
    {
        public int Id { get; set; }

        public int EventRegistrationId { get; set; }
        public EventRegistration? EventRegistration { get; set; }

        public int GiftItemId { get; set; }
        public GiftItem? GiftItem { get; set; }

        [MaxLength(10)]
        public string? SizeGiven { get; set; }

        public int Quantity { get; set; } = 1;

        public string? VolunteerUserId { get; set; }
        public ApplicationUser? VolunteerUser { get; set; }

        public DateTime DistributedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? Notes { get; set; }
    }
}
