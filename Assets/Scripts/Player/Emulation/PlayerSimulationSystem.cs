using System;
using System.Text;
using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using PolkaDOTS;
using PolkaDOTS.Deployment;
using Priority_Queue;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Profiling;
using UnityEngine;

namespace Opencraft.Player.Emulation
{
    /// <summary>
    /// Player emulation system that simulates players by determining player behaviour (e.g. inputs) through
    /// observing surroundings. Uses an adapted version of the A* search algorithm
    /// </summary>
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PlayerSimulationSystem : ISystem
    {
        private ProfilerMarker _markerPlayerSimulation;
        
        // Terrain structure references
        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private ComponentLookup<TerrainNeighbors> _terrainNeighborLookup;
        // Reusable block search input/output structs
        private TerrainUtilities.BlockSearchInput BSI;
        private TerrainUtilities.BlockSearchOutput BSO;
        
        // Reusable position offset sets
        private NativeHashSet<int3> _playerOffsets; 
        private NativeHashSet<int3> _belowOffsets;  
        private NativeHashSet<int3> _levelOffsets;  
        private NativeHashSet<int3> _jumpXOffsets;   
        private NativeHashSet<int3> _jumpZOffsets;  
        private NativeHashSet<int3> _surroundingOffsets;
        private NativeHashSet<int3> _walkable;
        
        // Current planned block path
        private NativeList<float3> _path;
        
        // Angles that a simulated player can look
        private static float LEFT = -90 * Mathf.Deg2Rad;
        private static float RIGHT = 90 * Mathf.Deg2Rad;
        private static float FORWARD = 0;
        private static float BACK = -180 * Mathf.Deg2Rad;
        
        // Area entity and location containing the player
        private Entity _containingArea;
        private int3 _containingAreaLoc;
        
        
        public void OnCreate(ref SystemState state)
        {
            if (ApplicationConfig.EmulationType != EmulationType.Simulation)
                state.Enabled = false;
            

            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(true);
            _terrainNeighborLookup = state.GetComponentLookup<TerrainNeighbors>(true);
            
            // The blocks a player fills (height 2, width and depth 1)
            _playerOffsets = new NativeHashSet<int3>(2, Allocator.Persistent);
            _playerOffsets.Add(new int3(0,0,0));
            _playerOffsets.Add(new int3(0,-1,0));
            
            // The block directly underneath a player, who is of height 2
            _belowOffsets= new NativeHashSet<int3>(1, Allocator.Persistent);
            _belowOffsets.Add(new int3(0,-2,0));
            
            // Used to check if we are falling onto a block 
            _levelOffsets = new NativeHashSet<int3>(3, Allocator.Persistent);
            _levelOffsets.Add(new int3(0,-2,0));
            _levelOffsets.Add(new int3(0,-3,0));
            _levelOffsets.Add(new int3(0,-4,0));
            
            // Used to check if a we are jumping onto a block
            _jumpXOffsets = new NativeHashSet<int3>(2, Allocator.Persistent);
            _jumpXOffsets.Add(new int3(1,-1,0));  // right
            _jumpXOffsets.Add(new int3(-1,-1,0)); // left
            _jumpZOffsets = new NativeHashSet<int3>(2, Allocator.Persistent);
            _jumpZOffsets.Add(new int3(0,-1,1));  // forward
            _jumpZOffsets.Add(new int3(0,-1,-1)); // back
            
            // Set of offsets for neighboring blocks, just 4 cardinal direction for now (e.g. no horizontal diagonals)
            _surroundingOffsets = new NativeHashSet<int3>(12, Allocator.Persistent);
            // middle, y + 0
            _surroundingOffsets.Add(new int3(-1,0,0));
            _surroundingOffsets.Add(new int3(0,0,-1));
            _surroundingOffsets.Add(new int3(0,0,1));
            _surroundingOffsets.Add(new int3(1,0,0));
            
            // bottom, y - 1
            _surroundingOffsets.Add(new int3(0,-1,-1));
            _surroundingOffsets.Add(new int3(0,-1,0));
            _surroundingOffsets.Add(new int3(0,-1,1));
            _surroundingOffsets.Add(new int3(1,-1,0));
            
            // top, y + 1
            _surroundingOffsets.Add(new int3(-1,1,0));
            _surroundingOffsets.Add(new int3(0,1,-1));
            _surroundingOffsets.Add(new int3(0,1,1));
            _surroundingOffsets.Add(new int3(1,1,0));
            
            
            // Set of neighboring blocks that a player can walk to 
            _walkable = new NativeHashSet<int3>(12, Allocator.Persistent);
            
            // Path of nodes a simulated player will follow
            _path = new NativeList<float3>(32, Allocator.Persistent);

            _containingArea = Entity.Null;
            _containingAreaLoc = int3.zero;

        }

        public void OnDestroy(ref SystemState state)
        {
            _path.Dispose();
            _walkable.Dispose();
            _surroundingOffsets.Dispose();
            _jumpXOffsets.Dispose();
            _jumpZOffsets.Dispose();
            _levelOffsets.Dispose();
            _belowOffsets.Dispose();
            _playerOffsets.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            _markerPlayerSimulation.Begin();
            _terrainBlockLookup.Update(ref state);
            _terrainNeighborLookup.Update(ref state);
            
            foreach (var (player, input) in SystemAPI.Query<PlayerAspect, RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
            {
                // Wait for the movement system to run at least once to set the containing area
                if (player.ContainingArea.Area == Entity.Null)
                   break;
                // Get players current position (both continuous and discrete)
                float3 pos = player.Transform.ValueRO.Position;
                int3 blockPos = NoiseUtilities.FastFloor(pos);
                _containingArea = player.ContainingArea.Area;
                _containingAreaLoc = player.ContainingArea.AreaLocation;
                
                // Check if we have arrived at the next position in the path
                if (!_path.IsEmpty && BlockCloseEnough(pos, _path[^1]))
                {
                    float3 nb = new float3(blockPos);
                    TerrainUtilities.DebugDrawTerrainBlock(in nb, Color.red, 6.0f);
                    _path.Length -= 1;
                    if (_path.IsEmpty)
                    {
                        Debug.Log("Target reached!");
                        _path.Clear(); // setup for reuse
                    }
                }
                
                // If there is now no next position, determine new path based on behaviour
                if (_path.IsEmpty)
                {
                    // TODO interchangeable behaviours to determine new path
                    // Set new target location
                    int3 target = new int3(0);
                    int targetOffset = 10;
                    int minTargetOffset = targetOffset - 3;
                    for (; targetOffset > minTargetOffset; targetOffset--)
                        if (FindTargetPos(blockPos + new int3(0, 0, targetOffset), out target))
                            break;

                    if (targetOffset <= minTargetOffset)
                    {
                        Debug.Log("Could not find a valid target location! Disabling player simulation");
                        state.Enabled = false;
                        return;
                    }

                    Debug.Log($"Player simulation searching for path from {blockPos} to {target}");
                    // Set new path using AStar
                    if (AStar(blockPos, target))
                    {
                        var sb = new StringBuilder($"Player simulation found path: ");
                        foreach (float3 node in _path)
                        {
                            int3 n = NoiseUtilities.FastFloor(node);
                            float3 fn = new float3(n);
                            TerrainUtilities.DebugDrawTerrainBlock(in fn, Color.yellow, 6.0f);
                            sb.Append($"[{n.x},{n.y},{n.z}]<-");
                        }
                        Debug.Log(sb.ToString());
                    }
                    else
                    {
                        // Failed to find a path
                        Debug.LogError($"Player simulation could not find a path from {blockPos} to {target}, will choose new target");
                        continue;
                    }
                }
                
                float3 nextBlock = _path[^1];
                int3 nextBlockInt = NoiseUtilities.FastFloor(nextBlock);
                
                // Check if we are off-path
                if (Distance(blockPos, nextBlockInt) > 2)
                {
                    Debug.Log($"Player simulation path has strayed, searching for a correction!");
                    // Add steps to get back on the original path
                    if (AStar( blockPos, nextBlockInt))
                    {
                        Debug.Log($"Player simulation found a correction path from {blockPos} to {nextBlockInt}!");
                        nextBlock = _path[^1];
                        nextBlockInt = NoiseUtilities.FastFloor(nextBlock);
                    }
                    else
                    {
                        // Failed to find a path
                        Debug.LogError($"Player simulation could not find a correction path from {blockPos} to {nextBlockInt}, will choose new target");
                        _path.Clear();
                        continue;
                    }
                }
                    
                // Determine inputs necessary to work towards next block
                bool forward = pos.z < nextBlock.z && !PosCloseEnough(pos.z, nextBlock.z);
                bool back = pos.z > nextBlock.z && !PosCloseEnough(pos.z, nextBlock.z);
                bool left = pos.x > nextBlock.x && !PosCloseEnough(pos.x, nextBlock.x);
                bool right = pos.x < nextBlock.x && !PosCloseEnough(pos.x, nextBlock.x);
                bool up = blockPos.y < nextBlockInt.y;
                //bool down = blockPos.y > nextBlockInt.y;
                
                input.ValueRW.Movement.y = 1;
                
                if (back)
                    input.ValueRW.Yaw = BACK;
                else if (forward)
                    input.ValueRW.Yaw = FORWARD;
                else if(left)
                    input.ValueRW.Yaw = LEFT;
                else if(right)
                    input.ValueRW.Yaw = RIGHT;
                if (up)
                    input.ValueRW.Jump.Set();
                else
                    input.ValueRW.Jump.Count = 0;
            }
            
            _markerPlayerSimulation.End();
        }

        private bool FindTargetPos( int3 start, out int3 target)
        {
            var currPos = start;
            var found = false;
            for (var currY = start.y+3; currY > start.y - 5; currY--)
            {
                currPos = new int3(currPos.x, currY, currPos.z);
                if (!IsSolidBlock(currPos, _belowOffsets) ||
                    IsSolidBlock(currPos, _playerOffsets, true)) continue;
                found = true;
                break;
            }
            target = currPos;
            
            return found;
        }
        
        /// <summary>
        /// Find a path from start to end, given a terrain area to start searching from.
        /// </summary>
        /// <param name="start">Starting block</param>
        /// <param name="end">Target block</param>
        /// <returns>True if a path could be found. The resulting path is stored in _path.</returns>
        private bool AStar(int3 start, int3 end)
        {
            NativeHashSet<int3> visited = new NativeHashSet<int3>(32, Allocator.Temp);
            
            NativeHashMap<int3, int3> previousNode = new NativeHashMap<int3, int3>(32, Allocator.Temp);
            
            SimplePriorityQueue<int3, int> toVisit = new SimplePriorityQueue<int3, int>();
            int baseCost = DistanceSquared(start, end);
            toVisit.Enqueue(start, baseCost);
            
            
            while (toVisit.Count > 0)
            {
                int3 current = toVisit.Dequeue(out int priority);
                visited.Add(current);

                double distance = Distance(current, end);
                
                // Early out if search space has become too big
                if (distance > 20 )
                {
                    Debug.LogWarning("AStar overflowed!");
                    break;
                }

                if (distance == 0 /*&& CanWalk(containingArea, containingAreaLoc, current, end)*/)
                {
                    // arrived at end, build path tracing backwards
                    float3 centerOffset = new float3(0.5f, 0, 0.5f);
                    //_path.Add(end + centerOffset );
                    int3 next = current;
                    while (previousNode.ContainsKey(next))
                    {
                        _path.Add(next + centerOffset );
                        /*if (DistanceSquared(next, previousNode[next]) < 1)
                        {
                            Debug.LogWarning($"Path broken at {next}!");
                            _path.Clear();
                            return false;
                        }*/
                        next = previousNode[next];
                    }
                    _path.Add(start + centerOffset );
                    
                    return true;
                }

                
                FindWalkable(current);
                foreach (int3 neighbor in _walkable)
                {
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }

                    var nCost = priority + 1 + DistanceSquared(neighbor, end);
                    if (!toVisit.Contains(neighbor, out _))
                    {
                        toVisit.Enqueue(neighbor, nCost);
                        previousNode[neighbor] = current;
                    } else if (toVisit.Contains(neighbor, out var nPriority) && nPriority > nCost)
                    {
                        toVisit.UpdatePriority(neighbor, nCost);
                        previousNode[neighbor] = current;
                    }
                }
            }
            // Can't reach the target
            return false;
        }
        
        /// <summary>
        /// Check if two floats are very close, e.g. within 0.05f
        /// </summary>
        /// <param name="a">A float value</param>
        /// <param name="b">Another float value</param>
        /// <returns>True a is close enough to b</returns>
        private bool PosCloseEnough(float a, float b)
        {
            float e = 0.05f;
            return Mathf.Abs(a - b) <= e;
        }
        
        /// <summary>
        /// Check if two position are very close on the x and z axis, and within the same y value.
        /// </summary>
        /// <param name="a">A position</param>
        /// <param name="b">Another position</param>
        /// <returns>True a is close enough to b</returns>
        private bool BlockCloseEnough(float3 a, float3 b)
        {
            return PosCloseEnough(a.x, b.x) && PosCloseEnough(a.z, b.z) && NoiseUtilities.FastFloor(a.y) == NoiseUtilities.FastFloor(b.y);
        }
        
        /// <summary>
        /// Calculate the distance from a to b
        /// </summary>
        /// <param name="a">A discrete position</param>
        /// <param name="b">Another discrete position</param>
        /// <returns>Absolute distance from a to b</returns>
        private double Distance(int3 a, int3 b) {
            return Mathf.Sqrt(DistanceSquared(a, b));
        }
        
        /// <summary>
        /// Calculate the squared distance from a to b
        /// </summary>
        /// <param name="a">A discrete position</param>
        /// <param name="b">Another discrete position</param>
        /// <returns>Squared absolute distance from a to b</returns>
        private int DistanceSquared(int3 a, int3 b) {
            int dx = a.x - b.x;
            int dy = a.y - b.y;
            int dz = a.z - b.z;

            return dx * dx + dy * dy + dz * dz;
        }
        
        /// <summary>
        /// Find the neighboring block positions around start that can be walked to
        /// </summary>
        /// <param name="start">Starting block</param>
        /// <returns></returns>
        /// <remarks>Walkable neighboring blocks stored in _walkable</remarks>
        private void FindWalkable(int3 start)
        {
            _walkable.Clear();
            foreach (var offset in _surroundingOffsets) {
                var target = start + offset;
                if (CanWalk(start, target))
                {
                    _walkable.Add(target);
                }
            }
        }   

        /// <summary>
        /// Determine if a player can walk from locA to locB
        /// </summary>
        /// <param name="locA">Starting block position</param>
        /// <param name="locB">Starting block position</param>
        /// <returns>True if a player can walk from locA to locB</returns>
        /// <remarks>locA and locB must be within 1 block</remarks>>
        [BurstCompile]
        private bool CanWalk(int3 locA, int3 locB)
        {
            int origX = locA.x, origY = locA.y, origZ = locA.z;
            int destX = locB.x, destY = locB.y, destZ = locB.z;
            
            // Destination and origin must be 1 apart at most
            if (Math.Abs(origX - destX) > 1 || Math.Abs(origY - destY) > 1 || Math.Abs(origZ - destZ) > 1)
            {
                return false;
            }
            
            // Both start and end must be traversable (ie the player can fit)
            if (IsSolidBlock(locA, _playerOffsets, true) ||
                IsSolidBlock( locB, _playerOffsets, true))
            {
                return false;
            }

            // Only one coord at a time
            bool movingX = origX != destX;
            bool movingY = origY != destY;
            bool movingZ = origZ != destZ;

            // Only allow single horizontal axis movement for now
            if (!((movingX && !movingZ)
                || (!movingX && movingY && !movingZ) // allow just falling
                || (!movingX && movingZ)))
            {
                return false;
            }
            
            // If we're staying level
            if (destY == origY) {
                // Origin or dest must have a block
                bool origUnder = IsSolidBlock(locA, _belowOffsets);
                //bool destUnder = IsSolidBlock(containingArea, containingAreaLoc, locB, _belowOffsets);

                if (!origUnder /*&& !destUnder*/)
                {
                    return false;
                }

                // Destination must have a solid block at least 3 blocks under
                if (!IsSolidBlock(locB, _levelOffsets, true))
                {
                    return false;
                }
            }

            // If we're jumping
            if (destY > origY) {
                // There must be a block below the origin
                if (!IsSolidBlock( locA,  _belowOffsets))
                {
                    return false;
                }

               
                if (movingX)
                {
                    if (!IsSolidBlock(locA, _jumpXOffsets, trueIfAny: true))
                    {
                        return false;
                    }
                }

                if (movingZ)
                {
                    if (!IsSolidBlock(locA, _jumpZOffsets, trueIfAny: true))
                    {
                        return false;
                    }
                }
                
            }
            
            // If we're just falling there is no further validity to check
            
            return true;
        }
        
        /// <summary>
        /// Determine if blocks at pos + offsets are solid (e.g. not <see cref="BlockType.Air"/>)
        /// </summary>
        /// <param name="pos">Origin position</param>
        /// <param name="offsets">Set of offsets to check around pos</param>
        /// <param name="trueIfAny">If this function should return true if any are solid, or if all are solid</param>
        /// <param name="debug">Whether to print additional debugging information</param>
        /// <returns>True if all blocks are solid OR any block is solid and trueIfAny is true</returns>
        [BurstCompile]
        private bool IsSolidBlock(int3 pos, NativeHashSet<int3> offsets, bool trueIfAny = false, bool debug = false)
        {
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.basePos = pos;
            BSI.areaEntity = _containingArea;
            BSI.terrainAreaPos = _containingAreaLoc;

            bool foundAir = false;
            foreach (int3 offset in offsets)
            {
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                BSI.offset = offset;

                if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO,
                        ref _terrainNeighborLookup, ref _terrainBlockLookup, debug))
                {
                    if (BSO.blockType != BlockType.Air)
                    {
                        if (trueIfAny)
                            return true;
                    }
                    else
                    {
                        foundAir = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"Player sim solid block check on location that is not loaded: {pos} + {offset} in area {BSO.containingAreaPos}");
                }
            }

            return !foundAir;

        }
    }
    
}