using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Opencraft.Terrain.Structures
{
    public static class Structure
    {
        // Tree structure vars
        public const int MIN_TRUNK_HEIGHT = 3;
        public const int TREE_NOISE_RANGE = 2;
        public const int MIN_CROWN_RADIUS = 1;
        public const int MAX_CROWN_RADIUS = MIN_CROWN_RADIUS + TREE_NOISE_RANGE;

        // Encodes the sizes of various structures
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 StructureToExtents(StructureType structureType, bool negativeBounds = false)
        {
            int3 ret = new int3(0);
            switch (structureType)
            {
                case StructureType.Tree:
                    if (negativeBounds)
                        ret = new int3(MAX_CROWN_RADIUS,
                            0,
                            MAX_CROWN_RADIUS);
                    else
                        ret = new int3(MAX_CROWN_RADIUS,
                            MIN_TRUNK_HEIGHT +  TREE_NOISE_RANGE + 1,
                            MAX_CROWN_RADIUS);
                    break;
            }

            return ret;
        }
        
        // Encodes the noise variation 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int StructureToNoiseRange(StructureType structureType)
        {
            int ret = 0;
            switch (structureType)
            {
                case StructureType.Tree:
                        ret = TREE_NOISE_RANGE;
                    break;
            }

            return ret;
        }
    }

    public enum StructureType
    {
        None,
        Tree
    }

}