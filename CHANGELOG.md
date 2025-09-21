# Changelog

## [1.2.1] - 2025-09-21

### Changed

- manifest.json

## [1.2.0] - 2025-09-21

### Changed

- Safe warp handling. Made sure to check if the player is already in a safe warp state before applying a new safe warp. This prevents conflicts and ensures that players are not inadvertently moved to incorrect positions.
- Checking for ground before warping. If the player is not on the ground, the safe warp will not be applied, preventing potential issues with mid-air warps.

## [1.1.2] - 2025-09-20

### Changed

- Late join handler to use cache for room data, improving performance and reliability.

## [1.1.1] - 2025-09-20

### Changed

- README.md

## [1.1.0] - 2025-09-20

### Fixed

- Saving previous room data for late joiners. If a player is dead and late joins, they will now correctly respawn as dead instead of alive.
- Proper handling of player states when late joining, ensuring that players are correctly positioned and oriented based on the current game state.

## [1.0.1] - 2025-09-19

### Changed

- README.md

## [1.0.0] - 2025-09-18

### Added

- Late join handler