# AGENTS.md

agent knowledge base for the Pokkat workspace

## project snapshot

Pokkat is a Unity 6.x ARFoundation experience targeting URP 17 and the Input System 1.14.
Core runtime code resides under `Assets/Scripts`, and directives plus cross-language examples live in `Agent/`.
The experience currently ships plane placement flows, multi-variant image tracking, neko interaction demos, and inspector tooling.

## repository structure

- `Agent/`: directives, AGENTS knowledge, multi-language references (`examples.*`).
- `Assets/Scripts/`: Unity gameplay scripts (CoreDemo, PlanePlacer, Neko*, ImageTracking*, HelpBox).
- `Packages/`: Unity manifest plus package cache (ARFoundation, URP, Input System).
- `Tooling/`, `Builds/`, `ContentPackages/`: export artifacts and helper utilities.
- `ProjectSettings/` and `UserSettings/`: engine configuration.

## workflow history

- **Nov 19 2025**: Annotated every Unity gameplay script with the assignment-mandated author/date/description banner and documented the change here so future agents know the directive was fulfilled.
- **Nov 19 2025**: Completed the directive-aligned rewrite of every `Assets/Scripts/*.cs` file (logging guards, lifecycle hygiene, prefab validation) and synced this AGENTS snapshot.
- **Nov 18 2025**: Consolidated coding conventions across Unity, Python, Rust, and Zig by reviewing `Agent/examples.*` and mirroring the style in active gameplay scripts.
- **Nov 17 2025**: Verified the uv → basedpyright → mypy → ruff toolchain for Python utilities and documented MDF expectations for future scripts.

## key gameplay and tooling systems

- **PlanePlacer**: caches AR raycast hits (XRRaycastManager), validates prefabs before instantiation, enforces cooldown timers, and logs via `(Pokkat) PlanePlacer:` prefix.
- **ImageTracking V1/V2/V3**: manage deterministic prefab dictionaries for ARTrackedImages, guard against null reference libraries, and handle added/updated/removed events with lifecycle symmetry.
- **Neko stack (NekoDemo, NekoManager, NekoTextureLoader)**: coordinates coroutine-driven movement, texture cycling, and inspector hints; coroutines are stored and stopped in `OnDisable`.
- **HelpBox**: editor helper using `HelpBoxAttribute` to surface styled inspector hints with resilient style lookup.
- **CoreDemo**: orchestrates neko-plane interactions, demonstrates logging toggles, dependency guards, and serialized data patterns.
- **Agent/examples.\* files**: canonical style references for C#, Python, Rust, Zig, and any new languages.

## coding style guide

### Unity / C#

- prepend every script with the multi-line directive banner (purpose, last update, copyright).
- provide XML `<summary>` blocks for classes, serialized fields, and public/protected methods; include `<param>` / `<returns>` where clarity matters.
- keep inspector fields `private` with `[SerializeField]` and lower camelCase names; expose read-only properties if runtime access is necessary.
- declare `private const string LoggingPrefix = "(Pokkat) ClassName:";` and `loggingEnabled` flags; guard every log statement.
- register listeners, input actions, and coroutines in `OnEnable`, and remove/stop them in `OnDisable` to avoid orphaned subscriptions.
- guard dependencies in `Awake` or `OnEnable` with early returns plus descriptive `Debug.LogError` output.
- prefer dictionary or lookup caching for AR-tracked prefabs; verify prefabs before instantiation and handle missing assets gracefully.
- comments stay terse, lowercase, and purposeful (e.g., `// guard check`).

### Python

- follow MDF (Meadow Docstring Format) for every function/class docstring with modern typing syntax (`list[str]`, `Path | None`).
- group imports as stdlib → third-party → local, alphabetize within each block, and keep spacing consistent.
- type-annotate all functions and module-level constants (`Final` when immutable); prefer descriptive identifiers.
- rely on guard clauses for validation with lowercase inline comments such as `# guard check` or `# using SGT`.
- run `uv run basedpyright`, `uv run mypy`, `uv run ruff check`, and `uv run ruff format`; sort imports with `uv run ruff --profile black --select I .` when introducing new modules.
- suppress diagnostics only via `pyright: ignore[diagnosticName]`, and only when third-party stubs are missing.

### Rust

- include the Zero-Clause BSD banner plus a concise module summary.
- order `use` statements with std before external crates, letting `rustfmt` control layout.
- name constants in screaming snake case with inline unit/context comments (e.g., `SOTA_SIDESTEP_LARGE_FILE_SIZE`).
- favor explicit structs/enums with named fields, using iterators (`filter_map`, `collect`) for file traversal.
- return `Result<T, Box<dyn Error>>` (or typed errors) from fallible helpers and log context before exiting.

### Zig

- keep the Zero-Clause BSD banner; document types/functions with triple-slash comments.
- uppercase snake_case for compile-time constants; describe packed structs/enums inline.
- manage allocators explicitly, offering `deinit` methods to release owned memory.
- prefer error unions (`!T`) with `try` propagation, and track loop state via descriptive booleans.

### JavaScript / TypeScript (if added)

- Bun is the runtime, package manager, and test runner (`bun install`, `bun test`, `bun run <script>`).
- mirror the Python readability approach: descriptive naming, guard clauses, minimal comments.
- align lint/format tooling with Bun scripts; document any deviations in this file.

## tooling workflow

- **Unity**: after script edits, trigger a recompile (enter/exit Play Mode) and confirm the Console is clear.
- **Python**: `uv run basedpyright`, `uv run mypy`, `uv run ruff check`, `uv run ruff format`.
- **Rust**: `cargo fmt`, `cargo clippy --all-targets --all-features`, `cargo test`.
- **Zig**: `zig fmt` plus relevant `zig build` or `zig run` commands.
- **JavaScript**: `bun run lint` and `bun run test` once scripts exist.
- **searching**: avoid broad wildcard or regex sweeps; target known directories with anchored globs (e.g., `Agent/examples.*`, `Assets/Scripts/**/*.cs`).

## debugging guidance

- **Plane placement issues**: verify `ARRaycastManager` reference, confirm `raycastPrefab` is assigned, and watch the cooldown timer in the inspector; enable logging to trace raycast hits.
- **Image tracking drift**: log the tracked image name and dictionary lookup key, ensure prefab mappings are serialized, and confirm XR Reference Image Library GUIDs align.
- **Coroutine leaks**: each coroutine handle stored in a field must be stopped in `OnDisable`; log when starting/stopping during debugging sessions.
- **Networking or service dependencies** (future): wrap asynchronous calls with guard clauses and log failure modes before retries.
- **Editor helpers**: if HelpBox styles fail, confirm `HelpBoxStyle` assets exist and the UI Toolkit editor window is open.
- **Python utilities**: run `uv run basedpyright` before executing scripts; if TOML parsing fails, print the offending key/value pair and compare against `Agent/examples.tomlantic.py`.

## knowledge + current status (Nov 19 2025)

- all scripts under `Assets/Scripts` follow the directive banner, logging pattern, guard clauses, and lifecycle symmetry; the newest edit now documents the assignment-specific author/date/description banner across the suite.
- Neko flows use cached coroutines and inspector help boxes; texture cycling is centralized.
- ImageTracking variants share deterministic prefab dictionaries for added/updated/removed events.
- PlanePlacer handles prefab validation and cooldowns; missing prefabs are logged and skipped.
- `Agent/examples.*` remain the canonical references for multi-language style.

## outstanding tasks

- [ ] Run a Unity recompile after the next batch of script edits and confirm zero warnings/errors before committing.
- [ ] Capture screenshots or short clips of each AR flow (plane placement, image tracking variants, neko demo) for future regression comparisons.
- [ ] Expand the Python tooling notes with an example `uv` command sequence once the next utility script is added.

## resuming work checklist

1. skim this file and `Agent/directives.md` before editing anything.
2. for Unity work, copy the banner/XML format from an existing script and keep `OnEnable`/`OnDisable` symmetry.
3. for other languages, open the corresponding `Agent/examples.*` file to mirror naming, formatting, and docstyle.
4. run the tooling commands listed above before handing off changes.
5. document architectural changes, new dependencies, or TODOs here before ending your session.

## open considerations

- run a Unity recompile after any substantial C# edit to catch diagnostics early.
- if new subsystems (networking, persistence, analytics) are added, extend the "key systems" and "tooling" sections accordingly.
- note any external packages lacking type stubs so future contributors know where suppressions are justified.

## reference list

1. `Agent/examples.unity.FirebaseBackend.cs` & `Agent/examples.unity.OklchColourPickerUI.cs` — canonical Unity banner + XML doc patterns.
2. `Agent/examples.surplus.py`, `Agent/examples.mulipea.py`, `Agent/examples.tomlantic.py` — MDF docstrings, uv workflow, error-handling style.
3. `Agent/examples.sidestepper.rs` — CLI ergonomics, iterator-heavy workflows, Zero-Clause BSD header usage.
4. `Agent/examples.zigby.zig` — allocator ownership, packed structs/enums, triple-slash docs.
5. `Assets/Scripts/*.cs` — live implementations of the directives (PlanePlacer, ImageTracking variants, Neko stack, HelpBox, CoreDemo).
