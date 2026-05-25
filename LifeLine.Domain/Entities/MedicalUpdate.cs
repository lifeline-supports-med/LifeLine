using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.Entities
{
    public class MedicalUpdate : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime PostedAt { get; set; } = DateTime.UtcNow;
        public Guid CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;
    }
}
