using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.Entities
{
    public class MedicalDocument : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Guid CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;
    }
}
