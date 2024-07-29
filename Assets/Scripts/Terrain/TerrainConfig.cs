﻿namespace Opencraft
{
    public static class Env
    {
        // 2^AREA_POW = blocks per area side
        public const int AREA_POW = 4;

        public const int INITIAL_COLUMNS_X = 3;
        public const int INITIAL_COLUMNS_Z = 3;
        public const int PLAYER_VIEW_RANGE = 2;
        public const int TERRAIN_SPAWN_RANGE = 3;
        public const int MAX_COL_PER_TICK = 10;

        #region DO NOT CHANGE THESE!

        public const float CAMERA_Y_OFFSET = 0.5f;

        public const int AREA_PADDING = 1;
        public const int AREA_PADDING_2 = AREA_PADDING * 2;

        // Visible chunk size
        public const int AREA_SIZE = 1 << AREA_POW; // 16
        public const int AREA_SIZE_1 = AREA_SIZE - 1; // 15
        public const int AREA_SIZE_POW_2 = AREA_SIZE * AREA_SIZE; // 256 
        public const int AREA_SIZE_POW_3 = AREA_SIZE * AREA_SIZE_POW_2; // 4096
        public const int AREA_SIZE_SHIFTED = (AREA_SIZE & 255) << 8;

        // Internal chunk size (visible size + padding)
        public const int AREA_SIZE_PLUS_PADDING = AREA_SIZE + AREA_PADDING;
        public const int AREA_SIZE_WITH_PADDING = AREA_SIZE + AREA_PADDING * 2;
        public const int AREA_SIZE_WITH_PADDING_POW_2 = AREA_SIZE_WITH_PADDING * AREA_SIZE_WITH_PADDING;
        public const int AREA_SIZE_WITH_PADDING_POW_3 = AREA_SIZE_WITH_PADDING * AREA_SIZE_WITH_PADDING_POW_2;

        #endregion
    }


}