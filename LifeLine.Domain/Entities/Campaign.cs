using LifeLine.Domain.Enum;
using System;
using System.Collections.Generic;

namespace LifeLine.Domain.Entities
{
    public class Campaign : BaseEntity
    {
        public Guid CampaignId { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string MedicalCondition { get; set; } = string.Empty;
        public string Story { get; set; } = string.Empty;
        public decimal GoalAmount { get; set; }
        public decimal AmountRaised { get; set; } = 0;
        public string? CoverImageUrl { get; set; }
        public string? CoverImagePublicId { get; set; }
        public string Slug { get; set; } = string.Empty;
        public DateTime? SurgeryDate { get; set; }

        // ==========================================
        // CAMPAIGN STATUS & REVIEWS
        // ==========================================
        public CampaignStatus Status { get; set; } = CampaignStatus.Pending;

        /// <summary>
        /// True once the Lifeline Admin has verified medical reports and patient identity.
        /// </summary>
        public bool IsVerified { get; set; } = false;

        public string? RejectionReason { get; set; }

        // ==========================================
        // BANK & NIBSS RESOLUTION DETAILS
        // ==========================================
        public string? BankName { get; set; }
        public string? BankCode { get; set; }
        public string? AccountNumber { get; set; }

        /// <summary>
        /// Official account name resolved directly from NIBSS via Paystack.
        /// </summary>
        public string? AccountName { get; set; }

        /// <summary>
        /// Indicates if the bank account number + bank code successfully resolved via NIBSS.
        /// </summary>
        public bool IsAccountNameResolved { get; set; } = false;

        // ==========================================
        // PAYSTACK SUBACCOUNT & SETTLEMENT READINESS
        // ==========================================
        /// <summary>
        /// Paystack Subaccount Code (e.g. ACCT_xxxxxxxxxxxx).
        /// </summary>
        public string? SubAccountCode { get; set; }

        /// <summary>
        /// Corresponds to Paystack's 'is_verified' field for this subaccount.
        /// </summary>
        public bool PaystackSubaccountIsVerified { get; set; } = false;

        /// <summary>
        /// Corresponds to Paystack's 'active' field for this subaccount.
        /// </summary>
        public bool PaystackSubaccountIsActive { get; set; } = false;

        /// <summary>
        /// True only when medicals are approved AND Paystack subaccount is active & verified.
        /// Hard requirement for accepting donations.
        /// </summary>
        public bool IsPaymentReady { get; set; } = false;

        /// <summary>
        /// Records any error that occurred during NIBSS resolution or Paystack subaccount creation.
        /// </summary>
        public string? PaymentSetupErrorMessage { get; set; }

        /// <summary>
        /// Timestamp when the subaccount was successfully cleared for live settlement.
        /// </summary>
        public DateTime? PaymentActivatedAt { get; set; }

        // ==========================================
        // CREATOR & RELATIONSHIPS
        // ==========================================
        public string CreatorId { get; set; } = string.Empty;
        public ApplicationUser Creator { get; set; } = null!;

        public ICollection<MedicalDocument> Documents { get; set; } = [];
        public ICollection<MedicalUpdate> Updates { get; set; } = [];
        public ICollection<Donation> Donations { get; set; } = [];
        public ICollection<SupportMessage> SupportMessages { get; set; } = [];
    }
}