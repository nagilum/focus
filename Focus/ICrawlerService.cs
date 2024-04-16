namespace Focus;

public interface ICrawlerService
{
    /// <summary>
    /// Run the crawler.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Run(CancellationToken cancellationToken);
}