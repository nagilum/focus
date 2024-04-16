namespace Focus;

public interface IResponse
{
    /// <summary>
    /// When the response was added.
    /// </summary>
    DateTimeOffset Created { get; }
    
    /// <summary>
    /// HTTP status code.
    /// </summary>
    int StatusCode { get; }
    
    /// <summary>
    /// HTTP status code description.
    /// </summary>
    string StatusDescription { get; }
    
    /// <summary>
    /// Response time.
    /// </summary>
    long Time { get; }
}