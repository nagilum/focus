using Focus.Models.Interfaces;

namespace Focus.Models;

public class QueueEntry(Uri uri, bool playwrightRequest) : IQueueEntry
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
    /// <inheritdoc cref="IQueueEntry.PlaywrightRequest"/>
    /// </summary>
    public bool PlaywrightRequest { get; } = playwrightRequest;

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
    /// <inheritdoc cref="IQueueEntry.Url"/>
    /// </summary>
    public Uri Url { get; } = uri;
}