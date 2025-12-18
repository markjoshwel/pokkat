# Pokkat Game - Agent Context

this document captures codebase knowledge, code style, and current development context for ai agent continuity.

**last updated:** december 18, 2025

## project overview

**pokkat** is a unity ar game using ar foundation for ios/android. the game features neko (cat) characters that can be spawned via image tracking, interact with bowls, and navigate on detected surfaces.

### package dependencies

- `com.unity.xr.arfoundation` - ar foundation for cross-platform ar
- `com.unity.xr.arkit` / `com.unity.xr.arcore` - platform-specific ar providers
- `com.unity.ai.navigation` - runtime navmesh baking (NavMeshSurface)
- `com.unity.inputsystem` - new input system for touch/mouse detection

## architecture

the PokkatCore namespace uses a **scene singleton pattern** for `CoreGameplay` (access via `CoreGameplay.instance`, does not persist across scenes), **dependency injection** (via inspector assignments), and **observer patterns** (events/callbacks).

### core components

| class | role |
|-------|------|
| `CoreGameplay` | scene singleton coordinator managing neko spawning, game state, image following, and multi-image detection |
| `ImageHandling` | wrapper for ARTrackedImageManager events, fires OnImageDetected/OnImageLost |
| `PlaneHandling` | wrapper for ARPlaneManager with area threshold, SpawnClosest utility, touch detection, and OnPlaneInteraction |
| `Logkat` | static logger with spam prevention (1s cooldown) for consistent "(Pokkat)" prefixed output |
| `AREntityNeko` | neko entity with texture loading, blinking, coroutine-based behaviour loop (not explicit FSM), and procedural Walk/Jump/Fall animations |
| `AREntityBowl` | bowl entity with ground stabilisation, consumption logic, and visual state |
| `Statskeeper` | persistence stub (json-based hunger/happiness - not yet implemented) |
| `CoreGameplayInterfaceInterop` | UI bridge that displays game state messages (e.g., "Scan tracker", "Move phone around") |

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
Logkat.Out("CoreGameplay: spawned MAIN neko");       // normal output
Logkat.Dev("CoreGameplay: spawn position={pos}");    // verbose debug (toggle via VerboseLogging const)
Logkat.Warn("CoreGameplay: no bowlPrefab assigned"); // warning
Logkat.Err("CoreGameplay: critical error");          // error
Logkat.Panic("CoreGameplay: unreachable");           // throws exception, never suppressed
```

**Logkat.Dev() verbose logging:**
- prefix: `(Pokkat Verbose) DEV: ...`
- controlled by `Logkat.VerboseLogging` const (set to false for release builds)
- use for debugging output that would spam logs in production
- migrated all `[Debug]` tagged logs from `Logkat.Out()` to `Logkat.Dev()`

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
│   ├── CoreGameplay.cs   # singleton coordinator (~580 lines, organised with #region)
│   ├── ImageHandling.cs  # ARTrackedImageManager wrapper (~190 lines, organised with #region)
│   ├── PlaneHandling.cs  # ARPlaneManager wrapper + SpawnClosest + NavMesh baking (~595 lines, organised with #region)
│   ├── Logkat.cs         # logging utility with spam prevention (~105 lines)
│   ├── AREntityNeko.cs   # neko with behaviour loop + procedural animations (~855 lines, organised with #region)
│   ├── AREntityBowl.cs   # bowl entity with consumption logic (~240 lines, organised with #region)
│   ├── Statskeeper.cs    # persistence stub (~25 lines)
│   └── Reference/        # reference implementations for study
├── CoreGameplayInterfaceInterop.cs  # UI bridge for game state messages (~45 lines)
├── CA2Demo/              # legacy demo scripts
│   ├── MarksImageTracking.cs
│   ├── NekoDemo.cs
│   ├── NekoManager.cs
│   ├── NekoTextureLoader.cs
│   ├── PlanePlacer.cs
│   └── PokkatCoreDemo.cs
└── ...
```

**note:** large pokkatcore scripts use `#region` blocks for organisation:
- **AREntityNeko**: Inspector Fields, Private Fields, Static Events, Unity Lifecycle, Ground Stabilisation, Texture Management, Blinking Animation, Following State, Movement Animations, Awareness Handlers, Behaviour Loop, Stat Hooks
- **PlaneHandling**: Inspector Fields, Private Fields, Public Properties, Unity Lifecycle, Events, Setup, Touch Input, Event Handlers, Plane Queries, Spawning, NavMesh Baking
- **CoreGameplay**: Inspector Fields, Private Fields, Public Properties, Unity Lifecycle, Setup, Following Neko Update, Event Handlers, Neko Spawning, NavMesh State
- **AREntityBowl**: Inspector Fields, Private Fields, Public Properties, Static Events, Unity Lifecycle, Ground Stabilisation, Bowl Consumption, Stat Hooks
- **ImageHandling**: Inspector Fields, Unity Lifecycle, Events, Setup, Event Handlers

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
  - **TouchHitsNeko(screenPosition)** - checks if touch hits neko before plane interaction (petting has priority)
  - **OnNavMeshReady event** (fires once when navmesh is first baked successfully)
  - SpawnClosest() method (projects position onto closest plane, orients toward camera, parents to plane)
  - FindClosestPlane() public helper (handles fragmented AR tracking)
  - TryProjectToPlane(Vector3, out Vector3) - projects position onto nearest plane, returns success bool
  - ProjectToPlane(Vector3) - projects position onto nearest plane, returns original if no plane
  - FindLargestTrackingPlane() helper
  - CalculateTotalPlaneArea() helper
  - ResetDetection() to allow OnPlaneReady to fire again
  - TryGetTouchPosition() - new input system touch/mouse detection
  - TryRaycastToPlane() - AR raycast to plane with TrackableType.PlaneWithinPolygon
  - **public accessors for entity use** (via `CoreGameplay.instance.planes`):
    - `surface` - NavMeshSurface for agent pathfinding
    - `arPlaneManager` - ARPlaneManager for plane queries
    - `navMeshReady` - true after first successful bake
    - `isReady` - true after minimum plane area threshold met
  - **runtime navmesh baking** via NavMeshSurface (com.unity.ai.navigation):
    - `navMeshSurface` field for NavMeshSurface component reference
    - `navMeshBakeCooldownSeconds` (default 2s) to throttle rebakes
    - `autoBakeOnPlaneUpdate` toggle for continuous baking on plane changes
    - `RequestNavMeshBake()` public method (respects cooldown)
    - auto-bakes on OnPlaneReady event
    - auto-bakes on OnPlanesUpdated event (if autoBakeOnPlaneUpdate enabled)
- [x] `CoreGameplay` - scene singleton coordinator (does not persist across scenes, uses .instance for prefab access without DI):
  - `instance` static property for scene-scoped access
  - `planes` public accessor for PlaneHandling (used by AREntityNeko)
  - `gameState` property (WaitingForAnything → HasPlane/HasTracker → NekoWaitingForNavMesh → Ok)
  - `TrackedNekoInstance` class for neko state tracking (entity, anchor, isFollowing, isGrounded)
  - `_trackedNekos` list holds all spawned nekos with their state (max 3)
  - `_activeImage` reference to currently tracked ARTrackedImage
  - `_mainNekoWaitingForNavMesh` bool for UX feedback
  - `_lastSpawnTime` and `spawnCooldown` (default 1.0s) to prevent double-spawning race conditions
  - **multispawn**: position jump detection triggers new neko spawn at new location
  - **multitrack**: following neko syncs to image position; falls on tracking loss/limited
  - robust tracking loss detection in Update() (handles TrackingState.Limited, not just OnImageLost)
  - main neko vs friend neko logic (first spawn = main, rest = friend with random texture)
  - bowl spawning on plane touch (singleton - only one bowl allowed)
  - max neko limit enforcement (default 3)
  - **navmesh UX hooks**: `NotifyMainNekoWaitingForNavMesh()` / `NotifyMainNekoHasNavMesh()` for state tracking
- [x] `AREntityNeko` - neko entity with:
  - texture loading from Resources/NekoTextures by ID (0-44)
  - SetTextureId() public method
  - RandomizeTextureId() private method (auto-called if tagged "NekoFriend")
  - periodic Blink() with configurable interval and duration
  - StartFollowing() / StopFollowing() for image tracking (unparents, resets grounded state)
  - Fall() and Fall(Action onComplete) - uses PlaneHandling.TryProjectToPlane(), sets _isGrounded, **snaps to navmesh after landing**
  - **continuous ground stabilisation**: timer-based projection to nearest plane (toggleable via enableGroundStabilisation)
  - **SnapToGround()** - helper to project neko to nearest plane
  - **WalkTowardCoroutine(targetPosition, arrivalDistance, interruptCheck)** - primary shared walking helper with choppy stop-motion animation and interrupt support (used by RoamOnce, MoveAndEat)
  - Jump() - **bounce easing** via EaseOutBack curve with configurable `jumpBounceFactor`
  - **direct AR object awareness** via static events:
    - `OnNekoSpawned` static event - fired when any neko spawns
    - `OnNekoDestroyed` static event - fired when any neko is destroyed
    - subscribes to `AREntityBowl.OnBowlSpawned` to detect bowls directly
    - subscribes to `AREntityNeko.OnNekoSpawned` to detect friend nekos directly
  - **queue-based friend handling** (dec 18 2025):
    - `_pendingFriendQueue` - Queue<AREntityNeko> for multiple friends spawning in quick succession
    - `TryDequeueAndPlayWithFriend()` - dequeues next friend and starts play interaction
    - cleans up null/destroyed friends from queue automatically
  - **mutual face-each-other recognition** (dec 18 2025):
    - `_currentPlayPartner` field tracks active play partner
    - both nekos face each other before jumping (via LookAt())
    - partner references cleared after play completes
  - **behaviour loop** (coroutine-based, not explicit FSM):
    - `BehaviourLoop()` - main loop: waits for grounded, checks navmesh, then roams or handles interactions
    - `RoamOnce()` - picks random navmesh point within roamRadius, walks to it via WalkTowardCoroutine (interruptible by friend interactions)
    - `PlayWithFriend(AREntityNeko)` - waits for friend grounded, face each other, synchronised jumping with offset delay
    - `MoveAndEat()` - walks to bowl via WalkTowardCoroutine, eating animation (continuous blinking + jumping), consumes bowl (interruptible by friend interactions)
  - **notification methods**:
    - StateNotifyBowlPlaced() - triggers move-and-eat behaviour (main neko only)
  - **navmesh state notifications** (ONLY main neko tagged `NekoMain` calls these):
    - calls `CoreGameplay.NotifyMainNekoWaitingForNavMesh()` if neko is not on navmesh
    - calls `CoreGameplay.NotifyMainNekoHasNavMesh()` when neko has navmesh
  - **stat hooks** (for stats integration):
    - OnFed() - called when neko eats from bowl
    - OnPlayedWithFriend() - called after playing with friend
  - LookAt(Vector3) - rotates to face target (Y-axis only)
  - ResetTilt() - resets z-axis tilt to upright after walking
  - EaseOutBack(t, bounceFactor) - helper for bounce easing curves
- [x] `AREntityBowl` - bowl entity with:
  - **direct AR object awareness** via static events:
    - `OnBowlSpawned` static event - fired when any bowl spawns
    - `OnBowlDestroyed` static event - fired when any bowl is destroyed
  - **ground stabilisation like neko** (dec 18 2024):
    - **unparents from plane in Start()** - critical for XZ locking (plane parenting causes drift)
    - `_spawnPosition` field for XZ locking (like grounded neko)
    - `_isGrounded` flag set after initial projection
    - `ProjectToGround()` called in Start() to project to nearest plane
    - `UpdateGroundStabilisation()` - locks XZ to spawn position, only allows Y stabilisation
    - `stabilisationInterval` default 0.1s (faster than neko for tighter tracking)
    - verbose logging via `Logkat.Dev()` for debugging
  - `isFull` public property (default true)
  - `Consume(AREntityNeko)` - sets isFull to false, fires OnConsumed event
  - `Refill()` - sets isFull to true
  - `OnConsumed` event (Action<AREntityNeko>)
  - `UpdateBowlVisual()` skeleton method (logs "mesh swap not implemented yet")
  - `OnNekoConsumed(AREntityNeko)` virtual skeleton hook for stats integration

### multispawn & multitrack feature (dec 18 2024)

**problem**: ar systems give the same TrackableId for identical reference images, making it impossible to track "multiple copies of the same image" natively.

**solution**: 
- **multispawn**: track spawn positions per trackable; if detected position is > 0.25m from all known positions, spawn a new neko (max 3)
- **multitrack**: the most recently spawned neko follows the tracked image position each frame; when TrackingState changes from Tracking to Limited/None (or image is removed), the neko falls to the nearest detected plane surface

### neko behaviour loop & bowl lifecycle (dec 18 2025)

**bowl lifecycle (singleton)**:
- only one bowl can exist at a time
- tapping plane destroys existing bowl and spawns replacement
- `CoreGameplay.activeBowl` provides public access to current bowl
- on bowl spawn, main neko is notified via `AREntityBowl.OnBowlSpawned` static event (direct AR object awareness)
- bowl has ground stabilisation like neko (timer-based projection to nearest plane with XZ locking)

**neko behaviour loop (coroutine-based, not explicit FSM)**:
- `BehaviourLoop()` is the main coroutine that runs while neko is alive
- waits for grounded state, checks navmesh availability, then roams or handles interactions
- **behaviours are interruptible**: friend spawn or bowl placement can interrupt roaming
- **WalkTowardCoroutine()** is the shared walking helper with choppy stop-motion animation
- `RoamOnce()` picks random navmesh point within roamRadius, walks via WalkTowardCoroutine
- `PlayWithFriend()` waits for friend grounded, both face each other, synchronised jumping
- `MoveAndEat()` walks to bowl, eating animation (continuous blinking + jumping), consumes bowl

**friend neko queue system (dec 18 2025)**:
- `_pendingFriendQueue` queues multiple friends spawning in quick succession
- `TryDequeueAndPlayWithFriend()` dequeues next friend and starts play interaction
- queue is cleaned of null/destroyed friends automatically

**mutual face-each-other recognition (dec 18 2025)**:
- `_currentPlayPartner` tracks the active play partner for both nekos
- both nekos LookAt() each other before jumping
- partner references cleared after play completes

**stat hooks**:
- OnFed(), OnPlayedWithFriend() virtual methods in AREntityNeko
- OnNekoConsumed(AREntityNeko) virtual method in AREntityBowl
- these integrate with Statskeeper for hunger/happiness tracking

**navmesh UX**:
- `CoreGameplayState.NekoWaitingForNavMesh` state for user feedback
- `CoreGameplayInterfaceInterop` displays "The cat doesn't know where to go!" message
- main neko calls `NotifyMainNekoWaitingForNavMesh()` when not on navmesh, `NotifyMainNekoHasNavMesh()` when roaming succeeds

### stubs (not yet implemented)

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
| navMeshSurface | NavMeshSurface component for runtime baking (optional) |
| navMeshBakeCooldownSeconds | seconds between navmesh rebakes (default 2) |
| autoBakeOnPlaneUpdate | if true, rebake navmesh when planes update (default true) |

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
| walkSpeed | walking speed in metres per second (default 0.5) |
| walkStepDuration | seconds per choppy walk step (default 0.08) |
| walkStepDistanceMultiplier | multiplier for step distance (default 0.5) |
| walkTiltAngle | walk tilt angle in degrees (default 15) |
| jumpHeight | jump height in metres (default 0.15) |
| jumpDuration | jump duration in seconds (default 0.3) |
| jumpBounceFactor | bounce easing intensity 0=none, 1=full (default 0.3) |
| fallSpeed | fall speed in metres per second (default 2) |
| enableGroundStabilisation | continuously project to nearest plane when grounded (default true) |
| stabilisationInterval | seconds between stabilisation checks (default 0.5) |
| stabilisationThreshold | minimum drift to trigger stabilisation (default 0.02) |
| roamRadius | roaming radius in metres for NavMesh sampling (default 0.5) |
| idleWaitMin | minimum idle wait in seconds before roaming (default 2) |
| idleWaitMax | maximum idle wait in seconds before roaming (default 5) |
| friendJumpDelay | delay before friend jumps in play sequence (default 0.5) |
| playJumpCount | number of jumps when playing with friend (default 3) |
| eatingDuration | eating animation duration in seconds (default 5) |

### inspector setup for AREntityBowl

| field | description |
|-------|-------------|
| enableGroundStabilisation | project to nearest plane continuously (default true) |
| stabilisationInterval | seconds between stabilisation checks (default 0.5) |
| stabilisationThreshold | minimum drift to trigger stabilisation (default 0.02) |

### tags required

- `"NekoMain"` - for main neko prefab (receives bowl/friend notifications, initiates play)
- `"NekoFriend"` - for friend neko prefabs (triggers random texture on Awake, follows main neko's play)
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

### HandledNekoInteraction
```csharp
public struct HandledNekoInteraction
{
    public AREntityNeko Neko;
    public Vector3 Position;
}
```
