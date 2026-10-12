# Changelog

All notable changes to this project are listed here. Format: [Keep a Changelog](https://keepachangelog.com/), versions follow semver.

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
