using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Common.Response.Donation
{
    public class DonationResponseDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string DonorName { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime DonatedAt { get; set; }
    }
}
