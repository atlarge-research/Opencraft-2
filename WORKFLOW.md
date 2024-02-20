# Working with Opencraft 2

## Git
Fork this repository and use pull requests (PRs) with a summary of your changes to contribute. Use branches prefaced with
"feature/" to indicate a new feature and with "fix/" to indicate a fix or patch. As much as possible,
stick to [conventional commit messages](https://www.conventionalcommits.org/en/v1.0.0/) and see
[this guide](https://cbea.ms/git-commit/) for an explanation of git message practices.

## Submodules
Opencraft 2 has two submodules for key game features and two submodules for experiment data processing.
If you edit these submodules, see this [git submodule workflow guide]("https://www.atlassian.com/git/articles/core-concept-workflows-and-tips#possible-workflows").
In short, make a fork of the submodule, set the submodule remote to that fork. When you edit the submodule,
be sure to push the submodule first before pushing the main repository.

### UnityRenderStreaming
The [./UnityRenderStreaming/](https://github.com/JerritEic/UnityRenderStreaming) submodule contains a 
fork of the UnityRenderStreaming package. It contains the source code for a WebRTC signaling webapp that can be built
using [./UnityRenderStreaming/pack_webapp.sh](https://github.com/JerritEic/UnityRenderStreaming/blob/main/pack_webapp.sh).


### PolkaDOTS
The [./Packages/PolkaDOTS/](https://github.com/atlarge-research/PolkaDOTS) submodule contains the PolkaDOTS framework
which enables dynamic, differentiated deployment on the Unity Data Oriented Technology Stack (DOTS). See the PolkaDOTS 
Package ReadMe for more info.


### Profile-Analyzer
The [./Packages/com.unity.performance.profile-analyzer](https://github.com/JerritEic/com.unity.performance.profile-analyzer)
submodule contains a fork of the Profile-Analyzer package. This package adds a new window to the Unity Editor which
can analyze in detail a .raw performance capture made using the Unity Profiler, or compare two captures. This functionality 
is useful for viewing and comparing performance variability.

### ProfileReader
The [./Packages/com.utj.profilerreader](https://github.com/JerritEic/ProfilerReader)
submodule contains a fork of the ProfilerReader package. This package adds a new window to the Unity Editor which
can convert a .raw performance capture made using the Unity Profiler to CSV. _This conversion does not currently work
with custom profiler modules._


## Unity Project Structure
The Opencraft 2 Unity project has a set structure, which you should attempt to maintain. 
```python
├── Config/                 # Configuration assets
    ├── TerrainLayers/      # Terrain Layer configuration assets
├── Materials/              # Assets for rendering 
    ├── Shaders/            # Shader .hlsl programs
    ├── Textures/           # Texture .png assets
├── Prefabs/                # GameObject prefabs 
    ├── Text/               # GameObject UI prefabs 
├── Scenes/                 # Scene .unity assets
    ├── AuthoringScene/     # Subscene for baking GameObject to entity
    ├── SceneLoaderScene/   # Subscene for loading AuthoringScene
├── Scripts/                # C# behaviours
    ├── Configuration/      # Configuration scripts
    ├── Editor/             # Editor-only scripts
    ├── Networking/         # Networking scripts
    ├── Player/             # Player scripts
        ├── Authoring/      # Baking player GameObjects to entity
        ├── Emulation/      # Player emulation scripts
        ├── Multiplay/      # Player linking scripts
    ├── Rendering/          # Rendering scripts
         ├── Authoring/     # Baking rendering GameObjects to entity
    ├── Statistics/         # Performance monitoring
    ├── Terrain/            # Terrain scripts
        ├── Authoring/      # Baking terrain GameObjects to entity
        ├── Blocks/         # Terrain block specification
        ├── Layers/         # Scripts defining terrain layer assets
        ├── Structures/     # Scripts handling terrain structure generation
        ├── Utilities/      # Shared utility functions for terrain
    ├── ThirdParty/         # Shared third-party utility functions
```