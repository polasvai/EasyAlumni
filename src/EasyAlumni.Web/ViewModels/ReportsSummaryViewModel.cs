using System;
using System.Collections.Generic;
using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;

namespace EasyAlumni.Web.ViewModels
{
    public class ReportsSummaryViewModel
    {
        // 1. Financial Overview
        public decimal TotalCollectedRevenue { get; set; }
        public int ApprovedPaymentsCount { get; set; }
        public decimal PendingAmount { get; set; }
        public int PendingPaymentsCount { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetSurplus { get; set; }

        // 2. Catering & Headcount
        public int TotalAlumni { get; set; }
        public int TotalSpouses { get; set; }
        public int TotalChildren { get; set; }
        public int TotalGuests { get; set; }
        public int GrandTotalCateringHeads { get; set; }

        // 3. T-Shirt Size Breakdown
        public Dictionary<string, int> TShirtSizeCounts { get; set; } = new();

        // 4. Batch Participation
        public List<BatchParticipationSummary> TopBatches { get; set; } = new();

        // 5. Payment Channel Breakdown
        public List<PaymentChannelSummary> PaymentChannels { get; set; } = new();

        // 6. Recent Approved Transactions
        public List<RegistrationPayment> RecentApprovedPayments { get; set; } = new();
    }

    public class BatchParticipationSummary
    {
        public int BatchYear { get; set; }
        public int RegisteredCount { get; set; }
        public decimal TotalCollected { get; set; }
        public int TotalHeads { get; set; }
    }

    public class PaymentChannelSummary
    {
        public string ChannelName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
