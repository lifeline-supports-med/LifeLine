using LifeLine.Application.Interfaces;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Settings.MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;


namespace LifeLine.Persistence.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<EmailSettings> settings,
            ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(
            string toEmail, string toName, string resetToken)
        {
            var subject = "Reset Your Lifeline Password";

            var body = $"""
            <html>
            <body style="font-family: Arial, sans-serif; background:#f5f5f5; padding:20px;">
              <div style="max-width:600px; margin:auto; background:#fff; 
                          border-radius:8px; padding:32px;">
                
                <h2 style="color:#0B6B4B;">Reset Your Password</h2>
                
                <p>Hi <strong>{toName}</strong>,</p>
                
                <p>We received a request to reset your Lifeline password. 
                   Use the token below to complete the reset:</p>
                
                <div style="background:#f0f9f5; border:1px solid #0B6B4B; 
                            border-radius:6px; padding:16px; 
                            font-size:14px; word-break:break-all; margin:24px 0;">
                  <strong>Reset Token:</strong><br/>
                  <code style="color:#0B6B4B;">{resetToken}</code>
                </div>
                
                <p style="color:#666; font-size:13px;">
                  This token expires in <strong>1 hour</strong>. 
                  If you did not request a password reset, 
                  please ignore this email.
                </p>
                
                <hr style="border:none; border-top:1px solid #eee; margin:24px 0;"/>
                
                <p style="color:#999; font-size:12px;">
                  — The Lifeline Team<br/>
                  <em>A trusted emergency support infrastructure 
                  for critical medical moments.</em>
                </p>
              </div>
            </body>
            </html>
            """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string toName)
        {
            var subject = "Welcome to Lifeline 💚";

            var body = $"""
            <html>
            <body style="font-family: Arial, sans-serif; background:#f5f5f5; padding:20px;">
              <div style="max-width:600px; margin:auto; background:#fff; 
                          border-radius:8px; padding:32px;">
                
                <h2 style="color:#0B6B4B;">Welcome to Lifeline, {toName}! 💚</h2>
                
                <p>Your account has been created successfully.</p>
                
                <p>With Lifeline, you can:</p>
                <ul style="color:#444; line-height:1.8;">
                  <li>Create medical emergency fundraisers</li>
                  <li>Share campaigns instantly via WhatsApp</li>
                  <li>Receive verified donations securely</li>
                  <li>Post recovery updates for your donors</li>
                </ul>
                
                <p>We're glad you're here. Every contribution counts.</p>
                
                <hr style="border:none; border-top:1px solid #eee; margin:24px 0;"/>
                
                <p style="color:#999; font-size:12px;">
                  — The Lifeline Team<br/>
                  <em>A trusted emergency support infrastructure 
                  for critical medical moments.</em>
                </p>
              </div>
            </body>
            </html>
            """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }

        public async Task SendDonationConfirmationEmailAsync(
    string toEmail, string toName,
    string campaignTitle, decimal amount)
        {
            var subject = "Thank You for Your Donation 💚 — Lifeline";
            var body = $"""
        <html>
        <body style="font-family:Arial,sans-serif;background:#f5f5f5;padding:20px;">
          <div style="max-width:600px;margin:auto;background:#fff;
                      border-radius:8px;padding:32px;">

            <h2 style="color:#0B6B4B;">Thank You, {toName}! 💚</h2>

            <p>Your donation has been received and confirmed.</p>

            <div style="background:#f0f9f5;border:1px solid #0B6B4B;
                        border-radius:6px;padding:20px;margin:24px 0;">
              <table style="width:100%;">
                <tr>
                  <td style="color:#666;padding:6px 0;">Campaign</td>
                  <td style="font-weight:bold;color:#333;
                             text-align:right;">{campaignTitle}</td>
                </tr>
                <tr>
                  <td style="color:#666;padding:6px 0;">Amount</td>
                  <td style="font-weight:bold;color:#0B6B4B;
                             text-align:right;font-size:18px;">
                    ₦{amount:N0}
                  </td>
                </tr>
                <tr>
                  <td style="color:#666;padding:6px 0;">Date</td>
                  <td style="font-weight:bold;color:#333;
                             text-align:right;">
                    {DateTime.UtcNow:MMMM dd, yyyy}
                  </td>
                </tr>
              </table>
            </div>

            <p>Your generosity is making a real difference. 
               Every naira counts toward saving a life.</p>

            <p style="color:#666;font-size:13px;">
              Share this campaign with others to help raise more funds.
            </p>

            <hr style="border:none;border-top:1px solid #eee;margin:24px 0;"/>
            <p style="color:#999;font-size:12px;">— The Lifeline Team</p>
          </div>
        </body>
        </html>
        """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }

        public async Task SendPasswordChangedEmailAsync(string toEmail, string toName)
        {
            var subject = "Your Lifeline Password Was Changed";

            var body = $"""
            <html>
            <body style="font-family: Arial, sans-serif; background:#f5f5f5; padding:20px;">
              <div style="max-width:600px; margin:auto; background:#fff; 
                          border-radius:8px; padding:32px;">
                
                <h2 style="color:#0B6B4B;">Password Changed</h2>
                
                <p>Hi <strong>{toName}</strong>,</p>
                
                <p>Your Lifeline account password was successfully changed 
                   on <strong>{DateTime.UtcNow:MMMM dd, yyyy} UTC</strong>.</p>
                
                <p style="color:#c0392b;">
                  If you did not make this change, please contact us 
                  immediately or reset your password.
                </p>
                
                <hr style="border:none; border-top:1px solid #eee; margin:24px 0;"/>
                
                <p style="color:#999; font-size:12px;">
                  — The Lifeline Team<br/>
                  <em>A trusted emergency support infrastructure 
                  for critical medical moments.</em>
                </p>
              </div>
            </body>
            </html>
            """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }

        private async Task SendEmailAsync(
            string toEmail, string toName,
            string subject, string htmlBody)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;

                message.Body = new BodyBuilder
                {
                    HtmlBody = htmlBody
                }.ToMessageBody();

                using var client = new SmtpClient();

                await client.ConnectAsync(
                    _settings.Host,
                    _settings.Port,
                    SecureSocketOptions.StartTls);

                await client.AuthenticateAsync(
                    _settings.SenderEmail,
                    _settings.Password);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation(
                    "Email sent to {Email} — Subject: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Failed to send email to {Email}: {Error}", toEmail, ex.Message);

               
            }
        }

        public async Task SendCampaignApprovedEmailAsync(
            string toEmail, string toName,
            string campaignTitle, string campaignSlug)
        {
            var subject = "🎉 Your Lifeline Campaign Has Been Verified!";
            var body = $"""
        <html>
        <body style="font-family:Arial,sans-serif;background:#f5f5f5;padding:20px;">
          <div style="max-width:600px;margin:auto;background:#fff;
                      border-radius:8px;padding:32px;">

            <h2 style="color:#0B6B4B;">Your Campaign is Now Live! 🎉</h2>

            <p>Hi <strong>{toName}</strong>,</p>

            <p>Great news! Your campaign <strong>"{campaignTitle}"</strong> 
               has been reviewed and verified by our team.</p>

            <p>It is now live on Lifeline and ready to receive donations. 
               Share it with family and friends to start raising funds:</p>

            <div style="text-align:center;margin:32px 0;">
              <a href="https://lifeline.ng/campaigns/{campaignSlug}"
                 style="background:#0B6B4B;color:#fff;padding:14px 28px;
                        border-radius:6px;text-decoration:none;
                        font-weight:bold;">
                View Your Campaign
              </a>
            </div>

            <p style="color:#666;font-size:13px;">
              Share on WhatsApp, Twitter, and Facebook to reach more donors. 
              Every share counts.
            </p>

            <hr style="border:none;border-top:1px solid #eee;margin:24px 0;"/>
            <p style="color:#999;font-size:12px;">— The Lifeline Team</p>
          </div>
        </body>
        </html>
        """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }

        public async Task SendCampaignSubmittedToCreatorAsync(
            string toEmail, string toName, string campaignTitle)
        {
            var subject = "Your Campaign is Under Review — Lifeline";
            var body = $"""
        <html>
        <body style="font-family:Arial,sans-serif;background:#f5f5f5;padding:20px;">
          <div style="max-width:600px;margin:auto;background:#fff;
                      border-radius:8px;padding:32px;">

            <h2 style="color:#0B6B4B;">We've Received Your Campaign 💚</h2>

            <p>Hi <strong>{toName}</strong>,</p>

            <p>Thank you for submitting <strong>"{campaignTitle}"</strong> 
               to Lifeline.</p>

            <p>Our verification team will review your campaign and the 
               uploaded medical documents. This usually takes 
               <strong>24–48 hours</strong>.</p>

            <div style="background:#f0f9f5;border-left:4px solid #0B6B4B;
                        padding:16px;margin:24px 0;border-radius:4px;">
              <p style="margin:0;color:#444;">
                <strong>What happens next?</strong><br/>
                ✔ Our team reviews your medical documents<br/>
                ✔ We verify the details of your campaign<br/>
                ✔ You get notified once it's approved or if changes are needed
              </p>
            </div>

            <p style="color:#666;font-size:13px;">
              While you wait, make sure you have a clear patient photo 
              ready to upload — it significantly increases donor trust.
            </p>

            <hr style="border:none;border-top:1px solid #eee;margin:24px 0;"/>
            <p style="color:#999;font-size:12px;">— The Lifeline Team</p>
          </div>
        </body>
        </html>
        """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }

        public async Task SendPayoutApprovedEmailAsync(
    string toEmail, string toName, string campaignTitle,
    decimal amount, string bankName, string accountNumber)
        {
            var subject = "✅ Your Payout Has Been Approved — Lifeline";
            var body = $"""
        <html>
        <body style="font-family:Arial,sans-serif;background:#f5f5f5;padding:20px;">
          <div style="max-width:600px;margin:auto;background:#fff;
                      border-radius:8px;padding:32px;">

            <h2 style="color:#0B6B4B;">Payout Approved ✅</h2>

            <p>Hi <strong>{toName}</strong>,</p>

            <p>Your payout request for <strong>"{campaignTitle}"</strong> 
               has been approved and is being processed.</p>

            <div style="background:#f0f9f5;border:1px solid #0B6B4B;
                        border-radius:6px;padding:20px;margin:24px 0;">
              <table style="width:100%;">
                <tr>
                  <td style="color:#666;padding:6px 0;">Amount</td>
                  <td style="font-weight:bold;color:#0B6B4B;
                             text-align:right;font-size:18px;">
                    ₦{amount:N0}
                  </td>
                </tr>
                <tr>
                  <td style="color:#666;padding:6px 0;">Bank</td>
                  <td style="font-weight:bold;color:#333;
                             text-align:right;">{bankName}</td>
                </tr>
                <tr>
                  <td style="color:#666;padding:6px 0;">Account</td>
                  <td style="font-weight:bold;color:#333;
                             text-align:right;">{accountNumber}</td>
                </tr>
              </table>
            </div>

            <p style="color:#666;font-size:13px;">
              Funds are typically transferred within 1–3 business days 
              depending on your bank.
            </p>

            <hr style="border:none;border-top:1px solid #eee;margin:24px 0;"/>
            <p style="color:#999;font-size:12px;">— The Lifeline Team</p>
          </div>
        </body>
        </html>
        """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }

        public async Task SendPayoutRejectedEmailAsync(
            string toEmail, string toName,
            string campaignTitle, decimal amount, string reason)
        {
            var subject = "Update on Your Payout Request — Lifeline";
            var body = $"""
        <html>
        <body style="font-family:Arial,sans-serif;background:#f5f5f5;padding:20px;">
          <div style="max-width:600px;margin:auto;background:#fff;
                      border-radius:8px;padding:32px;">

            <h2 style="color:#0B6B4B;">Payout Request Update</h2>

            <p>Hi <strong>{toName}</strong>,</p>

            <p>Your payout request of <strong>₦{amount:N0}</strong> for 
               <strong>"{campaignTitle}"</strong> could not be processed 
               at this time.</p>

            <div style="background:#fff5f5;border:1px solid #e74c3c;
                        border-radius:6px;padding:16px;margin:24px 0;">
              <strong style="color:#c0392b;">Reason:</strong>
              <p style="color:#444;margin:8px 0 0;">{reason}</p>
            </div>

            <p>Please contact our support team if you have questions 
               or need to resubmit your request.</p>

            <hr style="border:none;border-top:1px solid #eee;margin:24px 0;"/>
            <p style="color:#999;font-size:12px;">— The Lifeline Team</p>
          </div>
        </body>
        </html>
        """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }

        public async Task SendNewCampaignAlertToAdminAsync(
            string adminEmail, string creatorName,
            string campaignTitle, Guid campaignId)
        {
            var subject = "🔔 New Campaign Submitted for Review — Lifeline";
            var body = $"""
        <html>
        <body style="font-family:Arial,sans-serif;background:#f5f5f5;padding:20px;">
          <div style="max-width:600px;margin:auto;background:#fff;
                      border-radius:8px;padding:32px;">

            <h2 style="color:#0B6B4B;">New Campaign Needs Review</h2>

            <p>A new medical fundraising campaign has been submitted 
               and is awaiting your verification.</p>

            <table style="width:100%;border-collapse:collapse;margin:24px 0;">
              <tr style="background:#f0f9f5;">
                <td style="padding:10px 16px;font-weight:bold;color:#444;
                           border:1px solid #ddd;">Campaign</td>
                <td style="padding:10px 16px;color:#333;
                           border:1px solid #ddd;">{campaignTitle}</td>
              </tr>
              <tr>
                <td style="padding:10px 16px;font-weight:bold;color:#444;
                           border:1px solid #ddd;">Submitted By</td>
                <td style="padding:10px 16px;color:#333;
                           border:1px solid #ddd;">{creatorName}</td>
              </tr>
              <tr style="background:#f0f9f5;">
                <td style="padding:10px 16px;font-weight:bold;color:#444;
                           border:1px solid #ddd;">Campaign ID</td>
                <td style="padding:10px 16px;color:#333;
                           border:1px solid #ddd;">{campaignId}</td>
              </tr>
              <tr>
                <td style="padding:10px 16px;font-weight:bold;color:#444;
                           border:1px solid #ddd;">Status</td>
                <td style="padding:10px 16px;color:#e67e22;font-weight:bold;
                           border:1px solid #ddd;">Pending Review</td>
              </tr>
            </table>

            <div style="text-align:center;margin:32px 0;">
              <a href="https://lifeline.ng/admin/campaigns/{campaignId}"
                 style="background:#0B6B4B;color:#fff;padding:14px 28px;
                        border-radius:6px;text-decoration:none;
                        font-weight:bold;">
                Review Campaign
              </a>
            </div>

            <hr style="border:none;border-top:1px solid #eee;margin:24px 0;"/>
            <p style="color:#999;font-size:12px;">— Lifeline Admin System</p>
          </div>
        </body>
        </html>
        """;

            await SendEmailAsync(adminEmail, "Lifeline Admin", subject, body);
        }

        public async Task SendCampaignRejectedEmailAsync(
            string toEmail, string toName,
            string campaignTitle, string reason)
        {
            var subject = "Update on Your Lifeline Campaign Submission";
            var body = $"""
        <html>
        <body style="font-family:Arial,sans-serif;background:#f5f5f5;padding:20px;">
          <div style="max-width:600px;margin:auto;background:#fff;
                      border-radius:8px;padding:32px;">

            <h2 style="color:#0B6B4B;">Campaign Review Update</h2>

            <p>Hi <strong>{toName}</strong>,</p>

            <p>Thank you for submitting <strong>"{campaignTitle}"</strong> 
               to Lifeline. After careful review, we were unable to 
               approve this campaign at this time.</p>

            <div style="background:#fff5f5;border:1px solid #e74c3c;
                        border-radius:6px;padding:16px;margin:24px 0;">
              <strong style="color:#c0392b;">Reason:</strong>
              <p style="color:#444;margin:8px 0 0;">{reason}</p>
            </div>

            <p>You are welcome to make the necessary corrections and 
               resubmit your campaign. If you believe this decision 
               was made in error, please contact our support team.</p>

            <hr style="border:none;border-top:1px solid #eee;margin:24px 0;"/>
            <p style="color:#999;font-size:12px;">— The Lifeline Team</p>
          </div>
        </body>
        </html>
        """;

            await SendEmailAsync(toEmail, toName, subject, body);
        }
    }

}
