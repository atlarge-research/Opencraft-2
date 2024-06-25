using Unity.Mathematics;

namespace Opencraft.Terrain.Blocks
{
    // The enum of all supported types
    public enum BlockType : byte
    {
        Air,
        Off_Switch,
        On_Switch,
        Powered_Switch,
        AND_Gate,
        OR_Gate,
        NOT_Gate,
        XOR_Gate,
        Stone,
        Dirt,
        Tin,
        Gem,
        Grass,
        Leaf,
        Wood,
        Unbreakable,
        Power,
        Off_Wire,
        On_Wire,
        Off_Lamp,
        On_Lamp,
    }

    public enum Direction : byte
    {
        XP, XN, YP, YN, ZP, ZN
    }

    public static class BlockData
    {
        // Maps BlockType to texture array index, currently 1 to 1
        public static readonly int[] BlockToTexture =
        {
            0,
            (1 & 31) << 24,
            (2 & 31) << 24,
            (3 & 31) << 24,
            (4 & 31) << 24,
            (5 & 31) << 24,
            (6 & 31) << 24,
            (7 & 31) << 24,
            (8 & 31) << 24,
            (9 & 31) << 24,
            (10 & 31) << 24,
            (11 & 31) << 24,
            (12 & 31) << 24,
            (13 & 31) << 24,
            (14 & 31) << 24,
            (15 & 31) << 24,
            (16 & 31) << 24,
            (17 & 31) << 24,
            (18 & 31) << 24,
            (19 & 31) << 24,
            (20 & 31) << 24,
        };

        // UV sizing > 1 tiles a texture across multiple blocks, currently not done for any block types
        public static float[] BlockUVSizing = new float[]
        {
            1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f
        };

        public static readonly bool[] PowerableBlock = new bool[]
        {
            false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, true, true,
        };

        public static bool IsGate(BlockType type)
        {
            return type == BlockType.AND_Gate || type == BlockType.OR_Gate || type == BlockType.NOT_Gate || type == BlockType.XOR_Gate;
        }
        public static bool IsPowerTransmitter(BlockType type)
        {
            return type == BlockType.On_Wire || type == BlockType.On_Switch;
        }

        public static readonly BlockType[] DepoweredState = new BlockType[]
        {
            BlockType.Air,
            BlockType.Off_Switch,
            BlockType.On_Switch,
            BlockType.On_Switch,
            BlockType.AND_Gate,
            BlockType.OR_Gate,
            BlockType.NOT_Gate,
            BlockType.XOR_Gate,
            BlockType.Stone,
            BlockType.Dirt,
            BlockType.Tin,
            BlockType.Gem,
            BlockType.Grass,
            BlockType.Leaf,
            BlockType.Wood,
            BlockType.Unbreakable,
            BlockType.Power,
            BlockType.Off_Wire,
            BlockType.Off_Wire,
            BlockType.Off_Lamp,
            BlockType.Off_Lamp,
        };
        public static readonly BlockType[] PoweredState = new BlockType[]
        {
            BlockType.Air,
            BlockType.Off_Switch,
            BlockType.Powered_Switch,
            BlockType.Powered_Switch,
            BlockType.AND_Gate,
            BlockType.OR_Gate,
            BlockType.NOT_Gate,
            BlockType.XOR_Gate,
            BlockType.Stone,
            BlockType.Dirt,
            BlockType.Tin,
            BlockType.Gem,
            BlockType.Grass,
            BlockType.Leaf,
            BlockType.Wood,
            BlockType.Unbreakable,
            BlockType.Power,
            BlockType.On_Wire,
            BlockType.On_Wire,
            BlockType.On_Lamp,
            BlockType.On_Lamp,
        };

        public static readonly int3[] Int3Directions = new int3[]
        {
            new int3(-1, 0, 0),
            new int3(1, 0, 0),
            new int3(0, -1, 0),
            new int3(0, 1, 0),
            new int3(0, 0, -1),
            new int3(0, 0, 1)
        };

        public static readonly Direction[] AllDirections = new Direction[]
        {
            Direction.XP,
            Direction.XN,
            Direction.YP,
            Direction.YN,
            Direction.ZP,
            Direction.ZN
        };

        public static readonly Direction[] OppositeDirections = new Direction[]
        {
            Direction.XN,
            Direction.XP,
            Direction.YN,
            Direction.YP,
            Direction.ZN,
            Direction.ZP,
        };

    }
}