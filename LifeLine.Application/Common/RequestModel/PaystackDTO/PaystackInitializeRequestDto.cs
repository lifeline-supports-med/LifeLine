using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Common.RequestModel.PaystackDTO
{
    public class PaystackInitializeRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public long AmountKobo { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public List<PaystackSplitEntryDto> Splits { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
