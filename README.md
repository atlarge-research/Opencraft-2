# Opencraft 2

## Setup
Install *Unity 2022 LTS*, clone this repository using `git clone --recurse-submodules`, and open it in Unity. 
Unity should automatically install required packages. This includes the [ParrelSync](https://github.com/VeriorPies/ParrelSync) package,
which is useful for testing multiplayer functionality in-editor.
I recommend using [Rider](https://www.jetbrains.com/rider/) as your IDE, it has direct integration with Unity.

The game can be started from `Scenes/MainScene`. To run, select PlayMode type from Server, Client, or Server & Client in `Multiplayer > Window: PlayMode Tools`, and set an address,
by default `127.0.0.1:7979`. Then press the play button. Additional configuration can be specified in command line arguments, 
which can be set in-editor on the `Editor Args` field of the `GameBootstrap->Cmd Args Reader` singleton component.

## Multiplay
This project supports a single game client acting as a streamed gaming host for many players. 
This functionality requires a separate signalling service to be running to establish a direct connection between host and guest clients.
It is available in this repo under `./Multiplay_WebApp/` and run using `.\webserver -p <PORT>`.
The port the webserver listens on must be the same as the port configured using the command line argument `-signalingPort <int>`. 

Testing Multiplay is done using ParrelSync. Under `ParrelSync > Clones Manager` select `Add new clone`. 
Once it is initialized, add to its arguments in the clone manager `-multiplayRole Guest` to run it as a Multiplay guest client.
A word of warning, running multiple Unity instances at once requires *considerable* hardware, especially in terms of memory,
and especially alongside a modern IDE like Rider. If your device can't handle it, you may have to build a client executable for testing.


## Debugging and Analysis
Inspecting existing entities, components, systems, and queries in each existing world can be done using
Entities information pages under `Window > Entities`. Information not specific to ECS can be found under `Window > Analysis`,
with the `Profiler` being particularly important for determining performance impact of various changes.
Burst compiled generated code can be viewed in `Jobs > Burst > Open Inspector`, though this is mainly useful for low level optimization work.
Multiplayer functionality and behaviour can be visualized using the `Window > Multiplayer > Window: NetDbg (Browser)` tool,
which automatically gathers state snapshot metrics from Netcode for Entities connections.

If your Unity or your IDE starts giving strange errors, particularly about packages,
it is worth trying `Edit > Preferences > External Tools > Regenerate project files`. As a last resort, `Reimport All`
through the right-click menu in the `Project` window.

## Configuration and Parameters
The Opencraft 2 application can be configured in a variety of ways, with some methods being different in-editor and
when run standalone. In editor, the hierarchy of configuration is `Multiplayer PlayMode Tools`
### Command Line Arguments
| Argument              | Options                                                                 | Default                                                  | Description                                                          |
|-----------------------|-------------------------------------------------------------------------|----------------------------------------------------------|----------------------------------------------------------------------|
| -deploymentJson       | <FilePath>                                                              | null                                                     | Path to a deployment configuration Json file.                        |
| -deploymentID         | <int>                                                                   | 0                                                        | The ID of this node, used for deployment.                            |
| -remoteConfig         | true/false                                                              | false                                                    | Should this node fetch configuration from a deployment service.      |
| -deploymentURL        | <URL>                                                                   | 127.0.0.1                                                | URL of the deployment service.                                       |
| -deploymentPort       | <int>                                                                   | 7980                                                     | Port of the deployment service.                                      |
| -debug                | true/false                                                              | false                                                    | Enable/disable verbose logging.                                      |
| -seed                 | <string>                                                                | "42"                                                     | Seed used for terrain generation.                                    |
| -playType             | ClientAndServer<br/>Client<br/>Server<br/>StreamedClient<br/>ThinClient | Client                                                   | Mode to start the application in. Can be overriden by remote config. |
| -serverUrl            | <URL>                                                                   | 127.0.0.1                                                | URL of the game server.                                              |
| -serverPort           | <int>                                                                   | 7979                                                     | Port of the game server.                                             |
| -localConfigJson      | <FilePath>                                                              | null                                                     | Path to a command args Json file.                                    |
| -networkTickRate      | <int>                                                                   | 60                                                       | Rate of network snapshots.                                           |
| -simulationTickRate   | <int>                                                                   | 60                                                       | Rate of simulation steps.                                            |
| -signalingUrl         | <URL>                                                                   | 127.0.0.1                                                | URL of the stream signaling service.                                 |
| -signalingPort        | <int>                                                                   | 7981                                                     | Port of the stream signaling service.                                |
| -multiplayRole        | Disabled<br/>Host<br/>Guest                                             | Disabled                                                 | Stream gaming role.                                                  |
| -emulationType        | None<br/>PlaybackExplore<br/>PlaybackCollect<br/>PlaybackBuild<br/>SimulationExplore<br/>SimulationCollect<br/>SimulationBuild   | None          | Player emulation type mode.                                          |
| -emulationFile        | <FilePath>                                                              | Application.persistentDataPath\recordedInputs.inputtrace | Path to a player emulation trace.                                    |
| -numThinClientPlayers | <int>                                                                   | 0                                                        | Number of thin clients to run, useful for debugging or experiments.  |

### Deployment Configuration
The deployment service constructs a deployment graph based on a configuration file. The deployment configuration file path is set
using the command line argument `-deploymentJson <FilePath>`. In editor, a json file can be set on the `Deployment Config` field of the `GameBootstrap->Cmd Args Reader` singleton component.
The Json is expected to follow this formatting (excluding comments):
```
{
"nodes":[
	{
		"nodeID":0,                                   // ID of this node
		"nodeIP":"127.0.0.1",                         // Expected IP of this node, will throw warning if node with nodeID not at this IP.
		"worldConfigs":[                              // List of worlds to deploy on this node
			{
			"worldType":"Client",                    // None, Client, Server, ThinClient
			"initializationMode":"CreateAndConnect", // CreateAndConnect, Connect 
			"multiplayStreamingRoles":"Guest",       // None, Guest, Host
			"serverNodeID":-1,                       // The ID of the node to connect a client world to
			"streamingNodeID":1,                     // The ID of the node to connect a streamed guest client world to
			"numThinClients":0,
			"services":[],                           // Names of services, handled according to serviceFilterType
			"serviceFilterType":"Includes",          // Includes, Excludes, Only
			"emulationBehaviours":"None"             // None, PlaybackExplore, PlaybackCollect, PlaybackBuild, SimulationExplore, SimulationCollect, SimulationBuild 
			}
		]
	},
	{
		"nodeID":1,
		"nodeIP":"127.0.0.1",
		"worldConfigs":[
			{
			"worldType":"Server",
			"initializationMode":"CreateAndConnect",
			"multiplayStreamingRoles":"Disabled",
			"serverNodeID":-1,
			"streamingNodeID":-1,
			"numThinClients":0,
			"services":[],
			"serviceFilterType":"Includes",
			"emulationBehaviours":"None"
			},
			{
			"worldType":"Client",
			"initializationMode":"CreateAndConnect",
			"multiplayStreamingRoles":"Host",
			"serverNodeID":1,
			"streamingNodeID":-1,
			"numThinClients":0,
			"services":[],
			"serviceFilterType":"Includes",
			"emulationBehaviours":"None"
			}
		]
	}
]
}
```