namespace VoxelKing.Core.Llm;

using System;

/// <summary>
/// Machine-readable categories for errors that occur during LLM communication or parsing.
/// </summary>
public enum LlmErrorKind
{
    None = 0,

    /// <summary>
    /// The API key is missing, unauthorized, or invalid (HTTP 401/403).
    /// </summary>
    InvalidApiKey,

    /// <summary>
    /// Rate limit or quota exhausted (HTTP 429). Retryable with backoff.
    /// </summary>
    RateLimited,

    /// <summary>
    /// Request timed out waiting for the remote LLM server to reply. Retryable.
    /// </summary>
    Timeout,

    /// <summary>
    /// Network connection failure or DNS error. Retryable.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Remote server error (HTTP 500, 502, 503, 504). Retryable.
    /// </summary>
    ServerError,

    /// <summary>
    /// Request was malformed or rejected by the LLM endpoint (HTTP 400).
    /// </summary>
    BadRequest,

    /// <summary>
    /// The requested model or endpoint was not found (HTTP 404).
    /// </summary>
    NotFound,

    /// <summary>
    /// The response was received but could not be parsed as valid JSON matching the schema.
    /// </summary>
    InvalidJson,

    /// <summary>
    /// The request was cancelled by the client.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Unclassified or unexpected error.
    /// </summary>
    Unknown
}

/// <summary>
/// Details about an LLM generation failure.
/// </summary>
public sealed record LlmError
{
    /// <summary>
    /// Machine-readable category of the failure.
    /// </summary>
    public LlmErrorKind Kind { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// HTTP status code if the error was produced by an HTTP response.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Associated exception if available.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Indicates whether the failure is transient and can be retried automatically (F-403).
    /// </summary>
    public bool IsRetryable => Kind is LlmErrorKind.RateLimited
                                   or LlmErrorKind.ServerError
                                   or LlmErrorKind.Timeout
                                   or LlmErrorKind.NetworkError;

    public LlmError(LlmErrorKind kind, string message, int? statusCode = null, Exception? exception = null)
    {
        Kind = kind;
        Message = message ?? string.Empty;
        StatusCode = statusCode;
        Exception = exception;
    }

    public static LlmError InvalidApiKey(string message, int? statusCode = 401) =>
        new(LlmErrorKind.InvalidApiKey, message, statusCode);

    public static LlmError RateLimited(string message, int? statusCode = 429) =>
        new(LlmErrorKind.RateLimited, message, statusCode);

    public static LlmError Timeout(string message = "LLM request timed out.") =>
        new(LlmErrorKind.Timeout, message);

    public static LlmError Network(string message, Exception? ex = null) =>
        new(LlmErrorKind.NetworkError, message, exception: ex);

    public static LlmError Server(string message, int statusCode = 500) =>
        new(LlmErrorKind.ServerError, message, statusCode);

    public static LlmError BadRequest(string message, int statusCode = 400) =>
        new(LlmErrorKind.BadRequest, message, statusCode);

    public static LlmError InvalidJson(string message) =>
        new(LlmErrorKind.InvalidJson, message);

    public static LlmError Cancelled(string message = "Request was cancelled.") =>
        new(LlmErrorKind.Cancelled, message);

    public static LlmError Unknown(string message, Exception? ex = null) =>
        new(LlmErrorKind.Unknown, message, exception: ex);

    public override string ToString() =>
        StatusCode.HasValue
            ? $"[{Kind}] (HTTP {StatusCode.Value}): {Message}"
            : $"[{Kind}]: {Message}";
}
