using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
#endif

namespace Opencraft
{
    public class CmdArgsReader : MonoBehaviour
    {
        void Start()
        {
            var args = GetCommandlineArgs();
            // Streaming type, only used on builds with a client, e.g. UNITY_SERVER undefined
            if (args.TryGetValue("-streaming_type", out string type))
            {
                switch (type)
                {
                    case "host":
                        CmdArgs.ClientStreamingRole = CmdArgs.StreamingRole.Host;
                        break;
                    case "guest":
                        CmdArgs.ClientStreamingRole = CmdArgs.StreamingRole.Guest;
                        break;
                    case "disabled":
                    default:
                        CmdArgs.ClientStreamingRole = CmdArgs.StreamingRole.Disabled;
                        break;
                }
            }
#if UNITY_EDITOR
            Debug.Log($"Client streaming role is {CmdArgs.ClientStreamingRole} {CmdArgs.getPlayType()}");
#endif
            if (args.TryGetValue("-debug", out string _))
            {
                CmdArgs.DebugEnabled = true;
            }
        }

        private Dictionary<string, string> GetCommandlineArgs()
        {
            Dictionary<string, string> argDictionary = new Dictionary<string, string>();
            string[] args = new[] { "" };
#if UNITY_EDITOR
            // ParrelSync clones can have arguments passed to them in editor. The original project cannot.
            // So, use ParrelSync clones to test streamed guests.
            // todo add some sort of cmdline arg facade to original project
            if (ClonesManager.IsClone())
            {
                // Get the custom arguments for this clone project.  
                args = ClonesManager.GetArgument().Split(' ');
            }
#else
        args = System.Environment.GetCommandLineArgs();
#endif
            for (int i = 0; i < args.Length; ++i)
            {
                var arg = args[i].ToLower();
                if (arg.StartsWith("-"))
                {
                    var value = i < args.Length - 1 ? args[i + 1].ToLower() : null;
                    value = (value?.StartsWith("-") ?? false) ? null : value;

                    argDictionary.Add(arg, value);
                }
            }

            return argDictionary;
        }
    }
}