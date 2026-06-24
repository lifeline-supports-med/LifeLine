using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.Entities
{
    public class Donation : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public decimal Amount { get; set; }
        public string PaymentReference { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; } = false;
        public string? DonorName { get; set; }
        public string? DonorEmail { get; set; }
        public string? Message { get; set; }
        public Guid CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;
        public string? DonorId { get; set; }
        public ApplicationUser? Donor { get; set; }
        public bool IsVerified { get; set; }

        public int DonorSequenceNumber { get; set; }

        /// <summary>
        /// True if this donation's net amount (after the ₦100 platform fee)
        /// was routed to the platform routing account instead of the
        /// campaign creator's account. Applies to donors in the configured
        /// sequence window (e.g. the 20th–27th donor on a campaign).
        /// </summary>
        public bool WasRoutedToPlatform { get; set; } = false;
    }
}
