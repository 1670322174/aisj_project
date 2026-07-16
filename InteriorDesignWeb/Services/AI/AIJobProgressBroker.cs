using System.Collections.Concurrent;
using System.Threading.Channels;

namespace InteriorDesignWeb.Services.AI;

public sealed record AIJobProgressEvent(
    string JobId,
    string Status,
    int ProgressValue,
    string? ErrorMessage,
    DateTime UpdatedAt);

public sealed class AIJobProgressBroker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<AIJobProgressEvent>>> _subscribers = new();

    public Subscription Subscribe(string jobId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<AIJobProgressEvent>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _subscribers.GetOrAdd(jobId, _ => new())[id] = channel;
        return new Subscription(this, jobId, id, channel.Reader);
    }

    public void Publish(AIJobProgressEvent update)
    {
        if (!_subscribers.TryGetValue(update.JobId, out var subscribers)) return;
        foreach (var channel in subscribers.Values) channel.Writer.TryWrite(update);
    }

    private void Unsubscribe(string jobId, Guid id)
    {
        if (!_subscribers.TryGetValue(jobId, out var subscribers)) return;
        if (subscribers.TryRemove(id, out var channel)) channel.Writer.TryComplete();
        if (subscribers.IsEmpty) _subscribers.TryRemove(jobId, out _);
    }

    public sealed class Subscription : IDisposable
    {
        private readonly AIJobProgressBroker _owner;
        private readonly string _jobId;
        private readonly Guid _id;
        private bool _disposed;

        internal Subscription(AIJobProgressBroker owner, string jobId, Guid id, ChannelReader<AIJobProgressEvent> reader)
        {
            _owner = owner;
            _jobId = jobId;
            _id = id;
            Reader = reader;
        }

        public ChannelReader<AIJobProgressEvent> Reader { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.Unsubscribe(_jobId, _id);
        }
    }
}
