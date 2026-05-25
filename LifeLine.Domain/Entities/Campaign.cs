using LifeLine.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.Entities
{
    public class Campaign : BaseEntity
    {
        public Guid CampaignId { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string MedicalCondition { get; set; } = string.Empty;
        public string Story { get; set; } = string.Empty;
        public decimal GoalAmount { get; set; }
        public decimal AmountRaised { get; set; } = 0;
        public string? CoverImageUrl { get; set; }
        public string Slug { get; set; } = string.Empty;
        public CampaignStatus Status { get; set; } = CampaignStatus.Pending;
        public bool IsVerified { get; set; } = false;
        public string? RejectionReason { get; set; }
        public DateTime? SurgeryDate { get; set; }

        //Bank details for direct transfers (optional)
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public string CreatorId { get; set; } = string.Empty;
        public ApplicationUser Creator { get; set; } = null!;

        public ICollection<MedicalDocument> Documents { get; set; } = [];
        public ICollection<MedicalUpdate> Updates { get; set; } = [];
        public ICollection<Donation> Donations { get; set; } = [];
        public ICollection<SupportMessage> SupportMessages { get; set; } = [];
    }
}
