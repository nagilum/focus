namespace Focus.Models.Interfaces;

public interface IRequestError
{
    /// <summary>
    /// When the error occurred.
    /// </summary>
    DateTimeOffset Created { get; }
    
    /// <summary>
    /// Error message.
    /// </summary>
    string Message { get; }
    
    /// <summary>
    /// Error type.
    /// </summary>
    string Type { get; }
}