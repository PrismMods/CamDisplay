# CamDisplay

Shows your hand cam inside **A Dance of Fire and Ice**, so a plain screen recording of a clear
already has the cam in it — no OBS scene needed. Display only: CamDisplay does not record.

- Place, resize, rotate and crop the cam in PrismLib's shared layout editor (the same session
  lines it up with other Prism mods' elements).
- Horizontal and vertical flip, both on by default (a cam over the keyboard, facing back).
- Always shown while playing a level; menus, level select and the editor are opt-in.
- Settings: **Alt+C** (Option+C on a Mac), the button on CamDisplay's UMM page, or the shared Prism
  settings window (Alt+P) when another Prism mod is installed.

Supports ADOFAI 3.x (tested on 3.4.0, macOS) and 2.9 (not yet tested). **Windows: untested.**

## Install

**MelonLoader** (recommended): extract `CamDisplay.zip` into the game folder. It puts
`Mods/CamDisplay.dll`, `UserLibs/PrismLib.UI.dll` and `UserData/CamDisplay/PrismLib.bundle` in place.

**UnityModManager**: extract `CamDisplayUmm.zip` into `…/A Dance of Fire and Ice/Mods/` (or
`UMMMods/` under MelonLoader's UMM bridge). Install only one of the two.

## macOS: enabling the camera

macOS blocks the camera for ADOFAI as shipped: the game is not signed to ask for it, and when
Steam starts the game, macOS asks *Steam* for camera access, which Steam does not have. Once:

1. Open CamDisplay's settings (Alt+C) › **macOS camera** › **Enable camera**.
2. **Copy launch options** and paste them into Steam › ADOFAI › Properties › Launch Options
   (they put `CamDisplayLaunch` in front of the launcher you already use).
3. Quit the game. The patch is applied as it closes (re-signing a running game is not safe).
4. Start it from Steam and allow the camera when macOS asks.
5. Only the first time: System Settings › Privacy & Security › **Input Monitoring**, remove the
   game, add it again, restart it. Until then macOS blocks the game's keyboard hook, so Tab and
   F1-F12 do not count as hits.

What the patch does: adds a camera usage line to the game's `Info.plist` and re-signs the game
with its own entitlements plus `com.apple.security.device.camera`. It signs with a certificate
made once on your Mac (kept in `~/Library/Application Support/CamDisplay/signing`, outside your
login keychain), so macOS keeps the game's Input Monitoring permission (and, by the same rule, its
camera permission) when you re-patch after a game update; ad-hoc signing would make every re-patch
look like a new app. The
original signature files are backed up to `~/Library/Application Support/CamDisplay/sig-backup`;
**Undo camera patch** puts them back, and so does Steam › Verify integrity of game files. A game
update restores the original signature too — enable the camera again afterwards.

Once the game may use the camera, so may any mod it loads. CamDisplay keeps the camera open while
it is enabled (starting a camera stalls the game for about a second, so it is not restarted for
every level). Turning **Enabled** off, or switching CamDisplay off in UMM's mod list, releases it;
with CamDisplay off or not installed nothing opens the camera, even with the launch options and the
patch still in place.

## Performance

No per-frame CPU work on the image: crop and flips are texture coordinates, rotation is a
transform. Measured on an M-series Mac in the level select at 1280×720 @ 30: p99 frame time
15.60 ms with the cam hidden, 15.86 ms shown. If a dense level drops frames, pick
**640×360 @ 30** under Camera › Resolution.

## Building

`./deploy.sh` builds a Debug copy and installs it into the local game. `./release.sh [version]`
runs every check and builds both zips. Offline tests: `tools/test.sh` (core), 
`native/launch/test.sh` (launcher), `tools/test-mac-camera-patch.sh` (patch, on a throwaway
app bundle). A Debug build runs an in-game self-test when `UserData/CamDisplay/selftest` (or the
UMM mod folder's `selftest`) exists.

GPL-3.0.
