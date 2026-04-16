# Architecture by Genre — Core Systems in Unity

> Architectural reference for technical decisions when starting Unity projects across different game genres. Focus on patterns, systems, performance, and technology stack.

---

## 1. Survivors / Horde (Vampire Survivors style)

### Architectural Patterns

The central challenge of this genre is keeping thousands of entities active simultaneously without compromising framerate. The architecture must be data-oriented from the start.

**Data-Oriented Design** is the dominant pattern. Separating data from behavior allows the processor to work with cache hits instead of cache misses, which makes a dramatic difference when processing 10,000+ entities per frame. The original Vampire Survivors uses a God Object singleton (GameManager) that orchestrates everything — functional but limiting to scale. More modern implementations prefer pure or hybrid ECS.

**Massive Object Pooling** is non-negotiable. Enemies, projectiles, pickups, and visual effects must be pre-allocated and recycled. Never call `Instantiate()` or `Destroy()` during gameplay. The pool must be sized based on the expected peak of simultaneous entities.

**Spatial Partitioning** (grid hash, quadtree) to optimize collision detection. With thousands of entities, brute-force O(n²) collision is infeasible. A spatial hash grid with fixed-size cells allows each entity to only check neighbors in the same cell and adjacent ones.

### Core Systems

- **Entity Manager**: spawning, despawning, enemy lifecycle per wave
- **Object Pool System**: separate pools by type (enemies, projectiles, XP gems, damage numbers)
- **Wave/Spawner System**: controls timing, quantity, type, and spatial distribution of spawns
- **Weapon System**: automatic weapons with cooldown, area of effect, projectiles or hitboxes
- **Upgrade/Level-up System**: randomized upgrade selection on level up
- **Collision System**: optimized detection between player projectiles and enemy masses
- **Camera System**: simple follow with possible dynamic zoom-out based on entity density

### Performance Challenges

- **Entity volume**: the main bottleneck. Each enemy with MonoBehaviour + Collider2D + Rigidbody2D consumes significant resources. Beyond ~2,000 entities, pure MonoBehaviour starts to struggle
- **Mass collision**: Unity's standard Physics2D was not designed for 10k+ simultaneous colliders. Custom solutions (circle-circle with spatial hash) are typically 10-50x faster
- **Rendering**: individual SpriteRenderer per entity is expensive. GPU instancing or sprite batching via `Graphics.DrawMeshInstanced` drastically reduces draw calls
- **GC Pressure**: runtime allocations cause stuttering. Object pooling + structs + NativeArrays minimize garbage collection

### Recommended Technology Stack

| Aspect | Recommendation |
|--------|---------------|
| Paradigm | **DOTS/ECS** for ambitious projects (10k+ entities); **MonoBehaviour + aggressive pooling** for smaller scope (up to ~3k entities) |
| Rendering | 2D with GPU instancing, `SpriteRenderer` batching, or custom mesh rendering |
| Physics | Custom physics (no Physics2D). Circle overlap checks with spatial hashing |
| Jobs/Burst | Essential for parallel processing of AI, movement, and collision |
| Profiling | Unity Profiler + Frame Debugger from day 1 |

### Tips from Those Who Have Implemented This

- Start with a stress test prototype: spawn 5,000 entities before writing any gameplay. If framerate drops below 30fps, the base architecture needs to change
- The original Vampire Survivors proves MonoBehaviour works — but with heavy optimizations and an entity ceiling
- Consider [Arch ECS](https://github.com/genaray/Arch) as an alternative to Unity DOTS: lighter weight with a simpler API
- Sprite stacking (multiple sprite layers) is a cheap visual trick that adds depth without 3D cost

### Sources

- [Unity Case Study: Vampire Survivors — Simon Nordon](https://medium.com/@simon.nordon/unity-case-study-vampire-survivors-806eed11bebb)
- [Object Pooling in ECS — Tri-Powered Games](https://tri-powered-games.com/knowledge-center/unity-object-pooling-in-ecs)
- [Unity ECS — Visartech](https://www.visartech.com/blog/what-is-entity-component-system-ecs-and-how-to-benefit-in-unity/)
- [Unity DOTS/ECS Comprehensive Guide — Samuel Rivello](https://samuel-asher-rivello.medium.com/unity-dots-ecs-comprehensive-guide-b97a285f5fcc)
- [Optimization for Bullet Hell Games — Mikko Saari (Thesis)](https://www.theseus.fi/bitstream/handle/10024/894844/Saari_Mikko.pdf)
- [PoolECS — GitHub](https://github.com/tsartsaris/PoolECS)

---

## 2. RPG / Action-RPG

### Architectural Patterns

RPGs are games of interconnected systems. The architecture must be modular enough for inventory, quests, dialogue, and combat to evolve independently.

**ScriptableObject Architecture** is the backbone. Items, abilities, quests, dialogue — everything as ScriptableObjects. This allows game designers to create and balance content via the Inspector without touching code. ScriptableObjects also serve as **Event Channels**: a SO that acts as an event intermediary, decoupling systems that need to communicate.

**Observer Pattern** for communication between systems. When the player equips an item, the stats system needs to know, the visuals need to update, achievements may be triggered — all without direct references between them.

**State Machine** to control character states (idle, attack, dodge, stagger, death) and game states (exploration, combat, dialogue, cutscene). Hierarchical FSMs work well for characters with many states.

**Command Pattern** for player actions and undo systems. Each action (use item, equip, move) is encapsulated as a Command object, enabling replay, undo, and serialization.

**MVP (Model-View-Presenter)** for inventory, stats, and HUD UI. The Model holds the data, the View displays it, and the Presenter mediates — allowing UI swaps without touching business logic.

### Core Systems

- **Stat System**: base attributes + stackable modifiers (flat, percentage, multiplicative)
- **Inventory System**: slots, stacking, categorization, weight/capacity, serialization for save
- **Equipment System**: equipment slots, stat modifiers on equip/unequip
- **Skill/Ability System**: skill trees with prerequisites, cooldowns, resource costs, composite effects
- **Quest System**: quest states (inactive, active, completed, failed), trackable objectives, rewards
- **Dialogue System**: branching dialogue with conditions, consequences, and quest system integration
- **Combat System**: damage calculation (attack - defense + modifiers), hit detection, status effects
- **Save/Load System**: complete game state serialization (JSON, binary, or ScriptableObject-based)
- **Loot System**: loot tables with weighted random, rarity, level scaling

### Performance Challenges

- **Serialization**: RPG save files can be large. Binary serialization is faster and more compact than JSON, but less debuggable. Consider a hybrid format
- **Stat Recalculation**: with dozens of buffs/debuffs/equipment modifiers, recalculating stats every frame is costly. Use dirty flags — only recalculate when something changes
- **UI Complexity**: inventories with hundreds of items, each with custom tooltips. Object pooling for UI elements and virtualization for long lists
- **Scene Loading**: large worlds require streaming. Addressables + additive scenes allow loading/unloading areas without loading screens

### Recommended Technology Stack

| Aspect | Recommendation |
|--------|---------------|
| Paradigm | **MonoBehaviour + ScriptableObjects** — RPGs are systems-heavy but not entity-heavy. DOTS rarely justifies the complexity |
| Rendering | 2D (top-down/isometric) or 3D depending on scope. URP for most cases |
| Data | ScriptableObjects for content definition; JSON or binary for save files |
| UI | UI Toolkit or uGUI with MVP pattern |
| Dialogue | Yarn Spinner (open source) or Ink for complex narrative |

### Tips from Those Who Have Implemented This

- Invest time in the Stat System before anything else. It is the foundation that inventory, combat, and skills depend on. A poorly designed stat system contaminates the entire project
- ScriptableObjects as Event Channels (a pattern documented by Unity itself) eliminates most coupling problems
- For skill trees, consider a data-driven approach: each skill is a SO with references to prerequisites (other SOs). The graph assembles itself in the Inspector
- Do not build a dialogue system from scratch — Yarn Spinner and Ink are battle-tested and extensible

### Sources

- [ScriptableObjects as Event Channels — Unity Official](https://unity.com/how-to/scriptableobjects-event-channels-game-code)
- [Architect Code with ScriptableObjects — Unity Official](https://unity.com/how-to/architect-game-code-scriptable-objects)
- [Observer Pattern — Unity Learn](https://learn.unity.com/course/design-patterns-unity-6/tutorial/create-modular-and-maintainable-code-with-the-observer-pattern)
- [Command Pattern with ScriptableObjects — Bronson Zgeb](https://bronsonzgeb.com/index.php/2021/09/25/the-command-pattern-with-scriptable-objects/)
- [Equipment System for RPG — Pav Creations](https://pavcreations.com/equipment-system-for-an-rpg-unity-game/)
- [Quest System Architecture — Unity Discussions](https://discussions.unity.com/t/a-quest-system-architecture/840725)
- [Game Design Patterns Complete Guide 2025](https://generalistprogrammer.com/tutorials/game-design-patterns-complete-guide)

---

## 3. Roguelike / Roguelite

### Architectural Patterns

Roguelikes require everything to be procedural and modular. The architecture must support runtime generation, composition of unpredictable effects, and a clear separation between run progress and meta-progression.

**Seed-Based Procedural Generation** as an architectural principle. All RNG must pass through a deterministic seed, enabling run reproduction for debugging and sharing. Use `System.Random` with a seed instead of `UnityEngine.Random`.

**Strategy Pattern** for interchangeable generation algorithms. The same pipeline can use BSP (Binary Space Partitioning), WFC (Wave Function Collapse), or cellular automata depending on the room/level type.

**Decorator/Composite Pattern** for item effects. In games like Hades or Dead Cells, items modify behaviors in composite ways — "projectiles ricochet" + "projectiles poison" must combine without hard-coding each interaction.

**Separation of Concerns: Run vs Meta**. The run state (current HP, collected items, explored map) is volatile and resets on death. The meta state (unlocks, permanent currencies, achievement flags) persists. Architecturally, these are two completely separate save systems.

### Core Systems

- **Procedural Generation Pipeline**: map/dungeon generation in phases (layout → rooms → enemies → loot → connections)
- **Room/Level Templates**: a library of templates the generator combines. Hand-crafted templates with variable slots
- **Item/Power-up System**: items with composable effects via modifier stack or decorator chain
- **Run Manager**: controls the state of the current run (floor, rooms visited, resources, time)
- **Meta-Progression System**: permanent currencies, unlock trees, progression between runs
- **Permadeath Handler**: clears run state, calculates meta-progression rewards, transitions to hub
- **Enemy Scaling System**: difficulty that scales with floor/run number/meta-progression
- **Seed System**: global seed that feeds sub-seeds for each system (map gen, loot, enemy placement)

### Performance Challenges

- **Generation Time**: generating a complex level can take hundreds of milliseconds. Do it on the loading screen or async with coroutines/Jobs. Pre-generate the next level while the player is in the current one
- **Combinatorial Explosion**: with 100+ items that combine, testing all interactions is impossible. Architecting effects as independent modifiers reduces emergent bugs
- **Memory with Procedural Content**: generated levels can consume a lot of memory if not unloaded. Keep only the current floor + adjacent floors in memory
- **Replay/Seed Consistency**: ensuring the same seed produces the same result across all platforms requires care with floating point and operation order

### Recommended Technology Stack

| Aspect | Recommendation |
|--------|---------------|
| Paradigm | **MonoBehaviour** — roguelikes are more systems-heavy than entity-heavy. ECS only if handling enemy hordes (overlaps with Survivors genre) |
| Generation | Tilemaps for 2D. BSP + WFC as base algorithms. [GridForge](https://assetstore.unity.com/packages/tools/level-design/roguelikegenerator-pro-procedural-level-generator-224345) as reference |
| Data | ScriptableObjects for item/effect definitions; JSON for meta-save |
| Rendering | 2D top-down (URP) is most common. 3D isometric for larger projects |
| Pathfinding | A* Pathfinding Project (Aron Granberg) or NavMesh for 3D |

### Tips from Those Who Have Implemented This

- Start with the run loop (start → play → die → rewards → restart) before any content. If the loop is not satisfying with placeholders, content will not save it
- BSP for macro layout (where rooms are) + WFC for micro details (how each room is filled) is a powerful and well-documented combination
- Implement a "run log" that records every event (item picked up, room visited, damage taken). Invaluable for debugging and calculating meta-rewards
- Meta-progression needs to be tested early: if the player does not feel progress between runs, the game loses retention quickly

### Sources

- [WFC + BSP for Procedural Dungeons — Shaan Khan](https://shaankhan.dev/blog/wfc-and-bsp-for-procedural-dungeons-2021)
- [Procedural Generation in Games — Complete Guide](https://generalistprogrammer.com/procedural-generation-games)
- [Roguelike Dev Resources — GitHub (curated list)](https://github.com/marukrap/RoguelikeDevResources)
- [Create a 2D Roguelike — Unity Learn](https://learn.unity.com/project/2d-roguelike-tutorial)
- [Dungeon Generator (WFC + A* + Node Tree) — GitHub](https://github.com/Androteex/Dungeon-Generator)
- [Progression Systems in Roguelite Games — Eino Kammonen (Thesis)](https://www.theseus.fi/bitstream/handle/10024/881994/Kammonen_Eino.pdf)

---

## 4. Tower Defense

### Architectural Patterns

Tower Defense is a genre with well-defined and predictable systems. The architecture can be more rigid than other genres, focusing on composition and configuration.

**Component-Based Architecture** for towers. Each tower is a GameObject with interchangeable components: targeting (nearest, strongest, fastest), attacking (projectile, beam, AoE), special effect (slow, poison, splash). New towers are combinations of these components.

**State Pattern** for game flow: Build Phase → Wave Phase → intermission. Each state has different rules (can/cannot build, enemies spawn or not).

**Observer Pattern** for game events: enemy killed, wave started, wave completed, tower placed, resource changed. UI and scoring systems subscribe to these events.

**Factory Pattern** for creating enemies and towers. Factories read configurations from ScriptableObjects and create entities with the correct components.

### Core Systems

- **Grid/Placement System**: grid-based or free placement with snap. Position validation (path blocking, overlap, valid terrain)
- **Pathfinding System**: A* or flow fields. Must recalculate when towers are placed/removed (if towers block the path)
- **Wave Manager**: wave sequences defined in data (ScriptableObjects/JSON). Controls type, quantity, interval, and path for each enemy group
- **Tower System**: targeting, firing rate, damage, range, upgrades. Modular components
- **Enemy System**: HP, speed, resistances, rewards on death, special abilities (flying, armored, boss)
- **Economy System**: currency (gold/mana), income per kill, interest mechanics
- **Upgrade System**: tower level 1→2→3 with stat scaling, possibly branching upgrades

### Performance Challenges

- **Pathfinding Recalculation**: if towers block paths, each placement can trigger path recalculation for all enemies. Flow fields are more efficient than individual A* per enemy in this case
- **Projectile Volume**: in late game, hundreds of projectiles flying simultaneously. Object pooling is essential
- **Enemy Count**: late waves can have 100+ simultaneous enemies. Less than survivors, but still requires attention
- **Tower Range Checks**: each tower checking range against every enemy every frame is O(towers × enemies). Spatial partitioning or checking at intervals (every 0.1s) provides relief

### Recommended Technology Stack

| Aspect | Recommendation |
|--------|---------------|
| Paradigm | **MonoBehaviour** — TD is manageable with traditional OOP. Entity count rarely justifies ECS |
| Grid | Tilemap (2D) or custom grid system (3D). Grid with metadata (walkable, buildable, occupied) |
| Pathfinding | A* for few enemies, **Flow Fields** for many. [A* Pathfinding Project](https://arongranberg.com/astar/) is the reference |
| Rendering | 2D top-down or 3D isometric. URP is sufficient |
| Data | ScriptableObjects for tower/enemy/wave definitions |

### Tips from Those Who Have Implemented This

- Flow fields > A* per enemy as enemy count grows. A flow field is computed once and all enemies read from it — fixed cost regardless of entity count
- The grid system is the first and most important thing to build. Everything depends on it: placement, pathfinding, targeting, range visualization
- Wave design is game design, but the Wave Manager needs to be flexible enough to support: waves with sub-waves, delays between groups, enemies spawning from different points, mini-bosses
- Use `InvokeRepeating` or coroutines for spawning — do not spawn an entire wave all at once

### Sources

- [Demystifying Tower Defense Architecture — Cubix](https://www.cubix.co/blog/demystifying-tower-defense-game-architecture-practical-guide/)
- [Tower Defense Pathfinding — Unity Discussions](https://discussions.unity.com/t/tower-defense-pathfinding/396291)
- [Wave System for Tower Defense — Unity Discussions](https://discussions.unity.com/t/waves-system-for-a-tower-defense/904293)
- [Tower Defence Wave Manager — Unity Asset Store](https://assetstore.unity.com/packages/tools/game-toolkits/tower-defence-wave-manager-133428)
- [Procedural Content Generation for TD — ACM](https://dl.acm.org/doi/fullHtml/10.1145/3564982.3564993)
- [Automated Tower Allocation in Unity3D TD — ResearchGate](https://www.researchgate.net/publication/381660267_Intelligent_Automation_of_Tower_and_Resource_Allocation_in_Unity3D_Tower_Defense_Games)

---

## 5. Idle / Clicker

### Architectural Patterns

Idle games are deceptively simple on the surface but architecturally unique: numbers grow exponentially, the game runs offline, and the entire loop is based on math, not reflexes or skill.

**Data-Driven Architecture** is fundamental. Almost everything in an idle game is a number being modified by other numbers. Generators, upgrades, multipliers — all parameterized in data (ScriptableObjects or JSON), not in code.

**Event-Driven Updates** instead of polling. Do not recalculate everything every frame. When an upgrade is purchased, fire an event that recalculates only the affected systems.

**Prestige/Reset Pattern**: the mechanic of "resetting the game with a multiplier" requires the game state to be cleanly and easily reconstructible. Separate state into layers: Layer 0 (base), Layer 1 (first prestige), Layer 2 (second prestige), etc.

**Offline Calculation Pattern**: when the player returns, the game needs to simulate the progress that would have occurred. Two approaches: accelerated tick-by-tick simulation (more accurate, slower) or direct analytical calculation (fast but requires closed-form formulas for each system).

### Core Systems

- **Big Number System**: representation of numbers exceeding `double` (1e308). Custom scientific notation (mantissa + exponent) or libraries such as BreakInfinity
- **Generator System**: automatic producers with output/second, exponential cost, multipliers
- **Upgrade System**: upgrades that modify generators, unlock features, or change rules
- **Prestige System**: reset with meta-level currency, growing multipliers, permanent unlocks
- **Offline Progress Calculator**: simulates progress during the player's absence
- **Save System**: frequent auto-save (every 30s-60s). Robust serialization of big numbers
- **Notification System**: push notifications for important events (generator ready, milestone reached)
- **UI Number Formatter**: adaptive display (1.5K, 2.3M, 4.7B, 1.2e15, etc.)

### Performance Challenges

- **Big Number Math**: operations with custom numbers are slower than native primitives. Minimize operations per frame. Cache intermediate results
- **Offline Calculation**: simulating hours of progress in a single frame can cause a freeze. Do it async or limit precision (simulate in 1-minute chunks instead of 1-second)
- **Save File Size**: idle games with many generators/upgrades can have large saves. Serialize only the delta from the default state
- **UI Updates**: with numbers changing every frame, UI rebuilds are expensive. Update number text every 0.1s-0.5s, not every frame

### Recommended Technology Stack

| Aspect | Recommendation |
|--------|---------------|
| Paradigm | **MonoBehaviour** — idle games have few GameObjects. The complexity is in the math, not in the entities |
| Big Numbers | `double` for most cases (up to 1e308). [BreakInfinity.cs](https://github.com/Razenpok/BreakInfinity.cs) when you need to go beyond |
| Save | PlayerPrefs for prototype; JSON serialization for production. Auto-save is essential |
| UI | uGUI or UI Toolkit. TextMeshPro for number formatting |
| Platform | Mobile-first in most cases. Optimize for battery life (reduce update frequency) |

### Tips from Those Who Have Implemented This

- C#'s `double` type goes up to ±1.7×10³⁰⁸ — sufficient for most idle games. Only invest in big number libraries if you truly need to go beyond
- Prestige math: `prestigeReward = floor(totalEarnings ^ 0.5)` or similar with an exponent between 0.5 and 0.8 is the sweet spot for diminishing returns that still feel rewarding
- Offline progress: a pragmatic approach is to calculate "earnings per second" at logout time and multiply by the offline duration, with a cap. Simple and functional
- Unity Gaming Services offers an idle clicker game template with backend integration — worth referencing architecturally
- Test on low-end devices early. Idle games on mobile need to run on devices 5+ years old

### Sources

- [Clicker Games: Technical Exploration of Incremental Architecture](https://medium.com/@tommcfly2025/clicker-games-a-technical-exploration-of-incremental-system-architecture-b6d842e6963e)
- [The Math of Idle Games, Part III — Gamedeveloper](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii)
- [Dealing with Huge Numbers in Idle Games — InnoGames](https://blog.innogames.com/dealing-with-huge-numbers-in-idle-games/)
- [ClickerFramework — GitHub](https://github.com/snotwadd20/ClickerFramework)
- [Idle Clicker Game — Unity Gaming Services](https://docs.unity.com/ugs/en-us/solutions/manual/IdleClickerGame)
- [Big Numbers and Prefixes for Idle Games — Unity Discussions](https://answers.unity.com/questions/1522603/using-very-large-numbers-and-prefixes-for-an-idle.html)

---

## 6. 2D Platformer

### Architectural Patterns

2D platformers are centered on game feel — the character's response to input needs to be pixel-perfect. The architecture must prioritize fine control over physics and immediate feedback.

**Component-Based with Separation of Concerns**: separate Input Handling, Physics/Movement, and Animation into distinct components. This allows swapping the input system (keyboard vs gamepad vs touch) without touching physics, or changing animations without affecting gameplay.

**State Machine** for the player controller is nearly universal: Idle, Running, Jumping, Falling, WallSliding, Dashing, Attacking, etc. Each state has its own transition rules and physics parameters.

**Singleton Pattern** (used sparingly) for global managers: AudioManager, GameManager, LevelManager. The pattern is controversial but pragmatic for projects of controlled scope.

**Custom Physics** instead of relying 100% on Rigidbody2D. Many successful platformers use raycasts for ground/wall detection and apply movement via `transform.position` or manual `velocity`, gaining full control over feel.

### Core Systems

- **Player Controller**: state machine with movement states, customizable via parameters (gravity, jump force, coyote time, input buffer)
- **Physics System**: ground detection (raycasts), wall detection, one-way platforms, moving platforms
- **Camera System**: Cinemachine with dead zones, look-ahead, axis-differentiated smoothing (horizontal 0.15-0.25s, vertical 0.3-0.5s)
- **Level System**: Tilemaps with Rule Tiles for auto-tiling. Additive scenes for large levels
- **Hazard/Trap System**: spikes, projectiles, moving platforms, crushers — each with predictable timing
- **Checkpoint/Respawn System**: position save, respawn with invincibility frames
- **Animation System**: Animator with blend trees or sprite animation. Frame-by-frame for pixel art
- **Parallax System**: multiple background layers at different scroll speeds

### Performance Challenges

- **Physics Consistency**: Rigidbody2D in `FixedUpdate` can cause visual jitter. Interpolation (`Rigidbody2D.interpolation = Interpolate`) resolves this in most cases, but custom physics in `Update` with delta time gives more control
- **Tilemap Size**: very large maps with many tiles can impact loading and memory. Chunking or section streaming resolves this
- **Particle Systems**: excessive visual effects (dust, sparks, particles) impact mobile. Pool particles and limit emission
- **Input Latency**: any delay between input and on-screen response is fatal for this genre. Process input at the beginning of the frame, never in LateUpdate

### Recommended Technology Stack

| Aspect | Recommendation |
|--------|---------------|
| Paradigm | **MonoBehaviour** — platformers are entity-light. Focus on feel, not on volume |
| Physics | **Rigidbody2D** with heavy customization, or **custom physics** via raycasts. Physics2D settings: Gravity Y=-9.81, friction 0.4, bounciness 0 |
| Camera | **Cinemachine 2D** — dead zones, confiner, look-ahead. Official and well-documented package |
| Level Design | **Tilemap** + **Rule Tiles** + **Sprite Shape** for organic terrain |
| Input | **New Input System** for multi-platform support (keyboard, gamepad, touch) |
| Animation | **2D Animation package** or Spine for skeletal. Animator for sprite-based |

### Tips from Those Who Have Implemented This

- Coyote time (5-8 frames of grace period after leaving a platform) and input buffering (accepting jump input a few frames before landing) are essential for the game to feel responsive
- Cinemachine solves 90% of camera problems. Confiner to restrict the camera to the level, dead zone for stability, look-ahead to give visibility in the direction of movement
- For pixel art, configure: consistent `Pixel Per Unit`, `Filter Mode = Point`, `Compression = None`, and camera with orthographic size calculated for pixel-perfect rendering
- Test the controller with a gamepad from the start — the feel difference between keyboard (digital) and analog stick is significant
- Physics Material 2D with friction 0 on the character + friction on the ground gives better control than default friction on both

### Sources

- [Unity 2D Platformer Complete Tutorial 2025](https://generalistprogrammer.com/tutorials/unity-2d-platformer-complete-tutorial-game-development)
- [Game Camera Systems — Complete Guide 2025](https://generalistprogrammer.com/tutorials/game-camera-systems-complete-programming-guide-2025)
- [Camera Follow in 2D Platformer — Playgama](https://playgama.com/blog/unity/what-are-effective-techniques-for-designing-a-camera-system-that-follows-the-player-smoothly-in-a-2d-platformer-using-unity/)
- [2D Platformer Unity Template — GitHub](https://github.com/striderzz/2D-Platformer-Unity)
- [Make a 2D Platformer with Design Patterns — Udemy](https://www.udemy.com/course/unity-2020-2d-platformer/)

---

## 7. RTS / Strategy

### Architectural Patterns

RTS is probably the most architecturally complex genre. Dozens of units with individual AI, fog of war, massive pathfinding, selection/command input — all running in real time.

**Command Pattern** is central. Each player order (move, attack, patrol, build) is a Command object with `Execute()`, `Undo()`, and serialization for replay/multiplayer. This allows command queuing (shift+click) naturally.

**Event Bus** for global communication. Units should not know each other directly. When a unit dies, it fires an event. The score system, the UI, the fog system, and the audio system listen to that event independently.

**ScriptableObject-driven configs** for unit, building, and tech tree definitions. A `UnitCommandSet` ScriptableObject defines which commands a unit can execute, allowing command sets to be swapped at runtime (e.g., a promoted unit gains new commands).

**Behaviour Trees** for unit AI. More flexible than FSMs for complex behaviors with priorities (flee when low HP → attack if enemy in range → patrol → idle).

### Core Systems

- **Selection System**: box selection, click selection, control groups (Ctrl+1-9), double-click to select all of type
- **Command System**: command queue per unit, command types (move, attack-move, patrol, hold, build), waypoints
- **Unit Management**: unit registry, group behavior, formation movement
- **Pathfinding**: NavMesh or A* with local avoidance (RVO). Flow fields for large groups
- **Fog of War**: grid-based visibility (integer matrix: 0=unseen, 1=seen-but-hidden, 2=visible). Updated by each unit's vision range
- **Building/Production System**: construction queue, resource cost, tech prerequisites, rally points
- **Resource System**: gatherers, drop-off points, resource nodes with finite quantity
- **Tech Tree**: prerequisites, research time, unit/ability/upgrade unlocks
- **Minimap System**: real-time map representation with fog of war, unit positions, alerts

### Performance Challenges

- **Pathfinding for many units**: individual A* per unit does not scale. Flow fields or hierarchical pathfinding (HPA*) are necessary for 100+ units
- **Fog of War update**: updating visibility for each unit every frame is expensive. Batch updates, lower grid resolution, or update every N frames
- **Unit AI ticking**: hundreds of units running behaviour trees every frame. Distribute ticks across frames (10% of units per frame = each unit updates every 10 frames)
- **Networking (multiplayer)**: RTS multiplayer requires lockstep simulation — complete determinism. Floating point inconsistencies between platforms are the biggest challenge

### Recommended Technology Stack

| Aspect | Recommendation |
|--------|---------------|
| Paradigm | **Hybrid**: MonoBehaviour for systems + **ECS/DOTS** for units at scale (if 200+ simultaneous units). Pure MonoBehaviour viable up to ~100 units |
| Pathfinding | **Flow Fields** for groups, **NavMesh + RVO** for local avoidance, or [A* Pathfinding Project Pro](https://arongranberg.com/astar/) |
| Fog of War | Integer grid + shader that renders fog as an overlay. [RTS Engine Fog Module](https://docs.gamedevspice.com/rtsengine/manual/08_Modules/04_Fog_Of_War.html) as reference |
| AI | **Behaviour Trees** (NodeCanvas, Behaviour Designer, or custom). Not FSMs — RTS AI complexity outgrows FSMs quickly |
| Networking | Mirror or Netcode for GameObjects with lockstep model |
| Rendering | 3D (URP or HDRP) for most projects. 2D isometric viable for smaller projects |

### Tips from Those Who Have Implemented This

- The Command Pattern is not optional — it is the foundation. Without it, command queuing, waypoints, and replay are impossible to add later
- Fog of War: start with a simple grid (2D array of ints) rendered as a texture. Each unit defines a vision radius and marks cells as visible. Works surprisingly well
- For unit selection, each selectable unit needs a `SelectableUnit` component with a reference to its `UnitCommandSet`. Box selection uses `Physics.OverlapBox` or a screen-space rect check
- Event Bus > direct references. A `UnitDiedEvent` that carries the unit and the killer allows 10 different systems to react without coupling
- Consider [RTS Engine](https://assetstore.unity.com/packages/tools/game-toolkits/rts-engine-2022-79732) on the Asset Store as an architectural reference, even if you do not use it directly

### Sources

- [Architecture for Modular RTS Unit Commands — Unity Discussions](https://discussions.unity.com/t/architecture-for-modular-rts-unit-commands/853747)
- [Fog of War for RTS in Unity — Gemserk](https://blog.gemserk.com/2018/08/27/implementing-fog-of-war-for-rts-games-in-unity-1-2/)
- [Fog of War Module — RTS Engine Docs](https://docs.gamedevspice.com/rtsengine/manual/08_Modules/04_Fog_Of_War.html)
- [RTS Tutorials (complete series) — Mina Pecheux / GitHub](https://github.com/MinaPecheux/UnityTutorials-RTS)
- [How to Make: RTS (They Are Billions style) — Code Monkey](https://unitycodemonkey.com/howtomakegame.php?i=theyarebillions)
- [Fog of War for RTS — Unity Discussions](https://discussions.unity.com/t/released-fog-of-war-for-rts-games/1699229)

---

## 8. Card Game / Deckbuilder

### Architectural Patterns

Card games are games of data and rules. The architecture must represent hundreds of unique cards with composite effects, manage turn-based game state, and keep everything extensible for expansions.

**ScriptableObject per Card** is the universal standard. Each card is a SO with fields such as: name, cost, artwork, description, and references to effects. Effects are also SOs, enabling composition: a card can have [DealDamage(5), DrawCards(2), ApplyPoison(3)] as a list of effects.

**Command Pattern** for game actions. Each "play card", "draw card", "end turn" is a Command that can be executed, undone, and logged. This enables replay, undo, and network sync trivially.

**State Machine** for turn phases: DrawPhase → MainPhase → CombatPhase → EndPhase. Each phase has rules about what the player is allowed to do.

**Effects Pipeline / Chain of Responsibility**: when a card is played, its effect passes through a pipeline that can be intercepted by other active effects (trigger effects, reaction effects, modifiers). Example: "when you draw a card, gain 1 armor" intercepts the DrawCard event in the pipeline.

### Core Systems

- **Card Data System**: ScriptableObjects for card definitions. Each card references a list of CardEffects (also SOs)
- **Deck Manager**: draw pile, hand, discard pile, exhaust pile. Operations: shuffle, draw, discard, exhaust, search
- **Hand Manager**: visual layout of cards in hand, hover/select feedback, drag-to-play, hand limit
- **Effects Pipeline**: system that executes card effects in sequence, allowing triggers and interrupts from other active effects
- **Turn/Phase Manager**: state machine that controls turn phases and what is permitted in each
- **Target System**: target selection for cards that require a target (single, multiple, area, random)
- **Combat Resolution**: damage calculation with modifiers (armor, vulnerability, buffs/debuffs)
- **Reward/Draft System**: selection of new cards from options after combat (Slay the Spire style)
- **Relic/Artifact System**: permanent passive modifiers that intercept the effects pipeline

### Performance Challenges

- **Effects Combinatorics**: with 200+ cards and 50+ relics, the number of possible interactions is enormous. The effects pipeline must be robust and testable
- **UI Animation Sequencing**: cards moving from hand to field, visual effects, damage numbers — all must happen in sequence visually but be resolved instantaneously in logic. Separate game logic resolution from visual presentation
- **Undo Complexity**: if allowing undo, each action must be reversible. The Command Pattern resolves this, but effects with randomness (draw random card) are irreversible by design
- **Save State**: saving mid-combat requires a complete snapshot: deck state, hand, discard, relics, HP, buffs, enemy intents, RNG seed position

### Recommended Technology Stack

| Aspect | Recommendation |
|--------|---------------|
| Paradigm | **MonoBehaviour + ScriptableObjects** — card games are 100% systems, zero entity pressure. DOTS is completely unnecessary |
| Card Data | ScriptableObjects with custom editors. CardEffect as abstract SO with subclasses for each effect type |
| UI | **DOTween** or **LeanTween** for card animations (draw, play, discard). uGUI with Canvas for hand layout |
| State | **State Machine** for turns/phases. Can be enum-based simple or hierarchical |
| Networking | Authoritative server model. Clients send intentions (play card X targeting Y), server validates and resolves |
| Testing | **Unit tests** for the effects pipeline are more important here than in any other genre |

### Tips from Those Who Have Implemented This

- The separation between game logic and visual presentation is the most important architectural decision. The logic resolves "card X deals 5 damage to enemy Y" instantaneously. Then the visual layer animates the card flying, the damage appearing, the HP bar decreasing. Mixing the two turns the code into spaghetti
- Card effects as polymorphic ScriptableObjects is powerful but creates an Inspector problem: each effect has different parameters. Custom PropertyDrawers or Odin Inspector solve this
- Start with a system of 20 generic cards before implementing unique cards. If the pipeline works with DealDamage, Heal, DrawCard, and GainBlock, it will likely work with any effect
- For roguelite deckbuilders (Slay the Spire style), the architecture must integrate run management + card system + procedural encounters. Treat it as two genres combined

### Sources

- [Unity Card Game Architecture Ideas — David Rector](https://blog.rectorsquid.com/unity-card-game-architecture-ideas/)
- [Creating a Card Game System in Unity — Unity Coder Corner](https://medium.com/unity-coder-corner/unity-creating-a-card-game-ac7f46365a50)
- [Card Game Architecture — Unity Discussions](https://forum.unity.com/threads/i-need-help-to-define-an-architecture-for-my-card-game.1068815/)
- [Turn-Based Game Architecture Guide — Outscal](https://outscal.com/blog/turn-based-game-architecture)
- [Card Effects as ScriptableObjects — Unity Discussions](https://discussions.unity.com/t/card-game-cards-as-scriptableobject-card-effects-as-scriptableobjects-effects-have-different-parameters-how-to-make-an-inspector-ui-support-this-structure/203396)
- [Card Game Core — Unity Asset Store](https://assetstore.unity.com/packages/templates/systems/card-game-core-tcg-ccg-system-with-deck-builder-284361)

---

## Comparison Table — Quick Reference

| Genre | Primary Paradigm | Entity Count | Challenge #1 | Dominant Pattern |
|-------|-----------------|-------------|--------------|-----------------|
| Survivors/Horde | DOTS/ECS or heavy pooling | 5,000-50,000 | Entity performance | Data-Oriented Design |
| RPG/Action-RPG | MonoBehaviour + SO | 10-100 | System complexity | Observer + SO Events |
| Roguelike/Roguelite | MonoBehaviour | 50-500 | Procedural generation | Strategy + Decorator |
| Tower Defense | MonoBehaviour | 100-500 | Pathfinding | Component + Factory |
| Idle/Clicker | MonoBehaviour | <20 | Big number math | Data-Driven + Event |
| 2D Platformer | MonoBehaviour | <50 | Game feel / physics | State Machine |
| RTS/Strategy | Hybrid (MB + ECS) | 200-1,000 | Pathfinding + mass AI | Command + Event Bus |
| Card/Deckbuilder | MonoBehaviour + SO | <30 | Effects pipeline | Command + Chain of Resp. |

---

*Document generated in April 2026 as an architectural reference for game development in Unity.*
