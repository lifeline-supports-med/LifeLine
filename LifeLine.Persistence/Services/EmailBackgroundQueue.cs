using LifeLine.Application.Interfaces;
using LifeLine.Application.Interfaces.IServices;
using System.Threading.Channels;

namespace LifeLine.Persistence.Services;

public class EmailBackgroundQueue : IEmailBackgroundQueue
{
    private readonly Channel<(string Email, string Name)> _queue;

    public EmailBackgroundQueue()
    {
        _queue = Channel.CreateUnbounded<(string Email, string Name)>();
    }

    public async ValueTask QueueWelcomeEmailAsync(string email, string name)
    {
        await _queue.Writer.WriteAsync((email, name));
    }

    public async ValueTask<(string Email, string Name)> DequeueAsync(
        CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}