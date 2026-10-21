namespace VoxelKing.Core.Voxel;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Fast, deterministic 2D and 3D procedural Simplex noise implementation without external dependencies.
/// </summary>
public sealed class FastNoise
{
    private static readonly float[] Grad2X = [ 1, -1, 0,  0,  1, -1,  1, -1,  0,  0,  1, -1 ];
    private static readonly float[] Grad2Y = [ 0,  0, 1, -1,  1,  1, -1, -1,  1, -1,  0,  0 ];

    private static readonly float[] Grad3X = [ 1, -1, 1, -1, 1, -1, 1, -1, 0,  0,  0,  0 ];
    private static readonly float[] Grad3Y = [ 1,  1, -1, -1, 0,  0,  0,  0, 1, -1,  1, -1 ];
    private static readonly float[] Grad3Z = [ 0,  0,  0,  0, 1,  1, -1, -1, 1,  1, -1, -1 ];

    private const float F2 = 0.366025403784439f; // 0.5 * (sqrt(3) - 1)
    private const float G2 = 0.211324865405187f; // (3 - sqrt(3)) / 6

    private const float F3 = 1.0f / 3.0f;
    private const float G3 = 1.0f / 6.0f;

    private readonly byte[] _perm = new byte[512];
    private readonly byte[] _perm12 = new byte[512];

    public int Seed { get; }

    public FastNoise(int seed)
    {
        Seed = seed;
        InitializePermutations(seed);
    }

    private void InitializePermutations(int seed)
    {
        byte[] p = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            p[i] = (byte)i;
        }

        // Deterministic Linear Congruential Generator (LCG) for cross-platform bit-level repeatability
        uint state = (uint)seed;
        for (int i = 255; i > 0; i--)
        {
            state = (state * 1664525u) + 1013904223u;
            int j = (int)(state % (uint)(i + 1));
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 512; i++)
        {
            _perm[i] = p[i & 255];
            _perm12[i] = (byte)(_perm[i] % 12);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FastFloor(float x) => x >= 0 ? (int)x : (int)x - 1;

    /// <summary>
    /// Evaluates 2D Simplex noise in range [-1, 1].
    /// </summary>
    public float GetNoise2D(float x, float y)
    {
        float s = (x + y) * F2;
        int i = FastFloor(x + s);
        int j = FastFloor(y + s);

        float t = (i + j) * G2;
        float x0 = x - (i - t);
        float y0 = y - (j - t);

        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1;
            j1 = 0;
        }
        else
        {
            i1 = 0;
            j1 = 1;
        }

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1.0f + (2.0f * G2);
        float y2 = y0 - 1.0f + (2.0f * G2);

        int ii = i & 255;
        int jj = j & 255;

        int gi0 = _perm12[ii + _perm[jj]];
        int gi1 = _perm12[ii + i1 + _perm[jj + j1]];
        int gi2 = _perm12[ii + 1 + _perm[jj + 1]];

        float n0, n1, n2;

        float t0 = 0.5f - (x0 * x0) - (y0 * y0);
        if (t0 < 0)
        {
            n0 = 0.0f;
        }
        else
        {
            t0 *= t0;
            n0 = t0 * t0 * ((Grad2X[gi0] * x0) + (Grad2Y[gi0] * y0));
        }

        float t1 = 0.5f - (x1 * x1) - (y1 * y1);
        if (t1 < 0)
        {
            n1 = 0.0f;
        }
        else
        {
            t1 *= t1;
            n1 = t1 * t1 * ((Grad2X[gi1] * x1) + (Grad2Y[gi1] * y1));
        }

        float t2 = 0.5f - (x2 * x2) - (y2 * y2);
        if (t2 < 0)
        {
            n2 = 0.0f;
        }
        else
        {
            t2 *= t2;
            n2 = t2 * t2 * ((Grad2X[gi2] * x2) + (Grad2Y[gi2] * y2));
        }

        return 70.0f * (n0 + n1 + n2);
    }

    /// <summary>
    /// Evaluates 3D Simplex noise in range [-1, 1].
    /// </summary>
    public float GetNoise3D(float x, float y, float z)
    {
        float s = (x + y + z) * F3;
        int i = FastFloor(x + s);
        int j = FastFloor(y + s);
        int k = FastFloor(z + s);

        float t = (i + j + k) * G3;
        float x0 = x - (i - t);
        float y0 = y - (j - t);
        float z0 = z - (k - t);

        int i1, j1, k1;
        int i2, j2, k2;

        if (x0 >= y0)
        {
            if (y0 >= z0)
            {
                i1 = 1; j1 = 0; k1 = 0;
                i2 = 1; j2 = 1; k2 = 0;
            }
            else if (x0 >= z0)
            {
                i1 = 1; j1 = 0; k1 = 0;
                i2 = 1; j2 = 0; k2 = 1;
            }
            else
            {
                i1 = 0; j1 = 0; k1 = 1;
                i2 = 1; j2 = 0; k2 = 1;
            }
        }
        else
        {
            if (y0 < z0)
            {
                i1 = 0; j1 = 0; k1 = 1;
                i2 = 0; j2 = 1; k2 = 1;
            }
            else if (x0 < z0)
            {
                i1 = 0; j1 = 1; k1 = 0;
                i2 = 0; j2 = 1; k2 = 1;
            }
            else
            {
                i1 = 0; j1 = 1; k1 = 0;
                i2 = 1; j2 = 1; k2 = 0;
            }
        }

        float x1 = x0 - i1 + G3;
        float y1 = y0 - j1 + G3;
        float z1 = z0 - k1 + G3;

        float x2 = x0 - i2 + (2.0f * G3);
        float y2 = y0 - j2 + (2.0f * G3);
        float z2 = z0 - k2 + (2.0f * G3);

        float x3 = x0 - 1.0f + (3.0f * G3);
        float y3 = y0 - 1.0f + (3.0f * G3);
        float z3 = z0 - 1.0f + (3.0f * G3);

        int ii = i & 255;
        int jj = j & 255;
        int kk = k & 255;

        int gi0 = _perm12[ii + _perm[jj + _perm[kk]]];
        int gi1 = _perm12[ii + i1 + _perm[jj + j1 + _perm[kk + k1]]];
        int gi2 = _perm12[ii + i2 + _perm[jj + j2 + _perm[kk + k2]]];
        int gi3 = _perm12[ii + 1 + _perm[jj + 1 + _perm[kk + 1]]];

        float n0, n1, n2, n3;

        float t0 = 0.6f - (x0 * x0) - (y0 * y0) - (z0 * z0);
        if (t0 < 0) n0 = 0.0f;
        else
        {
            t0 *= t0;
            n0 = t0 * t0 * ((Grad3X[gi0] * x0) + (Grad3Y[gi0] * y0) + (Grad3Z[gi0] * z0));
        }

        float t1 = 0.6f - (x1 * x1) - (y1 * y1) - (z1 * z1);
        if (t1 < 0) n1 = 0.0f;
        else
        {
            t1 *= t1;
            n1 = t1 * t1 * ((Grad3X[gi1] * x1) + (Grad3Y[gi1] * y1) + (Grad3Z[gi1] * z1));
        }

        float t2 = 0.6f - (x2 * x2) - (y2 * y2) - (z2 * z2);
        if (t2 < 0) n2 = 0.0f;
        else
        {
            t2 *= t2;
            n2 = t2 * t2 * ((Grad3X[gi2] * x2) + (Grad3Y[gi2] * y2) + (Grad3Z[gi2] * z2));
        }

        float t3 = 0.6f - (x3 * x3) - (y3 * y3) - (z3 * z3);
        if (t3 < 0) n3 = 0.0f;
        else
        {
            t3 *= t3;
            n3 = t3 * t3 * ((Grad3X[gi3] * x3) + (Grad3Y[gi3] * y3) + (Grad3Z[gi3] * z3));
        }

        return 32.0f * (n0 + n1 + n2 + n3);
    }

    /// <summary>
    /// Evaluates Fractal Brownian Motion (fBm) multi-octave 2D noise normalized to [-1, 1].
    /// </summary>
    public float GetFbm2D(float x, float y, int octaves = 4, float persistence = 0.5f, float lacunarity = 2.0f, float frequency = 0.005f)
    {
        float total = 0f;
        float amplitude = 1f;
        float maxAmplitude = 0f;
        float curFreq = frequency;

        for (int i = 0; i < octaves; i++)
        {
            total += GetNoise2D(x * curFreq, y * curFreq) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            curFreq *= lacunarity;
        }

        return total / maxAmplitude;
    }
}
