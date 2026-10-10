# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Deferred initialization tasks are now credited to the mod whose def they set up. The game queues one such task per def for its graphics and references, all from its own code, so every one of them was listed under 'not directly related to mods' no matter whose def it was; a framework's per-def work now goes to the def's mod as well. Contributed by [beverage](https://github.com/beverage).
- The part of the startup time no timed step accounts for is now broken down by loading stage: it has a section and a bar of its own under the base game's, one segment per stage, and hovering the top bar's remaining segment lists its ten largest parts, largest first. Stored sessions keep the breakdown. The top bar's segments carry their times.
- Startup impact now also measures to the moment the main menu is ready to use, not only to the point loading finishes, and times the long events that run in between (the game's own interface initialization and the windows and setup other mods queue for after loading) and, while 'Patch late-loading initialization code' is on in the settings, the deferred tasks queued then, crediting each to the mod that runs it. Time the game spends paused in the background is left out: once loading is over, the game stops while another window has the focus unless 'Run in background' is on. The startup time the window shows is now the time to that frame, which the bar beneath it covers, and the history picker lists the same figure, with a note on a session not timed to it, such as an older one timed only to the end of loading. The time stored as the session's loading time is still the point the clock stops, so external tools that read the report file see the same loading time as before. The per-mod and base-game totals in that file now include what was timed after loading, the long events and the deferred tasks, so together they can come to more than it. What came after that point and nothing timed accounts for is listed with the remaining time.
- Four stretches that no timed step used to cover now have owners: the game's own pass over the static constructors after Loading Progress has run them exists so other mods' hooks on that call still fire, so its cost is those hooks, and each hook is now timed under the mod it belongs to for the duration of that call (a hook that cannot be timed on its own is named on the pass's heading instead), and the time Loading Progress takes to set up and remove that timing is listed with the remaining time as an entry of its own; the forced garbage collection and asset unload at the end of loading have a heading of their own under the base game; so does the development-mode check for missing StaticConstructorOnStartup attributes; and Loading Progress's own constructor, which the patch that times every other mod's constructor can never see, is credited to Loading Progress.
- The time other mods' Harmony patches take while the game loads is now credited to the mod each patch belongs to, under 'Harmony hooks on' and the patched method's name. A patch that runs in place of the method it patches, such as one that takes over the game's texture loading, leaves its time with the step it took over, labelled as replaced by that mod, for example 'Loading textures (replaced by Graphics Settings+)'. When the running Harmony is not a version this was made for, or timing a patch fails, patches run untimed as before and a warning in the log says why.

### Changed

- The top bar's 'Untracked' segment, the part of the startup time no timed step accounts for, is now called 'Remaining'.
- The loading window now stays on screen, with its clock running and its activity line naming each long event, until the main menu is usable (or a quicktest has gone straight into a game), instead of leaving when the interface begins initializing and handing the rest of the wait to the game's own status box, which reads '...' for an event with no text. The loading time it records for its estimate runs to the menu, leaves out time the game sat paused in the background, and matches the startup time the startup impact window shows, and so does the 'Game took X to load' figure in the main menu's corner, the pause menu and the mod settings; the history of earlier, shorter samples is cleared once. A startup that goes straight into a game records no loading time, since it never reaches the menu, and neither does one whose menu has not settled five minutes after loading, as when a mod keeps a long event queued on it: the window leaves anyway, and the corner, the pause menu and the settings say that no time was recorded.
- The startup impact window's sections for the base game and for the remaining time start as a heading line each, and open to their bars on a click that is remembered. Each heading shows its total and one detail: the base game's time on other threads while that bar is shown, or else its largest step, and the largest remaining entry. Hovering a closed heading, or the matching segment of the top bar, lists the largest steps or stages, and how many more there are when it leaves some out. The segments of every bar for time on other threads say so, in the phase grouping too. The HTML export's two sections fold too, with the same headings and tooltips.
- With automatic saving on, the startup impact session is now saved once, when the startup reaches the main menu or goes into a game, instead of when loading finishes. A startup that stops in the wait between the two is recorded as one that never finished, stopped after loading.
- Every bar segment in the startup impact window and the HTML export now has a colour: a heading that groups entries, such as the delayed initialization tasks, takes its group's colour, or one of its own when the group has none, instead of plain gray.
- A row's bar for time on other threads in the startup impact window is now shown only when it would be at least a few pixels wide, so rows no longer carry slivers too thin to read or hover.

### Fixed

- Faster Game Loading went unrecognized when its Workshop copy carried the '_steam' package id suffix the game adds while a local copy with the same id is installed. Its early content loading was then neither shown in the loading window nor taken into account when loading content.
- Each pair of bars for time on the loading thread and time on other threads, the base game's and every mod's, is now drawn on one scale, so the longer of the two spans the width and the other is drawn in proportion. The loading-thread bar used to fill the width on its own scale, so an off-thread total above it read as equal. The HTML export does the same.
- A deferred initialization task that threw left its startup impact category open: the time it had run was never recorded, and every category its mod, or the base game, started afterwards ran inside it.
- A startup impact category started while another was open for the same mod, or for the base game, lost the open one's time up to that point, so the open one was credited only with what it ran after the other stopped.
- A timed step that threw, such as a mod constructor that failed or a mod whose defs could not be loaded, left its startup impact category open, so the time it had run was never recorded.
- A mod's step that ran inside a base-game step, as a mod's XML inheritance registration does while the base game parses the XML, was counted in both the mod's startup impact and the base game's, so together they could come to more than the time they took. The step that ran is now credited with it, and the one around it leaves that stretch out, as a category on the same timer already did.

## [0.17.1] - 2026-10-08

### Fixed

- Fixed an error during startup that could stop the loading window from showing progress while mods' content was being loaded.

## [0.17.0] - 2026-10-03

### Added

- The mod list in the startup impact window can now be grouped by loading phase instead of by mod, using the new 'Grouping' button beside the filter. Each row is then a phase, such as 'Loading textures' or 'Running static constructors', showing the total time all mods together spent in it, sorted by which phase took the longest. Each row's bar shows how that time splits between the mods. The filter and hidden mods apply here too, so you can see where a particular group of mods spends its time. Exported HTML reports have the same option.

### Changed

- Removed the Russian translation. It had gone unmaintained apart from machine translation for a while and I'm tired of the maintenance burden. If someone wants to pick it back up, releasing it as a standalone translation mod is the way to go.
- The loading window no longer forces an immediate repaint on every loading stage change and before every step of reloading a mod's content, since all that repainting slowed loading down. The label shown can lag behind a slow step by a moment as a result. The old behavior can be turned back on with the new 'Repaint the loading window immediately on stage changes and content reload steps' setting.
- The loading time shown in the bottom-right corner of the main menu, in the pause menu and in the mod settings is now how long this launch took to load. It used to show the same averaged estimate the loading window counts down from.
- Times of a minute or longer in the startup impact window and in exported HTML reports now include minutes, and hours from an hour up, such as 5:24.3 or 1:02:05.4 rather than 324.3 s. Shorter times are shown in seconds or milliseconds as before. The new 'Show startup impact times in seconds and milliseconds only' setting brings back the old format.

### Fixed

- The startup time in the startup impact window now covers the whole load, including the final cleanup the game does right before the main menu appears. It used to stop just short of it, so it came out lower than the loading time shown on the main menu.
- When viewing a saved run in the startup impact window, the 'Saved run from' caption overlapped the 'Use logarithmic scale' checkbox. It is now shown at the bottom left, beside the buttons, and gives way to status messages while they are showing.

## [0.16.0] - 2026-09-22

### Added

- Startup impact sessions are now kept as a history instead of only the most recent one. Sessions reach it from automatic saving and from the Save button on the startup impact window, so a history builds up whether or not automatic saving is on. A 'Manage saved sessions' button in the startup impact settings opens a picker listing what has been kept, showing when each run happened, how long it took, how many mods it loaded and a hash of the mod list it ran under, so two runs can be told apart as comparable or not. Any run can be opened in the startup impact window, pinned so it survives past the limit, marked as the baseline everything is compared against, or deleted. How many to keep is configurable. 'StartupImpactData.xml' is still written at the same point in startup and in the same format, with two elements added to the session it holds, so external tools such as RimSort read it as they did before. Contributed by [beverage](https://github.com/beverage).
- Startups that never finish are now recorded, independently of the automatic saving setting, since a startup that never finished cannot be saved by hand afterwards. One that hangs or crashes partway through previously wrote no report at all, which lost the runs with the most diagnostic value. A small marker is now kept while the game loads and removed once it finishes, so one still present at the next startup becomes a history entry recording when that startup began and which loading stage it reached. There is no per-mod breakdown for such a run, because the game stopped before there was one. This can be turned off.
- Added a 'Middle (inverted)' loading window placement option, for anyone who prefers the loading window below the game's own tip/mod-summary panel instead of above it.

### Changed

- The Load button on the startup impact window is now History, and opens the saved session picker. It used to re-read 'StartupImpactData.xml'; that file is still written exactly as before and still readable by the external tools that consume it, but it is not itself one of the kept sessions, so a report written by an earlier version is no longer reachable from the window.
- The Save button on the startup impact window is now shown only when automatic saving is off, and only when tracking was on for the startup being saved. With automatic saving on, the report file is rewritten every startup and the session is kept in the history, so the button had nothing left to do.
- The mod settings screen now scrolls, so it is no longer limited to the number of rows the settings window can show at once.
- The Middle loading window placement now shows the status box and loading window above the game's own tip/mod-summary panel instead of below it, with the status box on top of the loading window. The previous order is still available as the new 'Middle (inverted)' option.

### Fixed

- The Save button on the startup impact window wrote whichever session was on screen to 'StartupImpactData.xml'. Opening a stored session and pressing Save therefore replaced the latest report with an older run, which external tools such as RimSort go on to read as the most recent load. It now always writes the current session.
- The spacing between the loading window, its status box and the FasterGameLoading window, and the gap toward the game's own tip/mod-summary panel, used a slightly narrower gap than the game itself uses between its own tip window and mod/DLC list, making everything look inconsistently spaced. All of it now uses the same gap the game does.
- The main progress bar sometimes visibly jumped backward instead of only ever moving forward, whenever the current stage's inner progress went further than expected. It now always advances, or at worst stands still, never backward.
- The in-game loading window (shown during in-game world generation, map generation and save loading, when the setting for it is on) could flicker backward for a single frame right as it moved from one stage to the next, since the stage and its progress bar were briefly readable in a mismatched combination. They are now always updated together, so this can no longer happen.
- Loading a save or starting a new game filled the in-game loading window's bar with the game's asset loading progress and then dropped it back to empty once the assets were loaded, since that progress was being shown as part of the stage that follows it rather than as a stage of its own. Loading assets is now its own stage, listed ahead of reading the file or setting up the map, so the bar carries straight on from it instead of falling back.
- The in-game loading window's activity line sometimes showed internal, code-like text (such as 'Scribe.loader.FinalizeLoading', 'listerFilthInHomeArea.RebuildAll()', a raw generation-step name like 'ElevationFertility' or 'AncientJunkClusters', or an internal renderer layer name like 'WorldDrawLayer_Hills') instead of something a player could make sense of. Generation steps added by other mods aren't covered by this and will still show their raw, technical name.

## [0.15.0] - 2026-09-14

### Added

- The loading window now also appears during in-game world generation, new-game map generation, loading a save, settling on a tile or setting up a camp from a caravan, and the planet's map mesh regeneration (e.g. right after generating a new world or opening the world map after loading a save), showing the current activity and elapsed time. Where possible, it shows the current phase (e.g. reading the file, loading maps, spawning things, spawning your colonists onto a new map) along with a progress bar tracking how far along that phase is, or, for map mesh regeneration, how many of the planet's layers have finished. For map generation that freezes the game with no opportunity to show live progress, such as arriving at a caravan site, being attacked while away from home, ambushes, caravan meetings and demands, peace talks, escaping by ship, transport pods arriving, gravship landings, and the new colony quest, the loading window instead briefly appears without live progress.
- Added a feature to keep the in-game loading window responsive while the map or world renderer regenerates after generation or loading finishes, instead of it appearing to freeze, with a progress bar tracking how much of it is done. This applies to new games, loaded saves, settling or generating maps while playing, and world generation. There's an option to turn it off in case it causes misbehavior.

### Fixed

- Opening the startup time dialog when startup loading impact tracking wasn't enabled when the game started no longer shows a confusing screen full of zeroes with an easy-to-miss note in the corner. It now clearly explains that tracking was off for this session and offers a button to turn it on for the next startup.

## [0.14.0] - 2026-08-02

### Added

- The loading window now shows the current managed heap size and the game process's total memory usage, next to the title. This can be turned off in the settings. Implements [#9](https://github.com/ilyvion/loading-progress/issues/9).

### Fixed

- Fixed a crash while tracking the 'giving short hashes to defs' stage's progress when a loaded mod's XML document had no root element. Contributed by [Spagles](https://github.com/Spagles).
- Fixed a crash on startup when a mod referenced a language for translations that doesn't actually have a folder for it. Contributed by [Spagles](https://github.com/Spagles).
- Mod translations are now found using the same rules RimWorld itself uses: both the active language's canonical folder name (e.g. 'Polish (Polski)') and its legacy name (e.g. 'Polish') are checked, and translations packed as a '.tar' archive are now picked up correctly.

## [0.13.2] - 2026-08-01

### Changed

- The startup message now includes the mod's version number.

### Fixed

- The Humanoid Alien Races loading patch was not active when the Humanoid Alien Races ~ Dev mod (package ID 'erdelf.humanoidalienraces.dev') was active instead of the regular mod.

## [0.13.1] - 2026-07-25

### Changed

- The 'Giving short hashes to defs' stage now shows a progress bar tracking how many defs have been given a short hash, instead of appearing to hang for its full duration with no feedback.

### Fixed

- The loading window could appear frozen on a stale message (e.g. stuck showing 'Giving short hashes to defs') for a long stretch while something else entirely was actually taking a long time in the background. The window now updates immediately whenever the loading stage changes or a mod's content starts reloading, so it correctly shows what's actually responsible for a long pause instead of whatever was on screen before it started.
- The very end of loading (calling all static constructors, baking texture atlases, and running garbage collection) ran as one uninterrupted block, freezing the loading window for however long all of it combined took. Each of these steps is now tracked and shown separately, so the window keeps updating throughout.

## [0.13.0] - 2026-07-25

### Added

- The Startup Impact window's mod table now has a filter box, so you can quickly find a specific mod's impact by name or package ID instead of scrolling through the whole (potentially very long) mod list.
- The Startup Impact window now shows a small summary of how much work the last load actually did: the number of defs parsed, patch operations applied, and mods loaded. This is also saved to the startup impact report file for external tools to read.
- The 'Loading window placement' setting now has a 'Custom' option. Picking it adds a 'Set custom position...' button that lets you drag a life-size stand-in for the loading window to wherever you want it on your screen. The chosen position is remembered proportionally, so it stays in the same relative spot even if you later change your screen resolution.
- The Startup Impact window now has a 'Detail' slider next to the 'Use logarithmic scale' checkbox, shown only while that option is enabled. It lets you tune how strongly the logarithmic scale compresses large values, so you can make small differences between fast mods easier to see, or keep big impacts looking proportionally larger.
- The Startup Impact window has a new 'Export HTML' button next to Save/Load. It writes a self-contained HTML report (StartupImpactReport.html, next to your save data) that looks like the in-game window itself, including working mod filtering, mod-visibility toggling and logarithmic scale controls. Unlike the existing Save button, which writes a format only this mod can read back in, the exported file can be opened in any browser or shared without anyone needing RimWorld running to view it.
- The Startup Impact window's mod table now has clickable 'Mod' and 'Impact' column headers, so you can sort the list alphabetically by mod name or by startup impact, in either direction, instead of always seeing it in fixed highest-impact-first order.
- The Startup Impact window now shows a third stacked bar, right below the 'Mods startup impact' heading, with all your mods pitted against each other in a single bar (largest impact first, colored per mod, hoverable for a name and time tooltip) in addition to the more detailed table below it.
- New opt-in setting, 'Show base game's background-thread startup impact' (off by default), that adds a second bar under the base game's impact bar in the Startup Impact window and exported HTML report, breaking out the base game's background-thread startup work separately from its main-thread work.

### Fixed

- The mod settings menu could crash with an error when opened before the mod had recorded any loading times (e.g. right after a fresh install). The loading time row is now hidden until at least one load has been recorded.
- The 'show last loading time' corner display could spam errors every frame on the main menu (and after returning to it) when no loading time had been recorded yet. It is now simply not shown until a loading time has been recorded.
- The Faster Game Loading progress window could crash the loading screen if Faster Game Loading's internal data couldn't be read at all, or couldn't be read yet (e.g. after a Faster Game Loading update changes its internals). It now falls back to showing 0 mods loaded instead.
- The warning logged when a mod calls a loading-related game API incorrectly (with a blank label) could itself crash the loading screen in rare cases instead of just printing the warning.
- The content-reload detection used to show detailed progress while mods reload could crash the loading screen if a future game update changed how that code is compiled internally. It now falls back to showing reduced progress detail instead.
- Progress bars (e.g. the 'Applying XML patches' bar when a mod list has no patches to apply) could render corrupted or invisible when their maximum value was 0, instead of just showing as empty. This is a very rare situation and would only realistically happen if Core itself was replaced with a mod that used 0 of any of the loading events.
- The Russian translation was missing the settings labels for the 'automatically save startup impact report' option added in 0.12.0, so it fell back to English for those two lines. Russian translations for both have been added.
- The 'basegame' bar in the Startup Impact window's profiler could show stale, duplicated values after hiding or showing a mod in the list, instead of reflecting the current numbers.
- Startup impact timing recorded for work done on background threads (e.g. while mods are being parsed in parallel) could be undercounted, since simultaneous updates from different threads could overwrite each other instead of adding up.
- The base game's share of background-thread startup work was never saved or exported, even though the equivalent per-mod background-thread work was.
- A malformed or corrupted translation file from any loaded mod could crash the loading screen while translations were being loaded. Such a file is now skipped (with a warning logged) instead.
- The loading window's 'Loading progress' header was always shown in English regardless of your game's language. It's now translated like the rest of the window (Russian translation included).
- Since 0.12.0, the 'current activity' label almost always showed the generic `ExecuteToExecuteWhenFinished()` instead of naming the specific delayed-initialization task actually running. This was a side effect of the 0.12.0 fix for stalls being attributed to the wrong mod; the correct task name is shown again now.
- The progress bar shown while Faster Game Loading loads mod content early ignored your custom progress bar colors from the settings menu, always using the default colors instead. It now matches the main loading window's bar.
- The Startup Impact window now always goes back to showing the current session's data once closed, instead of potentially still showing a previously loaded report if reopened.
- The Startup Impact window's exported HTML report was always in English, even for players using a non-English game language where the in-game window itself was fully translated. The exported report now uses the same language as the game.

## [0.12.0] - 2026-07-24

### Added

- New opt-in setting to automatically save the startup impact report to `StartupImpactData.xml` after every game startup, so external tools (such as the RimSort mod manager) can display each mod's startup load time without requiring a manual save from the startup impact window. ([#2])
- Startup impact reports now record each mod's package ID alongside its name, allowing external tools to match report entries to mods reliably. ([#2])

### Fixed

- The loading screen could name the wrong mod when a slow static constructor, delayed-initialization task, or content-reload step caused a long stall; it would instead display whatever came right after it. The stall is now attributed to the mod actually responsible. ([#6])
- The 'current activity' label could get stuck on a stale sub-step (e.g. staying on 'Loading strings for...' long after strings had finished loading) instead of returning to what was actually running. ([#6])
- The 'Applying XML patches' and 'Loading defs' progress bars could occasionally run past their maximum instead of stopping there.
- Fixed a rare crash ('Stack empty') that could occur while resolving cross-references during loading.

## [0.11.0] - 2026-06-08

### Added

- Since some mods mess with the main menu so much it makes our 'DrawInfoInCorner' label still inacessible, a button for showing the dialog has now been added to the mod settings as well.

## [0.10.0] - 2026-05-05

### Added

- The loading time estimate is now based on a weighted average of the last N recorded load times (default 10) instead of only the most recent one. The 10 most recent launches are given progressively increasing weight (1× to 10×); older launches beyond those 10 all contribute equally at 1×, so no historical data is ever completely discarded.
- A new setting controls how many previous load times to store (range 1–50, default 10). Its tooltip explains the weighted average scheme in plain language.
- The loading screen now shows a small 'estimate based on N previous game launches' label so players know how many data points the current estimate draws from.
- Added RimThemes as incompatible mod to About.xml
- The colors of the main progress bar and its sub-stage indicator can now be customized in the settings. Each color opens a full HSV/RGB color picker dialog.

### Changed

- Change loading time renderer to use finalizer patch to always show even if other mods mess with the 'DrawInfoInCorner' method.

### Fixed

- Loading times of 1 hour or longer are now displayed correctly (e.g. '1:23:45' instead of '23:45').
- When the game is running, render the loading time in the main menu drawer instead of on top of the game tab bar.

## [0.9.6] - 2026-02-21

### Added

- Russian localization, thanks to [Aks](https://steamcommunity.com/id/aks_kun/).

## [0.9.5] - 2025-08-26

### Fixed

- Missed a finalizer case. Luckily, it's the least likely one to be used.

## [0.9.4] - 2025-08-26

### Fixed

- Found and fixed edge case in mod constructor Harmony patching that caused certain mods to stop working as expected.

## [0.9.3] - 2025-08-22

### Fixed

- The mod should work fine with startup impact profiling enabled again now, as the cause of the problem has been addressed. For the sake of avoiding causing issues for people, I'm going to leave the setting disabled for now still.

## [0.9.2] - 2025-08-22

### Changed

- Set startup impact profiling to disabled by default until we figure out why it's causing problems for people.

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

[Unreleased]: https://github.com/ilyvion/loading-progress/compare/v0.17.1...HEAD
[0.17.1]: https://github.com/ilyvion/loading-progress/compare/v0.17.0..v0.17.1
[0.17.0]: https://github.com/ilyvion/loading-progress/compare/v0.16.0..v0.17.0
[0.16.0]: https://github.com/ilyvion/loading-progress/compare/v0.15.0..v0.16.0
[0.15.0]: https://github.com/ilyvion/loading-progress/compare/v0.14.0..v0.15.0
[0.14.0]: https://github.com/ilyvion/loading-progress/compare/v0.13.2..v0.14.0
[0.13.2]: https://github.com/ilyvion/loading-progress/compare/v0.13.1..v0.13.2
[0.13.1]: https://github.com/ilyvion/loading-progress/compare/v0.13.0..v0.13.1
[0.13.0]: https://github.com/ilyvion/loading-progress/compare/v0.12.0..v0.13.0
[0.12.0]: https://github.com/ilyvion/loading-progress/compare/v0.11.0..v0.12.0
[0.11.0]: https://github.com/ilyvion/loading-progress/compare/v0.10.0..v0.11.0
[0.10.0]: https://github.com/ilyvion/loading-progress/compare/v0.9.6..v0.10.0
[0.9.6]: https://github.com/ilyvion/loading-progress/compare/v0.9.5..v0.9.6
[0.9.5]: https://github.com/ilyvion/loading-progress/compare/v0.9.4..v0.9.5
[0.9.4]: https://github.com/ilyvion/loading-progress/compare/v0.9.3..v0.9.4
[0.9.3]: https://github.com/ilyvion/loading-progress/compare/v0.9.2..v0.9.3
[0.9.2]: https://github.com/ilyvion/loading-progress/compare/v0.9.1..v0.9.2
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
[#2]: https://github.com/ilyvion/loading-progress/issues/2
[#6]: https://github.com/ilyvion/loading-progress/issues/6
