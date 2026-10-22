namespace VoxelKing.Core.Npc.Memory;

using System;
using System.Collections.Generic;
using System.Text.Json;
using VoxelKing.Core.Common;

/// <summary>
/// Data transfer object for serializing and deserializing an entire NPC memory state.
/// </summary>
public sealed class NpcMemoryDto
{
    public Dictionary<string, JsonElement> KvStore { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, Vec3i> Places { get; set; } = new(StringComparer.Ordinal);
    public List<NpcMemoryEventDto> Events { get; set; } = new();
    public int EventCapacity { get; set; } = 50;
}

/// <summary>
/// Data transfer object for a single typed memory event.
/// </summary>
public sealed class NpcMemoryEventDto
{
    public string Type { get; set; } = "";
    public double Timestamp { get; set; }
    public string? Speaker { get; set; }
    public string? Text { get; set; }
    public bool? IsPlayer { get; set; }
    public string? Instructor { get; set; }
    public string? Instruction { get; set; }
    public string? TaskSummary { get; set; }
    public bool? Success { get; set; }
    public string? Detail { get; set; }
    public string? Error { get; set; }
    public string? Attacker { get; set; }
    public float? Damage { get; set; }
    public string? Message { get; set; }
    public string? Category { get; set; }

    public static NpcMemoryEventDto FromEvent(NpcMemoryEvent evt)
    {
        var dto = new NpcMemoryEventDto
        {
            Type = evt.EventType,
            Timestamp = evt.Timestamp
        };

        switch (evt)
        {
            case SpeechHeardEvent heard:
                dto.Speaker = heard.Speaker;
                dto.Text = heard.Text;
                dto.IsPlayer = heard.IsPlayer;
                break;
            case SpeechSpokeEvent spoke:
                dto.Text = spoke.Text;
                break;
            case InstructionReceivedEvent instructed:
                dto.Instructor = instructed.Instructor;
                dto.Instruction = instructed.Instruction;
                break;
            case TaskFinishedEvent finished:
                dto.TaskSummary = finished.TaskSummary;
                dto.Success = finished.Success;
                dto.Detail = finished.Detail;
                break;
            case BrainErrorEvent brainError:
                dto.Error = brainError.Error;
                break;
            case AttackedEvent attacked:
                dto.Attacker = attacked.Attacker;
                dto.Damage = attacked.Damage;
                break;
            case CustomMemoryEvent custom:
                dto.Message = custom.Message;
                dto.Category = custom.Category;
                break;
        }

        return dto;
    }

    public NpcMemoryEvent ToEvent()
    {
        return Type.ToLowerInvariant() switch
        {
            "heard" => new SpeechHeardEvent(Speaker ?? "", Text ?? "", IsPlayer ?? false, Timestamp),
            "spoke" => new SpeechSpokeEvent(Text ?? "", Timestamp),
            "instructed" => new InstructionReceivedEvent(Instructor ?? "", Instruction ?? "", Timestamp),
            "task_finished" => new TaskFinishedEvent(TaskSummary ?? "", Success ?? false, Detail, Timestamp),
            "brain_error" => new BrainErrorEvent(Error ?? "", Timestamp),
            "attacked" => new AttackedEvent(Attacker ?? "", Damage ?? 0f, Timestamp),
            "custom" => new CustomMemoryEvent(Message ?? "", Category ?? "general", Timestamp),
            _ => new CustomMemoryEvent(Message ?? $"[{Type}]", Category ?? Type, Timestamp)
        };
    }
}
