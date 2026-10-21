namespace VoxelKing.Game.World;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// Headless integration test validating that CharacterBody3D can walk on chunk terrain,
/// collide with voxel walls, and that collision shapes rebuild correctly on edit without falling through.
/// </summary>
public partial class TestCollisionRunner : Node3D
{
    private ChunkNode _chunkNode = null!;
    private CharacterBody3D _walker = null!;
    private ChunkMesher _mesher = null!;
    private BlockRegistry _registry = null!;
    private ChunkMaterials _materials = null!;
    private ChunkData _chunkData = null!;

    private int _physicsFrame = 0;
    private int _testPhase = 0; // 0 = falling, 1 = walk to wall, 2 = edit test, 3 = done

    public override void _Ready()
    {
        _registry = BlockRegistry.CreateDefault();
        _mesher = new ChunkMesher(_registry);
        _materials = new ChunkMaterials();

        // 1. Create a 32x32x32 chunk with a floor at y=0 and a 2-block wall at x=5
        _chunkData = new ChunkData(Vec3i.Zero);
        ushort stoneId = _registry.GetId("stone");

        // Floor from x: 0..10, z: 0..10 at y=0
        for (int x = 0; x <= 10; x++)
        {
            for (int z = 0; z <= 10; z++)
            {
                _chunkData.SetBlock(x, 0, z, stoneId);
            }
        }

        // Wall at x=5, y=1..2, z: 0..10
        for (int y = 1; y <= 2; y++)
        {
            for (int z = 0; z <= 10; z++)
            {
                _chunkData.SetBlock(5, y, z, stoneId);
            }
        }

        // 2. Mesh chunk and attach to scene
        _chunkNode = new ChunkNode(Vec3i.Zero);
        AddChild(_chunkNode);
        RebuildChunkMesh();

        // 3. Create CharacterBody3D in air at (2, 5, 2)
        _walker = new CharacterBody3D { Name = "Walker" };
        var colShape = new CollisionShape3D { Name = "WalkerShape" };
        colShape.Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.8f };
        _walker.AddChild(colShape);
        _walker.Position = new Vector3(2f, 5f, 2f);
        AddChild(_walker);

        GD.Print("[TestCollision] Setup complete. Starting physics tests...");
    }

    private void RebuildChunkMesh()
    {
        var snapshot = new ChunkMeshSnapshot(_chunkData);
        var meshData = _mesher.Mesh(snapshot);
        _chunkNode.ApplyMesh(meshData, _materials.Opaque, _materials.Transparent);
    }

    public override void _PhysicsProcess(double delta)
    {
        _physicsFrame++;
        float dt = (float)delta;

        switch (_testPhase)
        {
            case 0:
                // Phase 0: Gravity fall to floor
                _walker.Velocity = new Vector3(0f, -9.8f, 0f);
                _walker.MoveAndSlide();

                if (_walker.IsOnFloor())
                {
                    GD.Print($"[PASS] Phase 0: CharacterBody3D landed on terrain at Y={_walker.Position.Y:F2}. IsOnFloor == true.");
                    _testPhase = 1;
                    _physicsFrame = 0;
                }
                else if (_physicsFrame > 120)
                {
                    GD.PrintErr($"[FAIL] CharacterBody3D failed to land on floor within 120 ticks. Pos: {_walker.Position}");
                    GetTree().Quit(1);
                }
                break;

            case 1:
                // Phase 1: Walk horizontally towards wall at x=5
                _walker.Velocity = new Vector3(5.0f, -2.0f, 0f);
                _walker.MoveAndSlide();

                if (_walker.IsOnWall())
                {
                    AssertTrue(_walker.Position.X < 5.0f, "Walker must not pass through wall at x=5");
                    GD.Print($"[PASS] Phase 1: CharacterBody3D collided with wall at X={_walker.Position.X:F2}. IsOnWall == true.");
                    _testPhase = 2;
                    _physicsFrame = 0;

                    // Trigger block edit: Place stone block directly under the walker's path
                    // to raise floor by 1 at (2, 1, 2)
                    _chunkData.SetBlock(2, 1, 2, _registry.GetId("stone"));
                    RebuildChunkMesh();
                    _walker.Position = new Vector3(2f, 4f, 2f); // drop onto new elevated floor
                }
                else if (_physicsFrame > 120)
                {
                    GD.PrintErr($"[FAIL] Walker failed to collide with wall within 120 ticks. Pos: {_walker.Position}");
                    GetTree().Quit(1);
                }
                break;

            case 2:
                // Phase 2: Verify collision rebuild on edit (does not fall through new floor at y=1)
                _walker.Velocity = new Vector3(0f, -9.8f, 0f);
                _walker.MoveAndSlide();

                if (_walker.IsOnFloor())
                {
                    // Floor is now at top of y=1 block (world y=2.0).
                    // Capsule center should be at approx 2.0 + 0.9 = 2.9
                    AssertTrue(_walker.Position.Y >= 2.5f, $"Walker should rest on elevated floor at Y>=2.5, but is at {_walker.Position.Y}");
                    GD.Print($"[PASS] Phase 2: CharacterBody3D landed on edited elevated floor at Y={_walker.Position.Y:F2} without falling through.");
                    _testPhase = 3;
                }
                else if (_physicsFrame > 120)
                {
                    GD.PrintErr($"[FAIL] Walker fell through edited terrain. Pos: {_walker.Position}");
                    GetTree().Quit(1);
                }
                break;

            case 3:
                GD.Print("[PASS] ALL CHUNK COLLISION TESTS PASSED!");
                GetTree().Quit(0);
                break;
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            GD.PrintErr($"Assertion Failed: {message}");
            throw new InvalidOperationException(message);
        }
    }
}
