using Focus.Models.Interfaces;

namespace Focus.Models;

public class RequestError(string type, string message) : IRequestError
{
    /// <summary>
    /// <inheritdoc cref="IRequestError.Created"/>
    /// </summary>
    public DateTimeOffset Created { get; } = DateTimeOffset.Now;

    /// <summary>
    /// <inheritdoc cref="IRequestError.Message"/>
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// <inheritdoc cref="IRequestError.Type"/>
    /// </summary>
    public string Type { get; } = type;

    public RequestError(Exception ex) : this(ex.GetType().ToString(), ex.Message)
    {
    }
}