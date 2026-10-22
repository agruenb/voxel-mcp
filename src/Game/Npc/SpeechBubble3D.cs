namespace VoxelKing.Game.Npc;

using Godot;

/// <summary>
/// 3D speech bubble rendered above an NPC's head in the Godot scene tree (ARCHITECTURE §5.6).
/// Automatically billboards towards the camera, wraps text, and smoothly fades out over time.
/// </summary>
public partial class SpeechBubble3D : Node3D
{
    public const float DefaultDuration = 4.0f;
    public const float FadeDuration = 1.0f;

    public Label3D Label { get; private set; } = null!;

    private float _timeRemaining;
    private float _totalDuration = DefaultDuration;

    public override void _Ready()
    {
        EnsureLabelCreated();
        Visible = false;
    }

    private void EnsureLabelCreated()
    {
        if (Label != null)
        {
            return;
        }

        Label = new Label3D
        {
            Name = "BubbleLabel",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            OutlineSize = 6,
            OutlineModulate = new Color(0.08f, 0.10f, 0.15f, 0.95f),
            FontSize = 22,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Width = 260.0f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = Colors.White
        };

        AddChild(Label);
    }

    /// <summary>
    /// Displays a speech message in the bubble with an optional duration.
    /// Resets fade out countdown.
    /// </summary>
    public void ShowSpeech(string text, float duration = DefaultDuration)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        EnsureLabelCreated();

        Label.Text = $"\"{text.Trim()}\"";
        _totalDuration = Mathf.Max(1.0f, duration);
        _timeRemaining = _totalDuration;
        Label.Modulate = Colors.White;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible || _timeRemaining <= 0f)
        {
            return;
        }

        _timeRemaining -= (float)delta;

        if (_timeRemaining <= 0f)
        {
            Visible = false;
            Label.Modulate = new Color(1f, 1f, 1f, 0f);
            return;
        }

        // Smooth fade out over the last FadeDuration seconds
        if (_timeRemaining < FadeDuration)
        {
            float alpha = Mathf.Clamp(_timeRemaining / FadeDuration, 0f, 1f);
            Label.Modulate = new Color(1f, 1f, 1f, alpha);
        }
        else
        {
            Label.Modulate = Colors.White;
        }
    }
}
