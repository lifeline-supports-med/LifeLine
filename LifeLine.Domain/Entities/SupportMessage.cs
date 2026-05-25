using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.Entities
{
    public class SupportMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; } = false;
        public Guid CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;
    }
}
