using Focus.Models.Interfaces;

namespace Focus.Models;

public class Response : IResponse
{
    /// <summary>
    /// <inheritdoc cref="IResponse.ContentType"/>
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// <inheritdoc cref="IResponse.Created"/>
    /// </summary>
    public DateTimeOffset Created { get; } = DateTimeOffset.Now;

    /// <summary>
    /// <inheritdoc cref="IResponse.StatusCode"/>
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// <inheritdoc cref="IResponse.StatusDescription"/>
    /// </summary>
    public required string StatusDescription { get; init; }

    /// <summary>
    /// <inheritdoc cref="IResponse.Time"/>
    /// </summary>
    public required long Time { get; init; }
}