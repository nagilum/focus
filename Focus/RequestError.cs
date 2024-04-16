namespace Focus;

public class RequestError(string message) : IRequestError
{
    /// <summary>
    /// <inheritdoc cref="IRequestError.Created"/>
    /// </summary>
    public DateTimeOffset Created { get; } = DateTimeOffset.Now;

    /// <summary>
    /// <inheritdoc cref="IRequestError.Message"/>
    /// </summary>
    public string Message { get; } = message;
}