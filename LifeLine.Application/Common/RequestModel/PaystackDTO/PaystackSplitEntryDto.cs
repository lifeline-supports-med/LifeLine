using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Common.RequestModel.PaystackDTO
{
    public class PaystackSplitEntryDto
    {
        public string SubaccountCode { get; set; } = string.Empty;
        public long ShareKobo { get; set; }
    }
}
