# MinCubed Unity

## Setup
Install *Unity 2022 LTS*, clone this repository, and open it in Unity. Unity should automatically install required packages. This includes the [ParrelSync](https://github.com/VeriorPies/ParrelSync) package, which is useful for testing multiplayer functionality in-editor. I recommend using [Rider](https://www.jetbrains.com/rider/) as your IDE, it has direct integration with Unity.

To run, select PlayMode type from Server, Client, or Server & CLient in `Multiplayer > Window: PlayMode Tools`, and set an address, by default `127.0.0.1:7979`. Then press the play button.

## Multiplay
This project supports a single game client acting as a streamed gaming host for many players. This functionality requires a seperate signalling service to be running to establish a direct connection between host and guest clients. This service can be installed from Unity by going to `Window > Render Streaming > Render Streaming Wizard > Download latest version web app`, and run using `.\webserver -p <PORT>`. The port the webserver listens on must be the same as the port configured in `Assets/Config/RenderStreamingSettings`. 

Testing Multiplay is done using ParrelSync. Under `ParrelSync > Clones Manager` select `Add new clone`. Once it is initialized, set its arguments to `-streaming_type guest` to run it as a Multiplay guest client. A word of warning, running multiple Unity instances at once requires *considerable* hardware, especially in terms of memory, and especially alongside a modern IDE like Rider. If your device can't handle it, you may have no choice but to build a client executable for testing.

*Known bugs:* ParrelSync can interact unpredictably with NetCode for Entities. An annoying instance is in how it handles the `ClientServerBootstrap` in `Scripts/Networking/Game.cs`. Clients run as Multiplay guests should only get a `StreamClientWorld`, but upon a ParrelSync scene reload often it will still get a `ClientWorld` as well, which causes issues. A workaround is changing the NetCode playtype a few times and pressing play again. 


