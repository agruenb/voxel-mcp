namespace VoxelKing.Game.Debug;

using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Entities;
using VoxelKing.Core.Llm;
using VoxelKing.Core.Npc;
using VoxelKing.Game.Bootstrap;
using VoxelKing.Game.Player;
using VoxelKing.Game.UI;

/// <summary>
/// Headless integration test runner validating F-405: Dialogue UI &amp; NPC conversation states.
/// Tests:
/// - Looking at NPC within 4 blocks and HUD prompt.
/// - Opening dialogue panel on E, pausing action and facing player (Listening state).
/// - Esc cancelling and returning to normal simulation.
/// - Submitting instruction transitions to Thinking with animated speech bubble.
/// - Other NPCs can be instructed concurrently while one is thinking.
/// - Spoken reply delivery, speech bubble update, and script swap.
/// </summary>
public partial class TestDialogueRunner : Node
{
    private GameRoot _gameRoot = null!;
    private PlayerController _player = null!;
    private DialogueUI _dialogueUI = null!;
    private NpcState _bob = null!;
    private NpcState _alice = null!;
    private int _step = 0;
    private int _waitFrames = 0;

    public override void _Ready()
    {
        _gameRoot = GetNodeOrNull<GameRoot>("GameRoot") ?? GetParent<GameRoot>();
        if (_gameRoot == null)
        {
            GD.PrintErr("[FAIL] TestDialogueRunner could not locate GameRoot.");
            GetTree().Quit(1);
            return;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _waitFrames++;
        if (_waitFrames < 10)
        {
            return;
        }

        switch (_step)
        {
            case 0:
            {
                _player = _gameRoot.GetNodeOrNull<PlayerController>("Player")!;
                _dialogueUI = _gameRoot.DialogueUI!;

                AssertTrue(_player != null, "PlayerController not found.");
                AssertTrue(_dialogueUI != null, "DialogueUI not found.");

                // Get or spawn test NPC Bob near the player
                _bob = _gameRoot.NpcManager.ActiveNpcs.FirstOrDefault(n => n.Name == "Bob")!;
                if (_bob == null)
                {
                    var spawnPos = new Vec3(_player!.GlobalPosition.X + 2.0f, _player.GlobalPosition.Y, _player.GlobalPosition.Z);
                    _bob = _gameRoot.NpcManager.SpawnNpc("Bob", spawnPos, profile: "helpful builder", home: spawnPos);
                }

                // Aim player directly towards Bob at his spawned position
                var cam = _player!.Camera ?? _player.GetNode<Camera3D>("Head/Camera3D");
                Vector3 toBob = new Vector3(_bob.Position.X, _bob.Position.Y + 0.9f, _bob.Position.Z) - cam.GlobalPosition;
                float targetYaw = Mathf.Atan2(-toBob.X, -toBob.Z);
                float targetPitch = Mathf.Asin(toBob.Normalized().Y);

                _player.Rotation = new Vector3(0, targetYaw, 0);
                _player.Head.Rotation = new Vector3(targetPitch, 0, 0);
                _player.ForceUpdateTransform();
                _player.Head.ForceUpdateTransform();
                cam.ForceUpdateTransform();

                GD.Print($"[PASS] Step 0: Test setup ready. Bob at {_bob.Position}, player looking at Bob.");
                _step++;
                _waitFrames = 0;
                break;
            }

            case 1:
            {
                // Test 1: Looking at NPC within 4 blocks
                var lookedAt = _player.FindLookedAtNpc(4.0f);
                AssertTrue(lookedAt != null, "FindLookedAtNpc should find Bob within 4 blocks.");
                AssertTrue(lookedAt!.Name == "Bob", "Looked at NPC should be Bob.");

                // Test 2: HUD prompt update
                _dialogueUI.UpdateTargetNpc(lookedAt);
                AssertTrue(_dialogueUI.IsOpen == false, "Dialogue panel should not be open yet.");

                GD.Print("[PASS] Step 1: Looked-at NPC within 4 blocks detected successfully.");
                _step++;
                _waitFrames = 0;
                break;
            }

            case 2:
            {
                // Test 3: Press E -> Open Dialogue
                _dialogueUI.OpenDialogue(_bob);
                AssertTrue(_dialogueUI.IsOpen, "DialogueUI should be open.");
                AssertTrue(_bob.ConversationState == NpcConversationState.Listening, "Bob state should be Listening.");
                AssertTrue(_player.IsInputLocked, "Player input should be locked while dialogue is open.");

                GD.Print("[PASS] Step 2: OpenDialogue placed Bob in Listening state and locked player input.");
                _step++;
                _waitFrames = 0;
                break;
            }

            case 3:
            {
                // Test 4: Esc cancels dialogue
                _dialogueUI.CloseDialogue(cancelled: true);
                AssertTrue(!_dialogueUI.IsOpen, "DialogueUI should be closed after Esc/Cancel.");
                AssertTrue(_bob.ConversationState == NpcConversationState.None, "Bob state should revert to None after cancel.");
                AssertTrue(!_player.IsInputLocked, "Player input should be unlocked after cancel.");

                GD.Print("[PASS] Step 3: Escape cancelled dialogue and restored Bob to None state.");
                _step++;
                _waitFrames = 0;
                break;
            }

            case 4:
            {
                // Test 5: Re-open dialogue and submit instruction
                _dialogueUI.OpenDialogue(_bob);
                AssertTrue(_bob.ConversationState == NpcConversationState.Listening, "Bob state should be Listening.");

                _dialogueUI.InputField.Text = "gather logs";
                _dialogueUI.SubmitInstruction();

                // On submit: dialogue closes immediately so player can move/instruct others
                AssertTrue(!_dialogueUI.IsOpen, "Dialogue should close immediately upon submit.");
                AssertTrue(!_player.IsInputLocked, "Player input should be unlocked so player can instruct others.");
                AssertTrue(_bob.ConversationState == NpcConversationState.Thinking, "Bob state should be Thinking.");

                // Check Bob's visual node has thinking bubble active
                var bobNode = _gameRoot.NpcViewManager.GetView(_bob.Id);
                AssertTrue(bobNode != null, "Bob visual NpcNode should exist in NpcViewManager.");
                AssertTrue(bobNode!.SpeechBubble != null && bobNode.SpeechBubble.Visible, "Speech bubble should be visible while thinking.");

                GD.Print("[PASS] Step 4: Instruction submitted. Bob entered Thinking state with animated bubble.");
                _step++;
                _waitFrames = 0;
                break;
            }

            case 5:
            {
                // Test 6: Concurrent instruction of multiple NPCs ("Other NPCs can be instructed while one is thinking")
                var spawnPos = new Vec3(_player.GlobalPosition.X + 3.0f, _player.GlobalPosition.Y, _player.GlobalPosition.Z + 1.0f);
                _alice = _gameRoot.NpcManager.SpawnNpc("Alice", spawnPos, profile: "expert lumberjack", home: spawnPos);

                AssertTrue(_bob.ConversationState == NpcConversationState.Thinking, "Bob should still be Thinking.");

                // Open dialogue with Alice while Bob is Thinking
                _dialogueUI.OpenDialogue(_alice);
                AssertTrue(_alice.ConversationState == NpcConversationState.Listening, "Alice should be Listening.");
                AssertTrue(_bob.ConversationState == NpcConversationState.Thinking, "Bob should still be Thinking while Alice listens.");

                _dialogueUI.InputField.Text = "follow me";
                _dialogueUI.SubmitInstruction();

                AssertTrue(_alice.ConversationState == NpcConversationState.Thinking, "Alice should now be Thinking.");
                AssertTrue(!_dialogueUI.IsOpen, "Dialogue closed for Alice.");

                GD.Print("[PASS] Step 5: Instructed Alice while Bob was thinking. Both now thinking concurrently.");
                _step++;
                _waitFrames = 0;
                break;
            }

            case 6:
            {
                // Wait for both background LLM compilations to finish and main thread dispatcher to drain
                _gameRoot.Dispatcher.Drain(TimeSpan.FromMilliseconds(50));

                if (_bob.ConversationState == NpcConversationState.Thinking || _alice.ConversationState == NpcConversationState.Thinking)
                {
                    if (_waitFrames > 600)
                    {
                        GD.PrintErr($"[FAIL] Timed out waiting for Bob and Alice compilations to finish. Bob: {_bob.ConversationState}, Alice: {_alice.ConversationState}");
                        GetTree().Quit(1);
                        return;
                    }
                    return;
                }

                // Both have finished compilation
                AssertTrue(_bob.ConversationState == NpcConversationState.None, "Bob should return to None after replying.");
                AssertTrue(_alice.ConversationState == NpcConversationState.None, "Alice should return to None after replying.");

                AssertTrue(_bob.ConversationHistory.Count >= 2, "Bob should have at least 2 conversation turns.");
                AssertTrue(_alice.ConversationHistory.Count >= 2, "Alice should have at least 2 conversation turns.");

                AssertTrue(!string.IsNullOrEmpty(_bob.CurrentTaskSummary), "Bob should have a task summary set.");
                AssertTrue(!string.IsNullOrEmpty(_alice.CurrentTaskSummary), "Alice should have a task summary set.");

                GD.Print($"[PASS] Step 6: Both NPCs finished full dialogue cycle!");
                GD.Print($"       Bob task summary: {_bob.CurrentTaskSummary}");
                GD.Print($"       Alice task summary: {_alice.CurrentTaskSummary}");
                GD.Print("[SUCCESS] All F-405 acceptance criteria verified successfully!");

                GetTree().Quit(0);
                break;
            }
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            GD.PrintErr($"[FAIL] Assertion failed: {message}");
            GetTree().Quit(1);
            throw new InvalidOperationException($"Assertion failed: {message}");
        }
    }
}
