using System;
using System.Collections.Generic;
using UnityEngine;

namespace Opencraft.Terrain.Layers
{
    [CreateAssetMenu(fileName = "New Layer Collection", menuName = "OpenCraft/Layer Collection", order = -100)]
    public class TerrainGenerationConfiguration : ScriptableObject
    {
        [SerializeField]
        private string configName = "";
        
        [SerializeField]
        private LayerConfig[] layers = null;

        [SerializeField]
        private int areaColumnHeight = 1;

        public string Name => configName;
        
        public LayerConfig[] Layers { get { return layers; } }

        public int AreaColumnHeight
        {
            get { return areaColumnHeight; }
        }

        public void SortLayers()
        {
            Array.Sort(layers, new LayerConfigComparer());
        }
    }
    
    // Comparer for sorting locations by distance from zero
    struct LayerConfigComparer: IComparer<LayerConfig>
    {
        public int Compare(LayerConfig a, LayerConfig b)
        {
            if (a is null && b is not null)
                return -1;
            if (a is not null && b is null)
                return 1;
            if (a is null)
                return 0;
            if (a.Index > b.Index)
                return 1;
            if (a.Index < b.Index )
                return -1;
            return 0;
        }
    }
}