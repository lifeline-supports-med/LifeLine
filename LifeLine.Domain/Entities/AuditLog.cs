using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.Entities
{
    public class AuditLog : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Action { get; set; } = string.Empty; // e.g. "CampaignVerified"
        public string PerformedByUserId { get; set; } = string.Empty;
        public string? TargetEntityId { get; set; }
        public string? TargetEntityType { get; set; } // e.g. "Campaign"
        public string? Details { get; set; }
    }
}
