using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Common.RequestModel.PaystackDTO
{
    public class PaystackVerifyResultDto
    {
        public string Status { get; set; } = string.Empty; 
        public long AmountKobo { get; set; }
        public string GatewayResponse { get; set; } = string.Empty;
    }
}
