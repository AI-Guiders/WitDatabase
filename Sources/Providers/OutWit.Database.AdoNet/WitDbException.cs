using System.Data.Common;

namespace OutWit.Database.AdoNet;

/// <summary>
/// The exception that is thrown when a WitDatabase operation fails.
/// </summary>
public class WitDbException : DbException
{
    #region Constants

    /// <summary>
    /// Error code for general errors.
    /// </summary>
    public const int ERROR_GENERAL = 1;

    /// <summary>
    /// Error code for syntax errors.
    /// </summary>
    public const int ERROR_SYNTAX = 2;

    /// <summary>
    /// Error code for constraint violations.
    /// </summary>
    public const int ERROR_CONSTRAINT = 3;

    /// <summary>
    /// Error code for type conversion errors.
    /// </summary>
    public const int ERROR_TYPE = 4;

    /// <summary>
    /// Error code for I/O errors.
    /// </summary>
    public const int ERROR_IO = 5;

    /// <summary>
    /// Error code for transaction errors.
    /// </summary>
    public const int ERROR_TRANSACTION = 6;

    /// <summary>
    /// Error code for timeout errors.
    /// </summary>
    public const int ERROR_TIMEOUT = 7;

    /// <summary>
    /// Error code for lock errors.
    /// </summary>
    public const int ERROR_LOCK = 8;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbException"/> class.
    /// </summary>
    public WitDbException()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WitDbException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbException"/> class
    /// with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public WitDbException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbException"/> class
    /// with a specified error message and error code.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    public WitDbException(string message, int errorCode)
        : base(message)
    {
        WitErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbException"/> class
    /// with a specified error message, error code, and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public WitDbException(string message, int errorCode, Exception innerException)
        : base(message, innerException)
    {
        WitErrorCode = errorCode;
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a WitDbException from another exception.
    /// </summary>
    /// <param name="exception">The source exception.</param>
    /// <returns>A WitDbException wrapping the source exception.</returns>
    public static WitDbException FromException(Exception exception)
    {
        if (exception is WitDbException witEx)
            return witEx;

        var errorCode = exception switch
        {
            InvalidOperationException when exception.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase) => ERROR_CONSTRAINT,
            InvalidOperationException when exception.Message.Contains("syntax", StringComparison.OrdinalIgnoreCase) => ERROR_SYNTAX,
            InvalidCastException => ERROR_TYPE,
            IOException => ERROR_IO,
            TimeoutException => ERROR_TIMEOUT,
            _ => ERROR_GENERAL
        };

        return new WitDbException(exception.Message, errorCode, exception);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the WitDatabase-specific error code.
    /// </summary>
    public int WitErrorCode { get; }

    /// <inheritdoc/>
    public override int ErrorCode => WitErrorCode;

    #endregion
}
