namespace Focus.Models.Interfaces;

public interface IQueueEntry
{
    /// <summary>
    /// When the entry was added.
    /// </summary>
    DateTimeOffset Added { get; }
    
    /// <summary>
    /// Attempts on this URL.
    /// </summary>
    int Attempts { get; }
    
    /// <summary>
    /// List of request errors.
    /// </summary>
    List<RequestError> Errors { get; }
    
    /// <summary>
    /// When the entry was marked as finished.
    /// </summary>
    DateTimeOffset? Finished { get; }
    
    /// <summary>
    /// List of responses.
    /// </summary>
    List<Response> Responses { get; }
    
    /// <summary>
    /// URL to crawl.
    /// </summary>
    Uri Url { get; }
}