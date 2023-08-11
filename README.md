# Opencraft 2

## Setup
Install *Unity 2022 LTS*, clone this repository using `git clone --recurse-submodules`, and open it in Unity. 
Unity should automatically install required packages. This includes the [ParrelSync](https://github.com/VeriorPies/ParrelSync) package,
which is useful for testing multiplayer functionality in-editor.
I recommend using [Rider](https://www.jetbrains.com/rider/) as your IDE, it has direct integration with Unity.

The game can be started from `Scenes/MainScene`. Make sure that in the scene hierarchy, the checkmark next to `AuthoringScene` is enabled!
Otherwise there will be a variety of runtime errors about missing singleton components.

To run, select PlayMode type from Server, Client, or Server & Client in `Multiplayer > Window: PlayMode Tools`, and set an address,
by default `127.0.0.1:7979`. Then press the play button.

## Multiplay
This project supports a single game client acting as a streamed gaming host for many players. 
This functionality requires a separate signalling service to be running to establish a direct connection between host and guest clients.
It is available in this repo under `./Multiplay_WebApp/` 
or can be downloaded from Unity by going to `Window > Render Streaming > Render Streaming Wizard > Download latest version web app`,
and run using `.\webserver -p <PORT>`. The port the webserver listens on must be the same as the port configured in `Assets/Config/RenderStreamingSettings`. 

Testing Multiplay is done using ParrelSync. Under `ParrelSync > Clones Manager` select `Add new clone`. 
Once it is initialized, set its arguments to `-streaming_type guest` to run it as a Multiplay guest client.
A word of warning, running multiple Unity instances at once requires *considerable* hardware, especially in terms of memory,
and especially alongside a modern IDE like Rider. If your device can't handle it, you may have no choice but to build a client executable for testing.

*Known bugs:* ParrelSync can interact unpredictably with NetCode for Entities. An annoying instance is in how it handles 
the `ClientServerBootstrap` in `Scripts/Networking/Game.cs`. Clients run as Multiplay guests should only get a `StreamClientWorld`,
but upon a ParrelSync scene reload often it will still get a `ClientWorld` as well, which reduces performance.
A workaround is changing the NetCode playtype a few times and pressing play again. 


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

