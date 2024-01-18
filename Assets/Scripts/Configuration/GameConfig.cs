using Opencraft.Player.Emulation;
using PolkaDOTS;
using PolkaDOTS.Configuration;

namespace Opencraft
{
    /// <summary>
    /// Static global class holding game configuration parameters. Filled by <see cref="CmdArgsReader"/>
    /// </summary>
    [ArgumentClass]
    public static class GameConfig
    {
        public static readonly CommandLineParser.EnumArgument<SimulationBehaviour> PlayerSimulationBehaviour = new CommandLineParser.EnumArgument<SimulationBehaviour>("-playerSimulationBehaviour", SimulationBehaviour.BoundedRandom);
        public static readonly CommandLineParser.StringArgument TerrainType = new CommandLineParser.StringArgument("-terrainType", "default");
        
    }
}