namespace Focus.Services;

public interface ICrawlerService
{
    /// <summary>
    /// Dispose Playwright instance.
    /// </summary>
    Task DisposePlaywright();
    
    /// <summary>
    /// Run the crawler.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Run(CancellationToken cancellationToken);

    /// <summary>
    /// Setup Playwright instance.
    /// </summary>
    Task<bool> SetupPlaywright();
}