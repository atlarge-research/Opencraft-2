using System;

namespace PolkaDOTS.Emulation
{
    
    [Flags]
    [Serializable]
    public enum EmulationBehaviours :  int
    {
        None              = 0,
        Idle              = 0,
        Playback          = 1,
        Simulation        = 1 << 1,
        Record            = 1 << 2,
    }
}