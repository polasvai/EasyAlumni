using EasyAlumni.Core.Entities;

namespace EasyAlumni.Web.ViewModels
{
    public class GiftReportViewModel
    {
        public List<GiftItemSummaryItem> GiftItems { get; set; } = new();
        public int? SelectedGiftItemId { get; set; }
        public GiftItem? SelectedGiftItem { get; set; }
        public Dictionary<string, int> SelectedItemSizeDemands { get; set; } = new();
        public List<GiftBatchDemandItem> BatchDemands { get; set; } = new();
        public int TotalDemandedAllGifts { get; set; }
        public int TotalProcuredAllGifts { get; set; }
        public int TotalDistributedAllGifts { get; set; }
    }

    public class GiftItemSummaryItem
    {
        public int GiftItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSizeSpecific { get; set; }
        public int TotalProcured { get; set; }
        public int TotalDemanded { get; set; }
        public int TotalDistributed { get; set; }
        public int StockBalance => TotalProcured - TotalDemanded;
        public List<GiftItemSizeStock> SizeStocks { get; set; } = new();
    }

    public class GiftBatchDemandItem
    {
        public int BatchYear { get; set; }
        public int DemandCount { get; set; }
        public Dictionary<string, int> SizeCounts { get; set; } = new();
    }
}
