using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Structures;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Opencraft.Terrain.Authoring
{
    public class TerrainAreaAuthoring : MonoBehaviour
    {
        public class TerrainBlockBaker : Baker<TerrainAreaAuthoring>
        {
            public override void Bake(TerrainAreaAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                // Initialize with no neighbors
                var terrainNeighbors = new TerrainNeighbors()
                {
                    neighborXP = Entity.Null,
                    neighborXN = Entity.Null,
                    neighborYP = Entity.Null,
                    neighborYN = Entity.Null,
                    neighborZP = Entity.Null,
                    neighborZN = Entity.Null,
                };
                AddComponent(entity, terrainNeighbors);
                AddComponent<TerrainArea>(entity);
                AddComponent<NewSpawn>(entity);
                AddComponent<Remesh>(entity);
                
                AddComponent<GenStructures>(entity);
                SetComponentEnabled<GenStructures>(entity, false);
                
                AddBuffer<TerrainBlocks>(entity);
                AddBuffer<TerrainColMinY>(entity);
                AddBuffer<TerrainColMaxY>(entity);
                AddBuffer<TerrainStructuresToSpawn>(entity);

            }
        }
    }

    // Component representing a terrain area. On clients we set up references to neighbor areas
    public struct TerrainArea : IComponentData
    {
        [GhostField] public int3 location;
    }

    public struct TerrainNeighbors : IComponentData
    {
        public Entity neighborXP;
        public Entity neighborXN;
        public Entity neighborYP;
        public Entity neighborYN;
        public Entity neighborZP;
        public Entity neighborZN;
    }

    // NewSpawn component marks an entity as freshly instantiated.
    // todo - when to disable the newspawn component? End of same tick? Currently done at start of next tick by TerrainGenerationSystem on server
    // todo - and by end of same tick by TerrainRenderInitSystem on client
    public struct NewSpawn : IComponentData, IEnableableComponent
    {
    }

    // Remesh component marks an entity as needing to be remeshed by the TerrainMeshingSystem
    public struct Remesh : IComponentData, IEnableableComponent
    {
    }
    
    // Remesh component marks an entity as having structures to be generated
    public struct GenStructures : IComponentData, IEnableableComponent
    {
    }


    [InternalBufferCapacity(4096)]
    // The buffer component to store terrain blocks
    public struct TerrainBlocks : IBufferElementData
    {
        [GhostField] public BlockType type;
    }
    
    [InternalBufferCapacity(512)]
    // The buffer component to store heightmap column min
    public struct TerrainColMinY : IBufferElementData
    {
        [GhostField] public byte minY;
    }
    
    [InternalBufferCapacity(512)]
    // The buffer component to store heightmap column max
    public struct TerrainColMaxY : IBufferElementData
    {
        [GhostField] public byte maxY;
    }
    
    [InternalBufferCapacity(32)]
    // Buffer of terrain area columns we need to spawn but haven't yet
    public struct TerrainStructuresToSpawn : IBufferElementData
    {
        public int3 localPos;
        public StructureType structureType;
        public int3 extents;
    }
    
    
}



