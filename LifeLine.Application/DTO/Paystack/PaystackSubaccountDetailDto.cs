using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Paystack
{
    public class PaystackSubaccountDetailDto
    {
        public string SubaccountCode { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public string SettlementBank { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool IsVerified { get; set; }
    }
}
