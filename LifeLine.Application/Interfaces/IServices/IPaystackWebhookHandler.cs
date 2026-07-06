using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Interfaces.IServices
{
    public interface IPaystackWebhookHandler
    {
        Task HandleEventAsync(string rawBody, CancellationToken ct = default);
    }
}
