namespace Focus;

public class Options : IOptions
{
    /// <summary>
    /// <inheritdoc cref="IOptions.MaxRetryAttempts"/>
    /// </summary>
    public int MaxRetryAttempts { get; set; }

    /// <summary>
    /// <inheritdoc cref="IOptions.RequestTimeout"/>
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// <inheritdoc cref="IOptions.Urls"/>
    /// </summary>
    public List<Uri> Urls { get; } = [];
}