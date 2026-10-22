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
    private bool _isThinking;
    private float _thinkingTimer;
    private int _thinkingDots = 1;

    public bool IsThinking => _isThinking;

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
    /// Displays an animated "..." thinking bubble that pulses until thinking ends.
    /// </summary>
    public void SetThinking(bool isThinking)
    {
        _isThinking = isThinking;
        EnsureLabelCreated();

        if (_isThinking)
        {
            _thinkingTimer = 0f;
            _thinkingDots = 1;
            Label.Text = ".";
            Label.Modulate = Colors.White;
            Visible = true;
        }
        else
        {
            if (Visible && (Label.Text == "." || Label.Text == ".." || Label.Text == "..."))
            {
                Visible = false;
            }
        }
    }

    /// <summary>
    /// Displays a speech message in the bubble with an optional duration.
    /// Resets fade out countdown and clears thinking state.
    /// </summary>
    public void ShowSpeech(string text, float duration = DefaultDuration)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _isThinking = false;
        EnsureLabelCreated();

        Label.Text = $"\"{text.Trim()}\"";
        _totalDuration = Mathf.Max(1.0f, duration);
        _timeRemaining = _totalDuration;
        Label.Modulate = Colors.White;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        if (_isThinking)
        {
            _thinkingTimer += (float)delta;
            if (_thinkingTimer >= 0.35f)
            {
                _thinkingTimer = 0f;
                _thinkingDots = (_thinkingDots % 3) + 1;
                Label.Text = new string('.', _thinkingDots);
            }
            Label.Modulate = Colors.White;
            return;
        }

        if (_timeRemaining <= 0f)
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
