using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Opencraft.Terrain.Utilities
{
    // Helper class for shared static BlocksPerAreaSide parameter, initialized in TerrainSpawnerAuthoring
    public abstract class Constants
    {
        public static readonly SharedStatic<int> BlocksPerSide = SharedStatic<int>.GetOrCreate<Constants, BlocksPerSideKey>();
        public static readonly SharedStatic<int> BlocksPerLayer = SharedStatic<int>.GetOrCreate<Constants, BlocksPerLayerKey>();
        public static readonly SharedStatic<int> BlocksPerArea = SharedStatic<int>.GetOrCreate<Constants, BlocksPerAreaKey>();
        
        private class BlocksPerSideKey {}
        private class BlocksPerLayerKey {}
        private class BlocksPerAreaKey {}
    }

    [BurstCompile]
    public static class TerrainUtilities
    {
        // Draws outline of an area
        public static void DebugDrawTerrainArea(ref float3 terrainAreaPos, Color color, float duration = 0.0f)
        {
            var d = Constants.BlocksPerSide.Data;
            // Draw a bounding box
            Debug.DrawLine(terrainAreaPos, terrainAreaPos + new float3(d, 0, 0),color,duration );
            Debug.DrawLine(terrainAreaPos, terrainAreaPos + new float3(0, d, 0),color,duration );
            Debug.DrawLine(terrainAreaPos, terrainAreaPos + new float3(0, 0, d),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(d, d, 0), terrainAreaPos + new float3(d, 0, 0),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(d, d, 0), terrainAreaPos + new float3(0, d, 0),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(d, d, 0), terrainAreaPos + new float3(d, d, d),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(0, d, d), terrainAreaPos + new float3(0, d, 0),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(0, d, d), terrainAreaPos + new float3(0, 0, d),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(0, d, d), terrainAreaPos + new float3(d, d, d),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(d, 0, d), terrainAreaPos + new float3(d, 0, 0),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(d, 0, d), terrainAreaPos + new float3(d, d, d),color,duration);
            Debug.DrawLine(terrainAreaPos + new float3(d, 0, d), terrainAreaPos + new float3(0, 0, d),color,duration);
        }
        
        // Draws outline of a block
        public static void DebugDrawTerrainBlock(ref float3 terrainBlockPos, Color color, float duration = 0.0f)
        {
            var d = 1;
            // Draw a bounding box
            Debug.DrawLine(terrainBlockPos, terrainBlockPos + new float3(d, 0, 0),color,duration );
            Debug.DrawLine(terrainBlockPos, terrainBlockPos + new float3(0, d, 0),color,duration );
            Debug.DrawLine(terrainBlockPos, terrainBlockPos + new float3(0, 0, d),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, d, 0), terrainBlockPos + new float3(d, 0, 0),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, d, 0), terrainBlockPos + new float3(0, d, 0),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, d, 0), terrainBlockPos + new float3(d, d, d),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(0, d, d), terrainBlockPos + new float3(0, d, 0),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(0, d, d), terrainBlockPos + new float3(0, 0, d),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(0, d, d), terrainBlockPos + new float3(d, d, d),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, 0, d), terrainBlockPos + new float3(d, 0, 0),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, 0, d), terrainBlockPos + new float3(d, d, d),color,duration);
            Debug.DrawLine(terrainBlockPos + new float3(d, 0, d), terrainBlockPos + new float3(0, 0, d),color,duration);
        }
        
        // Converts a continuous world location to a discrete area location
        public static int3 GetContainingAreaLocation(ref float3 pos)
        {
            int bps = Constants.BlocksPerSide.Data;
            // Terrain Areas are placed in cube grid with intervals of blocksPerAreaSide
            //Debug.Log($"before{pos}");
            return new int3(
                (int) (bps  * math.floor(pos.x / bps )),
                (int) (bps  * math.floor(pos.y / bps )),
                (int) (bps  * math.floor(pos.z / bps )));
            //Debug.Log($"after{pos}");
        }
        
        // Converts world location to block location within an area
        public static int3 GetBlockLocationInArea(ref float3 blockPos, ref int3 terrainAreaPos)
        {
            return new int3(
                (int)math.floor(blockPos.x - terrainAreaPos.x),
                (int)math.floor(blockPos.y - terrainAreaPos.y),
                (int)math.floor(blockPos.z - terrainAreaPos.z));
        }
        
        // Converts a block position in an area to that block's index
        public static int BlockLocationToIndex(ref int3 blockPos)
        {
            int bps = Constants.BlocksPerSide.Data;
            return blockPos.y + blockPos.x * bps  + blockPos.z * bps  * bps ;
        }
        // Converts a block position in an area to it's column index
        public static int BlockLocationToColIndex(ref int3 blockPos)
        {
            int bps = Constants.BlocksPerSide.Data;
            return blockPos.x  + blockPos.z * bps ;
        }


        [BurstCompile]
        // Given an int3 position and an array of are transforms, return the containing area index if it exists.
        public static bool GetTerrainAreaByPosition(ref int3 pos,
            ref NativeArray<LocalTransform> terrainAreaTransforms,
            out int containingAreaIndex)
        {
            int bps = Constants.BlocksPerSide.Data;
            for (int i = 0; i < terrainAreaTransforms.Length; i++)
            {
                if (terrainAreaTransforms[i].Position.Equals(pos))
                {
                    containingAreaIndex = i;
                    return true;
                }
            }

            containingAreaIndex = -1;
            return false;
        }

        [BurstCompile]
        // Given a float3 position and an array of are transforms, return the containing area
        // entity and the index of the block within it
        // Returns true if the position is in an existing area, and false if no area contains it
        public static bool GetBlockLocationAtPosition(ref float3 pos,
            ref NativeArray<LocalTransform> terrainAreaTransforms,
            out int terrainAreaIndex,
            out int3 blockLocation)
        {
            int3 containingAreaLocation = GetContainingAreaLocation(ref pos);
            if (GetTerrainAreaByPosition(ref containingAreaLocation, ref terrainAreaTransforms,
                    out int containingAreaIndex))
            {
                int bps = Constants.BlocksPerSide.Data;
                int3 localPos = GetBlockLocationInArea(ref pos, ref containingAreaLocation);
                int index = BlockLocationToIndex(ref localPos);
                if (index < 0 || index >= bps * bps  * bps )
                {
                    Debug.LogError(
                        $"Block position index {index} out of bounds for location {pos} in area {containingAreaLocation}");
                    terrainAreaIndex = -1;
                    blockLocation = new int3(-1);
                    return false;
                }
#if UNITY_EDITOR
                float3 blockPos = new float3(
                    containingAreaLocation.x + localPos.x,
                    containingAreaLocation.y + localPos.y,
                    containingAreaLocation.z + localPos.z);
                DebugDrawTerrainBlock(ref blockPos, Color.green);
#endif
                //Debug.Log($"{pos} in area {containingArea}");
                terrainAreaIndex = containingAreaIndex;
                blockLocation = localPos;
                return true;
            }

            //Debug.Log($"Not in an existing area {pos}");
            terrainAreaIndex = -1;
            blockLocation = new int3(-1);
            return false;
        }

        // Given a float3 position and an array of terrain entities, area transforms, and block buffer lookup, return the value of 
        // the block.
        // Returns true if the block exists, and false otherwise
        public static bool GetBlockAtPosition(float3 pos,
            ref NativeArray<Entity> terrainAreasEntities,
            ref NativeArray<LocalTransform> terrainAreaTransforms,
            ref BufferLookup<TerrainBlocks> terrainBlockLookup,
            out BlockType blockType)
        {
            //Debug.Log($"Checking block type {pos}");
            if (GetBlockLocationAtPosition(ref pos, ref terrainAreaTransforms,
                    out int containingAreaIndex, out int3 blockLocation))
            {
                var terrainBuffer = terrainBlockLookup[terrainAreasEntities[containingAreaIndex]];

                BlockType block = terrainBuffer[BlockLocationToIndex(ref blockLocation)].type;
                if (block != BlockType.Air)
                {
                    blockType = block;
                    return true;
                }
            }
            blockType = BlockType.Air;
            return false;
        }



        // The VisibleFace functions check if there is a block directly in front of a terrain block face in the given direction
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceXN(int j, int access, bool min, int kBPS2, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborXN)
        {
            int bps = Constants.BlocksPerSide.Data;
            if (min)
            {
                //if (chunkPosX == 0)
                //    return false;

                if (neighborXN.IsEmpty)
                    return true;

                // If it is outside this chunk, get the block from the neighbouring chunk
                return neighborXN[(bps - 1) * bps  + j + kBPS2].type == BlockType.Air;
            }

            return blocks[access - bps].type == BlockType.Air;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceXP(int j, int access, bool max, int kBPS2, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborXP)
        {
            int bps = Constants.BlocksPerSide.Data;
            if (max)
            {
                //if (chunkPosX == Constants.ChunkXAmount - 1)
                //    return false;

                if (neighborXP.IsEmpty)
                    return true;

                // If it is outside this chunk, get the block from the neighbouring chunk
                return neighborXP[j + kBPS2].type == BlockType.Air;
            }

            return blocks[access +  bps].type == BlockType.Air;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceYN(int access, bool min, int iBPS, int kBPS2, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborYN)
        {
            int bps = Constants.BlocksPerSide.Data;
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
            int bps = Constants.BlocksPerSide.Data;
            int bpl = Constants.BlocksPerLayer.Data;
            if (min)
            {

                if (neighborZN.IsEmpty)
                    return true;

                return neighborZN[iBPS + j + (bps-1) * bpl].type == BlockType.Air;
            }

            return blocks[access - bpl].type == BlockType.Air;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VisibleFaceZP(int j, int access, bool max, int iBPS, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborZP)
        {
            int bpl = Constants.BlocksPerLayer.Data;
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