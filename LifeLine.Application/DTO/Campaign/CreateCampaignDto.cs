using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Campaign
{
    public class CreateCampaignDto
    {
        public string Title { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string MedicalCondition { get; set; } = string.Empty;
        public string Story { get; set; } = string.Empty;
        public decimal GoalAmount { get; set; }
        public DateTime? SurgeryDate { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
    }
}
