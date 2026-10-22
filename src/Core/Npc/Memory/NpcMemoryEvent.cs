namespace VoxelKing.Core.Npc.Memory;

using System;

/// <summary>
/// Abstract base class for all typed events stored in an NPC's memory ring buffer.
/// </summary>
public abstract record NpcMemoryEvent
{
    /// <summary>
    /// Machine-readable identifier for the event kind.
    /// </summary>
    public abstract string EventType { get; }

    /// <summary>
    /// In-game simulation timestamp in seconds when this event occurred.
    /// </summary>
    public double Timestamp { get; init; }

    protected NpcMemoryEvent(double timestamp = 0)
    {
        Timestamp = timestamp;
    }

    /// <summary>
    /// Renders the core human-readable description of the event.
    /// </summary>
    public abstract string RenderContent();

    /// <summary>
    /// Renders the event formatted as a markdown bullet point for LLM prompt context,
    /// optionally computing relative time if <paramref name="currentTime"/> is provided.
    /// </summary>
    public virtual string Render(double currentTime = -1)
    {
        string suffix = "";
        if (currentTime >= 0 && Timestamp >= 0)
        {
            double elapsed = currentTime - Timestamp;
            if (elapsed >= 60.0)
            {
                int minutes = (int)Math.Floor(elapsed / 60.0);
                suffix = $" ({minutes} min ago)";
            }
        }

        return $"- {RenderContent()}{suffix}";
    }
}

/// <summary>
/// Event recorded when the NPC hears a spoken message from a player or another entity.
/// </summary>
public sealed record SpeechHeardEvent : NpcMemoryEvent
{
    public override string EventType => "heard";

    public string Speaker { get; init; }
    public string Text { get; init; }
    public bool IsPlayer { get; init; }

    public SpeechHeardEvent(string speaker, string text, bool isPlayer = false, double timestamp = 0)
        : base(timestamp)
    {
        Speaker = speaker ?? "";
        Text = text ?? "";
        IsPlayer = isPlayer || Speaker.StartsWith("Player ", StringComparison.OrdinalIgnoreCase);
    }

    public override string RenderContent()
    {
        string prefix = IsPlayer && !Speaker.StartsWith("Player ", StringComparison.OrdinalIgnoreCase)
            ? $"Player {Speaker}"
            : Speaker;

        return $"{prefix} said: \"{Text}\"";
    }
}

/// <summary>
/// Event recorded when this NPC speaks a message.
/// </summary>
public sealed record SpeechSpokeEvent : NpcMemoryEvent
{
    public override string EventType => "spoke";

    public string Text { get; init; }

    public SpeechSpokeEvent(string text, double timestamp = 0)
        : base(timestamp)
    {
        Text = text ?? "";
    }

    public override string RenderContent() => $"Said: \"{Text}\"";
}

/// <summary>
/// Event recorded when a player or instructor gives a task instruction to this NPC.
/// </summary>
public sealed record InstructionReceivedEvent : NpcMemoryEvent
{
    public override string EventType => "instructed";

    public string Instructor { get; init; }
    public string Instruction { get; init; }

    public InstructionReceivedEvent(string instructor, string instruction, double timestamp = 0)
        : base(timestamp)
    {
        Instructor = instructor ?? "";
        Instruction = instruction ?? "";
    }

    public override string RenderContent() => $"Instructed by {Instructor}: \"{Instruction}\"";
}

/// <summary>
/// Event recorded when a task completes or finishes.
/// </summary>
public sealed record TaskFinishedEvent : NpcMemoryEvent
{
    public override string EventType => "task_finished";

    public string TaskSummary { get; init; }
    public bool Success { get; init; }
    public string? Detail { get; init; }

    public TaskFinishedEvent(string taskSummary, bool success, string? detail = null, double timestamp = 0)
        : base(timestamp)
    {
        TaskSummary = taskSummary ?? "";
        Success = success;
        Detail = detail;
    }

    public override string RenderContent()
    {
        string status = Success ? "Task completed" : "Task failed";
        string detailSuffix = !string.IsNullOrWhiteSpace(Detail) ? $" ({Detail})" : "";
        return $"{status}: {TaskSummary}{detailSuffix}";
    }
}

/// <summary>
/// Event recorded when a Lua brain execution fails with a runtime error.
/// </summary>
public sealed record BrainErrorEvent : NpcMemoryEvent
{
    public override string EventType => "brain_error";

    public string Error { get; init; }

    public BrainErrorEvent(string error, double timestamp = 0)
        : base(timestamp)
    {
        Error = error ?? "";
    }

    public override string RenderContent() => $"Task failed: brain_error \"{Error}\"";
}

/// <summary>
/// Event recorded when this NPC takes damage from an attacker or hazard.
/// </summary>
public sealed record AttackedEvent : NpcMemoryEvent
{
    public override string EventType => "attacked";

    public string Attacker { get; init; }
    public float Damage { get; init; }

    public AttackedEvent(string attacker, float damage, double timestamp = 0)
        : base(timestamp)
    {
        Attacker = attacker ?? "";
        Damage = damage;
    }

    public override string RenderContent() => $"Attacked: took {Damage:F0} damage from {Attacker}";
}

/// <summary>
/// Generic custom event for brain- or quest-specific memory logs.
/// </summary>
public sealed record CustomMemoryEvent : NpcMemoryEvent
{
    public override string EventType => "custom";

    public string Message { get; init; }
    public string Category { get; init; }

    public CustomMemoryEvent(string message, string category = "general", double timestamp = 0)
        : base(timestamp)
    {
        Message = message ?? "";
        Category = category ?? "general";
    }

    public override string RenderContent() => Message;
}
