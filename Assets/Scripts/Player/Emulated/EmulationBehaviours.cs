using System;

namespace Opencraft.Player.Emulated
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
        Explore           = 1 << 3, // Movement
        Collect           = 1 << 4, // Block destruction
        Build             = 1 << 5, // Block creation
        PlaybackExplore   = Explore | Playback,
        PlaybackCollect   = Collect | Playback, 
        PlaybackBuild     = Build   | Playback, 
        SimulationExplore = Explore | Simulation,
        SimulationCollect = Collect | Simulation,
        SimulationBuild   = Build   | Simulation,
    }
}