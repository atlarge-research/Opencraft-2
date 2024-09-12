# Opencraft 2

Opencraft 2 is an Minecraft-like online game built in Unity. It is intended for supporting experimental research on
online game and cloud gaming performance.

## Setup

Install *Unity 2022 LTS*, clone this repository using `git clone --recurse-submodules`, and open it in Unity.
Unity should automatically install required packages. This includes the [ParrelSync](https://github.com/VeriorPies/ParrelSync) package,
which is useful for testing multiplayer functionality in-editor.
The [Rider](https://www.jetbrains.com/rider/) IDE is recommended, it has direct integration with Unity.

The game can be started from `Scenes/MainScene`. To run select `Server & Client` in `Multiplayer -> Window: PlayMode Tools`,
then press the play button. Additional configuration can be specified in command line arguments,
which can be set in-editor on the `Editor Args` field of the `GameBootstrap -> Editor Cmd Args` singleton component.

## Building the Game

Opencraft 2 is built by the Unity editor, using `File -> Build Settings` with `Platform` set to `Windows, Mac, Linux` and
`Target Platform` set to `Linux` or `Windows` (Mac is untested). For debugging, analysis, and metric collection the `Development Build`
flag must be set. The builds folder is location under `./Builds/`. This folder also contains the Docker files for containerizing Opencraft 2.

## Running the Game

Opencraft 2 can be started in three ways:

1. From the Unity Editor (no building required!)
2. Using command-line arguments
3. Using a deployment graph.

The sub-sections deploy describe how to run the game in each case.

### From the Editor

To run the game from the editor in its simplest setup (a client connecting to a single, locally running server), all you need to do is open the game in the Unity editor, open `MainScene`, and click the play button. To configure how to run the game, keep reading.

You can configure how you want to run the game in the editor.
To do so, first make sure you have the game open the Unity editor,
then open the `MainScene` and click the `GameBootstrap` game object.
This should show the `Editor Cmd Args` script in the panel on the right-hand side.
This panel allows you to pass either command-line arguments or a deployment graph.
To determine which command-line arguments to pass,
or how to construct your deployment graph, see the sections on using [command-line arguments](#using-command-line-arguments) and [using a deployment graph respectively](#using-a-deployment-graph).

To run multiple instances of the game from the editor, use Parrelsync as described in the [Parrelsync Section](#parrelsync).

### Using Command-Line Arguments

This section gives a brief overview of how to launch Opencraft 2 in its most common configurations using command-line arguments.
For a full overview of available command-line arguments, see the [command-line arguments section](#command-line-arguments).

When using the commands below, replace `.\Opencraft.exe` with the name of your executable. (Probably `./opencraft2.x86_64` on Linux!)

To run the game as a server:

```powershell
.\Opencraft.exe -playType Server -batchmode -nographics
```

To run the game as a client:

```powershell
.\Opencraft.exe -playType Client
```

To run the game as a thin-client:

```powershell
.\Opencraft.exe -playType StreamedClient -iceServerUrl stun:stun.l.google.com:19302 -signalingUrl ws://<host>:<FOO>
```

To run the game as a renderer (i.e., positioned between a server and a thin client):

```powershell
.\Opencraft.exe -playType Client -multiplayRole CloudHost -iceServerUrl stun:stun.l.google.com:19302
.\webserver.exe -p <FOO>
```

The `Builds` directory contains three `.bat` scripts to get you started: `Opencraft Server.bat`, `Opencraft Client.bat`,
and `Opencraft Local.bat`.
These files require `Opencraft.exe` to be present, and run the game as a stand-alone server, a client,
or a combined server and client, respectively.

See the [Configuration Deployment section](#deployment-configuration) for a more in-depth example of the game's
configuration file.

### Using a Deployment Graph

This section gives a brief overview of how to launch Opencraft 2 in its most common configurations using a Deployment Configuration.
For a detailed description of the Deployment Configuration, see the [Deployment Configuration Section](#deployment-configuration).

>[!WARNING]
>TODO: add common Deployment Configuration snippets here.

## Docker

Opencraft 2 can be run as a container. The main game container can be built from the `./Builds/` folder using
`docker build -t jerriteic/opencraft2:base .`. This base image depends on `jerriteic/gpu_ubuntu20.04` which allows
container application to run graphics applications with NVIDIA GPU hardware acceleration. That container can
be built in the same folder using the `Dockerfile.ubuntugpu` dockerfile.

### Docker Requirements

The game container runs a hardware-accelerated 3D graphics application through VirtualGL.
This requires extensive configuration to the host platform:
1. Install correct NVIDIA drivers for your platform and GPU.
   1. Container is tested on Ubuntu 20.04 on NVIDIA Tesla T4 with proprietary driver version 535.
2. Install [Docker + NVIDIA container toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html#docker)
3. On headless hosts, configure display settings according to [this guide](https://github.com/trn84/recipe-wizard/blob/master/nvidia-headless.md).
   1. Do not reinstall the NVIDIA drivers, skip the step marked `Install NVIDIA Driver from CUDA .run shell script`.

### Running the Container

If the Docker requirements are met, run the container using the following command:

```
docker run \
--runtime=nvidia \
--name=opencraft_gpu \
--security-opt seccomp=unconfined \
--init \
--net=host \
--privileged=true \
--rm=false \
-e DISPLAY=:0 \
-v /tmp/.X11-unix/X0:/tmp/.X11-unix/X0:ro \
-v /etc/localtime:/etc/localtime:ro \
-v ./logs/:/opencraft2/logs/ \
jerriteic/opencraft2:base \
./opencraft2.x86_64 -logFile ./logs/opencraft2_log.txt
```

> [!WARNING]
> Running Docker containers with these flags gives them **UNRESTRICTED HOST ROOT ACCESS**! Act accordingly.

## Parrelsync

[ParrelSync](https://github.com/VeriorPies/ParrelSync) allows synchronizing multiple copies of a Unity project. These
can be run in parallel to test networked functionality.

> [!WARNING]
> In `ParrelSync -> Preferences`, make sure that `/UnityRenderStreaming`
> is listed under `Optional Folders to Symbolically Link`. Then, to create a new ParrelSync clone select `Add new clone` in `ParrelSync -> Clones Manager`.

## Multiplay

This project supports a single game client acting as a streamed gaming host for many players on guest clients.
Testing Multiplay is easiest using ParrelSync, in the clone's launch arguments in the clone manager add `-multiplayRole Guest`
to run it as a Multiplay guest client.

### Signalling Service WebApp

Multiplay functionality requires a signalling service to establish a direct connection between host and guest clients.
The signaling service is run as a webapp, the source is available in `./UnityRenderStreaming/WebApp/`
and can be build using `./UnityRenderStreaming/pack_webapp.sh` which has `npm` as a dependency.
The webapp can be run with a convenience script `.Builds/Multiplay_WebApp/start.sh` or directly with `.\webserver -p <PORT>`.
The port the webserver listens on must be the same as the signaling port configured using the
application command line argument `-signalingPort <int>`.


## Debugging and Analysis

Inspecting existing entities, components, systems, and queries in each existing world can be done using
Entities information pages under `Window -> Entities`. Information not specific to ECS can be found under `Window -> Analysis`,
with the `Profiler` being particularly important for determining performance impact of various changes.
Burst compiled generated code can be viewed in `Jobs -> Burst -> Open Inspector`, though this is mainly useful for low level optimization work.
Multiplayer functionality and behaviour can be visualized using the `Window -> Multiplayer -> Window: NetDbg (Browser)` tool,
which automatically gathers state snapshot metrics from Netcode for Entities connections.

If your Unity or your IDE starts giving strange errors, particularly about packages,
it is worth trying `Edit > Preferences -> External Tools -> Regenerate project files`. As a last resort, `Reimport All`
through the right-click menu in the `Project` window.

## Configuration and Parameters

The Opencraft 2 application can be configured in a variety of ways, with some methods being different in-editor and
when run standalone. In editor, the hierarchy of configuration is `Deployment Graph`>`Multiplayer PlayMode Tools` >`Editor Args`.

### Command Line Arguments

| Argument                    | Options                                                                      | Default                                                  | Description                                                          |
|-----------------------------|------------------------------------------------------------------------------|----------------------------------------------------------|----------------------------------------------------------------------|
| -deploymentJson             | _FilePath_                                                                   | null                                                     | Path to a deployment configuration Json file.                        |
| -deploymentID               | _int_                                                                        | -1                                                       | The ID of this node, used for deployment.                            |
| -remoteConfig               | true/false                                                                   | false                                                    | Should this node fetch configuration from a deployment service.      |
| -deploymentURL              | _URL_                                                                        | 127.0.0.1                                                | URL of the deployment service.                                       |
| -deploymentPort             | _int_                                                                        | 7980                                                     | Port of the deployment service.                                      |
| -networkTickRate            | _int_                                                                        | 60                                                       | Rate of network snapshots between servers and clients.               |
| -simulationTickRate         | _int_                                                                        | 60                                                       | Rate of simulation steps on both server and clients.                 |
| -maxSimulationStepsPerFrame | _int_                                                                        | 4                                                        | Max number of steps per frame when framerate is slow.                |
| -maxSimulationStepBatchSize | _int_                                                                        | 4                                                        | Max number of batched steps when simulation overloaded.              |
| -maxPredictAheadTimeMS      | _int_                                                                        | 250                                                      | Duration clients are allowed to predict in advance.                  |
| -disablePrediction          | _int_                                                                        | false                                                    | Disable predicted systems (only affects clients).                    |
| -nographics                 | true/false                                                                   | false                                                    | Disable the graphics frontend and any related systems.               |
| -debug                      | true/false                                                                   | false                                                    | Enable/disable verbose logging.                                      |
| -seed                       | _string_                                                                     | "42"                                                     | Seed used for terrain generation.                                    |
| -playType                   | ClientAndServer<br/>Client<br/>Server<br/>StreamedClient<br/>SimulatedClient | Client                                                   | Mode to start the application in. Can be overriden by remote config. |
| -serverUrl                  | _URL_                                                                        | 127.0.0.1                                                | URL of the game server.                                              |
| -serverPort                 | _int_                                                                        | 7979                                                     | Port of the game server.                                             |
| -localConfigJson            | _FilePath_                                                                   | null                                                     | Path to a command args Json file.                                    |
| -takeScreenshots            | true/false                                                                   | false                                                    | Take screenshots at intervals. No effect on Server-only builds.      |
| -screenshotInterval         | _int_                                                                        | 5                                                        | Set screenshot interval.                                             |
| -screenshotFolder           | _FilePath_                                                                   | Application.persistentDataPath\screenshots               | Set screenshot save location.                                        |
| -duration                   | _int_                                                                        | -1                                                       | Exit after a set amount of seconds.                                  |
| -startDelay                 | _int_                                                                        | 0                                                        | Seconds to wait before calling the bootstrap.                        |
| -userID                     | _int_                                                                        | 0                                                        | Player's user ID. Acts as a username.                                |
| -signalingUrl               | _URL_                                                                        | ws://127.0.0.1:7981                                      | URL of the stream signaling service.                                 |
| -multiplayRole              | Disabled<br/>Host<br/>CloudHost<br/>Guest                                    | Disabled                                                 | Stream gaming role.                                                  |
| -emulationType              | None<br/>Playback<br/>Simulation<br/>Record                                  | None                                                     | Player emulation type mode.                                          |
| -emulationFile              | _FilePath_                                                                   | Application.persistentDataPath\recordedInputs.inputtrace | Path to a player emulation trace.                                    |
| -numSimulatedPlayers        | _int_                                                                        | 1                                                        | Number of simulated client worlds when PlayType is SimulatedClient.  |
| -playerSimulationBehaviour  | BoundedRandom<br/>FixedDirection                                             | BoundedRandom                                            | Behaviour that simulated players exhibit.                            |
| -terrainType                | _string_                                                                     | "default"                                                | Name of TerrainGenerationConfiguration for terrain generation.       |
| -profiler-enable            | true/false                                                                   | false                                                    | Automatically starts the profiler module.                            |
| -profiler-log-file          | _FilePath_                                                                   | null                                                     | Specifies a `.raw` file to write profiler data to.                   |
| -profiler-maxusedmemory     | _int_                                                                        | 16000000                                                 | Max memory used by profiler in bytes. Default is 16MB.               |
| -logStats                   | true/false                                                                   | false                                                    | Log relevant Profiler statistics to file.                            |
| -statsFile                  | _FilePath_                                                                   | Application.persistentDataPath\stats.csv                 | What file to log statistics to.                                      |

### Deployment Configuration

The deployment service constructs a deployment graph based on a configuration file. The deployment configuration file path is set
using the command line argument `-deploymentJson <FilePath>`. In editor, a json file can be set on the `Deployment Config` field of the `GameBootstrap->Cmd Args Reader` singleton component.
The Json is expected to follow this formatting (excluding comments):

```
{
"nodes":[
   {
      "nodeID":0,                                 // ID of this node
      "nodeIP":"127.0.0.1",                       // Expected IP of this node, will throw warning if node with nodeID not at this IP.
      "worldConfigs":[                            // List of worlds to deploy on this node
         {
         "worldType":"GameServer",                // None, Client, Server, SimulatedClient
         "initializationMode":"Connect",          // Create, Start, Connect
         "multiplayStreamingRoles":"Disabled",    // Disabled, Guest, Host, CloudHost
         "serverNodeID":0,                        // The ID of the node to connect a client world to
         "streamingNodeID":-1,                    // The ID of the node to connect a streamed guest client world to
         "numSimulatedClients":0,
         "services":[],                           // Names of services, handled according to serviceFilterType
         "serviceFilterType":"Includes",          // Includes, Excludes, Only
         "emulationBehaviours":"None"             // None, Playback, Simulation
         }
      ]
   },
   {
      "nodeID":1,
      "worldConfigs":[
         {
         "worldName": "GameClient",
         "worldType":"Client",
         "initializationMode":"Connect",
         "serverNodeID":0,
         },
         {
         "worldName": "StreamedClient",
         "worldType":"Client",
         "initializationMode":"Create",
         "multiplayStreamingRoles":"Guest",
         "streamingNodeID":2,
         }
      ]
   },
   {
      "nodeID":2,
      "worldConfigs":[
         {
         "worldName": "CloudHostClient",
         "worldType":"Client",
         "initializationMode":"Start",
         "multiplayStreamingRoles":"CloudHost",
         "serverNodeID":0,
         "streamingNodeID":2,
         }
      ]
   }
],
"experimentActions":[
	{
		"delay": 30,                                              // Trigger these actions after a delay
		"actions": [
			{
				"nodeID": 1,                                      // ID of node to take actions on
				"worldNames": ["GameClient", "StreamedClient"],   // List of worlds on that node to take actions on
				"actions": ["Stop", "Connect"]                    // What action to take on each world, e.g. Stop, Start, Connect
			},
			{
				"nodeID": 2,
				"worldNames": ["CloudHostClient"],
				"actions": ["Connect"]
			}
		]
	}
]
}
```
## Contributing

See [WORKFLOW.md](WORKFLOW.md) for contribution guidelines and workflow.
