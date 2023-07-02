using System.Collections.Generic;
using Opencraft.Terrain.Authoring;
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
    public abstract class BlocksPerAreaSide
    {
        public static readonly SharedStatic<int> BlocksPerSide = SharedStatic<int>.GetOrCreate<BlocksPerAreaSide, BlocksPerSideKey>();
        
        private class BlocksPerSideKey {}
    }

    [BurstCompile]
    public static class TerrainUtilities
    {
        // Draws outline of an area
        public static void DebugDrawTerrainArea(ref float3 terrainAreaPos, Color color, float duration = 0.0f)
        {
            var d = BlocksPerAreaSide.BlocksPerSide.Data;
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
            int bps = BlocksPerAreaSide.BlocksPerSide.Data;
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
            int bps = BlocksPerAreaSide.BlocksPerSide.Data;
            return blockPos.x + blockPos.y * bps  + blockPos.z * bps  * bps ;
        }


        [BurstCompile]
        // Given an int3 position and an array of are transforms, return the containing area index if it exists.
        public static bool GetTerrainAreaByPosition(ref int3 pos,
            ref NativeArray<LocalTransform> terrainAreaTransforms,
            out int containingAreaIndex)
        {
            int bps = BlocksPerAreaSide.BlocksPerSide.Data;
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
                int bps = BlocksPerAreaSide.BlocksPerSide.Data;
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
            out int blockType)
        {
            //Debug.Log($"Checking block type {pos}");
            if (GetBlockLocationAtPosition(ref pos, ref terrainAreaTransforms,
                    out int containingAreaIndex, out int3 blockLocation))
            {
                var terrainBuffer = terrainBlockLookup[terrainAreasEntities[containingAreaIndex]];

                int block = terrainBuffer[BlockLocationToIndex(ref blockLocation)].Value;
                if (block != -1)
                {
                    blockType = block;
                    return true;
                }
                //Debug.Log($"Block type exists but is empty for {pos}");
            }

            //Debug.Log($"{pos} doesn't exist or has no type");
            blockType = -1;
            return false;
        }



        // The VisibleFace functions check if there is a block directly in front of a terrain block face in the given direction
        public static bool VisibleFaceXN(int i, int j, int k, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborXN)
        {
            int blocksPerSide = BlocksPerAreaSide.BlocksPerSide.Data;
            // Access from a neighbouring chunk
            if (i < 0)
            {
                if (neighborXN.IsEmpty)
                    return true;
                return neighborXN[(blocksPerSide - 1) + j * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
            }

            // Access from this chunk
            return blocks[i + j * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
        }

        public static bool VisibleFaceXP(int i, int j, int k, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborXP)
        {
            int blocksPerSide = BlocksPerAreaSide.BlocksPerSide.Data;
            if (i >= blocksPerSide)
            {
                if (neighborXP.IsEmpty)
                    return true;

                return neighborXP[0 + j * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
            }

            return blocks[i + j * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
        }

        public static bool VisibleFaceZN(int i, int j, int k, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborZN)
        {
            int blocksPerSide = BlocksPerAreaSide.BlocksPerSide.Data;
            if (k < 0)
            {
                if (neighborZN.IsEmpty)
                    return true;
                return neighborZN[i + j * blocksPerSide + (blocksPerSide - 1) * (blocksPerSide * blocksPerSide)].Value == -1;
            }

            return blocks[i + j * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
        }

        public static bool VisibleFaceZP(int i, int j, int k, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborZP)
        {
            int blocksPerSide = BlocksPerAreaSide.BlocksPerSide.Data;
            if (k >= blocksPerSide)
            {
                if (neighborZP.IsEmpty)
                    return true;
                return neighborZP[i + j * blocksPerSide].Value == -1;
            }

            return blocks[i + j * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
        }

        public static bool VisibleFaceYN(int i, int j, int k, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborYN)
        {
            int blocksPerSide = BlocksPerAreaSide.BlocksPerSide.Data;
            if (j < 0)
            {
                if (neighborYN.IsEmpty)
                    return true;
                return neighborYN[i + (blocksPerSide - 1) * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
            }

            return blocks[i + j * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
        }

        public static bool VisibleFaceYP(int i, int j, int k, ref DynamicBuffer<TerrainBlocks> blocks,
            ref DynamicBuffer<TerrainBlocks> neighborYP)
        {
            int blocksPerSide = BlocksPerAreaSide.BlocksPerSide.Data;
            if (j >= blocksPerSide)
            {
                if (neighborYP.IsEmpty)
                    return true;
                return neighborYP[i + k * (blocksPerSide * blocksPerSide)].Value == -1;
            }

            return blocks[i + j * blocksPerSide + k * (blocksPerSide * blocksPerSide)].Value == -1;
        }
    }
}