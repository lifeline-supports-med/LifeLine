using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Interfaces.IServices
{
    public interface IEmailBackgroundQueue
    {
        ValueTask QueueWelcomeEmailAsync(string email, string name);

        ValueTask<(string Email, string Name)> DequeueAsync(
            CancellationToken cancellationToken);
    }
}
