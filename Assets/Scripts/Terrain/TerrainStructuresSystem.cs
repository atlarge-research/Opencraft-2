using System;
using System.Runtime.CompilerServices;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Structures;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using TerrainStructuresToSpawn = Opencraft.Terrain.Authoring.TerrainStructuresToSpawn;

namespace Opencraft.Terrain
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainGenerationSystem))]
    [BurstCompile]
    // Populates terrain with structures (trees, etc) based on location marked during terrain generation
    public partial struct TerrainStructuresSystem : ISystem
    {
        private EntityQuery _structuresToSpawnQuery;
        private ProfilerMarker _markerStructureGen;
        public void OnCreate(ref SystemState state)
        {
            // Wait for scene load/baking to occur before updates. 
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<TerrainGenerationLayer>();
            state.RequireForUpdate<TerrainColumnsToSpawn>();
            state.RequireForUpdate<TerrainNeighbors>();
            _structuresToSpawnQuery = SystemAPI.QueryBuilder().WithAll<TerrainStructuresToSpawn, GenStructures, TerrainNeighbors, TerrainArea>().Build();
            state.RequireForUpdate(_structuresToSpawnQuery);
            _markerStructureGen = new ProfilerMarker("StructureGeneration");
        }

   

        public void OnUpdate(ref SystemState state)
        {
            if (_structuresToSpawnQuery.IsEmpty)
            {
                return;
            }
            _markerStructureGen.Begin();
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            EntityCommandBuffer.ParallelWriter parallelEcb = ecb.AsParallelWriter();
   
            JobHandle jobHandle = new GenerateStructuresJob()
            {
                ecb = parallelEcb,
            }.ScheduleParallel(state.Dependency);
            jobHandle.Complete();
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            _markerStructureGen.End();
        }
    }
    
    [BurstCompile]
    [WithAll(typeof(GenStructures))]
    // Given an area with structures positions, split each structures into sub-structures by what terrain areas 
    // they overlap, then construct each substructure. Each sub-structure can be split into further sub-structures.
    // Each split takes an additional frame to process!
    public partial struct GenerateStructuresJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;

        public void Execute(Entity entity, [EntityIndexInQuery]int index, in TerrainNeighbors terrainNeighbors, in TerrainArea terrainArea,
            ref DynamicBuffer<TerrainStructuresToSpawn> structuresToSpawnBuffer,
            ref DynamicBuffer<TerrainBlocks> terrainBlocksBuffer,
            ref DynamicBuffer<TerrainColMinY> colMinBuffer,
            ref DynamicBuffer<TerrainColMaxY> colMaxBuffer
            )
        {
            if (structuresToSpawnBuffer.IsEmpty)
            {
                ecb.SetComponentEnabled<GenStructures>(index, entity, false);
                return;
            }
            
            DynamicBuffer<BlockType> terrainBlocks = terrainBlocksBuffer.Reinterpret<BlockType>();
            DynamicBuffer<byte> colMin = colMinBuffer.Reinterpret<byte>();
            DynamicBuffer<byte> colMax = colMaxBuffer.Reinterpret<byte>();


            NativeList<TerrainStructuresToSpawn> structuresNotReadyToSpawn =
                new NativeList<TerrainStructuresToSpawn>(32, Allocator.Temp);
            
            // Loop through structures in this area
            foreach (var structure in structuresToSpawnBuffer)
            {
                if (!StructureContainingAreasExist(structure, terrainNeighbors))
                {
                    // This structure overlaps with an area that has not been created yet
                    structuresNotReadyToSpawn.Add(structure);   
                    continue;
                    
                }
                // Get the sub-structure that fits within this area, and send the rest to neighboring areas.
                TerrainStructuresToSpawn localStructure = SplitStructure(terrainArea, index, structure, terrainNeighbors);
                switch (localStructure.structureType)
                {
                    case StructureType.Tree:
                        GenerateTreeStructure(localStructure, terrainArea, terrainNeighbors, ref terrainBlocks,
                            ref colMin, ref colMax);
                        break;
                }
            }
            // Empty this area's structure buffer
            structuresToSpawnBuffer.Clear();
            // Re-place structures that aren't ready, and clear the generate structures flag otherwise
            if (!structuresNotReadyToSpawn.IsEmpty)
                structuresToSpawnBuffer.CopyFrom(structuresNotReadyToSpawn.AsArray());
            else
                ecb.SetComponentEnabled<GenStructures>(index, entity, false);

        }

        // Structure can be spawned if all areas it overlaps have been generated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool StructureContainingAreasExist(TerrainStructuresToSpawn structure, TerrainNeighbors terrainNeighbors )
        {
            if (structure.basePos.x - structure.extentsNeg.x < 0 && terrainNeighbors.neighborXN == Entity.Null)
                return false;
            if (structure.basePos.x + structure.extentsPos.x > Env.AREA_SIZE_1 && terrainNeighbors.neighborXP == Entity.Null)
                return false;
            if (structure.basePos.z - structure.extentsNeg.z < 0 && terrainNeighbors.neighborZN == Entity.Null)
                return false;
            if (structure.basePos.z + structure.extentsPos.z > Env.AREA_SIZE_1 && terrainNeighbors.neighborZP == Entity.Null)
                return false;
            if (structure.basePos.y - structure.extentsNeg.y < 0 && terrainNeighbors.neighborYN == Entity.Null)
                return false;
            if (structure.basePos.y + structure.extentsPos.y > Env.AREA_SIZE_1 && terrainNeighbors.neighborYP == Entity.Null)
                return false;
            return true;
        }

        // Return the sub-structure that fits within this area, and add sub-structures overlapping with
        // neighboring areas to those areas
        private TerrainStructuresToSpawn SplitStructure(TerrainArea terrainArea, int index, TerrainStructuresToSpawn structure, TerrainNeighbors terrainNeighbors)
        {
            // todo: if structure goes into area which is diagonal to it, choose only one direct neighbor to
            // todo: get sub structure overlapping with the diagonal area
            // XN
            int diffXN = structure.basePos.x - structure.extentsNeg.x;
            if (diffXN < 0)
            {
                int x = math.abs(diffXN);
                ecb.AppendToBuffer( index, terrainNeighbors.neighborXN, new TerrainStructuresToSpawn
                    {
                        basePos = new int3(Env.AREA_SIZE_1, structure.basePos.yz),
                        structureType = structure.structureType,
                        extentsNeg = new int3(x-1, structure.extentsNeg.yz),
                        extentsPos = new int3(0, structure.extentsPos.yz),
                        noise = structure.noise,
                        offset = structure.offset + new int3(structure.basePos.x+1,0,0)
                    }); 
                ecb.SetComponentEnabled<GenStructures>(index, terrainNeighbors.neighborXN, true);
                structure.extentsNeg.x -= x;
            }
            //XP
            int diffXP = structure.basePos.x + structure.extentsPos.x;
            if (diffXP > Env.AREA_SIZE_1)
            {
                int x = diffXP - Env.AREA_SIZE;
                ecb.AppendToBuffer( index, terrainNeighbors.neighborXP, new TerrainStructuresToSpawn
                    {
                        basePos = new int3(0, structure.basePos.yz),
                        structureType = structure.structureType,
                        extentsNeg = new int3(0, structure.extentsNeg.yz),
                        extentsPos = new int3(x, structure.extentsPos.yz),
                        noise = structure.noise,
                        offset = structure.offset + new int3(-(Env.AREA_SIZE - structure.basePos.x),0,0)
                    });
                ecb.SetComponentEnabled<GenStructures>(index, terrainNeighbors.neighborXP, true);
                structure.extentsPos.x -= (x+1);
            }
            // ZN
            int diffZN = structure.basePos.z - structure.extentsNeg.z;
            if (diffZN < 0)
            {
                int z = math.abs(diffZN);
                ecb.AppendToBuffer( index, terrainNeighbors.neighborZN, new TerrainStructuresToSpawn
                {
                    basePos = new int3(structure.basePos.xy,Env.AREA_SIZE_1),
                    structureType = structure.structureType,
                    extentsNeg = new int3(structure.extentsNeg.xy,z-1),
                    extentsPos = new int3(structure.extentsPos.xy,0),
                    noise = structure.noise,
                    offset = structure.offset + new int3(0,0,structure.basePos.z+1)
                });
                ecb.SetComponentEnabled<GenStructures>(index, terrainNeighbors.neighborZN, true);
                structure.extentsNeg.z -= z;
            }
            //zP
            int diffZP = structure.basePos.z + structure.extentsPos.z;
            if (diffZP > Env.AREA_SIZE_1)
            {
                int z = diffZP - Env.AREA_SIZE;
                ecb.AppendToBuffer( index, terrainNeighbors.neighborZP, new TerrainStructuresToSpawn
                {
                    basePos = new int3(structure.basePos.xy,0),
                    structureType = structure.structureType,
                    extentsNeg = new int3(structure.extentsNeg.xy, 0),
                    extentsPos = new int3(structure.extentsPos.xy, z),
                    noise = structure.noise,
                    offset = structure.offset + new int3(0,0,-(Env.AREA_SIZE - structure.basePos.z))
                });
                ecb.SetComponentEnabled<GenStructures>(index, terrainNeighbors.neighborZP, true);
                structure.extentsPos.z -= (z+1);
            }
            // YN
            int diffYN = structure.basePos.y - structure.extentsNeg.y;
            if (diffYN < 0)
            {
                int y = math.abs(diffYN);
                ecb.AppendToBuffer( index, terrainNeighbors.neighborYN, new TerrainStructuresToSpawn
                {
                    basePos =  new int3(structure.basePos.x, Env.AREA_SIZE_1, structure.basePos.z),
                    structureType = structure.structureType,
                    extentsNeg = new int3(structure.extentsNeg.x,y-1, structure.extentsNeg.z),
                    extentsPos = new int3(structure.extentsPos.x,0, structure.extentsPos.z),
                    noise = structure.noise,
                    offset = structure.offset + new int3(0,structure.basePos.y+1,0)
                }); 
                ecb.SetComponentEnabled<GenStructures>(index, terrainNeighbors.neighborYN, true);
                structure.extentsNeg.y -= y;
            }
            //YP
            int diffYP = structure.basePos.y + structure.extentsPos.y;
            if (diffYP > Env.AREA_SIZE_1)
            {
                int y = diffYP - Env.AREA_SIZE;
                ecb.AppendToBuffer( index, terrainNeighbors.neighborYP, new TerrainStructuresToSpawn
                {
                    basePos = new int3(structure.basePos.x, 0, structure.basePos.z),
                    structureType = structure.structureType,
                    extentsNeg = new int3(structure.extentsNeg.x, 0, structure.extentsNeg.z),
                    extentsPos =  new int3(structure.extentsPos.x, y, structure.extentsPos.z),
                    noise = structure.noise,
                    offset = structure.offset + new int3(0,-(Env.AREA_SIZE - structure.basePos.y),0)
                });
                ecb.SetComponentEnabled<GenStructures>(index, terrainNeighbors.neighborYP, true);
                structure.extentsPos.y -= (y+1);
            }
            return structure;
        }
        
        private void GenerateTreeStructure(TerrainStructuresToSpawn structure, TerrainArea terrainArea, TerrainNeighbors terrainNeighbors,
            ref DynamicBuffer<BlockType> terrainBlocks, ref DynamicBuffer<byte> colMin, ref DynamicBuffer<byte> colMax)
        {
            
            
            // We don't check area bounds here as SplitStructures ensures this structure is contained entirely in this area
            int startY = structure.basePos.y - structure.extentsNeg.y;
            int endY = structure.basePos.y + structure.extentsPos.y;
            int startX = structure.basePos.x - structure.extentsNeg.x;
            int endX = structure.basePos.x + structure.extentsPos.x;
            int startZ = structure.basePos.z - structure.extentsNeg.z;
            int endZ = structure.basePos.z + structure.extentsPos.z;
            
            // The base position of the original structure
            int3 origin = structure.basePos + structure.offset;

            int leavesRadius = Structure.MIN_CROWN_RADIUS + structure.noise;
            int trunkHeight = Structure.MIN_TRUNK_HEIGHT + structure.noise;
            int trunkStartHeight = origin.y;
            int trunkStopHeight =  trunkStartHeight + trunkHeight;
            int startLeafHeight =  trunkStartHeight + (trunkHeight / 2);
            int stopLeafHeight = trunkStopHeight;
            
            //OpencraftLogger.Log($"Creating tree structure at {structure.basePos} with offset
            //{structure.offset}, extends {startX} - {endX}, {startY} - {endY}, {startZ} - {endZ}");
            
            // x,y,z are in space of local terrain area (containing this sub-structure)
            // We compare then to the original origin position of the structure, which may be outside of this area
            for (int x = startX; x <= endX; x++)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    int colAccess = TerrainUtilities.BlockLocationToColIndex(x,z);
                    int blockAccess = TerrainUtilities.BlockLocationToIndex(x,0,z);
                    for (int y = startY; y <= endY; y++)
                    {
                        bool placed = false;
                        // Trunk
                        if (x == origin.x && z == origin.z && y >= trunkStartHeight && y < trunkStopHeight)
                        {
                            terrainBlocks[blockAccess + y] = BlockType.Wood;
                            placed = true;
                        }
                        // Leaves
                        else if (y >= startLeafHeight && y < stopLeafHeight
                                                      && (x >= origin.x - leavesRadius && x <= origin.x + leavesRadius)
                                                      && (z >= origin.z - leavesRadius && z <= origin.z + leavesRadius))
                        {
                            terrainBlocks[blockAccess + y] = BlockType.Leaf;
                            placed = true;
                        }
                        // Crown
                        else if (y == trunkStopHeight
                                 && (x >= origin.x - 1 && x <= origin.x + 1)
                                 && (z >= origin.z - 1 && z <= origin.z + 1))
                        {
                            terrainBlocks[blockAccess + y] = BlockType.Leaf;
                            placed = true;
                        }
                        
                        // Update column heights
                        if (placed)
                        {
                            if (colMin[colAccess] > y)
                                colMin[colAccess] = (byte)(y);
                            if (colMax[colAccess] < y + 1)
                                colMax[colAccess] = (byte)(y + 1);
                        }
                        
                    }
                }
            }


        }
    }
    

}