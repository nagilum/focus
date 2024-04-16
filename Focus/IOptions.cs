namespace Focus;

public interface IOptions
{
    /// <summary>
    /// Maximum number of retry attempts for non 2xx responses.
    /// </summary>
    int MaxRetryAttempts { get; }
    
    /// <summary>
    /// Request timeout, in seconds.
    /// </summary>
    TimeSpan RequestTimeout { get; }
    
    /// <summary>
    /// List of URLs to start with.
    /// </summary>
    List<Uri> Urls { get; }
}