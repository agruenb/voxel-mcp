namespace VoxelKing.Core.Llm;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service abstraction for communicating with an LLM provider to compile natural language instructions into Lua brains (F-402).
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends an instruction request to the LLM model and returns the structured brain response or error.
    /// </summary>
    /// <param name="request">Request parameters, including system instructions and turn messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task resolving to the parsed <see cref="LlmResponse"/>.</returns>
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
