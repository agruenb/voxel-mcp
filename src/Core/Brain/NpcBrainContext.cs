namespace VoxelKing.Core.Brain;

using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc;
using VoxelKing.Core.Npc.Memory;
using VoxelKing.Core.Npc.Perception;

/// <summary>
/// Default implementation of <see cref="INpcBrainContext"/> bridging the live NPC simulation
/// to Lua script bindings.
/// </summary>
public sealed class NpcBrainContext : INpcBrainContext
{
    private readonly NpcState _npc;
    private readonly Action<string>? _logCallback;
    private readonly Action<string>? _sayCallback;
    private readonly Func<string, CallbackArguments, ActionRequest>? _actionCallback;
    private float _currentTime;

    /// <summary>
    /// Initializes a new instance of <see cref="NpcBrainContext"/>.
    /// </summary>
    public NpcBrainContext(
        NpcState npc,
        Action<string>? logCallback = null,
        Action<string>? sayCallback = null,
        Func<string, CallbackArguments, ActionRequest>? actionCallback = null)
    {
        _npc = npc ?? throw new ArgumentNullException(nameof(npc));
        _logCallback = logCallback;
        _sayCallback = sayCallback;
        _actionCallback = actionCallback;
    }

    /// <inheritdoc />
    public NpcState Npc => _npc;

    /// <inheritdoc />
    public INpcMemory Memory => _npc.Memory;

    /// <inheritdoc />
    public INpcPerception? Perception => _npc.Perception;

    /// <inheritdoc />
    public bool IsInsideHandler { get; set; }

    /// <inheritdoc />
    public bool IsDone { get; private set; }

    /// <inheritdoc />
    public string? DoneSummary { get; private set; }

    /// <inheritdoc />
    public bool RestartRequested { get; private set; }

    /// <inheritdoc />
    public ActionRequest? LastActionRequest { get; private set; }

    /// <inheritdoc />
    public float CurrentTime
    {
        get => _currentTime;
        set => _currentTime = value;
    }

    /// <inheritdoc />
    public void Log(string message)
    {
        _logCallback?.Invoke(message);
    }

    /// <inheritdoc />
    public void Say(string text)
    {
        if (text == null) return;

        // Truncate to max 200 chars per LUA_BRAIN_API §3.3
        if (text.Length > 200)
        {
            text = text[..200];
        }

        // Rate limiting: 1 per 2 seconds. Extra calls dropped silently per LUA_BRAIN_API §3.3
        if (_npc.LastSayTime >= 0f && (_currentTime - _npc.LastSayTime) < 2.0f)
        {
            return;
        }

        _npc.LastSayTime = _currentTime;
        _npc.Memory.RecordSpeechSpoke(text, _currentTime);
        _sayCallback?.Invoke(text);
    }

    /// <inheritdoc />
    public void LookAt(Vec3i pos)
    {
        Vec3 targetCenter = new(pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f);
        Vec3 diff = targetCenter - _npc.Position;
        _npc.Facing = MathF.Atan2(diff.X, -diff.Z);
    }

    /// <inheritdoc />
    public void Done(string? summary = null)
    {
        IsDone = true;
        DoneSummary = summary;
        _npc.Memory.RecordTaskFinished(summary ?? "Task completed", success: true, timestamp: _currentTime);
    }

    /// <inheritdoc />
    public void RequestRestart()
    {
        RestartRequested = true;
    }

    /// <inheritdoc />
    public void Interrupt()
    {
        _npc.CurrentAction?.Cancel();
    }

    /// <inheritdoc />
    public ActionRequest BeginAction(string actionName, CallbackArguments args)
    {
        if (_actionCallback != null)
        {
            var req = _actionCallback(actionName, args);
            LastActionRequest = req;
            return req;
        }

        var argList = new List<object?>();
        for (int i = 1; i < args.Count; i++)
        {
            argList.Add(LuaValueConverter.ToClrObject(args[i]));
        }

        var request = new ActionRequest(actionName, argList, _currentTime);
        LastActionRequest = request;
        return request;
    }

    /// <inheritdoc />
    public void ClearLastActionRequest()
    {
        LastActionRequest = null;
    }

    /// <inheritdoc />
    public void ResetRestartRequest()
    {
        RestartRequested = false;
    }

    /// <inheritdoc />
    public void ResetDone()
    {
        IsDone = false;
        DoneSummary = null;
    }
}
