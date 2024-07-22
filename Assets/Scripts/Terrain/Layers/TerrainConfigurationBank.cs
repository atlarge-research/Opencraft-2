using UnityEngine;

namespace Opencraft.Terrain.Layers
{
    [CreateAssetMenu(fileName = "New Layer Collection", menuName = "OpenCraft/Layer Collection Bank", order = -100)]
    public class TerrainGenerationBank : ScriptableObject
    {
        [SerializeField]
        private TerrainGenerationConfiguration[] configs = null;
        
        public TerrainGenerationConfiguration[] Configs => configs;
    }
}