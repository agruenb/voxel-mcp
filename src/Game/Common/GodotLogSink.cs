namespace VoxelKing.Game.Common;

using System;
using Godot;
using VoxelKing.Core.Common;

/// <summary>
/// Routes Core logging events to Godot engine console sinks (GD.Print, GD.PushWarning, GD.PushError).
/// </summary>
public sealed class GodotLogSink : ILogSink
{
	public void Emit(LogLevel level, string category, string message, Exception? exception = null)
	{
		var formatted = string.IsNullOrEmpty(category)
			? $"[{level}] {message}"
			: $"[{category}] [{level}] {message}";

		if (exception != null)
		{
			formatted += $"\n{exception}";
		}

		switch (level)
		{
			case LogLevel.Error:
				GD.PrintErr(formatted);
				GD.PushError(formatted);
				break;
			case LogLevel.Warning:
				GD.PrintErr(formatted);
				GD.PushWarning(formatted);
				break;
			case LogLevel.Info:
			case LogLevel.Debug:
			case LogLevel.Trace:
				GD.Print(formatted);
				break;
			default:
				break;
		}
	}
}
