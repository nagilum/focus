using Focus.Models.Interfaces;

namespace Focus.Models;

public class Response : IResponse
{
    /// <summary>
    /// <inheritdoc cref="IResponse.ContentType"/>
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// <inheritdoc cref="IResponse.Headers"/>
    /// </summary>
    public Dictionary<string, string> Headers { get; } = [];

    /// <summary>
    /// <inheritdoc cref="IResponse.RequestType"/>
    /// </summary>
    public required RequestType RequestType { get; init; }

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