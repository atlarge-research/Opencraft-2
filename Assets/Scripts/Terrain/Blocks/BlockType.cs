namespace Opencraft.Terrain.Blocks
{
    public enum BlockType : int
    {
        Air,
        Stone,
        Dirt,
        Tin,
        Gem
    }

    public static class BlockData
    {
        public static readonly int[] BlockToTexture =
        {
            0,
            (1 & 31) << 24,
            (2 & 31) << 24,
            (3 & 31) << 24,
            (4 & 31) << 24
        };
    }
    
}