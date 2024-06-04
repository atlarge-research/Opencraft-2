using System;
using System.Collections.Generic;
using Opencraft.Terrain;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Layers;
using Opencraft.Terrain.Structures;
using Opencraft.Terrain.Utilities;
using Opencraft.ThirdParty;
using PolkaDOTS;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using System.Collections;
using UnityEditor.PackageManager;
using System.Collections.Concurrent;
using Unity.VisualScripting;
using Unity.NetCode;

[assembly: RegisterGenericJobType(typeof(SortJob<int2, Int2DistanceComparer>))]
namespace Opencraft.Terrain
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainStructuresSystem))]
    [BurstCompile]

    public partial struct TerrainPowerSystem : ISystem
    {
        public static ConcurrentDictionary<int3, PowerBlockData> powerBlocks;
        private int tickRate;
        private float timer;
        //private Queue<int3> poweredQueue;
        private BufferLookup<BlockPowered> terrainPowerStateLookup;
        private BufferLookup<TerrainBlocks> terrainBlocksLookup;
        private ComponentLookup<TerrainNeighbors> terrainNeighborsLookup;
        private ComponentLookup<TerrainArea> terrainAreaLookup;


        public struct PowerBlockData
        {
            public int3 BlockLocation;
            public Entity TerrainArea;
        }
        public void OnCreate(ref SystemState state)
        {
            powerBlocks = new ConcurrentDictionary<int3, PowerBlockData>();
            tickRate = 3;
            timer = 0;
            //poweredQueue = new Queue<int3>(); 
            terrainPowerStateLookup = state.GetBufferLookup<BlockPowered>(isReadOnly: false);
            terrainBlocksLookup = state.GetBufferLookup<TerrainBlocks>(isReadOnly: false);
            terrainNeighborsLookup = state.GetComponentLookup<TerrainNeighbors>(isReadOnly: false);
            terrainAreaLookup = state.GetComponentLookup<TerrainArea>(isReadOnly: false);
        }

        public void OnDestroy(ref SystemState state)
        {
            powerBlocks.Clear();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (timer < tickRate)
            {
                timer += Time.deltaTime;
                return;
            }
            timer = 0;
            terrainNeighborsLookup.Update(ref state);
            terrainBlocksLookup.Update(ref state);
            terrainPowerStateLookup.Update(ref state);
            terrainAreaLookup.Update(ref state);
            foreach (var powerBlock in powerBlocks)
            {
                int3 globalPos = powerBlock.Key;
                Entity blockEntity = powerBlock.Value.TerrainArea;
                int3 blockLoc = powerBlock.Value.BlockLocation;

                TerrainNeighbors neighbors = terrainNeighborsLookup[blockEntity];
                Entity neighborXN = neighbors.neighborXN;
                Entity neighborXP = neighbors.neighborXP;
                //Entity neighborYN = neighbors.neighborYN;
                //Entity neighborYP = neighbors.neighborYP;
                Entity neighborZN = neighbors.neighborZN;
                Entity neighborZP = neighbors.neighborZP;
                Entity[] terrainEntities = new Entity[] { blockEntity, neighborXN, neighborXP, /**neighborYN, neighborYP,**/ neighborZN, neighborZP };
                int3[] directions = new int3[] { new int3(-1, 0, 0), new int3(1, 0, 0), new int3(0, -1, 0), new int3(0, 1, 0), new int3(0, 0, -1), new int3(0, 0, 1) };

                Debug.Log("Checking Power Block at " + blockLoc.ToString() + " and " + globalPos.ToString() + " for power neighbors");

                int3 sixteens = new int3(16, 0, 16);

                for (int i = 0; i < directions.Length; i++)
                {
                    int offsetIndex = 0;
                    int3 notNormalisedBlockLoc = (blockLoc + directions[i]);
                    switch (notNormalisedBlockLoc.x)
                    {
                        case -1:
                            offsetIndex = 1;
                            break;
                        case 16:
                            offsetIndex = 2;
                            break;
                        default:
                            break;
                    }
                    switch (notNormalisedBlockLoc.z)
                    {
                        case -1:
                            offsetIndex = 3;
                            break;
                        case 16:
                            offsetIndex = 4;
                            break;
                        default:
                            break;
                    }
                    Entity neighborEntity = terrainEntities[offsetIndex];
                    if (neighborEntity == Entity.Null) continue;
                    int3 neighborBlockLoc = (notNormalisedBlockLoc + sixteens) % 16;
                    int blockIndex = TerrainUtilities.BlockLocationToIndex(ref neighborBlockLoc);
                    DynamicBuffer<BlockType> blocks = terrainBlocksLookup[neighborEntity].Reinterpret<BlockType>();
                    TerrainArea terrainArea = terrainAreaLookup[neighborEntity];
                    if (BlockData.PowerableBlock[(int)blocks[blockIndex]])
                    {
                        if (terrainPowerStateLookup.TryGetBuffer(neighborEntity, out DynamicBuffer<BlockPowered> terrainPowerState))
                        {
                            if (terrainPowerState[blockIndex].powered == false)
                            {
                                terrainPowerState[blockIndex] = new BlockPowered { powered = true };
                                DynamicBuffer<BlockType> blockTypes = terrainBlocksLookup[neighborEntity].Reinterpret<BlockType>();
                                blockTypes[blockIndex] = blockTypes[blockIndex] + 1;
                                Debug.Log("Powering " + neighborBlockLoc.ToString() + " in area " + terrainArea.location.ToString());
                            }
                        }
                    }
                    else if (blocks[blockIndex] != BlockType.Air)
                    {
                        Debug.Log("Not " + neighborBlockLoc.ToString() + " in area " + terrainArea.location.ToString());
                    }
                    else
                    {
                        Debug.Log("Air " + neighborBlockLoc.ToString() + " in area " + terrainArea.location.ToString());
                    }

                }
                //return;
            }
        }
    }
}