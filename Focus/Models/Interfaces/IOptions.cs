namespace Focus.Models.Interfaces;

public interface IOptions
{
    /// <summary>
    /// Which rendering engine to use.
    /// </summary>
    RenderingEngine RenderingEngine { get; }
    
    /// <summary>
    /// Request timeout, in seconds.
    /// </summary>
    float RequestTimeout { get; }
    
    /// <summary>
    /// List of URLs to start with.
    /// </summary>
    List<Uri> Urls { get; }
}