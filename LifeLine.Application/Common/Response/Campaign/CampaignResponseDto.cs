using LifeLine.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Common.Response.Campaign
{
    public class CampaignResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string MedicalCondition { get; set; } = string.Empty;
        public string Story { get; set; } = string.Empty;
        public decimal GoalAmount { get; set; }
        public decimal AmountRaised { get; set; }
        public decimal PercentageRaised =>
            GoalAmount > 0 ? Math.Round(AmountRaised / GoalAmount * 100, 1) : 0;
        public string? CoverImageUrl { get; set; }
        public string Slug { get; set; } = string.Empty;
        public CampaignStatus Status { get; set; }
        public bool IsVerified { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime? SurgeryDate { get; set; }
        public int DonorCount { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatorId { get; set; }
    }
}
