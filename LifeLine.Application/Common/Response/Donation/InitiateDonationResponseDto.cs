using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Common.Response.Donation
{
    public class InitiateDonationResponseDto
    {
        public string PaymentReference { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = string.Empty;
    }
}
