namespace Opencraft.Player.Emulation
{
    public enum SimulationBehaviour
    {
        /// <summary>
        /// Player moves in (pseudo)random ways within an area of a fixed size 
        /// </summary>
        BoundedRandom = 0,
        /// <summary>
        /// Player picks and follows cardinal direction
        /// </summary>
        FixedDirection = 1,
    }

    public enum CardinalDirection
    {
        /// <summary>
        /// Positive z
        /// </summary>
        None = 0,
        /// <summary>
        /// Positive z
        /// </summary>
        North = 1,
        /// <summary>
        /// Positive x
        /// </summary>
        East = 2,
        /// <summary>
        /// Negative z
        /// </summary>
        South = 3,
        /// <summary>
        /// Negative x
        /// </summary>
        West = 4
    }
}