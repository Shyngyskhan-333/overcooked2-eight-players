# Changelog

All notable changes to this project are listed here. Format: [Keep a Changelog](https://keepachangelog.com/), versions follow semver.

## [0.1.1] - 2026-10-12 (prototype)

Testing pass: the "not tested" rows of the 0.1.0 status table were run in the game, which found and fixed two bugs.

### Fixed
- **Versus only spawned 4 chefs** for 8 players (the extra-chef code was attached to the co-op level loader only). Versus now spawns chefs per team, so 4 against 4 gets four chefs on each side, and registers the new chef ids with the team lists.
- **Respawn bubble crash for players 5 to 8.** After a chef fell into a pit, `RespawnCounterUIController` indexed a 4-entry sprite array with the player number and threw `IndexOutOfRangeException` every frame (4392 log entries in one test). The player number is now wrapped like the ring colour. Verified: same scenario, 0 exceptions.

### Added
- Test commands for the sim layer: `find`, `state`, `tp`, `tpface`, `tpnear`, `uistate`.

### Verified in the game (simulated controllers)
- Pickup, place, chop, plate, serve and score with players 5 and 8; time's up and co-op results screen with 8 players; Versus team select, match and results; Arcade level; quit to menu and a second session; controller unplugged and re-attached; "unlock all chefs" (60 available on, 38 off).

### Known problems found
- The Chef tab does not show 8 chefs or 8 arrow pairs (see README, Known issues).

## [0.1.0] - 2026-10-12 (prototype)

First public release. Tested on one game build only (see README, "Status").

### Added
- Preloader patcher that raises the local player limit from 4 to 8 by rewriting `Assembly-CSharp` in memory at start-up.
- Runtime plugin: 2x4 lobby grid, 8 pause-menu chefs, chefs 5-8 spawned next to the originals, extra camera headroom, colour table for 8 players.
- Chef ring colours for players 5-8 match the apron colour (player number modulo 4).
- `Unlocks/UnlockAllChefs` (default on): shows every chef as unlocked using the game's own debug flag. Not tested in game yet.
- `scripts/install.ps1`, `scripts/uninstall.ps1`, `scripts/build.ps1`, `scripts/package.ps1`.
- Optional test aid `Debug/SimPads`: scripted virtual controllers, off by default.

### Known limits
- Tested with simulated controllers only; no physical controllers 5-8.
- Online play with unmodded clients does not work (message layouts were widened).
