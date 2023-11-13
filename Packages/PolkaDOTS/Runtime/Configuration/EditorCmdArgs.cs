using UnityEngine;

namespace PolkaDOTS.Configuration
{
    /// <summary>
    /// Allows running using command line arguments in-editor. Read by <see cref="CmdArgsReader"/>
    /// </summary>
    public class EditorCmdArgs : MonoBehaviour
    {
        public string editorArgs;
        public bool useDeploymentConfig = false;
        [Multiline(10)] public string deploymentConfig;
    }
}