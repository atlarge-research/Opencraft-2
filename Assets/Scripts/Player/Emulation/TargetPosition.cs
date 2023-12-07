using Unity.Mathematics;
using Unity.NetCode;

namespace Opencraft.Player.Emulation
{
    public enum SimulationBehaviour
    {
        BoundedRandom = 0,
        FixedDirection = 1,
    }
    
    public class TargetPosition
    {

        public int3 GetTargetPosition()
        {
            
            return int3.zero;
        }
    }
}