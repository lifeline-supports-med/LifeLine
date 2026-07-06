using LifeLine.Application.Interfaces.IServices;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Persistence.Services
{
    public class PaystackWebhookHandler : IPaystackWebhookHandler
    {
        private readonly IDonationService _donationService;
        private readonly ILogger<PaystackWebhookHandler> _logger;

        public PaystackWebhookHandler(IDonationService donationService, ILogger<PaystackWebhookHandler> logger)
        {
            _donationService = donationService;
            _logger = logger;
        }

        public async Task HandleEventAsync(string rawBody, CancellationToken ct = default)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<dynamic>(rawBody);
                string? eventType = payload?.@event?.ToString();
                if (eventType != "charge.success") return;

                string? reference = payload?.data?.reference?.ToString();
                if (string.IsNullOrEmpty(reference)) return;

                await _donationService.VerifyDonationAsync(reference, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Webhook event handling failed: {Error}", ex.Message);
            }
        }
    }
}
