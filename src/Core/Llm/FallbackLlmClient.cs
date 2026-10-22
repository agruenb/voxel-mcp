namespace VoxelKing.Core.Llm;

using System;
using System.Threading;
using System.Threading.Tasks;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;

/// <summary>
/// Composite LLM client that automatically falls back to <see cref="MockLlmClient"/> with an in-game HUD warning
/// when no Gemini API key is configured or when the provided key is rejected (F-402).
/// </summary>
public sealed class FallbackLlmClient : ILlmClient
{
    private readonly ILlmClient _primaryClient;
    private readonly ILlmClient _mockClient;
    private readonly LlmConfig _config;
    private readonly IEventBus? _eventBus;
    private readonly ILogger? _logger;
    private int _hasWarned;

    /// <summary>
    /// Gets a value indicating whether calls are currently falling back to the mock client.
    /// </summary>
    public bool IsUsingMock { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a warning notification has been published.
    /// </summary>
    public bool HasWarned => _hasWarned != 0;

    public FallbackLlmClient(
        ILlmClient primaryClient,
        ILlmClient mockClient,
        LlmConfig config,
        IEventBus? eventBus = null,
        ILogger? logger = null)
    {
        _primaryClient = primaryClient ?? throw new ArgumentNullException(nameof(primaryClient));
        _mockClient = mockClient ?? throw new ArgumentNullException(nameof(mockClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _eventBus = eventBus;
        _logger = logger;

        IsUsingMock = string.IsNullOrWhiteSpace(_config.ApiKey);
    }

    /// <inheritdoc/>
    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Case 1: No API key configured -> auto fallback to mock
        if (string.IsNullOrWhiteSpace(_config.ApiKey) || IsUsingMock)
        {
            IsUsingMock = true;
            EmitWarningOnce("No Gemini API key configured. Using Mock LLM client.");
            return await _mockClient.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // Case 2: API key configured -> call primary client
        var response = await _primaryClient.GenerateAsync(request, cancellationToken).ConfigureAwait(false);

        // Case 3: Remote API failure (invalid key, service unavailable, etc.) -> fallback to mock with warning
        if (!response.IsSuccess && response.Error != null && response.Error.Kind != LlmErrorKind.Cancelled)
        {
            IsUsingMock = true;
            string reason = response.Error.Kind == LlmErrorKind.InvalidApiKey
                ? $"Gemini API key rejected ({response.Error.Message})"
                : $"Gemini service unavailable ({response.Error.Message})";
            EmitWarningOnce($"{reason}. Falling back to Mock LLM client.");
            return await _mockClient.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private void EmitWarningOnce(string message)
    {
        if (Interlocked.Exchange(ref _hasWarned, 1) == 0)
        {
            _logger?.LogWarning(message);

            // Broadcast structured warning event
            _eventBus?.Publish(new LlmWarningEvent(message));

            // Broadcast on EntitySpoke so it appears immediately on HUD ChatLogUI
            _eventBus?.Publish(new EntitySpoke(
                SpeakerId: EntityId.None,
                SpeakerName: "System",
                Position: Vec3.Zero,
                Text: message,
                HearingRadius: 9999f));
        }
    }
}
