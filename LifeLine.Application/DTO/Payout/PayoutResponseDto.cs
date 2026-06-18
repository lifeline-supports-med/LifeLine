using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Payout
{
    public class PayoutResponseDto
    {
        public Guid Id { get; set; }
        public Guid CampaignId { get; set; }
        public string CampaignTitle { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string? Notes { get; set; }
        public string RequestedById { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
