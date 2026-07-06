using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Common.RequestModel.PaystackDTO
{
    public class PaystackInitializeResultDto
    {
        public string AuthorizationUrl { get; set; } = string.Empty;
        public string AccessCode { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
    }
}
