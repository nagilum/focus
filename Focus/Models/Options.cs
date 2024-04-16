using Focus.Models.Interfaces;

namespace Focus.Models;

public class Options : IOptions
{
    /// <summary>
    /// <inheritdoc cref="IOptions.MaxRetryAttempts"/>
    /// </summary>
    public int MaxRetryAttempts { get; set; }

    /// <summary>
    /// <inheritdoc cref="IOptions.RenderingEngine"/>
    /// </summary>
    public RenderingEngine RenderingEngine { get; set; } = RenderingEngine.Chromium;

    /// <summary>
    /// <inheritdoc cref="IOptions.RequestTimeout"/>
    /// </summary>
    public float RequestTimeout { get; set; } = 10000;

    /// <summary>
    /// <inheritdoc cref="IOptions.Urls"/>
    /// </summary>
    public List<Uri> Urls { get; } = [];
}