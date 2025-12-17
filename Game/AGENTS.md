# Pokkat Game - Agent Context

this document captures codebase knowledge, code style, and current development context for ai agent continuity.

## project overview

**pokkat** is a unity ar game using ar foundation for ios/android. the game features neko (cat) characters that can be spawned via image tracking, interact with bowls, and navigate on detected surfaces using navmesh.

### package dependencies

- `com.unity.xr.arfoundation` - ar foundation for cross-platform ar
- `com.unity.xr.arkit` / `com.unity.xr.arcore` - platform-specific ar providers
- `com.unity.ai.navigation` - runtime navmesh baking (NavMeshSurface)

## architecture

the PokkatCore namespace follows **dependency injection** (via inspector assignments) and **observer patterns** (events/callbacks) rather than singletons.

### core components

| class | role |
|-------|------|
| `CoreGameplay` | central coordinator managing neko spawning and game state |
| `ImageHandling` | wrapper for ARTrackedImageManager events |
| `PlaneHandling` | wrapper for ARPlaneManager with area threshold and SpawnClosest utility |
| `AREntityNeko` | neko character with state machine, navmesh navigation, procedural animations |
| `AREntityBowl` | bowl entity with food stages and consumption logic |
| `Statskeeper` | persistent json-based statistics (hunger, happiness) |

### data flow

1. `PlaneDetection` fires `OnPlaneReady` when sufficient plane area detected
2. `ImageDetection` fires `OnImageDetected` when tracking images
3. `CoreGameplay` coordinates spawning via `PlaneHandler.SpawnClosest()`
4. `AREntityNeko` instances use NavMeshAgent for navigation
5. `Statskeeper` persists hunger/happiness to `Application.persistentDataPath`

## code style

### file headers

```csharp
/*
 * ClassName: brief description of what the class does
 * last updated mon dd yyyy
 * for pokkat
 *
 * copyright (c) 2024 mark joshwel
 */
```

### naming conventions

- **classes/structs**: PascalCase (`CoreGameplay`, `NekoTexture`)
- **public properties**: PascalCase (`GameReady`, `CurrentHunger`)
- **private fields**: `_camelCase` with underscore prefix
- **SerializeField**: camelCase (`maxActiveNekos`, `spawnLookingAtPlayer`)
- **constants**: PascalCase (`SaveFileName`)

### xml documentation

- summaries are **lowercase** and describe the element
- method summaries start with "function to..." for actions, "callback for..." for event handlers
- use simple language, avoid redundant words

```csharp
/// <summary>
///     function to spawn a neko for the detected image
/// </summary>
/// <param name="trackedImage">the tracked image to spawn a neko for</param>
private void SpawnNekoForImage(ARTrackedImage trackedImage)
```

### logging

- no LoggingPrefix constants, just use simple inline strings
- all log messages are lowercase with classname prefix: `"classname: message here"`
- no conditional logging toggles (loggingEnabled fields)

```csharp
Debug.Log($"coregameplay: spawned neko for '{referenceName}' (total: {_activeNekos.Count})");
Debug.LogWarning("coregameplay: no bowl registered, cannot command nekos to seek");
Debug.LogError("planehandler: cannot spawn, prefab is null");
```

### patterns

- `RequireComponent` attribute for mandatory dependencies
- `MissingComponentException` for missing required components
- events use `Action<T>` delegates
- coroutines for procedural animations and cooldowns
- SerializeField for all inspector-exposed fields
- no `[Header]` attribute groupings unless absolutely necessary

### file structure

```
Assets/Scripts/
├── PokkatCore/           # core game systems (namespace: PokkatCore)
│   ├── CoreGameplay.cs
│   ├── ImageHandling.cs
│   ├── PlaneHandling.cs
│   ├── AREntityNeko.cs
│   ├── AREntityBowl.cs
│   └── Statskeeper.cs
├── MarksImageTracking.cs # legacy/demo image tracking
├── NekoDemo.cs           # texture demo
├── NekoManager.cs        # texture application
├── NekoTextureLoader.cs  # texture loading
├── PlanePlacer.cs        # simple plane placement
└── PokkatCoreDemo.cs     # legacy demo
```

## current state

### implemented (dec 11 2024)

- [x] `Statskeeper` - json persistence with hunger decay
- [x] `ImageHandling` - ARTrackedImageManager wrapper with game events
- [x] `PlaneHandling` - ARPlaneManager wrapper with area threshold + SpawnClosest method
- [x] `AREntityBowl` - food stages and consumption
- [x] `AREntityNeko` - state machine (Idle, Jumping, SeekingBowl, Socializing)
- [x] `CoreGameplay` - central coordinator

### neko state machine

```
states: Idle, Jumping, SeekingBowl, Socializing
- Idle: occasional pivot animation
- Jumping: procedural sin-curve jump
- SeekingBowl: NavMeshAgent pathfinding to bowl
- Socializing: look at nearby nekos, synced jumping
```

### tags required

- `"Neko"` - for neko gameobjects
- `"Bowl"` - for bowl gameobjects
- `"Player"` - for player hand/interaction colliders

## unity setup notes

1. **ARTrackedImageManager**: set "Tracked Image Prefab" to null (CoreGameplay handles spawning)
2. **NavMeshSurface**: assign to PlaneHandler; ensure ar plane prefabs have correct layer
3. **layer mask**: configure `nekoLayerMask` on AREntityNeko for socializing detection
