namespace VoxelKing.Core.Llm;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Sink for recording detailed LLM prompt and response audit logs (F-403).
/// </summary>
public interface ILlmRequestLogger
{
    /// <summary>
    /// Writes an audit record of an LLM generation interaction asynchronously.
    /// </summary>
    /// <param name="entry">Audit log entry containing timestamp, NPC info, request, response, and duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogAsync(LlmQueueLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op audit logger sink when request logging is disabled.
/// </summary>
public sealed class NullLlmRequestLogger : ILlmRequestLogger
{
    public static readonly NullLlmRequestLogger Instance = new();

    public Task LogAsync(LlmQueueLogEntry entry, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
