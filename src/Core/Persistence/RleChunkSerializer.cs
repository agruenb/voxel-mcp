namespace VoxelKing.Core.Persistence;

using System;
using System.Buffers.Binary;
using System.IO;
using VoxelKing.Core.Common;
using VoxelKing.Core.Voxel;

/// <summary>
/// High-efficiency Run-Length Encoding (RLE) binary serializer for 32x32x32 voxel chunks.
/// </summary>
public static class RleChunkSerializer
{
    public const uint Magic = 0x4B434B56; // "VKCK" in little-endian
    public const byte CurrentVersion = 1;
    public const int HeaderSize = 21; // 4 (magic) + 1 (ver) + 12 (coord) + 4 (volume)

    public static byte[] Serialize(ChunkData chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        using var ms = new MemoryStream(1024);
        Serialize(chunk, ms);
        return ms.ToArray();
    }

    public static void Serialize(ChunkData chunk, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(stream);

        var blocks = chunk.Blocks;
        int total = blocks.Length;
        if (total != ChunkData.Volume)
        {
            throw new InvalidOperationException($"Expected chunk volume of {ChunkData.Volume}, but got {total}.");
        }

        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Header
        writer.Write(Magic);
        writer.Write(CurrentVersion);
        writer.Write(chunk.ChunkCoord.X);
        writer.Write(chunk.ChunkCoord.Y);
        writer.Write(chunk.ChunkCoord.Z);
        writer.Write(ChunkData.Volume);

        // Count runs first
        int runCount = 0;
        int i = 0;
        while (i < total)
        {
            ushort blockId = blocks[i];
            int count = 1;
            while (i + count < total && blocks[i + count] == blockId && count < ushort.MaxValue)
            {
                count++;
            }
            runCount++;
            i += count;
        }

        writer.Write(runCount);

        // Write runs
        i = 0;
        while (i < total)
        {
            ushort blockId = blocks[i];
            int count = 1;
            while (i + count < total && blocks[i + count] == blockId && count < ushort.MaxValue)
            {
                count++;
            }

            writer.Write((ushort)count);
            writer.Write(blockId);
            i += count;
        }
    }

    public static ChunkData Deserialize(ReadOnlySpan<byte> bytes, Func<ushort, bool>? isIndexed = null)
    {
        if (bytes.Length < HeaderSize + 4)
        {
            throw new InvalidDataException("Byte data is too small to contain a valid chunk header.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes[0..4]);
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid magic bytes: 0x{magic:X8}. Expected: 0x{Magic:X8}.");
        }

        byte version = bytes[4];
        if (version != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported chunk format version: {version}.");
        }

        int x = BinaryPrimitives.ReadInt32LittleEndian(bytes[5..9]);
        int y = BinaryPrimitives.ReadInt32LittleEndian(bytes[9..13]);
        int z = BinaryPrimitives.ReadInt32LittleEndian(bytes[13..17]);
        int volume = BinaryPrimitives.ReadInt32LittleEndian(bytes[17..21]);

        if (volume != ChunkData.Volume)
        {
            throw new InvalidDataException($"Chunk volume mismatch: got {volume}, expected {ChunkData.Volume}.");
        }

        int runCount = BinaryPrimitives.ReadInt32LittleEndian(bytes[21..25]);
        int expectedPayloadSize = HeaderSize + 4 + (runCount * 4);
        if (bytes.Length < expectedPayloadSize)
        {
            throw new InvalidDataException($"Incomplete chunk payload. Expected at least {expectedPayloadSize} bytes, got {bytes.Length}.");
        }

        ushort[] blocks = new ushort[ChunkData.Volume];
        int writePos = 0;
        int offset = 25;

        for (int r = 0; r < runCount; r++)
        {
            ushort count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]);
            ushort blockId = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 2)..(offset + 4)]);
            offset += 4;

            if (writePos + count > ChunkData.Volume)
            {
                throw new InvalidDataException($"Decoded chunk run overflowed volume boundary at position {writePos + count}.");
            }

            Array.Fill(blocks, blockId, writePos, count);
            writePos += count;
        }

        if (writePos != ChunkData.Volume)
        {
            throw new InvalidDataException($"Decoded chunk has {writePos} voxels, expected {ChunkData.Volume}.");
        }

        return new ChunkData(new Vec3i(x, y, z), blocks, version: 1, isDirty: true, isIndexed: isIndexed, isModified: true);
    }

    public static ChunkData Deserialize(Stream stream, Func<ushort, bool>? isIndexed = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid magic bytes: 0x{magic:X8}. Expected: 0x{Magic:X8}.");
        }

        byte version = reader.ReadByte();
        if (version != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported chunk format version: {version}.");
        }

        int x = reader.ReadInt32();
        int y = reader.ReadInt32();
        int z = reader.ReadInt32();
        int volume = reader.ReadInt32();

        if (volume != ChunkData.Volume)
        {
            throw new InvalidDataException($"Chunk volume mismatch: got {volume}, expected {ChunkData.Volume}.");
        }

        int runCount = reader.ReadInt32();
        ushort[] blocks = new ushort[ChunkData.Volume];
        int writePos = 0;

        for (int r = 0; r < runCount; r++)
        {
            ushort count = reader.ReadUInt16();
            ushort blockId = reader.ReadUInt16();

            if (writePos + count > ChunkData.Volume)
            {
                throw new InvalidDataException($"Decoded chunk run overflowed volume boundary at position {writePos + count}.");
            }

            Array.Fill(blocks, blockId, writePos, count);
            writePos += count;
        }

        if (writePos != ChunkData.Volume)
        {
            throw new InvalidDataException($"Decoded chunk has {writePos} voxels, expected {ChunkData.Volume}.");
        }

        return new ChunkData(new Vec3i(x, y, z), blocks, version: 1, isDirty: true, isIndexed: isIndexed, isModified: true);
    }
}
