# Case Studies: Architectural Decisions and Lessons Learned

> Research and synthesis of 12 projects with a focus on software architecture, patterns, and actionable insights for solo/indie devs.

---

## Table of Contents

1. [Vampire Survivors](#1-vampire-survivors)
2. [Hollow Knight](#2-hollow-knight)
3. [Cult of the Lamb](#3-cult-of-the-lamb)
4. [Cities: Skylines 2](#4-cities-skylines-2)
5. [Among Us](#5-among-us)
6. [Fall Guys](#6-fall-guys)
7. [Slay the Spire](#7-slay-the-spire)
8. [Dead Cells](#8-dead-cells)
9. [Genshin Impact](#9-genshin-impact)
10. [Epic Survivors](#10-epic-survivors)
11. [DOTS Survivors](#11-dots-survivors)
12. [Open-Source Projects (PaddleGameSO, SO Architecture, Unity Patterns)](#12-open-source-projects)
13. [Conclusion: Recurring Patterns](#conclusion-recurring-patterns-across-the-cases)

---

## 1. Vampire Survivors

### Overview

Vampire Survivors is a minimalist roguelike created by Luca Galante (poncle), initially as a solo dev. It started as a free browser game in Phaser (HTML5/JavaScript) on itch.io in March 2021, then migrated to Unity in August 2023 to support the massive scale of entities. It sold millions of copies at $2.99, won BAFTA 2023 beating God of War: Ragnarok and Elden Ring, and accumulated an estimated £40M in revenue. The team grew from 1 to 25+ people.

### Technology Stack

- **Original engine:** Phaser (HTML5, JavaScript)
- **Current engine:** Unity (C#), IL2CPP compilation
- **IDE:** Visual Studio
- **Migration:** August 2023 — Phaser could not handle thousands of simultaneous sprites

### Key Architectural Decisions

**God Object Singleton (GameManager):** Controls all application state from a centralized singleton. A common pattern in Unity, functional for small scope, but creates tight coupling and scales poorly as the team grows.

**Object Pool for Enemies:** A centralized and well-optimized pool for reusing GameObjects. Spawns in waves every minute, with a cap of ~300 simultaneous enemies (only bosses spawn above that). Allows 100K+ kills per run without GC pressure.

**Finite State Machine:** Basic state management; attempts at Dependency Injection via IoC containers were partially implemented.

**3-Scene Structure:** Minimalist design — main gameplay scene, menu, and an auxiliary one. A lean approach focused on the essentials.

**Performance Mode:** Scales visual effects and physics calculations for weaker hardware. Damage numbers can be turned off to reduce draw calls.

### What Went Right

- Simplicity as a feature: movement is the only interaction, no manual aiming
- Power fantasy via rapid scaling — addictive "one more run" loop
- Gambling psychology applied ethically (chest animations, reward layers)
- Pragmatic engine migration when scale demanded it
- Intentionally "janky" pixel art became a visual identity
- Respectful monetization (free-to-play with optional ads)

### What Went Wrong

- **God Object antipattern:** tight coupling, difficult to modify without cascading changes
- **IMGUI UI System:** hardcoded in scene hierarchy, does not support simultaneous designer/programmer work
- **Fragile serialization:** entire save in one giant JSON, risk of corruption on version upgrades
- **Legacy Phaser code:** enemies still treated as Phaser physics bodies
- **Solo architecture did not scale:** growing to 25+ devs required significant refactoring

### Lessons for Solo/Indie Devs

- Start simple, iterate fast — "I didn't have a vision, I just put in elements to make it fun"
- God Object is acceptable for small scope, but plan to refactor
- Choose engine based on scalability, not preference — be willing to migrate
- Player psychology > features — understand competence and autonomy
- Graphic perfectionism is overrated — Vampire Survivors thrived with simple pixel art

### Sources

- [Unity Case Study: Vampire Survivors (Simon Nordon)](https://medium.com/@simon.nordon/unity-case-study-vampire-survivors-806eed11bebb)
- [Game Developer — VS development as open-source fever dream](https://www.gamedeveloper.com/design/vampire-survivors-development-sounds-like-an-open-source-fueled-fever-dream)
- [The Conversation — Gambling psychology in VS](https://theconversation.com/vampire-survivors-how-developers-used-gambling-psychology-to-create-a-bafta-winning-game-203613)
- [coherence Blog — VS Online Multiplayer Co-Op Case Study](https://coherence.io/blog/tradecraft/vampire-survivors-online-coop-case-study)
- [PC Gamer — Creator interview](https://www.pcgamer.com/vampire-survivors-creator-didnt-have-a-vision-when-he-started-making-the-game-that-allowed-him-to-quit-his-job/)

---

## 2. Hollow Knight

### Overview

Hollow Knight is a 2D Metroidvania developed by Team Cherry, a 4-person team in Adelaide, Australia. Released in February 2017, it generated $30M+ in revenue. The game is an interconnected world (not isolated levels) with exploration, combat, and memorable boss fights.

### Technology Stack

- **Engine:** Unity 5.6 (upgrading to Unity 6 for Silksong)
- **Visual Scripting:** PlayMaker (visual FSM for gameplay logic)
- **Plugins:** 2D Toolkit, TextMesh Pro, Sprite Packer
- **Art:** Hand-drawn animations in Photoshop exported as PNGs
- **Shaders:** sprite_default and sprite_diffuse with minimal modifications
- **Lighting:** Soft transparent shapes (not complex 3D systems)

### Key Architectural Decisions

**Room-Based Architecture:** Each area is a separate scene in Unity. Transitions between rooms use TransitionPoint components that trigger seamless loading of the next scene.

**Boss AI via PlayMaker FSM:** All boss behavior logic implemented as visual state machines in PlayMaker. Allows rapid iteration without code recompilation.

**State-Based Animation System:** Sprites with states: Idle, Run, Jump, Dash, Wall Slide, Wall Jump. Binary movement (maximum speed or stopped) using Unity 2D Physics.

**Save System:** 4 save slots, bench-based checkpoints, per-room state persistence.

### What Went Right

- Strategic use of proven Asset Store tools (PlayMaker saved months)
- Visual scripting enabled rapid gameplay iteration
- Hand-drawn art created a distinct visual identity
- Room-based architecture is efficient in memory and iteration
- Lean team with clear specialization (4 people, each with a defined role)
- Low cost of living in Adelaide extended development runway

### What Went Wrong

- CPU-bound performance on older hardware (particle systems, physics)
- PlayMaker creates a "black box" — logic is not readable code, hinders debugging
- Legacy constraints of Unity 5.6
- Limited save system (checkpoint only at benches, 4 slots)
- Disorganized development led to feature creep
- Minimal published technical documentation (hinders modding and analysis)

### Lessons for Solo/Indie Devs

- Use proven tools instead of reinventing the wheel
- Visual scripting accelerates iteration but creates technical debt
- Invest in art quality for a distinct identity
- Room-based architecture is excellent for Metroidvanias in Unity
- Small teams excel with clear specialization
- Plan for ports and modding from the architecture design phase

### Sources

- [Team Cherry Blog](https://www.teamcherry.com.au/blog/)
- [Source Gaming — Team Cherry Interview](https://sourcegaming.info/2019/04/18/team-cherry-talks-hollow-knight/)
- [Annotated Learning — Hollow Knight Technical Analysis](https://annotatedlearning.com/)
- [GitHub — Hollow Knight Modding API](https://github.com/hk-modding)
- [MCV/DEVELOP — Team Cherry Interview](https://mcvuk.com/)

---

## 3. Cult of the Lamb

### Overview

Cult of the Lamb combines roguelite dungeon crawling with colony simulation, developed by Massive Monster (Melbourne, ~4-5 core devs). Released in August 2022 by Devolver Digital, it sold 1 million copies in 10 days. The game balances two complete systems: crusades (combat) and base management (colony).

### Technology Stack

- **Engine:** Unity 2021.3.16
- **Animation:** Spine Animation (skeletal 2D in 3D world)
- **Input:** Unity Input Actions
- **Data:** ScriptableObjects for weapons and items
- **Visual:** 2D/3D hybrid — hand-drawn sprites rotated for the camera in 3D space
- **Integration:** Twitch Integration for streaming

### Key Architectural Decisions

**Genre Fusion with Intentional Simplification:** Each half (roguelite + colony sim) was deliberately simplified to avoid overwhelming the player. Getting the feedback loop between systems right took years of experimentation.

**Follower AI:** Procedurally generated NPCs with traits that affect behavior (insomnia, tendency to conspire, etc.). Traits influence faith and reactions to the player's actions.

**Procedural Dungeon Generation:** Node-based map (similar to Slay the Spire) with node types: combat, resources, followers, shops, bosses. Each crusade generates a randomized layout while maintaining handcrafted artistic quality.

**State Management Between Systems:** Bidirectional resource flow: combat generates gold/materials/followers → base generates faith/devotion/upgrades → upgrades improve future combat. Persistence via JSON + Steam Cloud.

### What Went Right

- **3-Pillar Design:** Defined at the start, every feature filtered through them
- **Aggressive iteration:** ~60% of the work was cut; a culture of speed and detachment
- **Late-stage emergence:** The game was "a pile of garbage" until ~9 months before release because systems-driven games only click with critical mass
- **Meme-able aesthetic:** Cute/horror art balance went viral
- **Social marketing from day 1:** 150K+ Twitter followers, 60K+ Discord in 3 months

### What Went Wrong

- **Buggy launch:** Failing quests, items not appearing, crashes, black screens
- **Performance with 50+ followers:** Significant lag, 16GB RAM recommended vs 8GB official
- **Shadows as bottleneck:** Shadow rendering was the main performance bottleneck
- **Vulnerable save system:** JSON susceptible to corruption, without robust validation
- **Switch version:** Serious memory management problems

### Lessons for Solo/Indie Devs

- Genre fusion requires ruthless simplification on each side
- 3-pillar design works — filter everything through it
- Accept that 60% of the work will be cut
- Invest in a robust save system from the start
- Plan for scaling (follower count, long play sessions)
- Art as a strategic asset: distinct aesthetic = organic marketing

### Sources

- [Game World Observer — Massive Monster Interview](https://gameworldobserver.com/2022/08/12/cult-of-the-lamb-interview-massive-monster)
- [Game Developer — Cute aesthetic & darker themes](https://www.gamedeveloper.com/design/interview-corralling-the-inherent-cuteness-of-cult-of-the-lamb)
- [Unity Blog — Recipe behind Cult of the Lamb](https://unity.com/blog/games/recipe-behind-smash-hit-cult-of-the-lamb)
- [GDC 2023 — Growing an Internet Cult (Marketing Postmortem)](https://gdcvault.com/play/1029153/Growing-an-Internet-Cult-Cult)
- [itch.io — Making mechanics of CotL in Unity (series)](https://itch.io/blog/536578/making-mechanics-of-cult-of-the-lamb-in-unity-movement-camera)

---

## 4. Cities: Skylines 2

### Overview

Cities: Skylines 2 is a city simulator by Colossal Order, using Unity 2022.3.7 with a full DOTS/ECS architecture. It supports cities with 1M+ inhabitants, each citizen with an individual lifepath, traffic AI, and complex economic systems. The October 2023 launch was marked by serious performance problems.

### Technology Stack

- **Engine:** Unity 2022.3.7
- **Architecture:** Full DOTS/ECS (Entity Component System)
- **Compiler:** Burst Compiler for loop optimization
- **Jobs:** Unity Jobs System for multithreading
- **Rendering:** Direct3D 11 + HDRP (High Definition Render Pipeline)
- **Custom:** Custom rendering implementation (did not use the Entities Graphics package)

### Key Architectural Decisions

**Full Migration to DOTS/ECS:** Completely abandoned traditional OOP. All simulation (citizens, vehicles, buildings) as entities with components processed by systems. A radical decision that promised massive performance.

**Mass Entity Simulation:** Each citizen is an individual entity with its own AI — lifepath, employment, needs, pathfinding. Vehicles, buildings, and infrastructure are also entities.

**Custom Rendering (NOT Entities Graphics):** Unity's Entities Graphics package was too immature. Colossal Order implemented custom rendering, which proved problematic.

### What Went Right

- **CPU Simulation:** 10-40x speedups via Burst Compiler in simulation calculations
- **Efficient multithreading:** Real utilization of all CPU cores
- **Unprecedented scale:** Individual simulation of each citizen (impossible in traditional OOP)
- **Proof of concept for ECS:** Demonstrated that ECS works for massive simulation

### What Went Wrong

- **Catastrophic GPU Bottleneck:** 121M vertices/frame, 7,000 draw calls — completely GPU-bound
- **No Level of Detail (LOD):** Entire objects rendered regardless of distance
- **Inadequate occlusion culling:** Hidden objects still rendered
- **Immature HDRP:** Integration with HDRP caused additional problems
- **Unacceptable performance at launch:** Devastating reviews, frustrated playerbase
- **Iceflake Studios took over:** In 2026 Colossal Order transferred development

### Lessons for Solo/Indie Devs

- **ECS excels at simulation, not graphics** — CPU gains do not compensate if the GPU is the bottleneck
- **Avoid beta technology in AAA production** — Entities Graphics and HDRP were not ready
- **Profile early and frequently** on target hardware
- **LOD and occlusion culling are not optional** — they are fundamental
- **Rendering complexity requires core architectural decisions** — it cannot be fixed afterwards
- **Custom solutions carry hidden costs** of maintenance and debugging

### Sources

- [Tom's Hardware — CS2 Performance Analysis](https://www.tomshardware.com/)
- [PC Gamer — CS2 Technical Deep-Dive](https://www.pcgamer.com/)
- [Windows Central — CS2 DOTS/ECS Analysis](https://www.windowscentral.com/)
- [Hacker News — CS2 Architecture Discussion](https://news.ycombinator.com/)
- [GamersNexus — CS2 GPU Benchmarks](https://www.gamersnexus.net/)

---

## 5. Among Us

### Overview

Among Us is a social deduction game made by InnerSloth, originally a 3-person team (Forest Willard as the sole programmer). Released in June 2018, it had 30-50 simultaneous players for 2 years before going virally explosive in 2020 via Twitch. It reached 500M+ players, generated $275.8M in revenue (2024), and grew to 74 people.

### Technology Stack

- **Engine:** Unity (C#)
- **Original networking:** Photon PUN2 (Photon Unity Networking)
- **Current networking:** Custom UDP via Hazel protocol (custom reliability layer over stateless UDP)
- **Ports:** UDP 22023-22923 (multiple for load distribution)
- **Infrastructure:** Amazon AWS with hardcoded IPs in the binary
- **Backend:** Unity Gaming Services for 500M+ players
- **Console ports:** Partnership with Schell Games

### Key Architectural Decisions

**Simple Client-Server Networking:** Lightweight protocol based on UDP with a custom reliability layer. Packets prefixed with length (uint16) + tag byte. Reliable packets (data[0] == 01) for state, unreliable for movement/pings.

**State Machine for Phases:** Lobby → Task Phase → Meeting (Discussion → Voting). Transitions broadcast over the network to synchronize all clients.

**Unified Cross-Platform:** Single codebase with rendering/input layers per platform. Forced cross-play (no platform segregation). Adapted controls: keyboard (PC), touch (mobile), controller (console).

**Asymmetric Information Design:** Impostors know each other, crewmates deduce from behavior. Tasks as minigames, sabotage with impact scale (lights → oxygen).

### What Went Right

- Simplicity and accessibility: minimal learning curve, even non-gamers play
- Organic viralization via streamers (Twitch, 2020)
- Lightweight networking scaled to 3M+ simultaneous players
- Persistence: maintained server for 2 years with 30-50 players until the viral moment
- Cross-platform created massive network effects

### What Went Wrong

- **Hardcoded IPs in the binary:** Impossible to migrate infrastructure without a client update — a catastrophic decision
- **AWS Burstable CPU:** Above 200 concurrent players, CPU credits drained, capped at 10%
- **Admitted spaghetti code:** Codebase grew organically without architectural planning
- **Devastating cheating:** Unity DLLs decompilable via dnSpy/dotPeek; ESP hacks, speed hacks, forced voting
- **Reactive anti-cheat:** Server-side fixes introduced new bugs; bypasses appeared quickly
- **Severe burnout:** One programmer worked 12h/day for weeks; the team considered leaving the industry

### Lessons for Solo/Indie Devs

- **Don't kill a game early:** Among Us persisted with 30 players for 2 years before going viral
- **Plan for 100x scaling:** Abstractions from day 1 (never hardcode IPs or infrastructure configs)
- **UDP > TCP for latency:** A custom reliability layer works better than pure TCP
- **Client-side logic will be hacked:** Decompilable DLLs are vulnerable; authority on the server
- **Simplicity ≠ sloppiness:** Establish architectural boundaries even in simple projects
- **Prepare for success:** Infrastructure, team scaling, and burnout are underestimated

### Sources

- [Unity — Among Us Case Study](https://unity.com/resources/innersloth-among-us)
- [GitHub — Among Us Protocol Documentation](https://github.com/roobscoob/among-us-protocol)
- [Netify — Among Us UDP Architecture](https://www.netify.ai/resources/protocols/among-us)
- [Kotaku — Developer Burnout](https://kotaku.com/among-us-developers-say-they-burnt-out-after-twitch-success-1847122851)
- [Tenable TechBlog — Hacking in Among Us](https://medium.com/tenable-techblog/hacking-in-among-us-b43ea0fdd3d7)
- [ResetEra — Server Issues Explained](https://www.resetera.com/threads/among-us-sole-developer-explains-server-issues-has-been-working-12-hours-for-a-week.281237/)
- [How To Market A Game — Viral Success Lessons](https://howtomarketagame.com/2020/09/14/among-us-the-4-lessons-of-their-viral-success/)

---

## 6. Fall Guys

### Overview

Fall Guys is a physics-based battle royale for 60 players, developed by Mediatonic (~30 people initially, growing to 150+). The concept was inspired by Takeshi's Castle, with 18 months of development. The August 2020 launch reached 1.5M players in 24 hours and $150.5M in revenue that same year. It became the most downloaded game in PS Plus history.

### Technology Stack

- **Engine:** Unity (2019-2020 era version)
- **Networking:** Manually modified UNet + ProtoBuf for client-server communication
- **Physics:** Custom ragdoll system (third-party solutions did not work over the network)
- **Technical partner:** The Multiplayer Group (MPG) for multiplayer engineering
- **Infrastructure:** PostgreSQL → Microsoft Azure (Kubernetes, Cosmos DB)
- **Anti-cheat:** Easy Anti-Cheat

### Key Architectural Decisions

**Physics-Based with 60 Players:** Mediatonic developed their own ragdoll system because third-party solutions did not network well and killed the frame rate. "Wobbly physics" with gelatinous masses and a high center of gravity. Intentional input delay simulating the clumsy control of a costume.

**Networking for Physics Synchronization:** Modified UNet HLAPI + ProtoBuf. Client/server authoritative with real-time replication of animation and physics state. Code kept "clean, simple and optimal" — pragmatism over architectural purity.

**UGC System (Creative Mode):** Launched in May 2023 with a visual editor for obstacles. Three modes: COURSE, SURVIVAL, POINTS. Budget of 2500 memory units per level. 12-digit share codes. Curation by Mediatonic for official rotation.

**Infrastructure Scaling:** Designed for ~80K concurrent; reached 650K (8x). Migration to Azure Kubernetes with auto-scaling. Login queue during F2P surges.

### What Went Right

- PS Plus day-one inclusion = 17.9M MAU in August 2020
- Ragdoll failures as viral content (memes, stream clips)
- Perfect session duration for streaming (5-40 min)
- Proved Unity is capable of 60-player physics-heavy multiplayer
- Cross-platform with cross-progression

### What Went Wrong

- **Day one overload:** 650K vs 80K projected — unstable matchmaking for 72h
- **PostgreSQL did not scale:** Migration to Azure Kubernetes was necessary
- **Anti-cheat with false positives:** Easy Anti-Cheat conflicted with security software
- **Post-viral fatigue:** Natural playerbase decline after the peak
- **Physics vs competitive:** Chaos/luck frustrates competitive players

### Lessons for Solo/Indie Devs

- Expect 10x peak load during viral launches
- Custom physics is worth the investment when it is core to the game's identity
- Partnering with networking specialists reduces technical risk (MPG was crucial)
- Streamability as a design requirement: failing should be fun to watch
- Short sessions = better for streaming content creation
- Cloud infrastructure with auto-scaling from day 1

### Sources

- [The Multiplayer Group — Fall Guys Case Study](https://www.themultiplayergroup.com/case-studies/fall-guys)
- [GDC Vault — Terminal Velocity: Server Scaling Lessons](https://gdcvault.com/play/1034308/Terminal-Velocity-Lessons-Learned-from)
- [Microsoft Azure — Record-breaking Fall Guys scaling](https://developer.microsoft.com/en-us/games/articles/2021/11/record-breaking-fall-guys-scales-faster-with-azure/)
- [Unity — Supporting the Meteoric Rise of Fall Guys](https://resources.unity.com/games/supporting-the-meteoric-rise-of-fall-guys)
- [Game Dev Unchained — Joe Walsh Interview](https://gamedevunchained.com/2021/01/05/designing-fall-guys-with-joe-walsh/)
- [Deconstructor of Fun — UGC Case Study](https://www.deconstructoroffun.com/blog/2023/5/31/the-hunger-games-for-user-generated-content-case-fall-guys)
- [GitHub — FallGuys ProtoBuf Protocol](https://github.com/klukule/FallGuys.Protocol)

---

## 7. Slay the Spire

### Overview

Slay the Spire is a roguelike deckbuilder made by 2 developers at Mega Crit Games (Seattle). Built in Java with LibGDX (not Unity), it is the reference case for card game architecture. Early Access in 2017, full release in January 2019, sold 1.5M+ copies. Slay the Spire 2 migrated to Godot. Included here for its architectural patterns applicable to Unity.

### Technology Stack

- **Framework:** LibGDX (Java) with LWJGL
- **Console ports:** C# → C++ (by Sickhead Games)
- **StS2 Engine:** Godot (migration driven by frustration with LibGDX broken by OS updates)
- **Modding:** ModTheSpire + BaseMod (open-source Java APIs)

### Key Architectural Decisions

**Card System as Command Objects:** Cards are command objects with data in ScriptableObject-like structures. Clear separation of game data vs game logic. Three types: Attack (damage), Skill (utility), Power (permanent upgrades per combat).

**Relics/Effects Pipeline:** Synergies built where at least one half works standalone, the other half changes the puzzle. Principle: "Cards should solve SOME problems. If any choice is obviously the best regardless of context, the design has failed."

**Procedural Map Generation (Node-Based):** Irregular 7x15 grid with triangular cells. Constraints: first 2 rooms not identical, paths do not cross, Elite/Merchant/Rest Site not consecutive. Creates meaningful strategic decisions.

**Intent System (Crucial Innovation):** Telegraphing enemy actions for the next turn. Evolution: text "Next Turn" → icons → exact damage numbers. Exact numbers were crucial for engagement.

**Modding Architecture:** ModTheSpire as an external loader + BaseMod as a high-level API. In StS2: JSON payloads + Godot scene system + BepInEx. Data-driven by design.

### What Went Right

- **Metrics-Driven Design:** GDC talk on balancing by data and Early Access metrics
- **Weekly patching:** Weekly update cadence during EA built momentum
- **Intent system:** Revolutionized UX for turn-based card games by removing hidden information
- **Vibrant modding community:** Open APIs created one of the largest modding communities

### What Went Wrong

- **LibGDX frequently broken** by OS updates, no console support
- **Early Access started slow:** ~800 copies in the first 3 days (saved by a Chinese streamer)
- **Limited Daily Challenge:** Only ~12 modifiers, often easier than the base game
- **Fixed Ascension scaling:** No modular difficulty customization

### Lessons for Solo/Indie Devs (Card Games in Unity)

- **Data-driven design is essential:** Separate logic from data (JSON, ScriptableObjects)
- **Modular effect systems:** Factory + Prototype + Component patterns for cards/relics
- **MVC for game state:** Models (rules), Views (UI), Controllers (input → commands)
- **Intent system:** Telegraphing enemy actions removes frustration and increases engagement
- **No dominant strategies:** Each solution should introduce new problems
- **Moddability from day 1:** Multiplies value and longevity of the game
- Unity templates: [NueDeck](https://github.com/Arefnue/NueDeck), [DeckBuilder Roguelike](https://github.com/K-mohameduuu/DeckBuilderRoguelikeEngine)

### Sources

- [GDC Vault — Slay the Spire: Metrics Driven Design and Balance](https://www.gdcvault.com/play/1025731/-Slay-the-Spire-Metrics)
- [GDC Vault — Success Through Marketability](https://www.gdcvault.com/play/1025667/-Slay-the-Spire-Success)
- [Cloudfall Studios — Game Design Tips from StS](https://www.cloudfallstudios.com/blog/2020/11/2/game-design-tips-reverse-engineering-slay-the-spires-decisions)
- [Steam Community — Procedural Map Analysis](https://steamcommunity.com/sharedfiles/filedetails/?id=2830078257)
- [Game Architecture for Card Games (Part 1)](https://bennycheung.github.io/game-architecture-card-ai-1)
- [ArXiv — Analysis of Uncertainty in StS Procedural Maps](https://arxiv.org/html/2504.03918v1)

---

## 8. Dead Cells

### Overview

Dead Cells is a "roguevania" 2D by Motion Twin, a French workers' cooperative with 8-10 people (equal salaries, collective decisions). Custom engine in Haxe/Heaps (not Unity), included here for its exceptional architectural patterns. Early Access May 2017, full release August 2018, 10M+ copies sold. 34+ free updates. Evil Empire created in 2019 as a spin-off (50+ people) for ongoing support.

### Technology Stack

- **Language:** Haxe (typed OOP + functional + macros)
- **Engine:** Heaps.io (lightweight, customizable 2D/3D scene graph)
- **VM:** HashLink (cross-platform compilation, JIT or native C for consoles)
- **Relevant tool:** LDtk (2D editor created by the Dead Cells director)

### Key Architectural Decisions

**Hybrid Procedural Generation (Handcrafted + Procedural):** Hand-crafted rooms assembled procedurally via a Concept Graph. The graph defines: level length, special tiles, labyrinthine density, entry/exit separation. For each node, the algorithm tries a random room template until it finds one that complies with the graph instructions.

**3D→2D Animation Pipeline (Innovation):** 3D models in 3DS Max, animated with skeleton/keyframes, exported FBX, rendered to 2D sprites via a homebrew tool. One artist (Thomas Vasseur) created ALL characters, monsters, animations, and effects for 1+ year. Flexibility: changing a weapon's weight does not require redrawing the entire animation.

**Data-Driven Enemy Design:** Stats dynamically scaled via formula: `Base HP × (ScaleFactor^(Tier-1)) × (1 + (Tier-1) × ScaleMulPerTier) × (1 + EnemyTypeBonus)`. Variables in configuration files, allowing balance without programming.

**Combat System:** Weapon types with 3 scaling stats (Brutality, Tactics, Survival). Rally Health (recover health by dealing damage within the "rally" window). Parry with shields. Combat integrated into platforming — aerial attacks, wall mechanics, momentum-based flow.

### What Went Right

- **Exceptional animation:** 3D→2D pipeline delivered unprecedented fluidity for an indie game
- **~50% of dev time on polishing core mechanics:** Jumping and combat were perfected
- **Hybrid procedural gen:** Solved the "handcrafted content vs replayability" dilemma
- **Cooperative model:** No hierarchy, equal salaries, collective decisions = positive dynamic
- **Early Access with community:** 90% of the build system inspired by forum feedback

### What Went Wrong

- **Cooperative model did not scale:** 8-10 people max; Evil Empire created as a traditional company
- **Custom engine = less documentation:** Heaps less documented than Unity, difficult onboarding
- **7+ years of continuous development:** Burnout risk for a small team
- **Complex ports:** Each platform required dedicated optimization

### Lessons for Solo/Indie Devs

- **Constrained procedural gen:** "Keep algorithmic involvement as restricted as possible" — use procedural to amplify handcrafted content
- **Alternative animation pipeline:** Investigate 3D→2D workflows if you have a small art team
- **Data-driven from the start:** Config files for enemy stats, balance, difficulty
- **Polish > features:** 50% of the time on core mechanics is the right investment
- **Community as design partner:** Direct but transparent feedback on experiments
- **Plan organizational transition:** If you grow, the initial model may not serve you

### Sources

- [GDC Vault — Dead Cells: What the F*n!?](https://gdcvault.com/play/1025788/-Dead-Cells-What-the)
- [Game Developer — Building Level Design of Procedural Metroidvania](https://www.gamedeveloper.com/design/building-the-level-design-of-a-procedurally-generated-metroidvania-a-hybrid-approach-)
- [Game Developer — Art Deep Dive: 3D Pipeline for 2D Animation](https://www.gamedeveloper.com/production/art-design-deep-dive-using-a-3d-pipeline-for-2d-animation-in-i-dead-cells-i-)
- [Game Developer — How Player Criticism Helped Make Dead Cells](https://www.gamedeveloper.com/design/how-player-criticism-helped-make-i-dead-cells-i-the-game-it-is-today)
- [Haxe.org — Shiro Games Technology Stack](https://haxe.org/blog/shirogames-stack/)
- [Heaps.io](https://heaps.io/about.html)
- [LDtk — Level Designer Toolkit](https://ldtk.io/)
- [Pocket Gamer — Dead Cells Post-Mortem Video](https://www.pocketgamer.biz/video-a-dead-cells-post-mortem-from-motion-twins-sebastian-bernard/)

---

## 9. Genshin Impact

### Overview

Genshin Impact is a free-to-play open-world action RPG by miHoYo/HoYoverse, built on Unity (custom modified). Released in September 2020, it generated $10B+ in lifetime revenue, reaching $1B in less than 6 months (faster than Pokémon GO). 300+ developers, 4+ years of development, $100M+ budget. Available on PC, mobile, PS4/5, Xbox.

### Technology Stack

- **Engine:** Unity (custom build based on version 2017, extensively modified)
- **Rendering API:** DirectX 11 (PC), Vulkan (mobile)
- **Physics:** Unity Physics (customized)
- **Animation:** Motion capture for all characters
- **Custom Systems:** Chemistry Engine (elemental interactions), scalable AI, asset streaming
- **Backend:** Alibaba Cloud ("One Architecture, Global Deployment")
- **Server:** 5 global regional zones with a single cluster per region

### Key Architectural Decisions

**Custom Rendering Pipeline (NOT URP):** Proprietary pipeline optimized for cross-platform cel-shading. Features: CSM with 8 cascades, HBAO, volumetric fog, Sobel edge detection for outlines, fake SSS on character silhouettes, SDR tone mapping with adaptive curve.

**Asset Streaming for Open World:** Dynamic LOD based on distance, texture streaming (only necessary textures in memory), dynamic asset loading/unloading, predictive loading based on player direction/speed. Early builds without streaming had severe stuttering.

**Cross-Platform by Design (not a port):** Designed for mobile first, scaled to PC/console. Per-platform: mobile 30/60 FPS with reduced shadow cascades; PC 4K uncapped; PS5 near-4K 60FPS with 2-3s load. HoYoverse account system syncs progression across platforms.

**Live Service Backend:** Alibaba Cloud with multi-zone failover. Simultaneous global updates every 6 weeks. Server-side for event dates, banners, shop. Client patches for game logic.

**Server-Side Gacha:** Pity counters stored and validated on the server (impossible to spoof client-side). Compliance with regional disclosure laws. Soft pity 75-80 pulls, hard pity 90 pulls.

### What Went Right

- **AAA visuals on mobile:** Optimized cel-shading runs on mid-range (Snapdragon 855+)
- **Record revenue:** $1B in <6 months, $10B+ lifetime
- **Cross-platform parity:** Same game, same progression, cross-device multiplayer
- **PS5 optimization:** Near-4K 60FPS, 2-3s load, DualSense haptics
- **Compelling open world:** Climbing anywhere, gliding, environmental storytelling

### What Went Wrong

- **Massive file sizes:** 20GB+ mobile, 40GB+ PC — "Insufficient Storage" frequent
- **Inconsistent loading:** HDD vs SSD = difference of 20s vs 3s
- **Inexperienced team for open world:** In 2017, they lacked expertise; almost gave up
- **Switch port impossible:** Insufficient hardware (even PS5 drops frames in intense scenes)
- **Degraded exploration:** Fast travel eliminates the incentive to walk through the world
- **Declining revenue:** From $1.9B (2022) to $710M (2024) — market saturation
- **HoYoverse planning to leave Unity:** Job postings indicate an in-house engine for future projects

### Lessons for Solo/Indie Devs

- **Design for mobile first, scale up** — not the other way around
- **Cel-shading as an alternative to photorealism:** Lower bandwidth/fill-rate, impressive visuals
- **Asset streaming is mandatory** for open world at any scale
- **Aggressive LOD and texture streaming** on devices with limited RAM
- **Server-authoritative for monetization:** Gacha/IAP always validated server-side
- **Cloud-native from day 1** for global live service
- **Realistic note:** Genshin required $100M+ and 300+ devs — the techniques are applicable, the scale is not

### Sources

- [Unity Blog — Genshin Impact on PS5](https://blog.unity.com/games/explore-the-immersive-world-of-genshin-impact-with-next-gen-performance-on-playstationr5)
- [GDC 2021 — Making of Genshin Impact's Open World](https://gdconf.com/news/learn-about-making-genshin-impacts-open-world-gdc-2021)
- [GDC Vault — Building a Scalable AI System](https://www.gdcvault.com/play/1026968/-Genshin-Impact-Building-a)
- [Alibaba Cloud — miHoYo Cloud-Native](https://www.alibabacloud.com/blog/mihoyo-a-first-generation-cloud-native-enterprise-realizes-imagination_597292)
- [Console Graphics Rendering Pipeline Analysis](https://nugglet.github.io/posts/2022/12/console_graphics_rendering_pipeline_genshin_impact)
- [Animatics — 3D Model Optimization for Mobile](https://www.animaticsassetstore.com/2024/09/13/how-genshin-impact-3d-models-are-optimized-for-mobile-performance/)
- [Business of Apps — Revenue Statistics](https://www.businessofapps.com/data/genshin-impact-statistics/)
- [Sensor Tower — $3B Mobile Revenue](https://sensortower.com/blog/genshin-impact-three-billion-revenue)

---

## 10. Epic Survivors

### Overview

Epic Survivors is an action roguelite built entirely with AI assistance, using MonoGame (not Unity). Developed by web3dev1337, the codebase has ~104K lines of interconnected C#. Included here as a case study of AI-assisted development and layered pattern architecture.

### Technology Stack

- **Framework:** MonoGame (explicitly chosen for AI transparency)
- **Language:** C#
- **Configuration:** YAML files
- **Assets:** Sprites, sounds, music as swappable files
- **Platform:** Steam (commercial)

### Key Architectural Decisions

**Framework Choice for AI Development:** MonoGame was chosen because Unity's metadata files and prefab instances were opaque to AI. MonoGame provides plain C#, YAML configs, and content files that AI can read and modify directly.

**Three-Layer Architecture (Intent):**
1. Framework Layer: MonoGame
2. Engine Layer: Custom engine for survivor roguelikes
3. Game Layer: Weapons, enemies, levels, characters, abilities, meta progression

**Convention-over-Configuration:** Inspired by Ruby on Rails — systems discover components automatically via naming conventions. .cs files following name patterns are loaded automatically.

**Data-Driven via YAML:** Game logic separated from configuration. Design changes without touching C#.

### What Went Right

- Full transparency for AI — every file readable and modifiable
- Convention-based discovery enables auto-loading
- YAML config allows balance adjustments by non-programmers
- Self-play AI testing for performance and regression
- Scaled to 104K+ lines

### What Went Wrong

- **Layer separation did not hold:** GameEngineFramework and Scripts eventually merged
- **Blurred boundaries between layers** in practice — clean architecture as intention vs reality

### Lessons for Solo/Indie Devs

- **File transparency matters for AI-assisted dev:** Avoid binary/opaque formats
- **Config-driven design pays dividends** in iteration and balance
- **Self-play testing** is a powerful tool for edge cases and performance
- **Intentional clean architecture collides with reality** — plan for graceful degradation
- **104K lines is viable with AI** if the framework is transparent

### Sources

- [Epic Survivors: 14 Months of Development](https://web3dev1337.github.io/epic-survivors-architecture/)
- [Epic Survivors on Steam](https://store.steampowered.com/app/2116300)
- [Developer Profile: web3dev1337](https://github.com/web3dev1337)

---

## 11. DOTS Survivors

### Overview

DOTS Survivors is a complete, production-ready survivors-style game built with Unity 6 DOTS/ECS. Published on the Unity Asset Store by Turbo Makes Games, it serves as both a commercial game and a learning resource with 4.5h of video tutorials and documentation for 420+ custom types.

### Technology Stack

- **Engine:** Unity 6
- **Architecture:** Full ECS (Entity Component System)
- **Performance:** Jobs System + Burst Compiler
- **Physics:** DOTS Physics
- **Rendering:** Shader-based animations
- **Documentation:** 4.5h video + API docs for 420+ types

### Game Features

12 weapons with upgrades, 12 passive abilities, 6 characters, 15 enemy types (42 variations with colors), shader-based animations, entity interaction systems, multithreaded job processing.

### Key Architectural Decisions

**Complete Data-Oriented Design:** Data (components) separated from behavior (systems). Aligned with CPU cache optimization.

**Entity Composition:** Lightweight entities instead of GameObjects. Processed by specialized systems.

**Performance First:** Burst Compiler + Job System enable massive entity counts without degradation.

**Multiple Prefab Approaches:** Documents various ways to create and manage ECS prefabs.

**Gameplay Pausing in ECS:** Demonstrates state management for pause/resume within ECS.

### What Went Right

- **5-50x CPU performance** depending on parallelization
- **10-100x with Job System, 1000x+ with Burst Compiler**
- **Ideal for the survivors genre** with hundreds/thousands of simultaneous entities
- **Better cache utilization** and reduced memory bandwidth
- **Comparative reference:** Hardspace: Shipbreaker reduced processes from 1 hour to 100ms with DOTS

### What Went Wrong

- **Steep learning curve** compared to the GameObject approach
- **Requires rethinking traditional OOP patterns**
- **Less intuitive for designers** unfamiliar with data-oriented thinking
- **Debugging/visualization tools** still maturing

### Lessons for Solo/Indie Devs

- **ECS is worth the investment** if your game has high entity counts (survivors, tower defense, RTS)
- **Separating data from behavior** naturally leads to parallel code
- **DOTS excels at exactly the problems** that survivor games face
- **Start with traditional, migrate to ECS** when performance demands it
- **Use as a reference:** Buying the asset and studying the architecture is an excellent educational investment

### Sources

- [DOTS Survivors — Unity Asset Store](https://assetstore.unity.com/packages/templates/tutorials/dots-survivors-complete-ecs-game-project-instructional-documenta-309340)
- [Turbo Makes Games Blog](https://www.tmg.dev/blog/)
- [Unity ECS Documentation](https://unity.com/ecs)
- [GitHub — Entity Component System Samples](https://github.com/Unity-Technologies/EntityComponentSystemSamples)
- [GitHub — DOTS Training Samples](https://github.com/Unity-Technologies/DOTS-training-samples)
- [Medium — Exploring Unity DOTS/ECS Performance](https://medium.com/superstringtheory/unity-dots-ecs-performance-amazing-5a62fece23d4)

---

## 12. Open-Source Projects

### 12A. PaddleGameSO (Official Unity Technologies)

Official Unity demo demonstrating ScriptableObject-based architecture via a ball-and-paddle game.

**5 Patterns Demonstrated:**

1. **Data Containers:** Static data in SOs for runtime, config/logic separation
2. **Delegate Objects:** Strategy pattern with interchangeable SOs
3. **Event Channels:** Messaging via SOs reducing coupling between systems
4. **ScriptableObject Enums:** Type-safe identifiers replacing traditional enums
5. **Runtime Sets:** Dynamic collections accessible cross-scene without singletons

**Value:** Demonstrates that event-driven architecture via SOs is more testable, visual (Inspector shows listeners), and designer-friendly than traditional approaches.

**Sources:**
- [GitHub — PaddleGameSO](https://github.com/UnityTechnologies/PaddleGameSO)
- [Unity — Get Started Guide](https://unity.com/how-to/get-started-with-scriptableobjects-demo)
- [E-book: Create Modular Architecture with ScriptableObjects](https://unity.com/resources/create-modular-game-architecture-with-scriptable-objects-ebook)

### 12B. ScriptableObject-Architecture (DanielEverland)

Community framework inspired by Ryan Hipple's talk (Unite 2017). Provides code generation and tools for rapid SO architecture implementation.

**Features:** Automatic code generation for typed variables and events, Runtime Sets, Clamped Variables, visual debugging with stacktraces in the Inspector, custom icons.

**Sources:**
- [GitHub — ScriptableObject-Architecture](https://github.com/DanielEverland/ScriptableObject-Architecture)
- [OpenUPM Package](https://openupm.com/packages/com.danieleverland.scriptableobjectarchitecture/)

### 12C. GenericScriptableArchitecture (SolidAlloy)

Minimalist implementation using C# generics to minimize the codebase. A single generic Variable and Event class supports any data type.

**Sources:**
- [GitHub — GenericScriptableArchitecture](https://github.com/SolidAlloy/GenericScriptableArchitecture)

### 12D. Ryan Hipple — Unite 2017 (The Foundational Work)

The most-watched Unite talk on game architecture. Ryan Hipple (Principal Engineer, Schell Games) introduced the patterns that inspired the entire SO architecture community.

**Core Principles:**
- **Modularity:** Atomic pieces of logic/data that recombine easily
- **Editability:** Runtime changes without recompilation
- **Debuggability:** Modular design simplifies testing individual pieces

**Key insight:** "The more modular your game, the easier it is to debug. The more editable at runtime, the faster you find the balance."

**Sources:**
- [GitHub — Unite2017 Sample Project](https://github.com/roboryantron/Unite2017)
- [Slides — Game Architecture with ScriptableObjects](https://www.slideshare.net/RyanHipple/game-architecture-with-scriptable-objects)
- [Unity — Architect Code with ScriptableObjects](https://unity.com/how-to/architect-game-code-scriptable-objects)

### 12E. Additional Resources

- [Scriptable-Objects-Architecture (lfeq)](https://github.com/lfeq/Scriptable-Objects-Architecture) — Foundational implementation
- [Kassets (kadinche)](https://github.com/kadinche/Kassets) — SO with UnityEvent, Commands, Collections, Transactions

---

## Conclusion: Recurring Patterns Across the Cases

Analyzing the 12 projects, clear patterns emerge that repeat consistently:

### 1. Simplicity Beats Complexity

Vampire Survivors, Among Us, and Slay the Spire demonstrate that simple architecture (even "spaghetti") is acceptable if the core gameplay is exceptional. God Object Singleton works for solo dev. Premature over-engineering kills indie projects. The pattern: **ship first, refactor when success demands it.**

### 2. Data-Driven Design Is Universal

Dead Cells (YAML/config for enemy stats), Slay the Spire (card data separated from logic), Cult of the Lamb (ScriptableObjects for weapons), Epic Survivors (YAML configs), DOTS Survivors (ECS components) — all separate data from behavior. This allows rapid balance iteration without touching code.

### 3. Procedural Generation Works Best as an Amplifier of Handcrafted Content

Dead Cells (graph-based assembly of handcrafted rooms), Slay the Spire (node-based map with constraints), Cult of the Lamb (dungeon nodes with designed encounters) — none use purely procedural generation. The pattern: **hand-craft the pieces, proceduralize the assembly.**

### 4. Object Pooling Is Mandatory for Survivors-Like Games

Vampire Survivors (centralized pool with cap of 300), DOTS Survivors (ECS entities), Fall Guys (60 networked physics bodies) — any game with dozens/hundreds of simultaneous entities needs pooling or ECS. GC pressure kills performance.

### 5. Save Systems Are Always Underestimated

Cult of the Lamb (corrupting JSON), Among Us (fragile state), Vampire Survivors (giant JSON) — save systems are a recurring source of critical post-launch bugs. **Invest in robust save validation from the start.**

### 6. Infrastructure Scaling Catches Everyone Off Guard

Among Us (hardcoded IPs, 8x projected), Fall Guys (650K vs 80K), Genshin Impact (5 global regions) — every multiplayer game underestimates peak load. **Expect 10x, prepare cloud auto-scaling, never hardcode infrastructure.**

### 7. Engine Choice Has Long-Term Consequences

Slay the Spire (LibGDX → Godot out of frustration), Vampire Survivors (Phaser → Unity for scale), Dead Cells (Heaps = control but smaller community), Cities: Skylines 2 (immature DOTS caused problems), Genshin (Unity so heavily customized that HoYo plans their own engine) — **the engine choice defines the technical ceiling of the project.**

### 8. Community and Modding Multiply Value

Slay the Spire (ModTheSpire/BaseMod), Dead Cells (34+ free updates), Hollow Knight (modding API), PaddleGameSO (open-source education) — **games with an active community and modding live far beyond launch.**

### 9. Distinct Art > Expensive Art

Vampire Survivors ("janky" pixel art), Cult of the Lamb (cute horror CalArts), Dead Cells (3D→2D pipeline), Hollow Knight (hand-drawn) — all with a strong visual identity using accessible techniques. **Invest in aesthetic coherence, not technical fidelity.**

### 10. Polish Core Mechanics > Quantity of Features

Dead Cells (50% of dev time on jumping/combat), Vampire Survivors (only movement), Among Us (only social deduction), Slay the Spire (only cards + intent) — the most successful games are those that did ONE thing exceptionally well. **Cut features ruthlessly. Protect the core.**

---

> *Document compiled in April 2026. Sources verified and linked in each section.*
