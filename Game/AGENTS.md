# Pokkat Game - Agent Context

this document captures codebase knowledge, code style, and current development context for ai agent continuity.

## project overview

**pokkat** is a unity ar game using ar foundation for ios/android. the game features neko (cat) characters that can be spawned via image tracking, interact with bowls, and navigate on detected surfaces.

### package dependencies

- `com.unity.xr.arfoundation` - ar foundation for cross-platform ar
- `com.unity.xr.arkit` / `com.unity.xr.arcore` - platform-specific ar providers
- `com.unity.ai.navigation` - runtime navmesh baking (NavMeshSurface)
- `com.unity.inputsystem` - new input system for touch/mouse detection

## architecture

the PokkatCore namespace uses a **singleton pattern** for `CoreGameplay` (global access via `CoreGameplay.Instance`), **dependency injection** (via inspector assignments), and **observer patterns** (events/callbacks).

### core components

| class | role |
|-------|------|
| `CoreGameplay` | singleton coordinator managing neko spawning, game state, image following, and multi-image detection |
| `ImageHandling` | wrapper for ARTrackedImageManager events, fires OnImageDetected/OnImageLost |
| `PlaneHandling` | wrapper for ARPlaneManager with area threshold, SpawnClosest utility, touch detection, and OnPlaneInteraction |
| `Logkat` | static logger with spam prevention (1s cooldown) for consistent "(Pokkat)" prefixed output |
| `AREntityNeko` | neko entity with texture loading, blinking, and procedural Walk/Jump/Fall animations |
| `AREntityBowl` | bowl entity stub (food stages and consumption logic - not yet implemented) |
| `Statskeeper` | persistence stub (json-based hunger/happiness - not yet implemented) |

### data flow

1. `PlaneHandling` fires `OnPlaneReady` when sufficient plane area detected (default 1.0m²)
2. `PlaneHandling` fires `OnPlaneInteraction` when user taps on a tracked plane
3. `PlaneHandling` fires `OnPlanesUpdated` whenever planes change (for navmesh baking)
4. `ImageHandling` fires `OnImageDetected` when tracking images (TrackingState.Tracking only)
5. `ImageHandling` fires `OnImageLost` when images are removed from tracking
6. `CoreGameplay` spawns neko via `PlaneHandling.SpawnClosest()` or at image position if no plane
7. Most recently spawned neko follows the tracked image (position updated in CoreGameplay.Update)
8. When image is lost, `CoreGameplay` calls `AREntityNeko.Fall()` to drop neko to ground

### multi-image spawn feature (multispawn & multitrack rewrite dec 18 2024)

arcore/arkit cannot distinguish between multiple physical prints of the same reference image (they share the same trackable id). the system now uses a `TrackedNekoInstance` class to decouple neko state from unity's trackable id limitation.

**TrackedNekoInstance class:**
- `entity` - the AREntityNeko component
- `gameObject` - the spawned gameobject (for cleanup)
- `anchorPosition` - last known stable position (spawn point or landing point)
- `isFollowing` - true if currently following the tracked image
- `isGrounded` - true if landed on a plane

**multispawn logic (in OnImageDetected):**
1. if a neko is already following the image:
   - calculate distance from current image position to following neko's anchor
   - if distance > `multiImageDistanceThreshold` (0.25m): position jump detected!
   - ground the current neko (trigger fall) and spawn a new one at the new position
2. if no neko is following:
   - check distance from image position to all grounded neko anchors
   - if far from all grounded nekos: spawn a new neko (max 3)
   - if near an existing grounded neko: do nothing (same physical card, just AR re-detection)

**multitrack logic (in Update, two phases):**
1. **phase 1 (following):** find the following neko, if image is `TrackingState.Tracking` sync position, else trigger fall
2. **phase 2:** ground stabilisation now handled by AREntityNeko.Update() with timer-based plane projection

**grounded nekos stay grounded:** once a neko has landed, it does not re-attach to a newly detected image. to spawn new friends, user removes tracker from view and re-places it at a new location.

**drift handling:** 
- grounded nekos are NOT parented to plane - they stay at fixed world coordinates
- **continuous ground stabilisation** in AREntityNeko.Update(): timer-based projection onto nearest plane
- configurable via inspector: `enableGroundStabilisation`, `stabilisationInterval`, `stabilisationThreshold`
- this keeps nekos on the ground while preventing them from sliding around with AR drift

**neko orientation:** nekos spawn facing the camera (rotation calculated from spawn position to camera position, Y-axis only)

## code style

### file headers

```csharp
/*
 * author: mark joshwel
 * date: dd/mm/yyyy
 * description: brief description of what the class does
 */
```

### naming conventions

- **classes/structs**: PascalCase (`CoreGameplay`, `NekoTexture`)
- **public properties**: camelCase (`gameState`, `isReady`, `planes`)
- **private fields**: `_camelCase` with underscore prefix
- **SerializeField**: camelCase (`maxActiveNekos`, `multiImageDistanceThreshold`)
- **constants**: PascalCase (`RepeatCooldownSeconds`)
- **singleton instance**: `Instance` (PascalCase exception for singleton pattern)

### xml documentation

- summaries are **lowercase** and describe the element
- method summaries start with "function to..." for actions, "callback for..." for event handlers
- use simple language, avoid redundant words

```csharp
/// <summary>
///     callback for tracked image detection - spawns neko at new locations
/// </summary>
private void OnImageDetected(HandledTrackedImage tracked)
```

### logging

use `Logkat` static class for all logging (has built-in spam prevention with 1s cooldown per unique message):

```csharp
Logkat.Out("CoreGameplay: spawned MAIN neko");
Logkat.Warn("CoreGameplay: no bowlPrefab assigned, skipping spawn");
Logkat.Err("CoreGameplay: critical error");
Logkat.Panic("CoreGameplay: unreachable"); // throws exception, never suppressed
```

### patterns

- singleton pattern for `CoreGameplay` (access via `CoreGameplay.Instance`)
- dependency injection via SerializeField inspector assignments
- `Logkat.Panic()` for missing required components (throws exception)
- events use `Action<T>` delegates with wrapper structs (e.g., `HandledTrackedImage`, `HandledPlaneInteraction`)
- coroutines for procedural animations and cooldowns
- SerializeField for all inspector-exposed fields
- `[Header]` and `[HelpBox]` for inspector organization

### file structure

```
Assets/Scripts/
├── PokkatCore/           # core game systems (namespace: PokkatCore)
│   ├── CoreGameplay.cs   # singleton coordinator (356 lines)
│   ├── ImageHandling.cs  # ARTrackedImageManager wrapper (169 lines)
│   ├── PlaneHandling.cs  # ARPlaneManager wrapper + SpawnClosest (379 lines)
│   ├── Logkat.cs         # logging utility with spam prevention (91 lines)
│   ├── AREntityNeko.cs   # neko with procedural animations (395 lines)
│   ├── AREntityBowl.cs   # bowl entity stub (23 lines)
│   ├── Statskeeper.cs    # persistence stub (24 lines)
│   └── Reference/        # reference implementations for study
├── CA2Demo/              # legacy demo scripts
│   ├── MarksImageTracking.cs
│   ├── NekoDemo.cs
│   ├── NekoManager.cs
│   ├── NekoTextureLoader.cs
│   ├── PlanePlacer.cs
│   └── PokkatCoreDemo.cs
└── ...
```

## current state

### implemented (dec 18 2024)

- [x] `Logkat` - static logger with:
  - spam prevention (1s cooldown per unique message via dictionary cache)
  - Out/Warn/Err/Panic methods
  - "(Pokkat)" prefix for logcat filtering
  - Panic throws exception and is never suppressed
- [x] `ImageHandling` - ARTrackedImageManager wrapper with:
  - OnImageDetected event (fires when TrackingState.Tracking)
  - OnImageLost event (fires when image removed)
  - HandledTrackedImage struct (State, Image, Id)
- [x] `PlaneHandling` - ARPlaneManager wrapper with:
  - OnPlaneReady event (fires once when minimum area threshold met, default 1.0m²)
  - OnPlanesUpdated event (fires on any plane change)
  - OnPlaneInteraction event (fires on touch, uses new input system)
  - SpawnClosest() method (projects position onto closest plane, orients toward camera, parents to plane)
  - FindClosestPlane() public helper (handles fragmented AR tracking)
  - TryProjectToPlane(Vector3, out Vector3) - projects position onto nearest plane, returns success bool
  - ProjectToPlane(Vector3) - projects position onto nearest plane, returns original if no plane
  - FindLargestTrackingPlane() helper
  - CalculateTotalPlaneArea() helper
  - ResetDetection() to allow OnPlaneReady to fire again
  - TryGetTouchPosition() - new input system touch/mouse detection
  - TryRaycastToPlane() - AR raycast to plane with TrackableType.PlaneWithinPolygon
- [x] `CoreGameplay` - singleton coordinator with:
  - `Instance` static property for global access
  - `planes` public accessor for PlaneHandling (used by AREntityNeko)
  - `gameState` property (WaitingForAnything → HasPlane/HasTracker → Ok)
  - `TrackedNekoInstance` class for neko state tracking (entity, anchor, isFollowing, isGrounded)
  - `_trackedNekos` list holds all spawned nekos with their state (max 3)
  - `_activeImage` reference to currently tracked ARTrackedImage
  - **multispawn**: position jump detection triggers new neko spawn at new location
  - **multitrack**: following neko syncs to image position; falls on tracking loss/limited
  - robust tracking loss detection in Update() (handles TrackingState.Limited, not just OnImageLost)
  - main neko vs friend neko logic (first spawn = main, rest = friend with random texture)
  - bowl spawning on plane touch
  - max neko limit enforcement (default 3)
- [x] `AREntityNeko` - neko entity with:
  - texture loading from Resources/NekoTextures by ID (0-44)
  - SetTextureId() public method
  - RandomizeTextureId() private method (auto-called if tagged "NekoFriend")
  - periodic Blink() with configurable interval and duration
  - StartFollowing() / StopFollowing() for image tracking (unparents, resets grounded state)
  - Fall() and Fall(Action onComplete) - uses PlaneHandling.TryProjectToPlane(), sets _isGrounded
  - **continuous ground stabilisation**: timer-based projection to nearest plane (toggleable via enableGroundStabilisation)
  - WalkTo(Vector3) - choppy stop-motion walk animation with alternating z-tilt
  - Jump() - sine-curve jump animation in place

### multispawn & multitrack feature (dec 18 2024)

**problem**: ar systems give the same TrackableId for identical reference images, making it impossible to track "multiple copies of the same image" natively.

**solution**: 
- **multispawn**: track spawn positions per trackable; if detected position is > 0.25m from all known positions, spawn a new neko (max 3)
- **multitrack**: the most recently spawned neko follows the tracked image position each frame; when TrackingState changes from Tracking to Limited/None (or image is removed), the neko falls to the nearest detected plane surface

### stubs (not yet implemented)

- [ ] `AREntityBowl` - currently just logs Awake/Start
- [ ] `Statskeeper` - currently just logs Awake/Start

### prefab requirements

| prefab | requirements |
|--------|--------------|
| Main Neko | neko model with AREntityNeko component |
| Friend Neko | neko model with AREntityNeko component, tagged "NekoFriend" (texture randomized on Awake) |
| Bowl | bowl model, optionally with AREntityBowl |

### inspector setup for CoreGameplay

| field | description |
|-------|-------------|
| imageHandling | reference to ImageHandling component |
| planeHandling | reference to PlaneHandling component |
| statskeeper | reference to Statskeeper component |
| arCamera | AR camera (Main Camera with AR Camera Manager) |
| mainNekoPrefab | spawned on first image detection |
| friendNekoPrefab | spawned on subsequent detections, tagged "NekoFriend" |
| bowlPrefab | spawned when tapping on plane |
| maxActiveNekos | limit on total spawned nekos (default 5) |
| multiImageDistanceThreshold | minimum distance (m) between image positions to spawn new neko (default 0.25) |

### inspector setup for PlaneHandling

| field | description |
|-------|-------------|
| planeManager | reference to ARPlaneManager |
| raycastManager | reference to ARRaycastManager |
| minimumAreaSquareMeters | minimum total plane area before firing OnPlaneReady (default 1.0) |
| fireOnceOnly | if true, OnPlaneReady fires only once (default true) |

### inspector setup for ImageHandling

| field | description |
|-------|-------------|
| trackedImageManager | reference to ARTrackedImageManager |

### inspector setup for AREntityNeko

| field | description |
|-------|-------------|
| textureId | texture ID (0-44) to load from Resources/NekoTextures |
| enableBlinking | periodic blinking (default true) |
| blinkInterval | seconds between blinks (default 3) |
| blinkDuration | blink duration in seconds (default 0.15) |
| walkSpeed | walking speed in metres per second (default 1) |
| walkStepDuration | seconds per choppy walk step (default 0.15) |
| walkTiltAngle | walk tilt angle in degrees (default 25) |
| jumpHeight | jump height in metres (default 0.15) |
| jumpDuration | jump duration in seconds (default 0.3) |
| fallSpeed | fall speed in metres per second (default 2) |
| enableGroundStabilisation | continuously project to nearest plane when grounded (default true) |
| stabilisationInterval | seconds between stabilisation checks (default 0.5) |
| stabilisationThreshold | minimum drift to trigger stabilisation (default 0.02) |

### tags required

- `"NekoFriend"` - for friend neko prefabs (triggers random texture on Awake)
- `"Bowl"` - for bowl gameobjects (future use)

## unity setup notes

1. **ARTrackedImageManager**: set "Tracked Image Prefab" to null (CoreGameplay handles spawning)
2. **ARTrackedImageManager**: set "Max Number Of Moving Images" to desired limit (e.g., 5)
3. **ARPlaneManager**: assign to PlaneHandling
4. **ARRaycastManager**: assign to PlaneHandling (for touch detection)
5. **Scene hierarchy**: CoreGameplay, PlaneHandling, ImageHandling, Statskeeper should be on a single manager GameObject

## wrapper structs

### HandledTrackedImage
```csharp
public struct HandledTrackedImage
{
    public TrackingState State;
    public ARTrackedImage Image;
    public TrackableId Id;
}
```

### HandledPlaneInteraction
```csharp
public struct HandledPlaneInteraction
{
    public Vector3 Position;
    public Pose Pose;
    public ARPlane Plane;
}
```
