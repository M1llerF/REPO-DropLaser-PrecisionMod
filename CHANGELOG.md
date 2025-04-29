# Changelog - REPO-DropLaser-LandingLaserMod

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