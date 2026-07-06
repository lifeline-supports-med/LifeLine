using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.Settings.Paystack
{
    public class PaystackSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public string PlatformFeeAccountName { get; set; } = string.Empty;
        public string PlatformFeeAccountNumber { get; set; } = string.Empty;
        public string PlatformFeeBankCode { get; set; } = string.Empty;
        public decimal PlatformFeeAmount { get; set; } = 100;
    }
}
