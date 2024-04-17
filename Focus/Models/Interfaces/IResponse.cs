namespace Focus.Models.Interfaces;

public interface IResponse
{
    /// <summary>
    /// Content type.
    /// </summary>
    string? ContentType { get; }
    
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