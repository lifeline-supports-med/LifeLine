using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Domain.Enum
{
    public enum CampaignStatus
    {
        Draft = 0,
        PendingReview = 1,
        PaymentSetupPending = 2,
        PaymentSetupFailed = 3,
        Active = 4,
        Paused = 5,
        Completed = 6,
        Rejected = 7,
        Suspended = 8,
        Pending = 9,
        Verified = 10
    }
}
