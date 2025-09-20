# Changelog

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