# Voxel Material Limit

Space Engineers draws voxel surfaces (planets, asteroids, ore) from a table of voxel materials, and its renderer only handles the first 128 entries
of that table. With many planet or voxel material mods installed, every material past 128 is drawn as plain stone. This plugin lifts that to 254
materials (255 is reserved by the game to mean "no material").

Two things in the renderer stop at 128, and both have to change:

| What stops at 128 | What the plugin does |
|---|---|
| The size of the material table in the voxel pixel shaders | Raises the size while the shader source is read for compilation |
| The way voxel vertices hand their material numbers to the shader (numbers from 128 up arrive as negative values and are lost) | Reads them back as unsigned bytes, again while the shader source is read |

The changed shaders are compiled by the game's own compiler at run time and cached like any other shader (the first launch with the
plugin recompiles the voxel shaders, a short one-time delay).

## Use
Every player who sees the affected planets needs the plugin, since it changes how each client draws them. A dedicated server does not render, so it needs nothing.
Search the game log for `VoxelMaterialLimit`:

    [VoxelMaterialLimit] installed; voxel material limit in shaders will be 256
    [VoxelMaterialLimit] voxel shaders will be compiled from <user data>/Storage/VoxelMaterialLimit/Shaders     (Linux compatibility layer only)

A line starting `WARNING` means a game update changed what the plugin looks for; it then changes nothing and the game behaves as it does without it.

## Platforms
| Setup | How it works |
|---|---|
| Stock Windows (Pulsar) | A hook on the game's shader include reader changes the text as it is read. Nothing is written to disk. A file whose text was changed is served from memory, and the plugin makes sure the files it includes are still found next to the original. Only the voxel material shaders get the vertex change, so every other shader keeps its cached bytecode. |
| Linux with the LinuxCompat plugin | LinuxCompat compiles shaders with a native compiler that reads the shader folder itself, so the hook above is never used. The plugin makes a local mirror of your own shader folder under `<user data>/Storage/VoxelMaterialLimit/Shaders` (links to your original files, plus two rewritten copies) and points only the voxel material shader compiles at it. It relies on LinuxCompat's class and method names; if they change the plugin logs that and does nothing. |

Tested on Linux with the .NET 10 Pulsar flavour. The code compiles for .NET Framework 4.8 and .NET 10.

## What it touches
- Reads the game's shader folder (Linux mirror only) and writes under `<user data>/Storage/VoxelMaterialLimit/` (Linux mirror only).

## Build
You need the .NET 10 SDK and a Space Engineers install. `Directory.Build.props` finds the game from Steam; to override, create `Directory.Build.props.user`
(see the comments in `Directory.Build.props`). Then:

    dotnet build ClientPlugin/ClientPlugin.csproj -c Release

## Licence
MIT (see `LICENSE`). The licence covers only the code in this repository. Space Engineers and its code and assets belong to Keen Software House.
