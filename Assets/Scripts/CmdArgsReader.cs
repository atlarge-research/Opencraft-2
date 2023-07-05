using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Opencraft.Player.Emulated;
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
#endif

namespace Opencraft
{
    public class CmdArgsReader : MonoBehaviour
    {
        public string editorArgs;

        void OnEnable()
        {
            Debug.Log("Reading cmdargs");
            var args = GetCommandlineArgs();
            // Multiplay streaming type, only used on builds with a client, e.g. UNITY_SERVER undefined
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
            // Debug flag, used for rendering outlines 
            if (args.TryGetValue("-debug", out string _))
            {
                CmdArgs.DebugEnabled = true;
            }
            
            // Emulation
            if (args.TryGetValue("-emulation", out string emulationType))
            {
                CmdArgs.EmulationEnabled = true;
                switch (emulationType)
                {
                    case "playback":
                        CmdArgs.emulationType = EmulationType.InputPlayback;
                        break;
                    case "record":
                        CmdArgs.emulationType = EmulationType.RecordInput;
                        break;
                    case "behaviour":
                    default:
                        CmdArgs.emulationType = EmulationType.BehaviourProfile;
                        break;
                }
            }
            
            // Seed
            if (args.TryGetValue("-seed", out string seed))
            {
                MD5 md5Hasher = MD5.Create();
                var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(seed));
                var ivalue = BitConverter.ToInt32(hashed, 0);
                CmdArgs.seed = ivalue;
            }
            
            
        }

        
        
        private Dictionary<string, string> GetCommandlineArgs()
        {
            Dictionary<string, string> argDictionary = new Dictionary<string, string>();
            string[] args = new[] { "" };
#if UNITY_EDITOR
            // ParrelSync clones can have arguments passed to them in the Clones Manager window
            if (ClonesManager.IsClone())
            {
                // Get the custom arguments for this clone project.  
                args = ClonesManager.GetArgument().Split(' ');
            } else {
                // Otherwise, use arguments in this MonoBehaviour 
                args = editorArgs.Split(' ');
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