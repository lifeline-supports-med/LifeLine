using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Donation
{
    public class InitiateDonationDto
    {
        public Guid CampaignId { get; set; }
        public decimal Amount { get; set; }
        public bool IsAnonymous { get; set; } = false;
        public string? Message { get; set; }

        // Only required for guest donations (not logged in)
        public string? DonorName { get; set; }
        public string? DonorEmail { get; set; }
    }
}
