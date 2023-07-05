using Opencraft.Player.Emulated;
using Unity.Logging;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace Opencraft
{
    // Static global class holding the parsed command line arguments
    public static class CmdArgs
    {
#if UNITY_SERVER
        private const StreamingRole DefaultStreamingRole = StreamingRole.Disabled;
#elif UNITY_EDITOR
        // Fetch the current play mode from the editor preferences. Has to be done at runtime or the value requires a recompilation
        public static ClientServerBootstrap.PlayType getPlayType()
        {
            string s_PrefsKeyPrefix = $"MultiplayerPlayMode_{Application.productName}_";
            string s_PlayModeTypeKey = s_PrefsKeyPrefix + "PlayMode_Type";
            return (ClientServerBootstrap.PlayType) EditorPrefs.GetInt(s_PlayModeTypeKey, (int) ClientServerBootstrap.PlayType.ClientAndServer);
        }

        private static StreamingRole DefaultStreamingRole = StreamingRole.Disabled;
#else
        // All clients are assumed hosts, a command line argument is necessary to start as a streaming Guest
        private const StreamingRole DefaultStreamingRole = StreamingRole.Host;
    
#endif
#if UNITY_EDITOR
        public static StreamingRole ClientStreamingRole
        {
            get
            {
                if (DefaultStreamingRole != StreamingRole.Disabled)
                {
                    return DefaultStreamingRole;
                }
                return getPlayType() == ClientServerBootstrap.PlayType.Server ? StreamingRole.Disabled : StreamingRole.Host;
            }
            set => DefaultStreamingRole = value;
        }
#else
        public static StreamingRole ClientStreamingRole { get; set; } = DefaultStreamingRole;
#endif
        public static bool DebugEnabled =  false;
        public static bool EmulationEnabled =  false;
        public static EmulationType emulationType = EmulationType.None;
        public static int seed = -1;


        // Client's streaming role for Multiplay
        public enum StreamingRole
        {
            Disabled,
            Host,
            Guest
        }
    }
}