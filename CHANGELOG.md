# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/ilyvion/loading-progress/compare/v0.4.0...HEAD
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
