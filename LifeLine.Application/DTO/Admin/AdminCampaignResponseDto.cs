using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Admin
{
    public class AdminCampaignResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string MedicalCondition { get; set; } = string.Empty;
        public string Story { get; set; } = string.Empty;
        public decimal GoalAmount { get; set; }
        public decimal AmountRaised { get; set; }
        public string? CoverImageUrl { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? SurgeryDate { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public string CreatorEmail { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
        public int DonorCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<AdminDocumentDto> Documents { get; set; } = [];
    }
}
