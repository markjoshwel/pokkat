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
| `PlaneHandling` | wrapper for ARPlaneManager with area threshold, SpawnClosest utility, touch detection, horizontal plane filtering, and OnPlaneInteraction |
| `Logkat` | static logger with spam prevention (1s cooldown) for consistent "(Pokkat)" prefixed output |
| `GroundingBehaviour` | unified grounding system for AR entities - handles anchor position storage, XZ locking, and timer-based Y-only stabilisation |
| `AREntityNeko` | neko entity with texture loading, blinking, coroutine-based behaviour loop (not explicit FSM), procedural Walk/Jump/Fall animations, and GroundingBehaviour |
| `AREntityBowl` | bowl entity with consumption logic, visual state, and GroundingBehaviour |
| `Statskeeper` | persistence stub (json-based hunger/happiness - not yet implemented) |
| `CoreGameplayInterfaceInterop` | UI bridge that displays game state messages (e.g., "Scan tracker", "Move phone around") |

### data flow

1. `PlaneHandling` fires `OnPlaneReady` when sufficient plane area detected (default 1.0m²)
2. `PlaneHandling` fires `OnPlaneInteraction` when user taps on a tracked plane (petting has priority)
3. `PlaneHandling` fires `OnPlanesUpdated` whenever planes change (for navmesh baking)
4. `ImageHandling` fires `OnImageDetected` when tracking images (TrackingState.Tracking only)
5. `ImageHandling` fires `OnImageLost` when images are removed from tracking
6. `CoreGameplay` spawns neko via `PlaneHandling.SpawnClosest()` or at image position if no plane
7. Most recently spawned neko follows the tracked image (position updated in CoreGameplay.Update)
8. When image is lost OR neko is >5cm above detected plane, `CoreGameplay` triggers `AREntityNeko.Fall()`
9. `TryTriggerEarlyFall()` runs each frame to catch nekos spawned mid-air at an angle

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

**entity separation (dec 18 2025):**
prevents nekos/bowls from spawning inside each other due to AR tracking blips:
- `entitySeparationDistance` (default 0.15m) - minimum distance between any two AR entities
- `IsTooCloseToExistingEntities(position, ignoreFollowing)` helper checks against all nekos and active bowl
- `TrySpawnNewNeko()` rejects spawn if too close to any grounded entity
- `OnPlaneInteraction()` (bowl spawning) rejects spawn if too close to any neko
- prevents duplicate friend nekos spawned at same marker due to AR image tracking blips

**multitrack logic (in Update, two phases):**
1. **phase 1 (following):** find the following neko, if image is `TrackingState.Tracking` sync position, else trigger fall
2. **phase 2:** ground stabilisation now handled by AREntityNeko.Update() with timer-based plane projection

**grounded nekos stay grounded:** once a neko has landed, it does not re-attach to a newly detected image. to spawn new friends, user removes tracker from view and re-places it at a new location.

**unified grounding system (dec 18 2025):**
all AR entities (nekos, bowls) use `GroundingBehaviour` for consistent plane stabilisation:
- **grounding terminology:**
  - **Ground** - the action of locking an entity to a plane (sets anchor position, enables stabilisation)
  - **Stabilise** - periodic Y-only adjustment to fight AR plane drift (XZ stays locked to anchor)
  - **Fall** - animated drop from spawn height to ground (triggers Ground on completion)
  - **Project** - one-shot plane raycast (low-level utility in PlaneHandling)
- **anchor position:** stored when entity is grounded; XZ coordinates are locked, only Y is adjusted
- **horizontal plane filtering:** `FindClosestHorizontalPlaneBelow()` filters out walls/steep slopes (normal.y >= 0.9)
- **UpdateAnchor():** called during walking to allow intentional XZ movement
- entities are NOT parented to planes - they stay at fixed world coordinates with Y stabilisation**⚠️ GROUNDING LESSONS LEARNED (dec 18 2025) - READ BEFORE MODIFYING GROUNDING CODE:**

this section documents failed approaches to prevent future agents/LLMs from repeating them.

**FAILED APPROACH 1: using `plane.infinitePlane.ClosestPointOnPlane()` for stabilisation**
- **what it did:** projected entity position onto the closest AR plane's infinite surface
- **why it failed:** `ClosestPointOnPlane()` returns a full XYZ position. when the plane's center/orientation is offset from the entity's XZ position, this shifts XZ significantly (observed: 0.48m X drift, 0.25m Z drift in logs)
- **symptom:** entities would drift sideways and sink into the surface as AR planes shifted
- **example from logs:**
  ```
  PlaneHandling: projected (1.80, -2.12, -1.92) to (2.28, -2.12, -2.17) on horizontal plane...
  ```
  notice XZ changed from (1.80, -1.92) to (2.28, -2.17) - a massive horizontal drift!

**FAILED APPROACH 2: using `TryProjectToHorizontalPlane()` for grounding stabilisation**
- **what it did:** found the closest horizontal plane below and returned full projected XYZ
- **why it failed:** same issue as approach 1 - the method returns full XYZ from `ClosestPointOnPlane()`, causing XZ drift. even though we intended to lock XZ, the projection itself was shifting XZ before we could lock it.
- **symptom:** nekos and bowls would "chase" different planes as AR tracking updated, sliding around and sinking

**FAILED APPROACH 3: parenting entities to AR planes**
- **what it did:** set entity.transform.parent = arPlane.transform
- **why it failed:** AR planes constantly update their transform as tracking refines. parented children inherit these updates, causing entities to drift with the plane.
- **symptom:** entities would float/sink/slide as the parent plane's position was adjusted by ARCore/ARKit

**CORRECT APPROACH: `TryGetPlaneHeightAt()` with XZ-locked anchor**
- **what it does:** 
  1. stores anchor position when entity is grounded (XZ is locked forever, only Y changes)
  2. `TryGetPlaneHeightAt(anchor, out float planeHeight)` returns ONLY the Y height - never changes XZ
  3. prefers planes whose boundary actually contains the entity's XZ position (bounding box check)
  4. entity position is set to `(anchor.x, planeHeight, anchor.z)` - XZ from anchor, Y from plane
- **why it works:**
  - XZ is NEVER derived from plane projection - it comes from the original spawn/anchor position
  - Y-only adjustment means AR plane drift only affects vertical position (which is correct - planes refine their height)
  - bounding box check prevents "plane hopping" to distant planes that happen to have similar Y values
- **key insight:** the problem was never about "finding the right plane" - it was about using `ClosestPointOnPlane()` which always shifts XZ. the fix is to ONLY extract Y from the plane and keep XZ locked.

**RULES FOR FUTURE GROUNDING CHANGES:**
1. **NEVER use `ClosestPointOnPlane()` for stabilisation** - it shifts XZ
2. **ALWAYS lock XZ to anchor position** - only Y should ever change during stabilisation
3. **use `TryGetPlaneHeightAt()` for Y-only queries** - this is the correct method for grounded entities
4. **`TryProjectToHorizontalPlane()` is ONLY for initial spawning** - when you need full XYZ (e.g., bowl spawn position), not for stabilisation
5. **NEVER parent entities to AR planes** - they drift. unparent immediately and use anchor-based stabilisation

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
│   ├── PlaneHandling.cs  # ARPlaneManager wrapper + SpawnClosest + NavMesh baking (~650 lines, organised with #region)
│   ├── Logkat.cs         # logging utility with spam prevention (~105 lines)
│   ├── GroundingBehaviour.cs  # unified grounding for AR entities (~160 lines)
│   ├── AREntityNeko.cs   # neko with behaviour loop + procedural animations (~900 lines, organised with #region)
│   ├── AREntityBowl.cs   # bowl entity with consumption logic (~150 lines, organised with #region)
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
- **AREntityNeko**: Inspector Fields, Private Fields, Static Events, Unity Lifecycle, Texture Management, Blinking Animation, Following State, Movement Animations, Awareness Handlers, Behaviour Loop, Stat Hooks
- **PlaneHandling**: Inspector Fields, Private Fields, Public Properties, Unity Lifecycle, Events, Setup, Touch Input, Event Handlers, Plane Queries, Spawning, NavMesh Baking
- **CoreGameplay**: Inspector Fields, Private Fields, Public Properties, Unity Lifecycle, Setup, Following Neko Update, Event Handlers, Neko Spawning, NavMesh State, Audio Stubs
- **AREntityBowl**: Private Fields, Public Properties, Static Events, Unity Lifecycle, Bowl Consumption, Stat Hooks
- **GroundingBehaviour**: Inspector Fields, Private Fields, Public Properties, Public Methods
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
    - **REQUIRES neko prefab to have a Collider component** for Physics.Raycast to detect it
  - **OnNavMeshReady event** (fires once when navmesh is first baked successfully)
  - SpawnClosest() method (projects position onto closest plane, orients toward camera, parents to plane)
  - FindClosestPlane() public helper (handles fragmented AR tracking)
  - **FindClosestPlaneBelow(Vector3)** - finds closest plane at or below position (for proper grounding)
  - **FindClosestHorizontalPlaneBelow(Vector3)** - finds closest horizontal (floor-like) plane below position, filters walls/slopes (normal.y >= 0.9)
  - TryProjectToPlane(Vector3, out Vector3) - projects position onto nearest plane below, returns success bool
  - **TryProjectToHorizontalPlane(Vector3, out Vector3)** - projects to nearest horizontal plane (may change XZ!)
  - **TryGetPlaneHeightAt(Vector3, out float)** - returns only the Y height at a given XZ position (XZ unchanged), prefers planes containing the XZ position
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
  - **early fall trigger** (dec 18 2025): `TryTriggerEarlyFall()` runs each frame and on OnPlaneReady
    - if following neko is >5cm above detected plane, triggers fall immediately
    - handles case where tracker is scanned at angle, spawning neko mid-air
  - robust tracking loss detection in Update() (handles TrackingState.Limited, not just OnImageLost)
  - main neko vs friend neko logic (first spawn = main, rest = friend with random texture)
  - bowl spawning on plane touch (singleton - only one bowl allowed)
  - max neko limit enforcement (default 3)
  - **navmesh UX hooks**: `NotifyMainNekoWaitingForNavMesh()` / `NotifyMainNekoHasNavMesh()` for state tracking
  - **audio stubs** (dec 18 2025): placeholder methods for sound effects, log warning when called
    - `PlayBowlPlaceSound()` - bowl placement sound
    - `PlayBowlConsumeSound()` - bowl consumption sound
    - `PlayStepSound()` - neko footstep (called on each walk step)
    - `PlayMeowSound()` - neko meow (called on petting, playing with friend)
    - `PlayEatingSound()` - neko eating (called during eating animation)
    - `PlayJumpSound()` - neko jump (called on jump)
- [x] `AREntityNeko` - neko entity with:
  - **[RequireComponent(typeof(GroundingBehaviour))]** - unified grounding via GroundingBehaviour
  - texture loading from Resources/NekoTextures by ID (0-44)
  - SetTextureId() public method
  - RandomizeTextureId() private method (auto-called if tagged "NekoFriend")
  - periodic Blink() with configurable interval and duration
  - StartFollowing() / StopFollowing() for image tracking (unparents, resets grounded state)
  - Fall() and Fall(Action onComplete) - uses `TryGetPlaneHeightAt()` to preserve XZ while getting plane Y, calls `_grounding.Ground()`, **snaps to navmesh after landing**
  - **ground stabilisation via GroundingBehaviour**: Update() calls `_grounding.Stabilise()` (skips while following)
  - **SnapToGround()** - delegates to `_grounding.SnapToGround()`
  - **WalkTowardCoroutine(targetPosition, arrivalDistance, interruptCheck)** - primary shared walking helper with choppy stop-motion animation, interrupt support, and `_grounding.UpdateAnchor()` on each step
  - Jump() - **bounce easing** via EaseOutBack curve with configurable `jumpBounceFactor`
  - **Pet()** - petting interaction triggered by touch input; neko faces player camera, blinks, and bounces once
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
    - both nekos face each other before jumping (via LookAt()), refreshed each jump
    - partner references cleared after play completes
  - **behaviour loop** (coroutine-based, not explicit FSM):
    - `BehaviourLoop()` - main loop: waits for grounded, checks navmesh, then roams or handles interactions
    - `RoamOnce()` - picks random navmesh point within roamRadius, walks to it via WalkTowardCoroutine (interruptible by friend interactions)
    - `PlayWithFriend(AREntityNeko)` - does NOT require friend to be grounded; faces friend's current position (refreshed each jump), synchronised jumping with offset delay
    - `MoveAndEat()` - walks to bowl via WalkTowardCoroutine, eating animation (continuous blinking + jumping), consumes bowl
      - **tracks `_targetBowl`** to detect bowl replacement mid-walk (dec 18 2025)
      - `_moveAndEatCoroutine` handle allows cancellation when new bowl spawns
      - `StateNotifyBowlPlaced()` cancels existing MoveAndEat before starting new one
      - interruptible by friend interactions OR bowl replacement/destruction
  - **notification methods**:
    - StateNotifyBowlPlaced() - triggers move-and-eat behaviour (main neko only), cancels previous walk
  - **navmesh state notifications** (ONLY main neko tagged `NekoMain` calls these):
    - calls `CoreGameplay.NotifyMainNekoWaitingForNavMesh()` if neko is not on navmesh
    - calls `CoreGameplay.NotifyMainNekoHasNavMesh()` when neko has navmesh
  - **stat hooks** (for stats integration):
    - OnFed() - called when neko eats from bowl
    - OnPlayedWithFriend() - called after playing with friend
    - OnPetted() - called when main neko is petted (only fires for `NekoMain` tag)
  - LookAt(Vector3) - rotates to face target (Y-axis only)
  - ResetTilt() - resets z-axis tilt to upright after walking
  - EaseOutBack(t, bounceFactor) - helper for bounce easing curves
- [x] `GroundingBehaviour` - unified grounding system for AR entities (dec 18 2025):
  - **purpose**: consistent XZ-locked, Y-only stabilised grounding for all AR entities (nekos, bowls)
  - **inspector fields**: `enableStabilisation`, `stabilisationInterval` (default 0.1s), `stabilisationThreshold` (default 0.02m)
  - **public properties**:
    - `isGrounded` - true after Ground() called
    - `anchorPosition` - current anchor (XZ locked, Y adjusted)
  - **public methods**:
    - `Ground(Vector3)` - sets anchor position, enables stabilisation, snaps entity to position
    - `UpdateAnchor(Vector3)` - updates anchor for intentional movement (walking)
    - `Stabilise()` - timer-based Y-only stabilisation; uses `TryGetPlaneHeightAt()` to get plane Y at anchor XZ
    - `SnapToGround()` - immediate Y-only snap using `TryGetPlaneHeightAt()` 
    - `Reset()` - resets grounding state (for respawn scenarios)
  - **key behaviour**: 
    - XZ coordinates are LOCKED to anchor, ONLY Y is adjusted during stabilisation
    - uses `PlaneHandling.TryGetPlaneHeightAt()` which returns only the Y height (not full XYZ projection)
    - prevents AR plane drift from shifting entity XZ position
    - prefers planes whose boundary contains the entity's XZ position for more stable grounding
- [x] `AREntityBowl` - bowl entity with:
  - **[RequireComponent(typeof(GroundingBehaviour))]** - unified grounding via GroundingBehaviour
  - **direct AR object awareness** via static events:
    - `OnBowlSpawned` static event - fired when any bowl spawns
    - `OnBowlDestroyed` static event - fired when any bowl is destroyed
  - **ground stabilisation via GroundingBehaviour** (dec 18 2025):
    - **unparents from plane in Start()** - critical for XZ locking (plane parenting causes drift)
    - Start() uses `TryGetPlaneHeightAt()` to get plane Y at spawn XZ, then calls `_grounding.Ground()` with XZ preserved
    - Update() calls `_grounding.Stabilise()` for timer-based Y-only adjustment
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
  - plays `PlayStepSound()` on each walk step
- `RoamOnce()` picks random navmesh point within roamRadius, walks via WalkTowardCoroutine
- `PlayWithFriend()` does NOT require friend to be grounded; faces friend's current position (refreshed each jump), synchronised jumping
  - plays `PlayMeowSound()` when play starts, `PlayJumpSound()` on each jump
- `MoveAndEat()` walks to bowl, eating animation (continuous blinking + jumping), consumes bowl
  - plays `PlayEatingSound()` during eating animation, `PlayJumpSound()` on each jump
  - **bowl replacement fix (dec 18 2025)**: tracks `_targetBowl` to detect when bowl is replaced mid-walk
  - `StateNotifyBowlPlaced()` cancels existing MoveAndEat coroutine before starting new one
  - prevents stuck walking animation when quickly spawning new bowls

**petting interaction (dec 18 2025)**:
- `PlaneHandling.Update()` checks touch input each frame
- `TouchHitsNeko()` is checked BEFORE plane interaction raycast (petting has priority)
- touch detection runs regardless of `OnPlaneInteraction` subscribers (petting always works)
- `Pet()` makes neko face the player camera, blink, and bounce once
- plays `PlayMeowSound()` and `PlayJumpSound()` on petting
- only main neko (tagged `NekoMain`) triggers `OnPetted()` stat hook
- petting touch blocks bowl placement - if neko is touched, plane interaction is skipped

**friend neko queue system (dec 18 2025)**:
- `_pendingFriendQueue` queues multiple friends spawning in quick succession
- `TryDequeueAndPlayWithFriend()` dequeues next friend and starts play interaction
- queue is cleaned of null/destroyed friends automatically

**mutual face-each-other recognition (dec 18 2025):**
- `_currentPlayPartner` tracks the active play partner for both nekos
- **initial facing uses gradual turn** (`TurnToward()` coroutine, 0.3s with ease-out) for natural "noticing" moment
- subsequent facing during jumps uses instant `LookAt()` (snappy during active play is fine)
- partner references cleared after play completes

**stat hooks**:
- OnFed(), OnPlayedWithFriend(), OnPetted() methods in AREntityNeko
- OnNekoConsumed(AREntityNeko) virtual method in AREntityBowl
- OnPetted() only fires for main neko (tagged `NekoMain`)
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
| Main Neko | neko model with AREntityNeko component, **Collider component for petting** (e.g., BoxCollider, CapsuleCollider) |
| Friend Neko | neko model with AREntityNeko component, tagged "NekoFriend", **Collider component for petting** |
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

| spawnCooldown | seconds between neko spawns (default 1.0) |
| entitySeparationDistance | minimum distance (m) between any two AR entities (nekos/bowls) to prevent overlap (default 0.15) |
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
