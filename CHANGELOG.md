# Changelog - REPO-DropLaser-LandingLaserMod

## [1.1.5]
### Fixed
- Linked saga lifecycle spam logs to `EnableLogging` so cut-off/recovery chatter is suppressed when logging is disabled.
- Linked ghost-preview warning chatter (including unavailable/no-renderer ghost cases) to `EnableLogging` so normal play no longer floods the log/chat.

## [1.1.4]
### Fixed
- Reworked ghost preview instantiation to use a visual-only ghost hierarchy instead of cloning full gameplay objects.
- Prevented runtime side effects from cloned object scripts/components during ghost creation (including `Awake()`-time null-reference crash paths such as enemy component initialization).
- Added guard behavior to skip ghost preview safely when a held object has no supported renderers.

## [1.1.3]
### Added
- The 3x3 bounds raycasting system was not great. Replaced with bottom-face downward casting.

### Fixed
- README should now display images and link to video on Thunderstore.
## [1.1.2]
### Added
- None

### Fixed
- README should now display images and link to video on Thunderstore.

## [1.1.1]
### Added
- New debug visualization architecture for cart hitbox inspection:
  - Dedicated beam debug service.
  - Cart hit-ray visualizer.
  - Cart part highlighter with forward/backward cycling.
- New debug/runtime config options:
  - `EnableDebugVisuals`
  - `EnableHitDiagnostics`

### Changed
- Refactored beam debug logic out of the main beam runtime path for cleaner architecture.
- Unified configured key parsing through a shared input helper.
- Debug stop override now uses the configured `DebugStopOverrideKey` consistently.
- `EnableDebugVisuals` now defaults to `true`.

### Fixed
- Fixed Harmony input prefix behavior so base grabber update is not skipped when toggling without a held object.
- Consolidated duplicate `ReleaseObject` patch behavior into a single release flow.
- Removed unused ghost projection subsystem that was not wired into runtime.
- Wired previously unused ghost configs into runtime behavior:
  - `UseCustomGhostColor`
  - `CustomGhostColor`
  - `GhostEmissionIntensity`
- Applied release hygiene cleanup (`.gitignore` newline and text normalization).

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

## [1.0.1]
### Added
- None

### Fixed
- Corrected Manifest

---

# Notes
- Fully compatible with BepInEx 5.4.

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
