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

        public struct PowerBlockData
        {
            public int3 BlockLocation;
            public Entity TerrainArea;
        }
        public void OnCreate(ref SystemState state)
        {
            powerBlocks = new ConcurrentDictionary<int3, PowerBlockData>();
            tickRate = 1;
            timer = 0;
            //poweredQueue = new Queue<int3>(); 
            terrainPowerStateLookup = state.GetBufferLookup<BlockPowered>(isReadOnly: false);
            terrainBlocksLookup = state.GetBufferLookup<TerrainBlocks>(isReadOnly: false);
            terrainNeighborsLookup = state.GetComponentLookup<TerrainNeighbors>(isReadOnly: false);
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
            foreach (var powerBlock in powerBlocks)
            {
                int3 globalPos = powerBlock.Key;
                Entity blockEntity = powerBlock.Value.TerrainArea;
                int3 blockLoc = powerBlock.Value.BlockLocation;

                TerrainNeighbors neighbors = terrainNeighborsLookup[blockEntity];
                Entity neighborXN = neighbors.neighborXN;
                Entity neighborXP = neighbors.neighborXP;
                Entity neighborYN = neighbors.neighborYN;
                Entity neighborYP = neighbors.neighborYP;
                Entity neighborZN = neighbors.neighborZN;
                Entity neighborZP = neighbors.neighborZP;
                Entity[] neighborBlocks = new Entity[] { neighborXN, neighborXP, neighborYN, neighborYP, neighborZN, neighborZP };


                for (int i = 0; i < neighborBlocks.Length; i++)
                {
                    if (neighborBlocks[i] != Entity.Null)
                    {

                    }
                }

                return;

            }


        }
    }
}