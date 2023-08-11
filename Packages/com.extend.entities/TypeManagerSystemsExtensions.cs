using System.Linq;
using Unity.Collections;

namespace Unity.Entities
{
    /// <summary>
    /// Extends <see cref="Unity.Entities.TypeManager"/> with Netcode for entities-aware system search
    /// </summary>
    static public unsafe partial class TypeManager
    {
        
        public static NativeList<SystemTypeIndex> GetUnitySystemsTypeIndices(
            WorldSystemFilterFlags filterFlags = WorldSystemFilterFlags.All,
            WorldSystemFilterFlags requiredFlags = 0)
        {
            // Expand default to proper types
            if ((filterFlags & WorldSystemFilterFlags.Default) != 0)
            {
                filterFlags &= ~WorldSystemFilterFlags.Default;
                filterFlags |= WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Presentation;
            }

            Assertions.Assert.IsTrue(s_Initialized, "The TypeManager must be initialized before the TypeManager can be used.");
            // By default no flags are required
            requiredFlags &= ~WorldSystemFilterFlags.Default;
            LookupFlags lookupFlags = new LookupFlags() { OptionalFlags = filterFlags, RequiredFlags = requiredFlags };

            
            if (s_SystemFilterTypeMap.TryGetValue(lookupFlags, out var systemTypeIndices))
                return systemTypeIndices;

            // Use a temp list since we don't know how many systems will be filtered out yet
            var tempFilteredSystemTypes = new NativeList<SystemTypeIndex>(s_SystemTypes.Count-1, Allocator.Temp);

            // Skip index 0 since that is always null
            for (int i = 1; i < s_SystemTypes.Count;++i)
            {
                var systemType = s_SystemTypes[i];
                var systemName = GetSystemName(systemType);
                // Include no user systems
                if (!systemName.Contains((FixedString64Bytes)"Unity") && !systemName.Contains((FixedString64Bytes)"Generated") )
                {
                    //UnityEngine.Debug.Log($"Excluding system {systemName}");
                    continue;
                }
                if (FilterSystemType(i, lookupFlags))
                    tempFilteredSystemTypes.Add(GetSystemTypeIndex(systemType));
            }

            SortSystemTypesInCreationOrder(tempFilteredSystemTypes);

            var persistentSystemList = new NativeList<SystemTypeIndex>(tempFilteredSystemTypes.Length, Allocator.Persistent);
            persistentSystemList.CopyFrom(tempFilteredSystemTypes);

            s_SystemFilterTypeMap[lookupFlags] = persistentSystemList;
            return persistentSystemList;
        }
    }
}