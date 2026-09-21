[![RimWorld 1.6](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)](http://rimworldgame.com/) [![Build](https://github.com/ilyvion/loading-progress/actions/workflows/ci.yml/badge.svg)](https://github.com/ilyvion/loading-progress/actions/workflows/ci.yml)

**Loading Progress** is a RimWorld mod that adds a detailed loading progress window to the game. It visually tracks each stage of RimWorld's startup process, showing which step is currently running, along with your managed heap size and the game process's total memory usage next to the title. This makes waiting for RimWorld to start slightly more interesting. The mod is especially useful for those with large mod lists. No configuration required—just install and enjoy better insight into your game's loading process!

## In-game loading progress

The progress window isn't just for startup anymore. It also appears during in-game world generation, new-game map generation, loading a save, settling or setting up camp from a caravan, and the planet's map mesh regeneration, showing the current activity, elapsed time and, where possible, a progress bar for the current phase. For situations that freeze the game with no opportunity to show live progress (caravan arrivals, ambushes, transport pods, gravship landings, and similar), the window instead briefly appears without live progress.

## Loading time impact tracking

Loosely based on the mod Startup Impact, the mod can track and show what mods are having what impact on your loading times. Click the loading time summary text in the bottom-right corner of your screen on the main menu to see the stats, including a sortable, filterable per-mod breakdown, a stacked comparison bar, and an option to export a self-contained HTML report you can open in any browser or share without RimWorld running. Saved sessions are kept as a history rather than only the most recent one, so two launches can be compared, and a launch that hangs or crashes partway through is recorded too, noting the loading stage it stopped at. This feature is disabled by default; enable it in the mod's settings if you'd like to use it.

## Mod compatibility

### Known incompatibilities

- [RimThemes](https://steamcommunity.com/sharedfiles/filedetails/?id=1668983184) — Makes this mod useless, because it just covers the whole loading process with a splash screen.
- [Startup Impact Continued](https://steamcommunity.com/sharedfiles/filedetails/?id=3544131315) — Messes with the startup process in ways that ruin this mod's progress and startup impact tracking, and this mod now has its own startup impact tracking anyway, so it's doing double duty.

## Troubleshooting

If you encounter any issues, try disabling the **Enable patching of initialization code** setting of Loading Progress to remove the most likely source of incompatibility. If that still doesn't work (and frankly, even if it does), please [leave a bug report](https://github.com/ilyvion/loading-progress/issues) so it can be addressed.

## Translations

The mod is fully translatable. Since it loads very early on, it can't use the game's translation system because translations are literally not loaded yet, so it has its own. It's supposed to be fully compatible with the regular RimWorld translation organization system, so if you make a translation and it doesn't work, please open an issue so it can be investigated.

### Available translations

- Russian (built-in), originally by [Aks](https://steamcommunity.com/id/aks_kun/), since maintained by machine translation.
- [Chinese](https://steamcommunity.com/sharedfiles/filedetails/?id=3542771983) by [大番薯之怒](https://steamcommunity.com/profiles/76561198293334177)
- [Polish](https://steamcommunity.com/sharedfiles/filedetails/?id=3775645721) by [Drizzt](https://steamcommunity.com/id/drizztgaming4life)

Want your translation added to this list? Release it as a standalone translation mod (i.e. a mod with only a `Languages` folder, not a copy of this entire mod plus the translation) and let us know, and it'll be added here.

## License

Licensed under either of

- Apache License, Version 2.0, ([LICENSE.Apache-2.0](LICENSE.Apache-2.0) or http://www.apache.org/licenses/LICENSE-2.0)
- MIT license ([LICENSE.MIT](LICENSE.MIT) or http://opensource.org/licenses/MIT)

at your option.

`SPDX-License-Identifier: Apache-2.0 OR MIT`

### Contribution

Unless you explicitly state otherwise, any contribution intentionally submitted
for inclusion in the work by you, as defined in the Apache-2.0 license, shall be
dual licensed as above, without any additional terms or conditions.
