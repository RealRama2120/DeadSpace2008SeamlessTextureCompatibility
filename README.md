# Dead Space (2008) Seamless Texture Compatibility

Source and release archive for Rama2120's Dead Space (2008) texture compatibility mod. This repository covers the original game only. The Dead Space 2 mod, Vortex extension, and other Dead Space patches are separate projects.

## Current source: v2.1.2

The repository root contains the browsable v2.1.2 source, `build.ps1`, the Vortex deployment manifest, documentation, and the source checksum list from the matching source ZIP. The original install and source ZIPs are preserved unchanged in [`versions/v2.1.2`](versions/v2.1.2).

For prerequisites, exact Windows build commands, output paths, and verification steps, see [`BUILDING.md`](BUILDING.md). The root [`build.ps1`](build.ps1) compiles the 32-bit launcher and `dinput8.dll`; it does not recreate the historical release ZIP byte-for-byte. [`Documentation/README.md`](Documentation/README.md) covers player installation and compatibility.

TexMod, texture packs, Dead Space game files, and the separate Vortex extension are not included.

## Version archive

| Version | Local release package | Source evidence | Notes |
| --- | --- | --- | --- |
| [v1.0.0](versions/v1.0.0) | Original ZIP, unchanged | `Source/` was included inside that ZIP and is also extracted as `source-snapshot/` for browsing | No separate original source ZIP or checksum file was found. |
| [v2.0.0](versions/v2.0.0) | Original ZIP, unchanged | `Source/` was included inside that ZIP and is also extracted as `source-snapshot/` for browsing | No separate original source ZIP or checksum file was found. |
| [v2.1.0](versions/v2.1.0) | Locally retained five-file Beta ZIP, unchanged | Matching `release-final-2.1.0/Source` snapshot is copied as `source-snapshot/` | The earlier, larger upload ZIP was overwritten during local repackaging; its exact bytes and a separate original source ZIP are unavailable locally. |
| [v2.1.2](versions/v2.1.2) | Original install ZIP, unchanged | Original source ZIP, unchanged; its exact source files are browsable at the repository root | Latest complete version. |

There is no local v2.1.1 release package or source snapshot in the inspected project locations. Each version folder has a newly generated `SHA256SUMS.txt` for the archived files and extracted source. These checksum lists are backup metadata, not claimed as original release files. The v2.1.2 source ZIP also contains its original `SOURCE_CHECKSUMS.txt`, reproduced unchanged at the repository root.

No historical tags were created: a version folder preserves each available source snapshot without implying a reconstructed commit is an original historical release commit.

The v2.1.2 EA App launch and exit path was tested with the final release binaries. Steam and ROG Ally were not retested for this version.
