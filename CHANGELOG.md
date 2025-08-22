# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.9.1] - 2025-08-22

### Fixed

- Mods have apparently decided to call DeepProfiler.Start with null. We didn't expect this. Now we're handling it.

## [0.9.0] - 2025-08-22

### Added

- Startup impact profiling for mod loading and base game processes. This feature provides insights into the performance impact of individual mods and core game loading steps during startup.

## [0.8.0] - 2025-08-20

### Added

- Additional progress window for Faster Game Loading's early mod content loading process. Only shown when the mod is active and can be disabled in the settings.

### Changed

- Attempt to improve mod compatibility by letting other mods' patches run on a specific method that we've taken over. Also, "take over" for Faster Game Loading once the content loading part of it merges with ours, so it's not constantly staying one mod ahead of us, ruining the progress tracking.

## [0.7.3] - 2025-08-10

### Fixed

- Improve active language loading logic so it only tries to load translations once.

## [0.7.2] - 2025-08-09

### Fixed

- Potential source for race condition null reference exception in a certain loading step.

## [0.7.1] - 2025-08-07

### Fixed

- Remove accidentally introduced flickering bug during gameplay.

## [0.7.0] - 2025-08-06

### Changed

- Enhanced "reload content" handling so it's more responsive.
- Made it so the big progress bar also progresses though "one step" while the smaller one does its full range for smoother progress tracking.
- Greatly improve loading progress fidelity in many steps so there are fewer moments of "nothing is happening" during load.

### Fixed

- Remove accidentally left in debug logging.

### Added

- Countdown mode for showing expected loading time, disabled by default, can be enabled in the settings.

## [0.6.0] - 2025-08-05

### Added

- Mod is now fully translatable. Since we're loading very early on, we can't use the game's translation system, so I had to write my own. If you make a translation, and it doesn't work, please let me know so I can investigate.
- Loading time display in the bottom-right corner of the main menu.

### Changed

- Loading time and mod list changes are always tracked now.

## [0.5.1] - 2025-08-03

### Fixed

- Don't allocate extra space for "mods have changed" label when it's not needed.

## [0.5.0] - 2025-08-03

### Added

- Loading time tracking and display features, all of which can be disabled in the settings.

## [0.4.1] - 2025-08-03

### Fixed

- Accidentally made 'top' the default loading window position; now it's 'middle' as it should be.
- Forgot to include translations for new setting.

## [0.4.0] - 2025-08-03

### Added

- Loading window placement setting.

## [0.3.3] - 2025-08-02

### Fixed

- Make sure we're not the mod RimWorld uses for language metadata even if we're loaded first.

## [0.3.2] - 2025-07-29

### Fixed

- Bug in string lookup code.

## [0.3.1] - 2025-07-29

### Changed

- Don't rely on RimWorld for translations as it's unreliable this early in the start-up process.

## [0.3.0] - 2025-07-29

### Added

- Patch for Humanoid Alien Races so it doesn't run its 'load graphics' hook too early since we 'un-hang' the game during the initialization stage, which it relies on for correct timing.

### Fixed

- Add missing stage GenerateImpliedDefs.

## [0.2.1] - 2025-07-28

### Fixed

- Don't hook into delayed execution after the game has already loaded. (Stops constant loading screen flickering.)

## [0.2.0] - 2025-07-28

### Changed

- Made the integration with RimWorld be as uninvasive as possible to reduce the risk of mod incompatibilities.

## [0.1.2] - 2025-07-27

### Changed

- Restore the 'improved' PlayDataLoader patch after figuring out what the issue was. Also add some code to attempt to deal with potential future/uknown issues with other mods and a setting to turn it off again.

## [0.1.1] - 2025-07-27

### Changed

- Improve progress logic so the progress doesn't risk getting stuck.
- Disable the 'improved' PlayDataLoader patch until we figure out why building graphics stop working in the architect menu.

### Fixed

- No longer relocate the information dialog once the game has been loaded, so it shows up where expected when e.g. starting a new game or loading a game.

## [0.1.0] - 2025-07-27

### Added

- First implementation of the mod.

[Unreleased]: https://github.com/ilyvion/loading-progress/compare/v0.9.1...HEAD
[0.9.1]: https://github.com/ilyvion/loading-progress/compare/v0.9.0..v0.9.1
[0.9.0]: https://github.com/ilyvion/loading-progress/compare/v0.8.0..v0.9.0
[0.8.0]: https://github.com/ilyvion/loading-progress/compare/v0.7.3..v0.8.0
[0.7.3]: https://github.com/ilyvion/loading-progress/compare/v0.7.2..v0.7.3
[0.7.2]: https://github.com/ilyvion/loading-progress/compare/v0.7.1..v0.7.2
[0.7.1]: https://github.com/ilyvion/loading-progress/compare/v0.7.0..v0.7.1
[0.7.0]: https://github.com/ilyvion/loading-progress/compare/v0.6.0..v0.7.0
[0.6.0]: https://github.com/ilyvion/loading-progress/compare/v0.5.1..v0.6.0
[0.5.1]: https://github.com/ilyvion/loading-progress/compare/v0.5.0..v0.5.1
[0.5.0]: https://github.com/ilyvion/loading-progress/compare/v0.4.1..v0.5.0
[0.4.1]: https://github.com/ilyvion/loading-progress/compare/v0.4.0..v0.4.1
[0.4.0]: https://github.com/ilyvion/loading-progress/compare/v0.3.3..v0.4.0
[0.3.3]: https://github.com/ilyvion/loading-progress/compare/v0.3.2..v0.3.3
[0.3.2]: https://github.com/ilyvion/loading-progress/compare/v0.3.1..v0.3.2
[0.3.1]: https://github.com/ilyvion/loading-progress/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/ilyvion/loading-progress/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/ilyvion/loading-progress/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/ilyvion/loading-progress/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/ilyvion/loading-progress/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/ilyvion/loading-progress/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ilyvion/loading-progress/releases/tag/v0.1.0
