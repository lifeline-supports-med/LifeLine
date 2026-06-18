namespace LifeLine.Domain.Entities;

public class Payout : BaseEntity
{
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string RequestedById { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = false;
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByAdminId { get; set; }
    public string? RejectionReason { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
}