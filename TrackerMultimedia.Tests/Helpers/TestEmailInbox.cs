using TrackerMultimedia.Services;

namespace TrackerMultimedia.Tests.Helpers;

public sealed record TestEmailMessage(string To, string Subject, string HtmlBody);

public sealed class TestEmailInbox
{
    private readonly List<TestEmailMessage> _messages = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<TestEmailMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToArray();
            }
        }
    }

    public void Add(string to, string subject, string htmlBody)
    {
        lock (_gate)
        {
            _messages.Add(new TestEmailMessage(to, subject, htmlBody));
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _messages.Clear();
        }
    }
}

internal sealed class RecordingEmailService(TestEmailInbox inbox) : IEmailService
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        inbox.Add(to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
