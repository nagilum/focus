using Focus.Models.Interfaces;

namespace Focus.Models;

public class QueueEntry(Uri uri) : IQueueEntry
{
    /// <summary>
    /// <inheritdoc cref="IQueueEntry.Added"/>
    /// </summary>
    public DateTimeOffset Added { get; } = DateTimeOffset.Now;

    /// <summary>
    /// <inheritdoc cref="IQueueEntry.Attempts"/>
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// <inheritdoc cref="IQueueEntry.Errors"/>
    /// </summary>
    public List<RequestError> Errors { get; } = [];

    /// <summary>
    /// <inheritdoc cref="IQueueEntry.Finished"/>
    /// </summary>
    public DateTimeOffset? Finished { get; set; }

    /// <summary>
    /// <inheritdoc cref="IQueueEntry.Responses"/>
    /// </summary>
    public List<Response> Responses { get; } = [];

    /// <summary>
    /// <inheritdoc cref="IQueueEntry.Started"/>
    /// </summary>
    public DateTimeOffset? Started { get; set; }

    /// <summary>
    /// <inheritdoc cref="IQueueEntry.Url"/>
    /// </summary>
    public Uri Url { get; } = uri;
}