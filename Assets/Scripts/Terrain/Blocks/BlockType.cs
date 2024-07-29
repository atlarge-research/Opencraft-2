﻿using System;
using Unity.Mathematics;

namespace Opencraft.Terrain.Blocks
{
    // The enum of all supported types
    public enum BlockType : byte
    {
        Air,
        Off_Input,
        On_Input,
        Clock,
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
        };

        // UV sizing > 1 tiles a texture across multiple blocks, currently not done for any block types
        public static float[] BlockUVSizing = new float[]
        {
            1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f
        };

        public static readonly bool[] CanReceiveLogic = new bool[]
        {
            false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, true, true, true, true,
        };

        public static bool IsLogic(BlockType type)
        {
            return IsInput(type) || IsGate(type) || type == BlockType.Off_Wire || type == BlockType.On_Wire || type == BlockType.Off_Lamp || type == BlockType.On_Lamp;
        }

        public static bool IsGate(BlockType type)
        {
            return IsTwoInputGate(type) || type == BlockType.NOT_Gate;
        }
        public static bool IsTwoInputGate(BlockType type)
        {
            return type == BlockType.AND_Gate || type == BlockType.OR_Gate || type == BlockType.XOR_Gate;
        }
        public static bool IsTransmitter(BlockType type)
        {
            return type == BlockType.On_Wire || type == BlockType.On_Input || type == BlockType.Clock;
        }

        public static bool IsInput(BlockType type)
        {
            return type == BlockType.On_Input || type == BlockType.Off_Input || type == BlockType.Clock;
        }

        public static readonly BlockType[] OffState = new BlockType[]
        {
            BlockType.Air,
            BlockType.Off_Input,
            BlockType.On_Input,
            BlockType.Clock,
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
            BlockType.Off_Wire,
            BlockType.Off_Wire,
            BlockType.Off_Lamp,
            BlockType.Off_Lamp,
        };
        public static readonly BlockType[] OnState = new BlockType[]
        {
            BlockType.Air,
            BlockType.Off_Input,
            BlockType.On_Input,
            BlockType.Clock,
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


        private static bool Strips(int row)
        {
            return (row % 2 != 0);
        }

        private static bool Edges(int row, int col)
        {
            return row == 0 || row == 15 || col == 0 || col == 15;
        }
        private static bool TwoInputGate(int row, int col, double add)
        {
            return Strips(row) && !Edges(row, col) && (col % 12 == (add + (row + 1) * 0.5) % 12);
        }

    }
}