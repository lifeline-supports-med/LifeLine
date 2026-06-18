using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Donation
{
    public class DonationResponseDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string DonorName { get; set; } = string.Empty;
        public string? Message { get; set; }
        public bool IsAnonymous { get; set; }
        public bool IsVerified { get; set; }
        public DateTime DonatedAt { get; set; }
    }
}
