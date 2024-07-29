//using Opencraft.Editor;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Structures;
using UnityEngine;

namespace Opencraft.Terrain.Layers
{
    [CreateAssetMenu(fileName = "New Layer", menuName = "OpenCraft/New Layer")]
    public class LayerConfig : ScriptableObject
    {
        [SerializeField]
        [Tooltip("What kind of terrain layer this object creates.")]
        private LayerType layerType = LayerType.Absolute;
        [SerializeField]
        [Tooltip("The name of the created layer.")]
        private string layerName = "New Layer";
        [SerializeField]
        [Tooltip("Lower index layers are applied before higher indices.")]
        private int index = 0;
        [SerializeField]
        [Tooltip("The block this layer will create.")]
        [DrawIf("layerType", new[] { LayerType.Absolute, LayerType.Additive, LayerType.Random, LayerType.Surface, LayerType.Calculated_Layer })]
        private BlockType blockType;

        [SerializeField]
        [Tooltip("Noise parameter.")]
        [DrawIf("layerType", new[] { LayerType.Absolute, LayerType.Additive })]
        private float frequency = 0f;
        [SerializeField]
        [Tooltip("Noise parameter.")]
        [DrawIf("layerType", new[] { LayerType.Absolute, LayerType.Additive })]
        private float exponent = 0f;

        /*[SerializeField]
        [Tooltip("Level to start randomly sampling at. (MinHeight to MinHeight+BaseHeight gets filled)")]
        [DrawIf("layerType", new[]{LayerType.Absolute, LayerType.Additive})]
        private int baseHeight = 0;*/
        [SerializeField]
        [Tooltip("Lowest possible column floor.")]
        [DrawIf("layerType", new[] { LayerType.Absolute, LayerType.Additive })]
        private int minHeight = 0;
        [SerializeField]
        [Tooltip("Highest possible column ceiling.")]
        [DrawIf("layerType", new[] { LayerType.Absolute, LayerType.Additive })]
        private int maxHeight = 0;

        [SerializeField]
        [Tooltip("Likelihood of spawning.")]
        [DrawIf("layerType", new[] { LayerType.Structure, LayerType.Random })]
        private float chance = 0;
        [SerializeField]
        [Tooltip("Structure to spawn.")]
        [DrawIf("layerType", LayerType.Structure)]
        private StructureType structureType = StructureType.None;


        public LayerType LayerType => layerType;
        public string LayerName => layerName;
        public int Index => index;
        public BlockType BlockType => blockType;
        public float Frequency => frequency;
        public float Exponent => exponent;
        //public int BaseHeight => baseHeight;
        public int MinHeight => minHeight;
        public int MaxHeight => maxHeight;
        public float Chance => chance;
        public StructureType StructureType => structureType;
    }

    public enum LayerType
    {
        Absolute,
        Random,
        Structure,
        Surface,
        Additive,
        Calculated_Layer,
    }
}