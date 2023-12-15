using System;
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
    [BurstCompile]
    public static class TerrainUtilities
    {
        
        [Serializable]
        public class TerrainChunkNotLoadedException : Exception
        {
            public TerrainChunkNotLoadedException() {  }

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 GetContainingAreaLocation(ref float3 pos)
        {
            // Terrain Areas are placed in cube grid at intervals of Env.AREA_SIZE
            return new int3(
                (Env.AREA_SIZE* NoiseUtilities.FastFloor(pos.x / Env.AREA_SIZE )),
                (Env.AREA_SIZE * NoiseUtilities.FastFloor(pos.y / Env.AREA_SIZE)),
                (Env.AREA_SIZE* NoiseUtilities.FastFloor(pos.z / Env.AREA_SIZE )));
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
            return blockPos.y + blockPos.x * Env.AREA_SIZE  + blockPos.z * Env.AREA_SIZE_POW_2;
        }
        
        // Converts a block position in an area to that block's index
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockLocationToIndex(int x, int y, int z)
        { 
            return y + x * Env.AREA_SIZE + z * Env.AREA_SIZE_POW_2;
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
            return blockPos.x  + blockPos.z * bps ;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockLocationToColIndex(int x, int z)
        {
            int bps = Env.AREA_SIZE;
            return x  + z * bps ;
        }


        [BurstCompile]
        // Given an int3 position and an array of area transforms, return the containing area index if it exists.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetTerrainAreaByPosition(ref int3 pos,
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

        /*
        [BurstCompile]
        // Given a float3 position and an array of are transforms, return the containing area
        // entity and the index of the block within it
        // Returns true if the position is in an existing area, and false if no area contains it
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetBlockLocationAtPosition(ref float3 pos,
            in NativeArray<TerrainArea> terrainAreas,
            out int terrainAreaIndex,
            out int3 blockLocation)
        {
            int3 containingAreaLocation = GetContainingAreaLocation(ref pos);
            if (GetTerrainAreaByPosition(ref containingAreaLocation, in terrainAreas,
                    out int containingAreaIndex))
            {
                int3 localPos = GetBlockLocationInArea(in pos, in containingAreaLocation);
                int index = BlockLocationToIndex(ref localPos);
                if (index < 0 || index >= Env.AREA_SIZE_POW_3 )
                {
                    Debug.LogError(
                        $"Block position localpos {localPos} with index {index} out of bounds for location {pos} in area {containingAreaLocation}");
                    terrainAreaIndex = -1;
                    blockLocation = new int3(-1);
                    return false;
                }
#if UNITY_EDITOR
                float3 blockPos = new float3(
                    containingAreaLocation.x + localPos.x,
                    containingAreaLocation.y + localPos.y,
                    containingAreaLocation.z + localPos.z);
                DebugDrawTerrainBlock(in blockPos, Color.green);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetBlockAtPosition(float3 blockGlobalPos,
            in NativeArray<Entity> terrainAreasEntities,
            in NativeArray<TerrainArea> terrainAreas,
            in BufferLookup<TerrainBlocks> terrainBlockLookup,
            out BlockType blockType)
        {
            //Debug.Log($"Checking block type {pos}");
            if (GetBlockLocationAtPosition(ref blockGlobalPos, in terrainAreas,
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
        }*/

        public struct BlockSearchInput
        {
            public int3 basePos;
            public int3 offset;
            public Entity areaEntity;
            public int3 terrainAreaPos;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void DefaultBlockSearchInput(ref BlockSearchInput bsi)
            {
                bsi.basePos = new int3(-1);
                bsi.offset = int3.zero;
                bsi.areaEntity = Entity.Null;
                bsi.terrainAreaPos = new int3(-1);
            }
        }

        public struct BlockSearchOutput
        {
            public Entity containingArea;
            public int3 containingAreaPos;
            public int3 localPos;
            public BlockType blockType;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void DefaultBlockSearchOutput(ref BlockSearchOutput bso)
            {
                bso.containingArea = Entity.Null;
                bso.containingAreaPos = new int3(-1);
                bso.localPos = new int3(-1);
                bso.blockType = BlockType.Air;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // Uses neighbor entity links to check block types via offset from an existing position with a given terrain area
        // Returns true if the block exists and is not air
        public static bool GetBlockAtPositionByOffset(in BlockSearchInput input, ref BlockSearchOutput output,
            ref ComponentLookup<TerrainNeighbors> terrainNeighborsLookup,
            ref BufferLookup<TerrainBlocks> terrainBlockLookup, bool debug = false)
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
            int3 terrainAreaPosAdj = input.terrainAreaPos;
            // Iterate through neighbors to get containing area of the offsetPos
            int step = 0;
            while (true)
            {
                if (debug)
                {
                    Debug.Log($"   [{step}]: g {offsetPos} l {localPos} c {terrainAreaPosAdj}");
                }

                step++;

                currentNeighbors = terrainNeighborsLookup[currentEntity];
                if (localPos.x < 0)
                {
                    if (currentNeighbors.neighborXN != Entity.Null)
                    {
                        localPos.x += Env.AREA_SIZE;
                        terrainAreaPosAdj.x -= Env.AREA_SIZE;
                        currentEntity = currentNeighbors.neighborXN;
                        continue;
                    }
                    //Debug.LogWarning($"NeighborXN not found!");
                    notFound = true;
                    break;
                }
                if (localPos.x >= Env.AREA_SIZE)
                {
                    if (currentNeighbors.neighborXP != Entity.Null)
                    {
                        localPos.x -= Env.AREA_SIZE;
                        terrainAreaPosAdj.x += Env.AREA_SIZE;
                        currentEntity = currentNeighbors.neighborXP;
                        continue;
                    } 
                    //Debug.LogWarning($"NeighborXP not found!");
                    notFound = true;
                    break;
                }
                if (localPos.y < 0)
                {
                    if (currentNeighbors.neighborYN != Entity.Null)
                    {
                        localPos.y += Env.AREA_SIZE;
                        terrainAreaPosAdj.y -= Env.AREA_SIZE;
                        currentEntity = currentNeighbors.neighborYN;
                        continue;
                    } 
                    //Debug.LogWarning($"NeighborYN not found!");
                    notFound = true;
                    break;
                }
                if (localPos.y >= Env.AREA_SIZE)
                {
                    if (currentNeighbors.neighborYP != Entity.Null)
                    {
                        localPos.y -= Env.AREA_SIZE;
                        terrainAreaPosAdj.y += Env.AREA_SIZE;
                        currentEntity = currentNeighbors.neighborYP;
                        continue;
                    } 
                    //Debug.LogWarning($"NeighborYP not found!");
                    notFound = true;
                    break;
                }
                if (localPos.z < 0)
                {
                    if (currentNeighbors.neighborZN != Entity.Null)
                    {
                        localPos.z += Env.AREA_SIZE;
                        terrainAreaPosAdj.z -= Env.AREA_SIZE;
                        currentEntity = currentNeighbors.neighborZN;
                        continue;
                    } 
                    //Debug.LogWarning($"NeighborZN not found!");
                    notFound = true;
                    break;
                }
                if (localPos.z >= Env.AREA_SIZE)
                {
                    if (currentNeighbors.neighborZP != Entity.Null)
                    {
                        localPos.z -= Env.AREA_SIZE;
                        terrainAreaPosAdj.z += Env.AREA_SIZE;
                        currentEntity = currentNeighbors.neighborZP;
                        continue;
                    } 
                    //Debug.LogWarning($"NeighborZP not found!");
                    notFound = true;
                    break;
                }
                break;
            }

            if (notFound)
            {
                return false;
            }
            DynamicBuffer<TerrainBlocks> blocks = terrainBlockLookup[currentEntity];
             BlockType block = blocks[BlockLocationToIndex(ref localPos)].type;
             output.containingArea = currentEntity;
             output.containingAreaPos = terrainAreaPosAdj;
             output.localPos = localPos;
             output.blockType = block;
#if UNITY_EDITOR
            if(debug)
            {
                float3 p = new float3(offsetPos);
                DebugDrawTerrainBlock(in p, Color.green,  3.0f);
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
            int bps = Env.AREA_SIZE;
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
            int bps = Env.AREA_SIZE;
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
            int bps = Env.AREA_SIZE;
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
            int bps = Env.AREA_SIZE;
            int bpl = Env.AREA_SIZE_POW_2;
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
            int bpl = Env.AREA_SIZE_POW_2;
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