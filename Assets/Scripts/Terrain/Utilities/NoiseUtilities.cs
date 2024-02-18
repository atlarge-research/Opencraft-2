using System.Runtime.CompilerServices;
using Opencraft.ThirdParty;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Opencraft.Terrain.Utilities
{
    [BurstCompile]
    public static class NoiseUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        // Wrapper around FastNoise lib, applies scaling, power operations to noise
        public static float GetNoise(float x, float y, float z, int seed, float scale, int max, float power, float frequency = 0.01f, FastNoise.NoiseType noiseType = FastNoise.NoiseType.Simplex)
        {
            float scaleInv = 1f / scale;
            float n = FastNoise.GetNoise(x * scaleInv, y * scaleInv, z * scaleInv, seed, frequency, noiseType) + 1f;
            n *= (max >> 1);

            if (math.abs(power - 1f) > float.Epsilon)
            {
                n = math.pow(n, power);
            }

            return n;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static void GetNoiseInterpolatorSettings(ref NoiseInterpolatorSettings nis, int areaSize, int downsamplingFactor)
        {
            int step = downsamplingFactor;
            int size = (areaSize >> step) + 1;
            int size2 = size * size;
            nis.size = size;
            nis.sizePow2 = size2;
            nis.sizePow2plusSize = size2 + size;
            nis.step = downsamplingFactor;
            nis.scale = 1f / (1 << step);
        } 
        
        public struct NoiseInterpolatorSettings
        {
            public int size;
            public int sizePow2;

            public int sizePow2plusSize;
            //! +1 interpolated into downsampled state
            public int step;
            //! Interpolation scale
            public float scale;
        }
        
 
        /// <summary>
        /// Interpolates the given coordinate into a downsampled coordinate and returns a value from the lookup table on that position
        /// </summary>
        /// <param name="x">Position on the x axis</param>
        /// <param name="lookupTable">Lookup table to be used to interpolate</param>
        public static float Interpolate(NoiseInterpolatorSettings settings, int x, NativeArray<float> lookupTable)
        {
            float xs = (x + 0.5f) * settings.scale;

            int x0 = FastFloor(xs);

            xs = (xs - x0);

            return Interpolate(lookupTable[x0], lookupTable[x0 + 1], xs);
        }

        /// <summary>
        /// Interpolates given coordinates into downsampled coordinates and returns a value from the lookup table on that position
        /// </summary>
        /// <param name="x">Position on the x axis</param>
        /// <param name="z">Position on the z axis</param>
        /// <param name="lookupTable">Lookup table to be used to interpolate</param>
        public static float Interpolate(NoiseInterpolatorSettings settings, int x, int z, NativeArray<float> lookupTable)
        {
            float xs = (x + 0.5f) * settings.scale;
            float zs = (z + 0.5f) * settings.scale;

            int x0 = FastFloor(xs);
            int z0 = FastFloor(zs);

            xs = (xs - x0);
            zs = (zs - z0);

            int lookupIndex = GetIndex1DFrom2D(x0, z0, settings.size);
            int lookupIndex2 = lookupIndex + settings.size; // x0,z0+1

            return Interpolate(
                Interpolate(lookupTable[lookupIndex], lookupTable[lookupIndex + 1], xs),
                Interpolate(lookupTable[lookupIndex2], lookupTable[lookupIndex2 + 1], xs),
                zs);
        }

        /// <summary>
        /// Interpolates given coordinates into downsampled coordinates and returns a value from the lookup table on that position
        /// </summary>
        /// <param name="x">Position on the x axis</param>
        /// <param name="y">Position on the y axis</param>
        /// <param name="z">Position on the z axis</param>
        /// <param name="lookupTable">Lookup table to be used to interpolate</param>
        public static float Interpolate(NoiseInterpolatorSettings settings, int x, int y, int z, NativeArray<float> lookupTable)
        {
            float xs = (x + 0.5f) * settings.scale;
            float ys = (y + 0.5f) * settings.scale;
            float zs = (z + 0.5f) * settings.scale;

            int x0 = FastFloor(xs);
            int y0 = FastFloor(ys);
            int z0 = FastFloor(zs);

            xs = (xs - x0);
            ys = (ys - y0);
            zs = (zs - z0);

            //int lookupIndex = Helpers.GetIndex1DFrom3D(x0, y0, z0, size, size);
            int lookupIndex = GetIndex1DFrom3D(x0, y0, z0,  settings.size,  settings.size);
            int lookupIndexY = lookupIndex + settings.sizePow2; // x0, y0+1, z0
            int lookupIndexZ = lookupIndex + settings.size;  // x0, y0, z0+1
            int lookupIndexYZ = lookupIndex + settings.sizePow2plusSize; // x0, y0+1, z0+1

            return Interpolate(
                 Interpolate(
                     Interpolate(lookupTable[lookupIndex], lookupTable[lookupIndex + 1], xs),
                     Interpolate(lookupTable[lookupIndexY], lookupTable[lookupIndexY + 1], xs),
                    ys),
                 Interpolate(
                    Interpolate(lookupTable[lookupIndexZ], lookupTable[lookupIndexZ + 1], xs),
                    Interpolate(lookupTable[lookupIndexYZ], lookupTable[lookupIndexYZ + 1], xs),
                    ys),
                zs);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Interpolate(float x0, float x1, float alpha)
        {
            return x0 + (x1 - x0) * alpha;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastFloor(float val)
        {
            return (val >= 0) ? (int)val : (int)val - 1;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastCeil(float val)
        {
            return (val >= 0) ? (int)val : (int)val - 1;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastRound(float val)
        {
            var temp = (int)val;
            return (val - temp <= 0.5) ? temp : temp + 1;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 FastFloor(float3 vals)
        {
            return new int3(FastFloor(vals.x), FastFloor(vals.y), FastFloor(vals.z));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 FastRound(float3 vals)
        {
            return new int3(FastRound(vals.x), FastRound(vals.y), FastRound(vals.z));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 FastCeil(float3 vals)
        {
            return new int3(FastCeil(vals.x), FastCeil(vals.y), FastCeil(vals.z));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex1DFrom2D(int x, int z, int sizeX)
        {
            return x + z * sizeX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex1DFrom3D(int x, int y, int z, int sizeX, int sizeZ)
        {
            return x + sizeX * (z + y * sizeZ);
        }
        
        private const float MULTIPLIER = 1f / 10000f;
        //! First 255 prime numbers and 1. Used for randomizing a number in the RandomPercent function.
        private static readonly int[] primeNumbers =
        {
            1, 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67,
            71, 73, 79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149,
            151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229,
            233, 239, 241, 251, 257, 263, 269, 271, 277, 281, 283, 293, 307, 311, 313,
            317, 331, 337, 347, 349, 353, 359, 367, 373, 379, 383, 389, 397, 401, 409,
            419, 421, 431, 433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499,
            503, 509, 521, 523, 541, 547, 557, 563, 569, 571, 577, 587, 593, 599, 601,
            607, 613, 617, 619, 631, 641, 643, 647, 653, 659, 661, 673, 677, 683, 691,
            701, 709, 719, 727, 733, 739, 743, 751, 757, 761, 769, 773, 787, 797, 809,
            811, 821, 823, 827, 829, 839, 853, 857, 859, 863, 877, 881, 883, 887, 907,
            911, 919, 929, 937, 941, 947, 953, 967, 971, 977, 983, 991, 997, 1009, 1013,
            1019, 1021, 1031, 1033, 1039, 1049, 1051, 1061, 1063, 1069, 1087, 1091, 1093,
            1097, 1103, 1109, 1117, 1123, 1129, 1151, 1153, 1163, 1171, 1181, 1187, 1193,
            1201, 1213, 1217, 1223, 1229, 1231, 1237, 1249, 1259, 1277, 1279, 1283, 1289,
            1291, 1297, 1301, 1303, 1307, 1319, 1321, 1327, 1361, 1367, 1373, 1381, 1399,
            1409, 1423, 1427, 1429, 1433, 1439, 1447, 1451, 1453, 1459, 1471, 1481, 1483,
            1487, 1489, 1493, 1499, 1511, 1523, 1531, 1543, 1549, 1553, 1559, 1567, 1571,
            1579, 1583, 1597, 1601, 1607, 1609, 1613, 1619
        };
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Random(int h, byte seed)
        {
            int hash = h;
            unchecked
            {
                hash *= primeNumbers[seed];

                if (hash < 0)
                {
                    hash *= -1;
                }

                return (hash % 10000) * MULTIPLIER;
            }
        }
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RandomPrecise(int h, byte seed)
        {
            int hash = h;
            unchecked
            {
                hash *= primeNumbers[seed];
                hash *= primeNumbers[++seed] * h;

                if (hash < 0)
                {
                    hash *= -1;
                }

                return (hash % 10000) * MULTIPLIER;
            }
        }
        
    }

   
}