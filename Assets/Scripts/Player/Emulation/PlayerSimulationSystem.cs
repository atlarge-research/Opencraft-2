using System;
using System.Text;
using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using Opencraft.ThirdParty;
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
        private NativeHashSet<int3> _aboveOffsets;  
        private NativeHashSet<int3> _levelOffsets;  
        private NativeHashSet<int3> _jumpXOffsets;   
        private NativeHashSet<int3> _jumpZOffsets;  
        private NativeHashSet<int3> _surroundingOffsets;
        private NativeHashSet<int3> _walkable;
        
        // Current planned block path
        private NativeList<float3> _path;
        private CardinalDirection _previousDirection;
        
        // Angles that a simulated player can look
        private static float LEFT = -90 * Mathf.Deg2Rad;
        private static float RIGHT = 90 * Mathf.Deg2Rad;
        private static float FORWARD = 0;
        private static float BACK = -180 * Mathf.Deg2Rad;
        
        // Area entity and location containing the player
        private Entity _containingArea;
        private int3 _containingAreaLoc;

        private CardinalDirection _cardinalDirection;
        private int2 _boundedArea;
        private int _simulationSeed;
        
        
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
            
            // The block directly above a player
            _aboveOffsets= new NativeHashSet<int3>(1, Allocator.Persistent);
            _aboveOffsets.Add(new int3(0,1,0));
            
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
            
            // Cardinal direction determined by user ID to evenly distribute
            _cardinalDirection = (CardinalDirection)(ApplicationConfig.UserID % 4);
            // Bounded area set to a fixed size, todo make this a parameter
            _boundedArea = new int2(32, 32);
            // Pseudorandom seed starts as userID
            _simulationSeed = ApplicationConfig.UserID;

            _markerPlayerSimulation = new ProfilerMarker("PlayerSimulationMarker");

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
            _aboveOffsets.Dispose();
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
                    bool foundPath = false;
                    for (int attempt = 0; attempt < 9; attempt++)
                    {
                        if (!FindTargetPos(blockPos, attempt, out int3 target))
                        {
                            continue;
                        }
                        Debug.Log($"Player simulation searching for path from {blockPos} to {target}");
                        if (AStar(blockPos, target))
                        {
                            /*
                            var sb = new StringBuilder($"Player simulation found path: ");
                            foreach (float3 node in _path)
                            {
                                int3 n = NoiseUtilities.FastFloor(node);
                                float3 fn = new float3(n);
                                TerrainUtilities.DebugDrawTerrainBlock(in fn, Color.yellow, 6.0f);
                                sb.Append($"[{n.x},{n.y},{n.z}]<-");
                            }
                            Debug.Log(sb.ToString());*/
                            foundPath = true;
                            break;
                        }
                        
                        Debug.Log($"Player simulation could not find a path from {blockPos} to {target}, will choose new target");
                        
                    }

                    if (!foundPath)
                    {
                        Debug.LogError("Player simulation could not find a path in 9 attempts! Disabling player simulation.");
                        state.Enabled = false;
                        _markerPlayerSimulation.End();
                        return;
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
                var zDiff = nextBlock.z - pos.z;
                var xDiff = nextBlock.x - pos.x;
                const float epsilon = 0.1f;

                CardinalDirection primaryDirection = CardinalDirection.None;
                CardinalDirection secondaryDirection = CardinalDirection.None;
                
                // Primary direction is largest difference amount, secondary is smaller difference amount
                if (math.abs(zDiff) >= math.abs(xDiff))
                {
                    // Forward or back primary
                    if (zDiff >= epsilon)
                    {
                        primaryDirection = CardinalDirection.North;
                    }
                    else if (zDiff <= -epsilon)
                    {
                        primaryDirection = CardinalDirection.South;
                    }
                    
                    // Left or right secondary
                    if (xDiff >= epsilon)
                    {
                        secondaryDirection = CardinalDirection.East;
                    }
                    else if (xDiff <= -epsilon)
                    {
                        secondaryDirection = CardinalDirection.West;
                    }
                    
                }
                else
                {
                    // Left or right primary
                    if (xDiff >= epsilon)
                    {
                        primaryDirection = CardinalDirection.East;
                    }
                    else if (xDiff <= -epsilon)
                    {
                        primaryDirection = CardinalDirection.West;
                    }
                    
                    // Forward or back secondary
                    if (zDiff >= epsilon)
                    {
                        secondaryDirection = CardinalDirection.North;
                    }
                    else if (zDiff <= -epsilon)
                    {
                        secondaryDirection = CardinalDirection.South;
                    }
                }
                
                CardinalDirection currentDirection = primaryDirection;
                input.ValueRW.Movement.y = 1;
                input.ValueRW.Movement.x = 0;
                // If we have no primary direction but do have a secondary direction, then use previous direction
                // without moving forward and calculate secondary directions
                if (primaryDirection == CardinalDirection.None)
                {
                    input.ValueRW.Movement.y = 0;
                    currentDirection = _previousDirection;
                }
                else
                {
                    // Otherwise we do have a primary direction, keep track of it
                    _previousDirection = currentDirection;
                }
                
                switch (currentDirection)
                {
                    case CardinalDirection.North:
                        input.ValueRW.Yaw = FORWARD;
                        if(secondaryDirection == CardinalDirection.East)
                            input.ValueRW.Movement.x = 1;
                        if(secondaryDirection == CardinalDirection.West)
                            input.ValueRW.Movement.x = -1;
                        break;
                    case CardinalDirection.East:
                        input.ValueRW.Yaw = RIGHT;
                        if(secondaryDirection == CardinalDirection.South)
                            input.ValueRW.Movement.x = 1;
                        if(secondaryDirection == CardinalDirection.North)
                            input.ValueRW.Movement.x = -1;
                        break;
                    case CardinalDirection.South:
                        input.ValueRW.Yaw = BACK;
                        if(secondaryDirection == CardinalDirection.West)
                            input.ValueRW.Movement.x = 1;
                        if(secondaryDirection == CardinalDirection.East)
                            input.ValueRW.Movement.x = -1;
                        break;
                    case CardinalDirection.West:
                        input.ValueRW.Yaw = LEFT;
                        if(secondaryDirection == CardinalDirection.North)
                            input.ValueRW.Movement.x = 1;
                        if(secondaryDirection == CardinalDirection.South)
                            input.ValueRW.Movement.x = -1;
                        break;
                    case CardinalDirection.None:
                    default:
                        // Shouldn't occur
                        break;
                }
                
                
                // Check for jump
                if (blockPos.y < nextBlockInt.y)
                {
                    input.ValueRW.Jump.Set();
                }
                else
                {
                    input.ValueRW.Jump.Count = 0;
                }
            }
            
            _markerPlayerSimulation.End();
        }

        
        /// <summary>
        /// Determines the next target position for a simulated player
        /// </summary>
        /// <param name="playerPos">The block the player is currently standing on</param>
        /// <param name="attempt">What retry number we are on</param>
        /// <param name="target">A target position</param>
        /// <returns>True if a target position was found</returns>
        private bool FindTargetPos(int3 playerPos, int attempt, out int3 target)
        {
            switch (GameConfig.PlayerSimulationBehaviour.Value)
            {
                case SimulationBehaviour.FixedDirection:
                    return FindTargetPosFixedDirection(playerPos, attempt, out target);
                    break;
                case SimulationBehaviour.BoundedRandom:
                default:
                    return FindTargetPosBoundedRandom(playerPos, attempt, out target);
                    break;
            }
        }

        /// <summary>
        /// Searches in a cardinal direction for an available target position
        /// </summary>
        /// <param name="playerPos">The block the player is currently standing on</param>
        /// <param name="attempt">What retry number we are on</param>
        /// <param name="target">A target position</param>
        /// <returns>True if a target position was found</returns>
        private bool FindTargetPosFixedDirection(int3 playerPos, int attempt, out int3 target)
        {
            // Secondary offset allows simulated players to go around obstacles 
            var secondaryOffset = new[] { 0, -2, 2, 0, -2, 2, 0, -2, 2}[attempt];
            var primaryOffset = new[] { 10, 10, 10, 6, 6, 6, 2, 2, 2 }[attempt];
           
            int3 offset;
            switch (_cardinalDirection)
            {
                case CardinalDirection.North:
                    offset = new int3(secondaryOffset, 0, primaryOffset);
                    break;
                case CardinalDirection.East:
                    offset = new int3(primaryOffset, 0, secondaryOffset);
                    break;
                case CardinalDirection.South:
                    offset = new int3(secondaryOffset, 0, -primaryOffset);
                    break;
                case CardinalDirection.West:
                default:
                    offset = new int3(-primaryOffset, 0, secondaryOffset);
                    break;
            }

            if (FindAvailablePos(playerPos + offset, out target))
                return true;


            target = int3.zero;
            return false;
        }

        /// <summary>
        /// Searches within a bounded area for an available target position
        /// </summary>
        /// <param name="playerPos">The block the player is currently standing on</param>
        /// <param name="attempt">What retry number we are on</param>
        /// <param name="target">A target position</param>
        /// <returns>True if a target position was found</returns>
        private bool FindTargetPosBoundedRandom(int3 playerPos, int attempt, out int3 target)
        {
            int startSeed = _simulationSeed + attempt;
            // Find local bounds within a wider bounding area
            int maxOffset = 10;
            int posXBound = math.min(playerPos.x + maxOffset, _boundedArea.x);
            int negXBound = math.max(playerPos.x - maxOffset, -_boundedArea.x);
            int posZBound = math.min(playerPos.z + maxOffset, _boundedArea.y);
            int negZBound = math.max(playerPos.z - maxOffset, -_boundedArea.y);
            
            // Get random values based on seed and current position
            int blockHash = TerrainUtilities.BlockLocationHash(playerPos.x, playerPos.y, playerPos.z);
            float randVal1 = NoiseUtilities.RandomPrecise(blockHash, (byte)startSeed);
            float randVal2 = NoiseUtilities.RandomPrecise(blockHash, (byte)(startSeed+128));
            //float randVal1 = (FastNoise.GetNoise(playerPos.x, playerPos.y, playerPos.z, startSeed) + 1.0f) / 2.0f;
            //float randVal2 = (FastNoise.GetNoise(playerPos.x, playerPos.y, playerPos.z, -(startSeed+1)) + 1.0f) / 2.0f;
            
            // Calculate starting target position
            int rangeX = (posXBound - negXBound) + 1;
            int rangeZ = (posZBound - negZBound) + 1;
            int xVal = negXBound + NoiseUtilities.FastFloor(randVal1 * rangeX);
            int zVal = negZBound + NoiseUtilities.FastFloor(randVal2 * rangeZ);
            int3 startingTarget = new int3(xVal, playerPos.y, zVal);
            
            Debug.Log($"+x {posXBound} -x {negXBound} +z {posZBound} -z {negZBound} rv1 {randVal1} rv2 {randVal2}");
            
            if (FindAvailablePos(startingTarget, out target))
                return true;
            
            target = int3.zero;
            return false;
        }

        /// <summary>
        /// In a given x and z column, find a y value that a player can stand in
        /// </summary>
        /// <param name="start">Position to search from (on x and z)</param>
        /// <param name="target">Resulting position a player can stand in</param>
        /// <returns>True if there is an available position.</returns>
        private bool FindAvailablePos( int3 start, out int3 target)
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

                if (distance == 0)
                {
                    // Arrived at end, build path tracing backwards
                    float3 centerOffset = new float3(0.5f, 0, 0.5f);
                    int3 next = current;
                    var sb = new StringBuilder($"PATH: ");
                    while (previousNode.ContainsKey(next))
                    {
                        float3 floatPos = next;
                        
                        _path.Add(floatPos + centerOffset);
                        
                        TerrainUtilities.DebugDrawTerrainBlock(in floatPos, Color.yellow, 6.0f);
                        sb.Append($"[{next.x},{next.y},{next.z}]<-");
                        
                        next = previousNode[next];
                    }
                    //_path.Add(start + centerOffset );
                    Debug.Log(sb.ToString());
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
        /// <returns>True if a is close enough to b</returns>
        private bool PosCloseEnough(float a, float b)
        {
            const float epsilon = 0.1f;
            return Mathf.Abs(a - b) <= epsilon;
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
                if (!IsSolidBlock(locA,  _belowOffsets))
                {
                    return false;
                }
                
                // There cannot be a block above the origin
                if (IsSolidBlock(locA,  _aboveOffsets, trueIfAny: true))
                {
                    return false;
                }

                // Check for surrounding blocks around start, prevents unnecessary jumps
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
            
             // If we're falling
             if (destY < origY)
             {
                 // There cannot be a block above the destination
                 if (IsSolidBlock(locB,  _aboveOffsets, trueIfAny: true))
                 {
                     return false;
                 }
             }

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