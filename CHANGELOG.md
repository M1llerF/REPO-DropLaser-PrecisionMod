# Changelog - REPO-DropLaser-LandingLaserMod

## [1.1.0]
### Added
- Ghost preview landing mode with configurable behavior:
  - `0 = never`
  - `1 = on cart`
  - `2 = always`
- New ghost preview tuning options:
  - Custom ghost color support.
  - Ghost opacity setting.
  - Ghost emission intensity setting.
  - Ghost update frame interval setting.
- Config migration support so existing user settings are preserved across schema changes/updates.
- Cart state accessor helpers to improve landing/preview decision logic.

### Changed
- Reworked drop beam update flow for more stable ghost preview behavior.
- Added vertical lock handling during preview updates.
- Reduced default laser light intensity by 50% for a less overpowering glow.
- Refactored controller lifecycle for cleaner activation/deactivation handling.

### Fixed
- Prevented the laser from rendering above objects during object intersections.
- Fixed logger info output to correctly print the provided message.

## [1.0.0]
### Added
- Initial public release.
- Adds a downward-pointing laser when holding objects.
- Laser dynamically shows where the held object would land if dropped.
- Configurable options for:
  - Enabling/disabling the laser system.
  - Customizing laser color (match grab beam or set your own).
  - Adjusting laser start width, end width, and max scan distance.
  - Adjusting laser glow light intensity and range.
  - Choosing a custom toggle key (default `L`).
  - Auto-enable laser when grabbing an object.
- Multiplayer support: laser is local-only and behaves correctly in multiplayer sessions.
- Scene transitions handled cleanly with no leftover objects or memory leaks.
- Fully optimized for minimal CPU and GPU impact.

### Fixed
- No fixes required

---

# Notes
- Fully compatible with BepInEx 5.4.
