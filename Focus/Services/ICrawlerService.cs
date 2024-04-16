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

    /// <summary>
    /// Update/redraw the UI.
    /// </summary>
    /// <param name="redraw">Whether to redraw the whole UI.</param>
    void UpdateUi(bool redraw);

    /// <summary>
    /// Write queue to disk.
    /// </summary>
    Task WriteQueueToDisk();
}