# Microsoft Store package

This directory contains the Microsoft Store identity and MSIX manifest for
OpenTrace.

Store identity:

- Package name: `NYALabs.OpenTrace`
- Publisher: `CN=33B5F0AF-2704-46FB-8180-E63B444C2020`
- Publisher display name: `NYA Labs`
- Package family name: `NYALabs.OpenTrace_065q9hydehnh0`
- Store ID: `9N894BGLCQTR`

Use `scripts\Build-StorePackage.ps1` from PowerShell to create the x64 and
ARM64 packages plus a combined bundle. The script is the reproducible build
entry point; the `.wapproj` is useful for manifest editing and for associating
the project with Partner Center in Visual Studio. Open `traceroute-store.sln`
when the Visual Studio MSIX workload is installed; the regular
`traceroute.sln` intentionally remains independent of that optional workload.

The build pins NextTrace rather than downloading `latest`. See
the build script parameters for reproducible packaging options. Store images
are generated at build time from `HomePage\img\logo.png`; generated image files
and Partner Center submission notes are intentionally not versioned.
