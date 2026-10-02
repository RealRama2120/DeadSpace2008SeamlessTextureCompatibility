# Dead Space (2008) Seamless Texture Compatibility

Version 2.1.2  
Author: Rama2120

This is a Vortex-deployable compatibility mod for TexMod `.tpf` textures. It uses the regular Dead Space Play button: no separate launcher shortcut, Steam Launch Option, executable rename, or manual TexMod session is required.

## What changed in 2.1.2

- The initial game process now ends immediately after a successful TexMod handoff, avoiding a shutdown hang in third-party DLL detach callbacks.
- After the real game closes, the launcher removes only the EA activation helper associated with that launch, so EA App can make Play available again. It verifies the helper's parent process, path, PID, and start time before cleanup, and leaves it alone while another Dead Space process is running.
- Two normal EA App launch/exit cycles completed with textures enabled, with no game, launcher, TexMod, or activation-helper process left running. Steam and ROG Ally were not retested for this update. I also fully played through the entire game with all my Dead Space (2008) mods and texture packs loaded.

## What the player does

1. Install the Dead Space (2008) Vortex game extension normally.
2. Download this compatibility mod with Vortex, then enable and deploy it.
3. Download Dead Space `.tpf` texture mods with Vortex, then enable and deploy them.
4. Press the normal Play button in Steam, EA App, or Vortex.

On the first normal game launch only, Windows asks for permission to download classic TexMod 0.9b directly from its archived original project. The launcher verifies the downloaded archive and executable against pinned checksums. Accept once and the same game launch continues automatically. This consent cannot be removed safely because TexMod has no clear redistribution license and is not included in this ZIP.

After that, newly deployed `.tpf` files are detected and loaded automatically every time Dead Space starts. The player never needs to open TexMod or remember a different executable.

## Vortex behavior

Rama2120's existing Dead Space Vortex extension already deploys extracted `.tpf` files to:

```text
Dead Space\TexMod Packages
```

This mod rescans that folder recursively on every launch. Enabling, disabling, updating, resolving a conflict, deploying, or purging a texture mod in Vortex therefore changes the next launch automatically.

The compatibility ZIP does not contain or modify the Vortex extension. It only relies on the extension's existing root-file and `.tpf` deployment behavior.

## How normal launching works

The package deploys a small 32-bit `dinput8.dll` bootstrap beside `Dead Space.exe`. Dead Space loads that standard Windows library name during startup. The bootstrap:

1. Confirms the process is exactly `Dead Space.exe`.
2. Starts `DeadSpaceTextureLauncher.exe` from the same folder.
3. Hands off the initial process.
4. The launcher finds every deployed `.tpf`, controls TexMod 0.9b off-screen, and starts Dead Space through TexMod.
5. A child marker prevents the TexMod-launched game from triggering the bootstrap again.

All six exports from the real 32-bit Windows `dinput8.dll` are forwarded. This code does not modify DirectInput data, mouse input, controller input, game memory, or controller profiles. If the launcher is missing or cannot start, the bootstrap leaves the normal game process alone.

TexMod does not provide a reliable supported command line for selecting a target and multiple packages. The launcher therefore uses guarded Windows UI automation and verifies every stage. If the expected TexMod 0.9b window is not found, TexMod is restored on-screen and the log explains where automation stopped.

## TexMod acquisition and licensing

TexMod is a separate third-party tool created by RS/Racer_S. No explicit redistribution permission for its packed executable was found, so neither `TexMod.exe` nor any texture pack is included.

The one-time download comes directly from the archived original Google Code project:

```text
https://storage.googleapis.com/google-code-archive-downloads/v2/code.google.com/texmod/texmod.zip
```

HTTPS is attempted first. On older or restricted Windows TLS configurations, the launcher can retry the same Google Storage object over HTTP; the result is still rejected unless both the archive hash and extracted executable SHA-256 match the pinned originals. Nothing from a failed or mismatched download is kept or executed.

Pinned checksums:

```text
texmod.zip SHA-1:
c05a59ef20c5cb682230de2be9973945562ab86d

TexMod.exe SHA-256:
f662be61eee7c3d2849e1c734b3b83e9e25fa4e873c7352852130f0c0ceb98af
```

It is stored at:

```text
%LOCALAPPDATA%\Rama2120\DeadSpaceTextureLauncher\Tools\TexMod.exe
```

TexMod is unsigned, packed 2006 software that injects into Direct3D 9. Antivirus software may flag it. This mod never disables security software, creates exclusions, accepts a checksum mismatch, or silently downloads TexMod without consent.

## Controller safety and other mods

Do not combine this with the old Dead Space Mouse Fix. Both use the filename `dinput8.dll`, and the old mouse patch is known to break controller look/aim. Let this compatibility mod win any Vortex conflict for `dinput8.dll`.

DeadSpace2008Fixes is supported. It uses `xinput1_3.dll`, `SDL3.dll`, and `DeadSpaceFixes.ini`, so it does not replace this mod's file. Its XInput/SDL controller support remains separate. This compatibility bootstrap does not implement the old mouse fix.

Other mods that install their own `dinput8.dll` conflict at the file level. Windows can load only one same-named proxy from the game folder; choose one in Vortex.

## Texture-pack rules

- The actual `.tpf` must be present after Vortex installs the mod. Archives that contain another nested ZIP/RAR/7z may need repackaging by that texture-mod author.
- Packs load in deterministic alphabetical full-path order after any packages pinned in Settings.
- Duplicate paths are removed.
- TexMod cannot identify which game an arbitrary `.tpf` belongs to. This Vortex mode intentionally scans the Dead Space deployment folder, not the user's entire Downloads folder.
- Conflicting packs can replace the same textures. Vortex file-conflict rules cannot inspect conflicts inside `.tpf` containers.
- Very large pack combinations can exceed TexMod's practical 32-bit memory limits or produce a long blank loading period.

## Settings and logs

Hold Shift while starting the game to open settings instead of launching immediately.

```text
%LOCALAPPDATA%\Rama2120\DeadSpaceTextureLauncher\settings.ini
%LOCALAPPDATA%\Rama2120\DeadSpaceTextureLauncher\latest.log
```

There is no telemetry. Network access occurs only after the player approves the verified TexMod download.

## Troubleshooting

### The normal game starts without the launcher

- Confirm this compatibility mod is enabled and deployed.
- Confirm `dinput8.dll`, `DeadSpaceTextureLauncher.exe`, and `DeadSpaceTextureLauncher.vortex.json` are beside `Dead Space.exe`.
- Resolve any `dinput8.dll` conflict so this mod wins.
- Purge and redeploy once if Vortex shows an out-of-date deployment.

### A texture pack is missing

- Confirm Vortex shows the texture mod enabled and deployed.
- Confirm an extracted `.tpf` exists under `Dead Space\TexMod Packages`.
- Check `latest.log`; it records every discovered pack and each TexMod loading step.

### TexMod appears or automation stops

Close Dead Space and TexMod, increase the UI delay in Settings, and try again. Use classic 32-bit TexMod 0.9b. Do not set TexMod and the launcher to mismatched administrator/compatibility modes.

### Antivirus quarantines TexMod

Compare the detected file against the pinned checksum above and decide according to your security policy. Do not disable antivirus or trust a mismatched file.

## Uninstall

Disable this compatibility mod and choose Purge/Deploy in Vortex. That removes the Vortex-managed launcher, bootstrap, and deployment marker without changing `Dead Space.exe` or the Vortex extension.

Optional user data can then be deleted from:

```text
%LOCALAPPDATA%\Rama2120\DeadSpaceTextureLauncher
```

## Release status

The x86 build, export table, forwarding, marker detection, package discovery, TexMod UI automation, child-recursion guard, no-pack fallback, and ZIP layout have automated/mock coverage. Real Steam and EA App sessions, antivirus products, controller models, and large public texture packs still require community beta testing before calling the release universally proven.

Rama2120's launcher and bootstrap source are MIT licensed. See `LICENSE.txt`, `THIRD_PARTY_NOTICES.txt`, `RESEARCH_NOTES.md`, and `Source`.
