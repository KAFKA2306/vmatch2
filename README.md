# vmatch2

This repository contains the `Assets` folder for the VirtualTokyoMatching world.

## Restoring the Unity project

1. Install Unity **2022.3.10f1**.
2. Install the .NET 8 SDK and the `vrc-get` CLI.
3. Run `vrc-get install com.vrchat.worlds` to resolve VRChat dependencies.
4. Use `vrc-get info project` to verify package versions.

## Testing

No Unity test assemblies are currently included. Running `dotnet test` will report `MSB1003` because no project or solution files exist.
