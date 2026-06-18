using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Payout
{
    public class RequestPayoutDto
    {
        public Guid CampaignId { get; set; }
        public decimal Amount { get; set; }
        public string? Notes { get; set; }

        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
    }
}
