using Unity.Burst;

// COMPILE SETTINGS

// Uncomment the line below to swap all the inputs/outputs of FastNoise to doubles instead of floats
//#define FN_USE_DOUBLES

// Uncomment the line below to disable the method aggressive inlining attribute, do this if it is unsupported, ie Unity
//#define FN_DISABLE_AGGRESSIVE_INLINING

// ----------------

#if FN_USE_DOUBLES
using FN_DECIMAL = System.Double;
#else
using FN_DECIMAL = System.Single;
#endif

using System;
#if! FN_DISABLE_AGGRESSIVE_INLINING
using System.Runtime.CompilerServices;
#endif


namespace Opencraft.ThirdParty
{
    // Static version of managed FastNoise for Perlin noise, so it can be burst compiled.
    // See https://github.com/Auburn/FastNoise and https://github.com/Hertzole/Voxelmetric
    [BurstCompile]
    public static class FastNoise
    {
        public enum Interp { Linear, Hermite, Quintic };
        public enum NoiseType { Perlin, Simplex };

        private const Interp interp = Interp.Quintic;
        private const int PrimeX = 501125321;
        private const int PrimeY = 1136930381;
        private const int PrimeZ = 6971;
        private const int PrimeW = 1013;
        private const FN_DECIMAL F3 = (FN_DECIMAL)(1.0 / 3.0);
        private const FN_DECIMAL G3 = (FN_DECIMAL)(1.0 / 6.0);
        private const FN_DECIMAL SQRT3 = 1.7320508075688772935274463415059f;
        private const FN_DECIMAL G2 = (3 - SQRT3) / 6;
        private const FN_DECIMAL F2 = 0.5f * (SQRT3 - 1);
        
        [BurstCompile]
        public static FN_DECIMAL GetNoise(FN_DECIMAL x, FN_DECIMAL y, FN_DECIMAL z, int seed, FN_DECIMAL frequency = 0.01f, NoiseType noiseType = NoiseType.Simplex)
        {
            x *= frequency;
            y *= frequency;
            z *= frequency;

            switch (noiseType)
            {
                case NoiseType.Perlin:
                    return PerlinGetNoise(x, y, z, seed);
                case NoiseType.Simplex:
                    return SimplexGetNoise(x, y, z, seed);
                default:
                    return 0.0f;
            }
        }
 
        [BurstCompile]
        public static FN_DECIMAL PerlinGetNoise(FN_DECIMAL x, FN_DECIMAL y,FN_DECIMAL z, int seed)
        {
            int x0 = FastFloor(x);
            int y0 = FastFloor(y);
            int z0 = FastFloor(z);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            int z1 = z0 + 1;

            FN_DECIMAL xs, ys, zs;
            switch (interp)
            {
                default:
                /*case Interp.Linear:
                    xs = x - x0;
                    ys = y - y0;
                    zs = z - z0;
                    break;
                case Interp.Hermite:
                    xs = InterpHermiteFunc(x - x0);
                    ys = InterpHermiteFunc(y - y0);
                    zs = InterpHermiteFunc(z - z0);
                    break;*/
                case Interp.Quintic:
                    xs = InterpQuinticFunc(x - x0);
                    ys = InterpQuinticFunc(y - y0);
                    zs = InterpQuinticFunc(z - z0);
                    break;
            }

            FN_DECIMAL xd0 = x - x0;
            FN_DECIMAL yd0 = y - y0;
            FN_DECIMAL zd0 = z - z0;
            FN_DECIMAL xd1 = xd0 - 1.0f;
            FN_DECIMAL yd1 = yd0 - 1.0f;
            FN_DECIMAL zd1 = zd0 - 1.0f;

            FN_DECIMAL xf00 = Lerp(GradCoord3D(seed, x0, y0, z0, xd0, yd0, zd0), GradCoord3D(seed, x1, y0, z0, xd1, yd0, zd0), xs);
            FN_DECIMAL xf10 = Lerp(GradCoord3D(seed, x0, y1, z0, xd0, yd1, zd0), GradCoord3D(seed, x1, y1, z0, xd1, yd1, zd0), xs);
            FN_DECIMAL xf01 = Lerp(GradCoord3D(seed, x0, y0, z1, xd0, yd0, zd1), GradCoord3D(seed, x1, y0, z1, xd1, yd0, zd1), xs);
            FN_DECIMAL xf11 = Lerp(GradCoord3D(seed, x0, y1, z1, xd0, yd1, zd1), GradCoord3D(seed, x1, y1, z1, xd1, yd1, zd1), xs);

            FN_DECIMAL yf0 = Lerp(xf00, xf10, ys);
            FN_DECIMAL yf1 = Lerp(xf01, xf11, ys);

            return Lerp(yf0, yf1, zs);
        }
        
        [BurstCompile]
        public static FN_DECIMAL SimplexGetNoise(FN_DECIMAL x, FN_DECIMAL y, FN_DECIMAL z, int seed)
        {
             FN_DECIMAL t = (x + y + z) * F3;
            int i = FastFloor(x + t);
            int j = FastFloor(y + t);
            int k = FastFloor(z + t);

            t = (i + j + k) * G3;
            FN_DECIMAL X0 = i - t;
            FN_DECIMAL Y0 = j - t;
            FN_DECIMAL Z0 = k - t;

            FN_DECIMAL x0 = x - X0;
            FN_DECIMAL y0 = y - Y0;
            FN_DECIMAL z0 = z - Z0;

            int i1, j1, k1;
            int i2, j2, k2;

            if (x0 >= y0)
            {
                if (y0 >= z0)
                {
                    i1 = 1;
                    j1 = 0;
                    k1 = 0;
                    i2 = 1;
                    j2 = 1;
                    k2 = 0;
                }
                else if (x0 >= z0)
                {
                    i1 = 1;
                    j1 = 0;
                    k1 = 0;
                    i2 = 1;
                    j2 = 0;
                    k2 = 1;
                }
                else // x0 < z0
                {
                    i1 = 0;
                    j1 = 0;
                    k1 = 1;
                    i2 = 1;
                    j2 = 0;
                    k2 = 1;
                }
            }
            else // x0 < y0
            {
                if (y0 < z0)
                {
                    i1 = 0;
                    j1 = 0;
                    k1 = 1;
                    i2 = 0;
                    j2 = 1;
                    k2 = 1;
                }
                else if (x0 < z0)
                {
                    i1 = 0;
                    j1 = 1;
                    k1 = 0;
                    i2 = 0;
                    j2 = 1;
                    k2 = 1;
                }
                else // x0 >= z0
                {
                    i1 = 0;
                    j1 = 1;
                    k1 = 0;
                    i2 = 1;
                    j2 = 1;
                    k2 = 0;
                }
            }

            FN_DECIMAL x1 = x0 - i1 + G3;
            FN_DECIMAL y1 = y0 - j1 + G3;
            FN_DECIMAL z1 = z0 - k1 + G3;
            FN_DECIMAL x2 = x0 - i2 + 2.0f * G3;
            FN_DECIMAL y2 = y0 - j2 + 2.0f * G3;
            FN_DECIMAL z2 = z0 - k2 + 2.0f * G3;
            FN_DECIMAL x3 = x0 - 1.0f + 3.0f * G3;
            FN_DECIMAL y3 = y0 - 1.0f + 3.0f * G3;
            FN_DECIMAL z3 = z0 - 1.0f + 3.0f * G3;

            FN_DECIMAL n0, n1, n2, n3;

            t = 0.6f - x0 * x0 - y0 * y0 - z0 * z0;
            if (t < 0.0f)
            {
                n0 = 0.0f;
            }
            else
            {
                t *= t;
                n0 = t * t * GradCoord3D(seed, i, j, k, x0, y0, z0);
            }

            t = 0.6f - x1 * x1 - y1 * y1 - z1 * z1;
            if (t < 0.0f)
            {
                n1 = 0.0f;
            }
            else
            {
                t *= t;
                n1 = t * t * GradCoord3D(seed, i + i1, j + j1, k + k1, x1, y1, z1);
            }

            t = 0.6f - x2 * x2 - y2 * y2 - z2 * z2;
            if (t < 0.0f)
            {
                n2 = 0.0f;
            }
            else
            {
                t *= t;
                n2 = t * t * GradCoord3D(seed, i + i2, j + j2, k + k2, x2, y2, z2);
            }

            t = 0.6f - x3 * x3 - y3 * y3 - z3 * z3;
            if (t < 0.0f)
            {
                n3 = 0.0f;
            }
            else
            {
                t *= t;
                n3 = t * t * GradCoord3D(seed, i + 1, j + 1, k + 1, x3, y3, z3);
            }

            return 32.0f * (n0 + n1 + n2 + n3);
        }
        
#if !FN_DISABLE_AGGRESSIVE_INLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif   
        private static int FastFloor(FN_DECIMAL f)
        {
            return f >= 0 ? (int)f : (int)f - 1;
        }
        
#if !FN_DISABLE_AGGRESSIVE_INLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static FN_DECIMAL GradCoord(int seed, int xPrimed, int yPrimed, float xd, float yd)
        {
            int hash = Hash(seed, xPrimed, yPrimed);
            hash ^= hash >> 15;
            hash &= 127 << 1;

            float xg = Gradients2D[hash];
            float yg = Gradients2D[hash | 1];

            return xd * xg + yd * yg;
        }
        
#if !FN_DISABLE_AGGRESSIVE_INLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static FN_DECIMAL GradCoord3D(int seed, int x, int y, int z, FN_DECIMAL xd, FN_DECIMAL yd, FN_DECIMAL zd)
        {
            int hash = seed;
            hash ^= PrimeX * x;
            hash ^= PrimeY * y;
            hash ^= PrimeZ * z;

            hash = hash * hash * hash * 60493;
            hash = (hash >> 13) ^ hash;

            hash &= 15;
            FN_DECIMAL u = hash < 8 ? xd : yd; // gradient directions, and compute dot product.
            FN_DECIMAL v = hash < 4 ? yd : hash == 12 || hash == 14 ? xd : zd; // Fix repeats at h = 12 to 15
            return ((hash & 1) != 0 ? -u : u) + ((hash & 2) != 0 ? -v : v);
        }
        
#if !FN_DISABLE_AGGRESSIVE_INLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static int Hash(int seed, int xPrimed, int yPrimed)
        {
            int hash = seed ^ xPrimed ^ yPrimed;

            hash *= 0x27d4eb2d;
            return hash;
        }
        
#if !FN_DISABLE_AGGRESSIVE_INLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static FN_DECIMAL Lerp(FN_DECIMAL a, FN_DECIMAL b, FN_DECIMAL t) { return a + t * (b - a); }
        
#if !FN_DISABLE_AGGRESSIVE_INLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static FN_DECIMAL InterpHermiteFunc(FN_DECIMAL t) { return t * t * (3 - 2 * t); }
        
#if !FN_DISABLE_AGGRESSIVE_INLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static FN_DECIMAL InterpQuinticFunc(FN_DECIMAL t) { return t * t * t * (t * (t * 6 - 15) + 10); }
        

        static readonly FN_DECIMAL[] Gradients2D =
        {
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f,
            0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f,
            -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f,
            -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f,
            -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f,
            0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f,
            0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f,
            0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f,
            -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f,
            -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f,
            -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f,
            0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f,
            0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f,
            0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f,
            -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f,
            -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f,
            -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f,
            0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f,
            0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f,
            0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f,
            -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f,
            -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f,
            -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f,
            0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f,
            0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f,
            0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f,
            -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f,
            -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f,
            -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f,
            0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f,
            0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.38268343236509f, 0.923879532511287f, 0.923879532511287f, 0.38268343236509f, 0.923879532511287f,
            -0.38268343236509f, 0.38268343236509f, -0.923879532511287f,
            -0.38268343236509f, -0.923879532511287f, -0.923879532511287f, -0.38268343236509f, -0.923879532511287f,
            0.38268343236509f, -0.38268343236509f, 0.923879532511287f,
        };
    }
}