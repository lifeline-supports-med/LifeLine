using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Interfaces.IServices
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken);
        Task SendWelcomeEmailAsync(string toEmail, string toName);
        Task SendPayoutApprovedEmailAsync(string toEmail, string toName, string campaignTitle, decimal amount, string bankName, string accountNumber);
        Task SendPayoutRejectedEmailAsync(string toEmail, string toName, string campaignTitle, decimal amount, string reason);
        Task SendDonationConfirmationEmailAsync(string toEmail, string toName,string campaignTitle, decimal amount);
        Task SendCampaignSubmittedToCreatorAsync(string toEmail, string toName, string campaignTitle);
        Task SendNewCampaignAlertToAdminAsync(string adminEmail, string creatorName, string campaignTitle, Guid campaignId);
        Task SendCampaignApprovedEmailAsync(string toEmail, string toName, string campaignTitle, string campaignSlug);
        Task SendCampaignRejectedEmailAsync(string toEmail, string toName, string campaignTitle, string reason);
        Task SendPasswordChangedEmailAsync(string toEmail, string toName);
    }
}
