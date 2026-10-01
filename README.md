# Clicks

An offline Windows desktop app that adds satisfying sounds to your keyboard, mouse, and scroll wheel. Version **1.4.1** includes eight short sound profiles and a full-length Gunshot preset that restarts on every press. Short taps never stack into louder audio. No website or browser extension is needed while the app runs.

## Run

**[Download Clicks.exe](https://github.com/BilalUchiha/Clicks/raw/refs/heads/main/app/Clicks.exe)** and run it. The executable is also included at `app/Clicks.exe` in this repository. No installation, admin rights, Python, or .NET installation is needed for the portable build. Requires Windows 10/11, 64-bit.

Keyboard sounds and mouse sounds are enabled initially. Choose a preset for each and use **Preview** to hear it. Scroll ticks and click ripples are optional, initially off. Release sounds are off by default: each key press or click plays one brief tap.

Closing the window keeps Clicks running in the notification area near your clock. Double-click its purple icon to open settings. Right-click it to mute or quit. Launching a second copy displays a reminder to use that icon.

**Mute shortcut:** Ctrl+Alt+M. If another application already uses it, Clicks automatically tries **Ctrl+Alt+F10**. The active shortcut appears in the window and tray menu. If both are taken, use the mute button or tray menu.

## Your sound

- **Hush:** a muffled low-pass click, 34 ms; the keyboard default.
- **Felt:** a tiny, cushioned tap, 20 ms; the mouse default.
- **Tap:** a deep, round trackpad thud, 54 ms.
- **Thock:** a creamy bass impact, 39 ms.
- **Walnut:** a warm wooden knock, 44 ms.
- **Crisp:** a bright snap, 22 ms.
- **Bubble:** a short rising pop, 64 ms.
- **Tok:** a hollow wooden block tap, 74 ms.
- **Gunshot:** the complete bundled MP3. No trimming, normalization, fading, or pitch changes. Stereo is preserved. Volume follows the keyboard or mouse slider. Every press stops the previous shot and immediately restarts the clip from the beginning, without stacking or waiting. When you stop pressing, the last shot plays to the end. Mute and Quit still stop playback.

A new short tap smoothly replaces the previous tap with a 1 ms transition, so rapid typing and clicking cannot stack into louder audio or queue a long tail. Six variations per effect have matched energy and no automatic volume changes. Space, Enter, and Backspace are slightly deeper; right clicks have a slightly different pitch. Holding a key produces one tap rather than a burst from repeat events. Optional release sounds use the profile's quieter, brighter release parameters. Synthesized effects never exceed 75 ms, and imported short taps are capped at 120 ms. Gunshot is a full-length recording and has no duration cap; it triggers only on press, never release or scrolling.

Keyboard and mouse have independent volume and preset controls. Scroll and release levels are separate; release level is a percentage of the keyboard or mouse volume. Stereo gives keyboard sides a subtle position and mouse clicks follow the horizontal screen position. Turn it off for centered sound.

**Import sound…** accepts a mono/stereo WAV or MP3 up to two seconds long. It trims leading silence and uses a maximum 120 ms tap with a fade at the end. It copies the file locally, so moving your original will not break it. Imports are normalized for consistent levels. Optional custom release plays that same short sample at the release level.

**Quiet in these apps:** enter process names, separated by commas, for example `Teams, Zoom, Discord`. Names are case-insensitive; `.exe` is optional. Muting applies while the named app has focus, checked about twice per second. This does not detect calls automatically.

**Start with Windows** is optional and off initially. It creates a startup entry for the current executable path. If you move the executable later, turn startup off and back on from its new location.

## Troubleshooting

If you change headphones or audio devices, use **Reconnect audio**. Check the Windows volume mixer and your output device if Preview is silent. Sleep/resume automatically reconnects playback.

The app uses normal Windows input events and never blocks or remaps input. It ignores software-generated events. Secure Windows screens, other user sessions, some protected games, and certain elevated applications may not supply events to a normal desktop app. It does not change security settings or require elevation. Trackpad taps work when Windows reports them as mouse clicks; scrolling uses the wheel events Windows provides.

## Privacy and removal

Clicks makes no network requests. Key codes are used transiently to select effects and suppress repeats; they are never decoded into text, written to disk, or transmitted. Session counters exist only in memory. Settings and imported sounds live in `%LOCALAPPDATA%\Clicks`.

To remove it: turn off **Start with Windows**, quit through the tray menu, and delete the app folder. You can also delete `%LOCALAPPDATA%\Clicks` to remove preferences and imported audio.

## Build and verify

Source is in `src/`. Requires .NET 10 SDK and the NAudio 2.2.1 NuGet package.

```powershell
dotnet build src/Clicks.csproj -c Release
dotnet publish src/Clicks.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o app
```

Run `Clicks.exe --test` for waveform, mixer, mute, stereo, custom import, Windows hook registration, and audio-loopback checks. It writes `test-results.json` next to the executable, plus audio test files. `--render` renders the control panel and exits, without saving preferences or playing sounds. `--tray` starts in the notification area.

The automated checks do not replace testing with a physical keyboard and mouse or listening to choose your favorite sound. End-to-end desktop UI automation was unavailable in the build environment.

## Reference scope

Clacky's site was inspected on October 1, 2026. Its principal browser previews are synthesized rather than downloadable WAV recordings. Version 1.2 independently implements their noise filtering, fast exponential envelopes, oscillator glides, and layer frequencies for eight profiles. The noise varies each time, and the native output path differs from a browser, so this is a close acoustic recreation, not an official Clacky port or a claim of identical output. This app adds Windows keyboard feedback. Recording, drag effects, gesture shortcuts, stats cards, and mascot features are outside its scope.

No Clacky JavaScript is included or executed by the app; all playback is native and offline. The Gunshot MP3 was supplied by the repository owner and is embedded from `src/assets/gunshot.mp3`.

NAudio is used under its MIT license. See `THIRD-PARTY-NOTICES.txt` in the app folder.
