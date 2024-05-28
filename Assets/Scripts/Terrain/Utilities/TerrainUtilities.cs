using System;
using System.Runtime.CompilerServices;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Opencraft.Terrain.Utilities
{
    [BurstCompile]
    public static class TerrainUtilities
    {

        [Serializable]
        public class TerrainChunkNotLoadedException : Exception
        {
            public TerrainChunkNotLoadedException() { }

            public TerrainChunkNotLoadedException(string message)
                : base(String.Format("Terrain chunk not loaded: {0}", message))
            {

            }
        }

        // Draws outline of an area
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugDrawTerrainArea(in float3 terrainAreaPos, Color color, float duration = 0.0f)
        {
            DebugDrawTerrainBlock(in terrainAreaPos, color, duration, Env.AREA_SIZE);
        }

        // Draws outline of a block
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugDrawTerrainBlock(in float3 terrainBlockPos, Color color, float duration = 0.0f, float size = 1.0f)
        {
            var d = size;
            // Draw a bounding box
            Debug.DrawLine(terrainBlockPos, terrainBlockPos + new float3(d, 0, 0), color, duration);
            Debug.DrawLine(terrainBlockPos, terrainBlockPos + new float3(0, d, 0), color, duration);
            Debug.DrawLine(terrainBlockPos, terrainBlockPos + new float3(0, 0, d), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, d, 0), terrainBlockPos + new float3(d, 0, 0), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, d, 0), terrainBlockPos + new float3(0, d, 0), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, d, 0), terrainBlockPos + new float3(d, d, d), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(0, d, d), terrainBlockPos + new float3(0, d, 0), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(0, d, d), terrainBlockPos + new float3(0, 0, d), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(0, d, d), terrainBlockPos + new float3(d, d, d), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, 0, d), terrainBlockPos + new float3(d, 0, 0), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, 0, d), terrainBlockPos + new float3(d, d, d), color, duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, 0, d), terrainBlockPos + new float3(0, 0, d), color, duration);
        }

        // Converts a continuous world location to a discrete area location
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 GetContainingAreaLocation(in float3 pos)
        {
            // Terrain Areas are placed in cube grid at intervals of Env.AREA_SIZE
            return new int3(
                (Env.AREA_SIZE * NoiseUtilities.FastFloor(pos.x / Env.AREA_SIZE)),
                (Env.AREA_SIZE * NoiseUtilities.FastFloor(pos.y / Env.AREA_SIZE)),
                (Env.AREA_SIZE * NoiseUtilities.FastFloor(pos.z / Env.AREA_SIZE)));
        }

        private static int GetTerrainAreaIndex(in int3 blockPos, in NativeArray<TerrainArea> terrainAreas)
        {

            if (!TerrainUtilities.GetTerrainAreaByPosition(in blockPos, terrainAreas, out int containingAreaIndex))
            {
                return -1;
            }

            return containingAreaIndex;

        }

        // Converts world location to block location within an area
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 GetBlockLocationInArea(in int3 blockPos, in int3 terrainAreaPos)
        {
            return new int3(
                blockPos.x - terrainAreaPos.x,
                blockPos.y - terrainAreaPos.y,
                blockPos.z - terrainAreaPos.z);
        }

        // Converts a block position in an area to that block's index
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockLocationToIndex(ref int3 blockPos)
        {
            return blockPos.y + blockPos.x * Env.AREA_SIZE + blockPos.z * Env.AREA_SIZE_POW_2;
        }

        // Converts a block position in an area to that block's index
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockLocationToIndex(int x, int y, int z)
        {
            return y + x * Env.AREA_SIZE + z * Env.AREA_SIZE_POW_2;
        }

        public static int3 BlockIndexToLocation(int index)
        {
            int z = index / Env.AREA_SIZE_POW_2;
            int rem_z = index % Env.AREA_SIZE_POW_2;
            int x = rem_z / Env.AREA_SIZE;
            int y = rem_z % Env.AREA_SIZE;
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockLocationHash(int x, int y, int z)
        {
            unchecked
            {
                int hashCode = x;
                hashCode = (hashCode * 397) ^ y;
                hashCode = (hashCode * 397) ^ z;
                return hashCode;
            }
        }

        // Converts a block position in an area to it's column index
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockLocationToColIndex(ref int3 blockPos)
        {
            int bps = Env.AREA_SIZE;
            return blockPos.x + blockPos.z * bps;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockLocationToColIndex(int x, int z)
        {
            int bps = Env.AREA_SIZE;
            return x + z * bps;
        }


        [BurstCompile]
        // Given an int3 position and an array of area transforms, return the containing area index if it exists.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetTerrainAreaByPosition(in int3 pos,
            in NativeArray<TerrainArea> terrainAreas,
            out int containingAreaIndex)
        {
            //int bps = Env.AREA_SIZE;
            for (int i = 0; i < terrainAreas.Length; i++)
            {
                int3 loc = terrainAreas[i].location * Env.AREA_SIZE;
                if (loc.Equals(pos))
                {
                    containingAreaIndex = i;
                    return true;
                }
            }

            containingAreaIndex = -1;
            return false;
        }

        public struct BlockSearchInput
        {
            public int3 basePos;
            public int3 offset;
            public Entity areaEntity;
            public int3 terrainAreaPos;
            public int columnHeight;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void DefaultBlockSearchInput(ref BlockSearchInput bsi)
            {
                bsi.basePos = new int3(-1);
                bsi.offset = int3.zero;
                bsi.areaEntity = Entity.Null;
                bsi.terrainAreaPos = new int3(-1);
                bsi.columnHeight = 1;
            }
        }

        public enum BlockSearchResult
        {
            SearchNotCompleted = 0,
            NotLoaded = 1,
            OutOfBounds = 2,
            Found = 3
        }

        public struct BlockSearchOutput
        {
            public Entity containingArea;
            public int3 containingAreaPos;
            public int3 localPos;
            public BlockType blockType;
            public BlockSearchResult result;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void DefaultBlockSearchOutput(ref BlockSearchOutput bso)
            {
                bso.containingArea = Entity.Null;
                bso.containingAreaPos = new int3(-1);
                bso.localPos = new int3(-1);
                bso.blockType = BlockType.Air;
                bso.result = BlockSearchResult.SearchNotCompleted;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // Uses neighbor entity links to check block types via offset from an existing position with a given terrain area
        // Returns true if the block exists and is not air
        public static bool GetBlockAtPositionByOffset(in BlockSearchInput input, ref BlockSearchOutput output,
            in ComponentLookup<TerrainNeighbors> terrainNeighborsLookup,
            in BufferLookup<TerrainBlocks> terrainBlockLookup, bool debug = false)
        {
            if (input.areaEntity == Entity.Null)
            {
                Debug.Log($"Block at offset called with null area entity");
                return false;
            }
            int3 offsetPos = input.basePos + input.offset;
            int3 localPos = GetBlockLocationInArea(in offsetPos, in input.terrainAreaPos);
            Entity currentEntity = input.areaEntity;
            TerrainNeighbors currentNeighbors;
            bool notFound = false;
            bool outOfBounds = false;
            int3 terrainAreaPosAdj = input.terrainAreaPos;
            // Iterate through neighbors to get containing area of the offsetPos
            int step = 0;
            while (true)
            {
                if (debug)
                    Debug.Log($"   BlockSearchByOffset [{step}]: global {offsetPos} = local {localPos} in area {terrainAreaPosAdj}");


                step++;

                currentNeighbors = terrainNeighborsLookup[currentEntity];
                if (localPos.x < 0)
                {
                    terrainAreaPosAdj.x -= Env.AREA_SIZE;
                    localPos.x += Env.AREA_SIZE;
                    if (currentNeighbors.neighborXN != Entity.Null)
                    {
                        currentEntity = currentNeighbors.neighborXN;
                        continue;
                    }
                    if (debug)
                        Debug.LogWarning($"NeighborXN not found!");
                    notFound = true;
                    break;
                }
                if (localPos.x >= Env.AREA_SIZE)
                {
                    terrainAreaPosAdj.x += Env.AREA_SIZE;
                    localPos.x -= Env.AREA_SIZE;
                    if (currentNeighbors.neighborXP != Entity.Null)
                    {
                        currentEntity = currentNeighbors.neighborXP;
                        continue;
                    }
                    if (debug)
                        Debug.LogWarning($"NeighborXP not found!");
                    notFound = true;
                    break;
                }
                if (localPos.y < 0)
                {
                    terrainAreaPosAdj.y -= Env.AREA_SIZE;
                    localPos.y += Env.AREA_SIZE;
                    if (currentNeighbors.neighborYN != Entity.Null)
                    {
                        currentEntity = currentNeighbors.neighborYN;
                        continue;
                    }
                    if (debug)
                        Debug.LogWarning($"NeighborYN not found!");
                    if (terrainAreaPosAdj.y < 0)
                    {
                        outOfBounds = true;
                    }
                    else
                    {
                        notFound = true;
                    }
                    break;
                }
                if (localPos.y >= Env.AREA_SIZE)
                {
                    terrainAreaPosAdj.y += Env.AREA_SIZE;
                    localPos.y -= Env.AREA_SIZE;
                    if (currentNeighbors.neighborYP != Entity.Null)
                    {
                        currentEntity = currentNeighbors.neighborYP;
                        continue;
                    }
                    if (debug)
                        Debug.LogWarning($"NeighborYP not found!");
                    if (terrainAreaPosAdj.y >= input.columnHeight * Env.AREA_SIZE)
                    {
                        outOfBounds = true;
                    }
                    else
                    {
                        notFound = true;
                    }
                    break;
                }
                if (localPos.z < 0)
                {
                    terrainAreaPosAdj.z -= Env.AREA_SIZE;
                    localPos.z += Env.AREA_SIZE;
                    if (currentNeighbors.neighborZN != Entity.Null)
                    {
                        currentEntity = currentNeighbors.neighborZN;
                        continue;
                    }
                    if (debug)
                        Debug.LogWarning($"NeighborZN not found!");
                    notFound = true;
                    break;
                }
                if (localPos.z >= Env.AREA_SIZE)
                {
                    terrainAreaPosAdj.z += Env.AREA_SIZE;
                    localPos.z -= Env.AREA_SIZE;
                    if (currentNeighbors.neighborZP != Entity.Null)
                    {
                        currentEntity = currentNeighbors.neighborZP;
                        continue;
                    }
                    if (debug)
                        Debug.LogWarning($"NeighborZP not found!");
                    notFound = true;
                    break;
                }
                break;
            }

            output.containingAreaPos = terrainAreaPosAdj;
            output.localPos = localPos;
            if (notFound)
            {
                output.result = BlockSearchResult.NotLoaded;
                return false;
            }

            if (outOfBounds)
            {
                output.result = BlockSearchResult.OutOfBounds;
                return false;
            }
            DynamicBuffer<TerrainBlocks> blocks = terrainBlockLookup[currentEntity];
            BlockType block = blocks[BlockLocationToIndex(ref localPos)].type;
            output.containingArea = currentEntity;
            output.blockType = block;
            output.result = BlockSearchResult.Found;
#if UNITY_EDITOR
            if (debug)
            {
                float3 p = new float3(offsetPos);
                DebugDrawTerrainBlock(in p, Color.green, 3.0f);
                Debug.Log($"Global pos {offsetPos} is localPos {localPos} in chunk {terrainAreaPosAdj}, block is {block}");
            }
            /*Debug.Log($"Offset pos is {localPos} in {terrainAreaPos}");
            float3 blockPos = new float3(
                terrainAreaPosAdj.x + localPos.x,
                terrainAreaPosAdj.y + localPos.y,
                terrainAreaPosAdj.z + localPos.z);
            DebugDrawTerrainBlock(ref blockPos, Color.green);*/
#endif

            return true;

        }



        // The VisibleFace functions check if there is a block directly in front of a terrain block face in the given direction
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceXN(int j, int access, bool min, int kBPS2, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborXN)
        {
            const int bps = Env.AREA_SIZE;
            if (min)
            {
                //if (chunkPosX == 0)
                //    return false;

                if (neighborXN.IsEmpty)
                    return true;

                // If it is outside this chunk, get the block from the neighbouring chunk
                return neighborXN[(bps - 1) * bps + j + kBPS2].type == BlockType.Air;
            }

            return blocks[access - bps].type == BlockType.Air;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceXP(int j, int access, bool max, int kBPS2, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborXP)
        {
            const int bps = Env.AREA_SIZE;
            if (max)
            {
                //if (chunkPosX == Constants.ChunkXAmount - 1)
                //    return false;

                if (neighborXP.IsEmpty)
                    return true;

                // If it is outside this chunk, get the block from the neighbouring chunk
                return neighborXP[j + kBPS2].type == BlockType.Air;
            }

            return blocks[access + bps].type == BlockType.Air;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceYN(int access, bool min, int iBPS, int kBPS2, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborYN)
        {
            const int bps = Env.AREA_SIZE;
            if (min)
            {

                if (neighborYN.IsEmpty)
                    return true;

                // If it is outside this chunk, get the block from the neighbouring chunk
                return neighborYN[iBPS + (bps - 1) + kBPS2].type == BlockType.Air;
            }

            return blocks[access - 1].type == BlockType.Air;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceYP(int access, bool max, int iBPS, int kBPS2, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborYP)
        {
            if (max)
            {
                if (neighborYP.IsEmpty)
                    return true;

                return neighborYP[iBPS + kBPS2].type == BlockType.Air;
            }

            return blocks[access + 1].type == BlockType.Air;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceZN(int j, int access, bool min, int iBPS, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborZN)
        {
            const int bps = Env.AREA_SIZE;
            const int bpl = Env.AREA_SIZE_POW_2;
            if (min)
            {

                if (neighborZN.IsEmpty)
                    return true;

                return neighborZN[iBPS + j + (bps - 1) * bpl].type == BlockType.Air;
            }

            return blocks[access - bpl].type == BlockType.Air;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceZP(int j, int access, bool max, int iBPS, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborZP)
        {
            const int bpl = Env.AREA_SIZE_POW_2;
            if (max)
            {

                if (neighborZP.IsEmpty)
                    return true;

                return neighborZP[iBPS + j].type == BlockType.Air;
            }

            return blocks[access + bpl].type == BlockType.Air;
        }
    }
}