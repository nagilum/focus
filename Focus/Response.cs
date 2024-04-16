namespace Focus;

public class Response(
    HttpResponseMessage res,
    long responseTime) : IResponse
{
    /// <summary>
    /// <inheritdoc cref="IResponse.Created"/>
    /// </summary>
    public DateTimeOffset Created { get; } = DateTimeOffset.Now;

    /// <summary>
    /// <inheritdoc cref="IResponse.StatusCode"/>
    /// </summary>
    public int StatusCode { get; } = (int)res.StatusCode;

    /// <summary>
    /// <inheritdoc cref="IResponse.StatusDescription"/>
    /// </summary>
    public string StatusDescription { get; } = Tools.GetStatusCodeDescription((int)res.StatusCode);

    /// <summary>
    /// <inheritdoc cref="IResponse.Time"/>
    /// </summary>
    public long Time { get; } = responseTime;
}