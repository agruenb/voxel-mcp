# Render Audit Strategy

How to diagnose "missing / wrong faces" in voxel chunks. Work top to bottom: each layer
is cheaper to check than the next, and most bugs are caught by Core tests before Godot runs.

## Conventions (source of truth)

| Item | Convention |
|---|---|
| Handedness | Right-handed, +Y up, `Vec3.Forward = (0,0,-1)` (matches Godot) |
| Front face | **Clockwise** as seen by the camera (Godot 4 default, `cull_back`) |
| Mesher quad corners | `v0..v3` CCW viewed along +normal; indices emitted `(0,2,1),(0,3,2)` |
| Normals | Outward, unit length, constant per quad |
| Chunk-local coords | Vertices in `[0, 32]`; `ChunkNode.Position` adds the chunk offset |

> Known incident (2026-10-21): mesher emitted CCW triangles → Godot culled every outward face,
> leaving only the interior far-side faces visible ("one side per block"). Guarded by
> `ChunkMesherTests.AllTriangles_AreClockwiseFromOutside_ForGodotFrontFaces`.

## Layer 1 — Core geometry invariants (xUnit, no engine)

Run `dotnet test`. Mesher tests must assert, for representative snapshots
(single voxel, 2×2×2 cube, chunk-border voxel, water next to stone):

1. **Winding**: for every triangle `(a,b,c)`, `dot((b-a)×(c-a), normal) < 0`.
2. **Face count**: isolated voxel → 6 quads, one per cardinal normal.
3. **Normals match geometry**: all 4 corners of a quad share the coordinate along the normal axis.
4. **Bounds**: every vertex within `[0, 32]` on all axes.
5. **Culling**: shared faces between two opaque blocks are absent; opaque↔transparent faces present.
6. **Neighbor borders**: face at chunk edge is culled iff the neighbor snapshot holds an opaque block.
7. **UVs**: every UV inside the face's atlas tile rect.

Any new rendering bug gets a Layer 1 regression test first.

## Layer 2 — Godot glue checks

In `ChunkNode.BuildSurfaceArrays` / materials:

- Index array passed unchanged (no reordering in glue).
- Opaque material `cull_mode = CULL_BACK`; transparent material may use `CULL_DISABLED`
  only for foliage — never as a workaround for wrong winding.
- `ArrayMesh.GetSurfaceCount()` matches non-empty submeshes.

## Layer 3 — Visual audit in the editor / running game

1. **Cull-off A/B**: temporarily set the opaque material to `CULL_DISABLED`.
   If faces reappear → winding bug (go to Layer 1). If still missing → mesh data bug.
2. **Cull-front A/B**: set `CULL_FRONT`. Now you should see *only* the inside of blocks;
   if outer faces show, winding is inverted.
3. **Normal debug**: Viewport → *Display Advanced → Normal Buffer* (or a debug shader
   `ALBEDO = NORMAL * 0.5 + 0.5`). Each cube side must show a distinct, flat color.
4. **Wireframe**: *Display Wireframe* to confirm greedy-merged quads and no T-gaps.
5. **Orbit test**: spawn a single floating block at a known position and orbit the camera
   all 6 directions; every side must be visible from outside, none from inside.
6. **Chunk border**: stand on a chunk boundary (multiple of 32) and check for holes / doubled faces.

## Layer 4 — Automated screenshot smoke test (optional)

A debug scene places one block per type in a row, renders from 6 fixed cameras, and
compares against golden PNGs with a tolerance. Run before merging mesher/material changes.

## Triage table

| Symptom | Likely cause |
|---|---|
| Only far/inner side of each block visible | Winding inverted |
| Faces visible only from inside | Winding inverted |
| Faces black / lit wrong but present | Normals inverted or not normalized |
| Holes at chunk edges | Neighbor snapshot missing or border indexing off-by-one |
| Interior faces visible through water | Transparent culling rule or draw order |
| Stretched / wrong texture on merged quads | UV not tiled across greedy quad width/height |
