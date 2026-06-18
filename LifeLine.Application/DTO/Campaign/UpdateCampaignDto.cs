using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Campaign
{
    public class UpdateCampaignDto
    {
        public string? Title { get; set; }
        public string? Story { get; set; }
        public decimal? GoalAmount { get; set; }
        public DateTime? SurgeryDate { get; set; }
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
    }
}
