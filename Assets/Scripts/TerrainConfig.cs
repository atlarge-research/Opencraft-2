using Unity.Mathematics;

namespace Opencraft
{
    public static class Env
    {
        //! Size of chunk's side
        public const int AREA_POW = 4;
        
        //! How many terrain areas from world bottom to world top
        public const int AREA_COLUMN_HEIGHT = 4;
        public const int HALF_AREA_COLUMN_HEIGHT = 4;
        
        #region DO NOT CHANGE THESE!

        public const float CAMERA_Y_OFFSET = 0.5f;
        
        public const int AREA_POW_2 = AREA_POW << 1;
        public const int AREA_MASK = (1 << AREA_POW_2) - 1;
        
        public const int AREA_PADDING = 1;
        public const int AREA_PADDING_2 = AREA_PADDING * 2;

        //! Visible chunk size
        public const int AREA_SIZE = 1 << AREA_POW;
        public const int AREA_SIZE_1 = AREA_SIZE - 1;
        public const int AREA_SIZE_POW_2 = AREA_SIZE * AREA_SIZE;
        public const int AREA_SIZE_POW_3 = AREA_SIZE * AREA_SIZE_POW_2;
        public const int AREA_SIZE_SHIFTED = (AREA_SIZE & 255) << 8;

        //! Internal chunk size (visible size + padding)
        public const int AREA_SIZE_PLUS_PADDING = AREA_SIZE + AREA_PADDING;
        public const int AREA_SIZE_WITH_PADDING = AREA_SIZE + AREA_PADDING * 2;
        public const int AREA_SIZE_WITH_PADDING_POW_2 = AREA_SIZE_WITH_PADDING * AREA_SIZE_WITH_PADDING;
        public const int AREA_SIZE_WITH_PADDING_POW_3 = AREA_SIZE_WITH_PADDING * AREA_SIZE_WITH_PADDING_POW_2;
        
        public const int WORLD_HEIGHT = AREA_COLUMN_HEIGHT * AREA_SIZE;

        #endregion
    }
    
    
}