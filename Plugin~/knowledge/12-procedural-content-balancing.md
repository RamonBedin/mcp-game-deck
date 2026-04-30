# 12 — Procedural Generation & Mathematical Balancing for Unity

> Technical guide with algorithms, formulas, pseudocode, and references for procedural content generation and mathematical balancing in Unity games.

---

## Part 1 — Procedural Generation

### 1.1 Binary Space Partitioning (BSP) — Rectangular Dungeons

BSP is the classic algorithm for generating dungeons with rectangular rooms connected by corridors. It works by recursively subdividing a space into two halves until a minimum room size is reached.

**How it works:**

1. Start with a rectangle representing the entire dungeon.
2. Randomly choose an axis (horizontal or vertical) and a split position.
3. Divide the rectangle into two children.
4. Repeat recursively until children reach the minimum size.
5. In each leaf of the tree, create a room with a random size within the node's bounds.
6. Connect sibling rooms with corridors.

**Pseudocode:**

```
function BSP_Split(node, minSize, maxDepth, depth = 0):
    if depth >= maxDepth OR node.width < minSize * 2 AND node.height < minSize * 2:
        node.room = CreateRoom(node.bounds, padding = 2)
        return

    // Choose split axis
    if node.width > node.height:
        splitHorizontal = false
    else if node.height > node.width:
        splitHorizontal = true
    else:
        splitHorizontal = Random.Bool()

    if splitHorizontal:
        splitPos = Random.Range(minSize, node.height - minSize)
        node.left  = new Node(node.x, node.y, node.width, splitPos)
        node.right = new Node(node.x, node.y + splitPos, node.width, node.height - splitPos)
    else:
        splitPos = Random.Range(minSize, node.width - minSize)
        node.left  = new Node(node.x, node.y, splitPos, node.height)
        node.right = new Node(node.x + splitPos, node.y, node.width - splitPos, node.height)

    BSP_Split(node.left, minSize, maxDepth, depth + 1)
    BSP_Split(node.right, minSize, maxDepth, depth + 1)
    ConnectRooms(node.left.GetRoom(), node.right.GetRoom())
```

**Control parameters:**

| Parameter | Effect |
|-----------|--------|
| `minSize` | Minimum room size — larger values = bigger rooms, fewer rooms |
| `maxDepth` | Maximum tree depth — more depth = more smaller rooms |
| `padding` | Space between room and node edge — controls density |
| `splitRatio` | Range of the split point (e.g., 0.3–0.7) — avoids very thin rooms |

**When to use:** Dungeons with well-defined room and corridor layouts, classic roguelike style.

**Sources:** [Procedural Dungeons with BSP in Unity — Wayline](https://www.wayline.io/blog/procedural-dungeons-bsp-unity) · [Random 2D dungeon BSP — Romain Beaudon](http://www.rombdn.com/blog/2018/01/12/random-dungeon-bsp-unity/)

---

### 1.2 Wave Function Collapse (WFC) — Constraint-Based Generation

WFC is a constraint propagation algorithm inspired by quantum mechanics. Each grid cell starts in "superposition" (all possible tiles) and collapses to a single tile based on adjacency rules.

**How it works:**

1. Initialize the grid with all cells in superposition (all tiles possible).
2. Select the cell with the lowest entropy (fewest remaining options).
3. Collapse that cell to a random tile (weighted by weight).
4. Propagate constraints to neighbors, removing incompatible tiles.
5. Repeat until all cells are collapsed or a contradiction is detected.

**Pseudocode:**

```
function WFC_Generate(grid, tileSet, adjacencyRules):
    // Initialize superposition
    for each cell in grid:
        cell.possibleTiles = tileSet.All()

    while HasUncollapsedCells(grid):
        // 1. Observation — lowest entropy
        cell = GetLowestEntropyCell(grid)
        if cell.possibleTiles.Count == 0:
            return FAILURE  // Contradiction — backtrack or restart

        // 2. Collapse
        chosenTile = WeightedRandom(cell.possibleTiles)
        cell.Collapse(chosenTile)

        // 3. Propagation
        stack = [cell]
        while stack is not empty:
            current = stack.Pop()
            for each neighbor in current.GetNeighbors():
                for each tile in neighbor.possibleTiles:
                    if not IsCompatible(current.tile, tile, direction):
                        neighbor.possibleTiles.Remove(tile)
                        if neighbor.changed:
                            stack.Push(neighbor)

    return SUCCESS

function GetLowestEntropyCell(grid):
    // Shannon entropy: H = -Σ(p_i * log(p_i))
    // In practice, count of possibilities + small random noise
    return grid.uncollapsed
        .OrderBy(c => c.possibleTiles.Count + Random.Range(0, 0.1))
        .First()
```

**Two models:**

- **Simple Tiled Model:** Pre-defined tiles with explicit adjacency rules. Ideal for games with tilesets.
- **Overlapping Model:** Extracts NxN patterns from a sample image and reproduces them in the output. Ideal for generating textures or maps "in the style of" a given example.

**Performance:** WFC is not fast — for large maps, consider running it on a separate thread or generating by chunks.

**Sources:** [WFC Original — mxgmn/GitHub](https://github.com/mxgmn/WaveFunctionCollapse) · [WFC Tips and Tricks — BorisTheBrave](https://www.boristhebrave.com/2020/02/08/wave-function-collapse-tips-and-tricks/) · [WFC for Unity — PVS Studio](https://pvs-studio.com/en/blog/posts/csharp/1027/)

---

### 1.3 Cellular Automata — Organic Caves

Cellular automata (CA) is the simplest and most effective algorithm for generating caves with organic shapes. It operates on a grid of cells that change state based on their neighbors.

**How it works:**

1. Initialize the grid randomly — each cell has an X% chance of being a "wall" (e.g., 45%).
2. For each iteration (typically 4–5):
   - For each cell, count "wall" neighbors within radius 1 (8 neighbors — Moore neighborhood).
   - If wall neighbors >= threshold (typically 4–5), the cell becomes a wall.
   - Otherwise, it becomes floor.
3. After the iterations, the grid will have organic cave shapes.

**Pseudocode:**

```
function CellularAutomata_Generate(width, height, fillPercent, iterations, threshold):
    // Initialization
    grid = new int[width, height]
    for x in 0..width:
        for y in 0..height:
            if IsBorder(x, y, width, height):
                grid[x,y] = WALL
            else:
                grid[x,y] = (Random.Float() < fillPercent) ? WALL : FLOOR

    // Smoothing iterations
    for i in 0..iterations:
        newGrid = Copy(grid)
        for x in 1..width-1:
            for y in 1..height-1:
                wallCount = CountWallNeighbors(grid, x, y)  // Moore neighborhood
                if wallCount >= threshold:
                    newGrid[x,y] = WALL
                else:
                    newGrid[x,y] = FLOOR
        grid = newGrid

    // Post-processing: flood fill to ensure connectivity
    EnsureConnectivity(grid)
    return grid

function CountWallNeighbors(grid, x, y):
    count = 0
    for dx in -1..1:
        for dy in -1..1:
            if dx == 0 AND dy == 0: continue
            if grid[x+dx, y+dy] == WALL: count++
    return count
```

**Control parameters:**

| Parameter | Typical Value | Effect |
|-----------|-------------|--------|
| `fillPercent` | 0.40–0.50 | Initial percentage of walls — higher = smaller caves |
| `iterations` | 4–6 | More iterations = smoother shapes |
| `threshold` | 4–5 | Neighbor threshold for becoming a wall |
| `width/height` | 50–200 | Grid size |

**Variation — rule B678/S345678:** "Birth" with 6, 7, or 8 neighbors; "Survival" with 3+ neighbors. Produces more open caves.

**Important post-processing:** After generation, use flood fill to identify disconnected regions and connect them with tunnels, or discard regions that are too small.

**Sources:** [Procedural Cave Generation — Unity](https://discussions.unity.com/t/procedural-cave-generation/566852) · [Procedural Generation with Cellular Automata — Bronson Zgeb](https://bronsonzgeb.com/index.php/2022/01/30/procedural-generation-with-cellular-automata/)

---

### 1.4 Noise Functions — Terrain and Variation

Noise functions produce continuous, smooth pseudo-random values essential for terrain, textures, and any organic variation.

#### 1.4.1 Perlin Noise

Invented by Ken Perlin in 1983. Generates smooth gradients in 2D/3D. Unity has `Mathf.PerlinNoise(x, y)` built in.

**Conceptual formula for octaves (fractal noise):**

```
function FractalNoise(x, y, octaves, persistence, lacunarity, scale):
    total = 0
    amplitude = 1
    frequency = 1
    maxValue = 0  // For normalization

    for i in 0..octaves:
        sampleX = x / scale * frequency
        sampleY = y / scale * frequency
        noiseValue = PerlinNoise(sampleX, sampleY)  // returns 0..1
        total += noiseValue * amplitude
        maxValue += amplitude
        amplitude *= persistence   // typically 0.5 — each octave has half the amplitude
        frequency *= lacunarity    // typically 2.0 — each octave has double the frequency

    return total / maxValue  // Normalize to 0..1
```

**Octave parameters:**

| Parameter | Typical Value | Effect |
|-----------|-------------|--------|
| `octaves` | 4–8 | More octaves = more fine detail |
| `persistence` | 0.4–0.6 | How much each octave contributes — higher = rougher |
| `lacunarity` | 1.8–2.5 | Frequency increase per octave |
| `scale` | 20–100 | Noise zoom — higher = larger features |

#### 1.4.2 Simplex Noise

Also created by Perlin (2001), an improvement over classic Perlin Noise: fewer directional artifacts, better performance in higher dimensions (3D+), O(n²) complexity vs Perlin's O(2^n).

Unity does not have Simplex built in, but open-source implementations exist (e.g., Stefan Gustavson's library).

**Use in terrain heightmap:**

```
function GenerateTerrainHeightmap(terrain, noiseParams, seed):
    heights = new float[resolution, resolution]
    prng = new Random(seed)
    offsets = GenerateOctaveOffsets(prng, noiseParams.octaves)

    for x in 0..resolution:
        for y in 0..resolution:
            height = FractalNoise(
                x + offsets[i].x,
                y + offsets[i].y,
                noiseParams.octaves,
                noiseParams.persistence,
                noiseParams.lacunarity,
                noiseParams.scale
            )
            // Optional: apply curve to control height distribution
            height = heightCurve.Evaluate(height)
            heights[x, y] = height

    terrain.SetHeights(0, 0, heights)
```

**Sources:** [Unity Mathf.PerlinNoise](https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html) · [Math Noises for Procedural Generation — Mina Pêcheux](https://medium.com/geekculture/how-to-use-math-noises-for-procedural-generation-in-unity-c-44902a21d8e)

---

### 1.5 Seed-Based Generation — Reproducibility

All procedural generation should be seed-based to allow reproducibility (level replay, bug reports, sharing).

**Principle:** The same seed must produce exactly the same result, always.

```
function InitializeGeneration(seed):
    if seed == 0:
        seed = System.DateTime.Now.Ticks  // Random seed

    masterRNG = new System.Random(seed)

    // Derive sub-seeds for each system — avoids coupling
    terrainSeed  = masterRNG.Next()
    enemySeed    = masterRNG.Next()
    lootSeed     = masterRNG.Next()
    decorSeed    = masterRNG.Next()

    // Each system uses its own RNG
    terrainRNG = new System.Random(terrainSeed)
    enemyRNG   = new System.Random(enemySeed)
    // ...
```

**Critical tip:** Use `System.Random` with an explicit seed, not `UnityEngine.Random` (which is global and shared). Deriving sub-seeds ensures that changing enemy generation does not affect terrain.

---

### 1.6 Procedural Placement — Spawns, Items, and Decoration

#### Enemy Spawns with Progressive Difficulty

```
function PlaceEnemies(rooms, difficultyBudget, enemyTable, rng):
    for each room in rooms:
        roomBudget = difficultyBudget * room.sizeMultiplier
        spentBudget = 0

        while spentBudget < roomBudget:
            enemy = WeightedRandom(enemyTable, rng)
            if spentBudget + enemy.cost > roomBudget * 1.1:  // 10% tolerance
                break
            position = FindValidSpawnPoint(room, rng)
            Spawn(enemy, position)
            spentBudget += enemy.cost
```

#### Item Distribution — Poisson Disk Sampling

To distribute items/decorations naturally (no clusters or gaps), use Poisson Disk Sampling:

```
function PoissonDiskSampling(width, height, minDistance, maxAttempts = 30):
    cellSize = minDistance / sqrt(2)
    grid = new Grid(ceil(width/cellSize), ceil(height/cellSize))
    points = []
    activeList = []

    // First random point
    initial = RandomPoint(width, height)
    points.Add(initial)
    activeList.Add(initial)
    grid.Insert(initial)

    while activeList is not empty:
        index = Random.Range(0, activeList.Count)
        point = activeList[index]
        found = false

        for attempt in 0..maxAttempts:
            // Generate candidate in the ring [minDistance, 2*minDistance]
            angle = Random.Float() * 2 * PI
            distance = minDistance + Random.Float() * minDistance
            candidate = point + (cos(angle), sin(angle)) * distance

            if IsInBounds(candidate) AND NoNeighborTooClose(grid, candidate, minDistance):
                points.Add(candidate)
                activeList.Add(candidate)
                grid.Insert(candidate)
                found = true
                break

        if not found:
            activeList.RemoveAt(index)

    return points
```

---

## Part 2 — Mathematical Balancing

### 2.1 Difficulty Curves

The difficulty curve defines how challenge scales throughout the game. Each curve type creates a different experience.

#### Linear: `y = ax + b`

```
EnemyHP(level) = 80 + 20 * level
```

- Level 1: 100 HP, Level 10: 280 HP, Level 50: 1080 HP
- **When to use:** Early game, tutorials, simple systems.
- **Problem:** Becomes tedious — growth is too predictable.

#### Exponential: `y = a * b^x`

```
EnemyHP(level) = 100 * 1.15^level
```

- Level 1: 115, Level 10: 405, Level 50: 108,366
- **When to use:** RPGs with power fantasy — player gets very powerful, so do enemies.
- **Problem:** Numbers become absurd quickly (number inflation). Requires soft caps.

#### Logarithmic: `y = a * ln(x) + b`

```
EnemyHP(level) = 200 * ln(level) + 100
```

- Level 1: 100, Level 10: 561, Level 50: 883
- **When to use:** When you want diminishing returns — early upgrades are impactful, then it stabilizes.

#### S-Curve (Sigmoid): `y = L / (1 + e^(-k(x - x0)))`

```
Difficulty(level) = maxDifficulty / (1 + e^(-0.2 * (level - 25)))
```

Where:
  L   = maximum value (plateau)
  k   = curve steepness (0.1 = smooth, 0.5 = steep)
  x0  = inflection point (where 50% of max is reached)

- **When to use:** Smooth start (onboarding), ramp-up in mid-game, plateau at end-game.
- **Best curve for most games** — combines the advantages of the others.

**Conceptual visualization:**

```
Difficulty
│          ___________
│         /
│        /
│       /
│  ____/
│ /
│/
└──────────────────── Level
  Onboard  Ramp  Plateau
```

---

### 2.2 DPS and TTK (Time to Kill)

**DPS (Damage Per Second):**

```
DPS = (BaseDamage * CritMultiplier * CritChance + BaseDamage * (1 - CritChance)) / AttackInterval

// Simplified:
DPS = BaseDamage * (1 + CritChance * (CritMultiplier - 1)) / AttackInterval

// Example:
BaseDamage = 50, CritChance = 0.25, CritMultiplier = 2.0, AttackInterval = 0.8s
DPS = 50 * (1 + 0.25 * (2.0 - 1)) / 0.8
DPS = 50 * 1.25 / 0.8
DPS = 78.125
```

**TTK (Time to Kill):**

```
TTK = EnemyHP / EffectiveDPS

// With armor/resistance:
EffectiveDPS = DPS * (1 - DamageReduction)
DamageReduction = Armor / (Armor + K)    // K = constant (e.g., 100)

// Example:
EnemyHP = 500, DPS = 78.125, EnemyArmor = 50, K = 100
DamageReduction = 50 / (50 + 100) = 0.333
EffectiveDPS = 78.125 * (1 - 0.333) = 52.1
TTK = 500 / 52.1 = 9.6 seconds
```

**Armor Diminishing Returns Formula** — `Armor / (Armor + K)`:

This formula is used in most games (League of Legends, Diablo, etc.) because it has natural diminishing returns. Each armor point provides progressively less damage reduction, never reaching 100%.

```
Armor =  50 → 33% reduction
Armor = 100 → 50% reduction
Armor = 200 → 67% reduction
Armor = 500 → 83% reduction
(with K = 100)
```

**TTK guidelines by genre:**

| Genre | Ideal TTK (approximate) |
|--------|----------------------|
| Arena Shooter (Quake) | 0.5–2s |
| Battle Royale | 1–4s |
| Tactical Shooter (CS) | 0.1–0.5s |
| MOBA (teamfight) | 3–8s |
| RPG/Action (boss) | 60–300s |
| Soulslike (mob) | 5–15s |

---

### 2.3 Scaling Formulas — Stats by Level

#### Polynomial Formula (recommended for most cases)

```
Stat(level) = base * (1 + growthRate * level^exponent)

// Example: player HP
PlayerHP(level) = 100 * (1 + 0.12 * level^1.3)

Level  1: 100 * (1 + 0.12 * 1)    = 112
Level 10: 100 * (1 + 0.12 * 20)   = 340
Level 25: 100 * (1 + 0.12 * 56.2) = 774
Level 50: 100 * (1 + 0.12 * 138)  = 1756
```

#### Compound Growth (RPG style with multipliers)

```
Stat(level) = base * multiplier^level

// Enemy HP:
EnemyHP(level) = 80 * 1.08^level

Level  1: 86
Level 10: 173
Level 25: 548
Level 50: 3753
```

#### Multi-Factor Scaling (more control)

```
EnemyPower(level, zone, eliteRank):
    basePower = levelCurve.Evaluate(level)         // AnimationCurve in Unity
    zoneMult  = 1.0 + (zone - 1) * 0.25           // Each zone +25%
    eliteMult = [1.0, 2.0, 5.0, 12.0][eliteRank]  // Normal, Elite, Champion, Boss
    return basePower * zoneMult * eliteMult
```

**Sanity check rule of thumb:** Calculate the expected TTK every 5 levels. If TTK is rising too much (player becomes weak) or falling too much (too easy), adjust the exponents.

---

### 2.4 Economy Balancing — Sources vs. Sinks

The game economy is a flow system: **Sources** (faucets) produce resources, **Sinks** (drains) consume them.

**Fundamental rule:** `Income Rate ≈ Spending Rate` over time, with intentional fluctuations.

#### Flow Model

```
// Economy state per session
IncomePerHour = (GoldPerEnemy * EnemiesPerMinute * 60)
              + (QuestRewards / AvgQuestTime * 60)
              + PassiveIncome

SpendingPerHour = (ConsumableCost * ConsumablesPerHour)
               + (UpgradeCost / HoursBetweenUpgrades)
               + (RepairCosts + Taxes)

// Ideal ratio: slightly greater than 1.0 (player accumulates slowly)
EconomyRatio = IncomePerHour / SpendingPerHour

// Target: 1.05–1.20 (player feels progress but doesn't over-accumulate)
```

#### Practical Example — Mobile RPG

```
// Player Level 10, zone 2
GoldPerEnemy     = 15
EnemiesPerMinute = 8
QuestReward      = 500 (every ~30min)
PassiveIncome    = 0

IncomePerHour = (15 * 8 * 60) + (500 / 0.5) + 0
             = 7200 + 1000
             = 8200 gold/hour

// Sinks
HealthPotion     = 50 gold, uses ~10/hour = 500
UpgradeCost      = 5000 gold, every ~2 hours = 2500/hour
RepairCost       = 200/hour

SpendingPerHour = 500 + 2500 + 200 = 3200 gold/hour

EconomyRatio = 8200 / 3200 = 2.56  ← PROBLEM! Player accumulates too fast.

// Fix: increase upgrade cost or reduce gold per enemy
```

**Inflation control — Diminishing Returns on farming:**

```
GoldMultiplier(killCount) = 1.0 / (1 + killCount * 0.01)

// After 100 kills in the same area: multiplier = 0.5 (half the gold)
// Incentivizes progression instead of grinding
```

---

### 2.5 Drop Rate Math

#### Weighted Random — Basic Loot System

```
// Loot table with weights
LootTable = [
    { item: "Common Sword",   weight: 60 },
    { item: "Rare Shield",    weight: 25 },
    { item: "Epic Staff",     weight: 10 },
    { item: "Legendary Helm", weight: 4 },
    { item: "Mythic Ring",    weight: 1 },
]
totalWeight = Sum(weights) = 100

function WeightedRandom(table, rng):
    roll = rng.Next(0, totalWeight)  // 0..99
    cumulative = 0
    for each entry in table:
        cumulative += entry.weight
        if roll < cumulative:
            return entry.item
```

**Probability by rarity:**

```
P(Common)    = 60/100 = 60%
P(Rare)      = 25/100 = 25%
P(Epic)      = 10/100 = 10%
P(Legendary) =  4/100 =  4%
P(Mythic)    =  1/100 =  1%
```

**Probability of getting at least 1 item in N attempts:**

```
P(at least 1 in N) = 1 - (1 - dropRate)^N

// How many attempts for a 50% chance of dropping a Mythic (1%)?
0.5 = 1 - (1 - 0.01)^N
0.5 = 0.99^N
N = ln(0.5) / ln(0.99)
N ≈ 69 attempts

// For 90% chance:
N = ln(0.1) / ln(0.99) ≈ 229 attempts
// For 99% chance:
N = ln(0.01) / ln(0.99) ≈ 458 attempts
```

#### Pity System — Protection Against Bad Luck

The pity system ensures that after N failed attempts, the probability increases (soft pity) or the drop is guaranteed (hard pity).

```
// Soft Pity — probability grows gradually
function GetDropRate(baseRate, failStreak, pityStart, pityGrowth):
    if failStreak < pityStart:
        return baseRate
    else:
        bonusRate = (failStreak - pityStart) * pityGrowth
        return min(baseRate + bonusRate, 1.0)  // Cap at 100%

// Genshin Impact-style example:
// baseRate = 0.006 (0.6%), pityStart = 73, pityGrowth = 0.06
// Pull 73: 0.006 + 0 * 0.06 = 0.6%
// Pull 74: 0.006 + 1 * 0.06 = 6.6%
// Pull 80: 0.006 + 7 * 0.06 = 42.6%
// Pull 90: HARD PITY — guaranteed (100%)
```

#### Pseudo-Random Distribution (PRD) — Dota 2 Style

PRD ensures that the actual probability converges toward the nominal value, avoiding both success and failure streaks.

```
// Instead of a fixed chance C, uses an incremental chance
// Starts with P(1) = C_prd (lower than nominal C)
// Each failure: P(n) = n * C_prd
// On success: resets to P(1) = C_prd

function PRD_GetChance(nominalChance, failCount):
    C_prd = GetPRDConstant(nominalChance)  // Lookup table or calculation
    return min(failCount * C_prd, 1.0)

// C_prd values for common chances:
// Nominal Chance → C_prd
//  5%  → 0.00380
// 10%  → 0.01475
// 15%  → 0.03222
// 20%  → 0.05570
// 25%  → 0.08474
// 30%  → 0.11895
```

**PRD advantage:** A real 25% chance means on average every 4 hits it procs, but with PRD you will never go 15 hits without a proc or get 3 procs in a row — the distribution stays "fair".

---

### 2.6 Power Curves and Power Spikes in Upgrade Systems

#### Power Budget by Level

```
// Define a total "power budget" per level distributed across stats
TotalPower(level) = 100 + 50 * level

// Distribution (player chooses "build"):
// Warrior: 50% HP, 30% ATK, 10% DEF, 10% SPD
// Mage:    20% HP, 50% ATK, 10% DEF, 20% SPD

WarriorHP(level)  = TotalPower(level) * 0.50
WarriorATK(level) = TotalPower(level) * 0.30
```

#### Power Spikes — Moments of Power Jumps

```
// Power spikes happen at specific milestones
// Example: every 10 levels, the player gains a talent that gives +20% power

EffectivePower(level):
    base = TotalPower(level)
    talentCount = floor(level / 10)
    talentMult = 1.0 + talentCount * 0.20
    return base * talentMult

// Level  9: 550 * 1.0  = 550
// Level 10: 600 * 1.2  = 720   ← Power spike! +31% jump
// Level 11: 650 * 1.2  = 780
// Level 19: 1050 * 1.2 = 1260
// Level 20: 1100 * 1.4 = 1540  ← Another spike! +22%
```

**Design insight:** Power spikes are motivators. The player feels the upgrade and wants to reach the next milestone. Without spikes, progression feels flat and unrewarding.

#### Upgrade Cost Scaling — Avoiding Inflation

```
// Upgrade cost must grow faster than income
UpgradeCost(level) = baseCost * costMultiplier^level

// If income grows linearly but cost grows exponentially,
// each upgrade takes progressively longer — natural pacing.

// Example:
baseCost = 100, costMultiplier = 1.5
Level 1:  100 * 1.5^1  = 150
Level 5:  100 * 1.5^5  = 759
Level 10: 100 * 1.5^10 = 5767

// Time to farm (income = 500 gold/hour):
// Level 1:  ~18 minutes
// Level 5:  ~1.5 hours
// Level 10: ~11.5 hours  ← natural paywall, incentivizes purchase or zone change
```

---

## Part 3 — Balancing Tools

### 3.1 Spreadsheets as the Source of Truth

The spreadsheet (Google Sheets / Excel) is the central balancing tool. Designers edit values in the spreadsheet and export them to the game via CSV.

**Recommended pipeline:**

```
Google Sheets (designers edit)
    ↓ Export CSV
Assets/Data/EnemyStats.csv
    ↓ Import in Unity (custom importer or runtime)
ScriptableObject or Dictionary<int, EnemyData>
    ↓ Referenced by code
EnemySpawner, CombatSystem, etc.
```

**Example balancing CSV:**

```csv
EnemyID,Name,Level,HP,ATK,DEF,Speed,XPReward,GoldReward
slime_01,Green Slime,1,50,8,2,3,10,5
slime_02,Blue Slime,3,85,12,4,4,25,12
goblin_01,Goblin Scout,5,120,18,8,6,45,20
goblin_02,Goblin Warrior,8,200,28,15,5,80,35
boss_01,Goblin King,10,800,45,25,4,500,200
```

**Auto-import in Unity:**

```
// AssetPostprocessor that detects changes in CSV files
class CSVImporter : AssetPostprocessor {
    void OnPreprocessAsset() {
        if (assetPath.EndsWith(".csv") && assetPath.Contains("Data/")) {
            // Parse CSV and update corresponding ScriptableObjects
            UpdateScriptableObjectsFromCSV(assetPath)
        }
    }
}
```

---

### 3.2 Automated Playtesting

Autonomous agents that play the game and collect metrics automatically.

```
// Concept of a playtesting bot
class PlaytestBot:
    strategy: AIStrategy  // Aggressive, Defensive, Random, Optimal
    metrics: SessionMetrics

    function PlayLevel(levelConfig):
        Reset()
        while not levelComplete AND not dead:
            action = strategy.DecideAction(gameState)
            Execute(action)
            metrics.Record(gameState)

        return metrics  // TTK, DPS dealt, damage taken, items used, etc.

// Run 1000 simulations with different strategies
function AutomatedBalanceTest(levelConfig, iterations = 1000):
    results = []
    for strategy in [Aggressive, Defensive, Random, Optimal]:
        for i in 0..iterations:
            bot = new PlaytestBot(strategy)
            metrics = bot.PlayLevel(levelConfig)
            results.Add(metrics)

    // Analysis
    avgTTK = results.Average(r => r.ttk)
    winRate = results.Count(r => r.won) / results.Count
    avgItemsUsed = results.Average(r => r.itemsUsed)

    // Balance flags
    if winRate < 0.3: FLAG("Too difficult")
    if winRate > 0.95: FLAG("Too easy")
    if avgTTK > targetTTK * 1.5: FLAG("TTK too high — bullet sponge")
```

---

### 3.3 Metrics and Analytics for Fine-Tuning

**Essential metrics for balancing:**

```
// Metrics per session
SessionMetrics:
    levelCompletionRate     // % of players who complete the level
    averageTTK              // average time to kill enemies
    deathHeatmap            // where players die (position + cause)
    itemUsageRate           // which items are used and which are ignored
    goldAtLevelEnd          // economy — accumulating or spending?
    sessionDuration         // time played
    retentionDay1/7/30      // retention

// Automatic flags
if levelCompletionRate < 0.5:  → Level too difficult
if itemUsageRate["HealthPotion"] > 0.8:  → Enemy damage too high
if goldAtLevelEnd > expectedGold * 2:  → Insufficient sinks
if weaponUsageRate["SwordA"] < 0.02:  → Weapon needs a buff
```

---

### 3.4 A/B Testing in Game Balance

```
// Split players into groups to test different balance configs
ABTest "EnemyHP_v2":
    GroupA (control): EnemyHP_multiplier = 1.0
    GroupB (variant): EnemyHP_multiplier = 0.85  // -15% HP

    Observed metrics:
        - levelCompletionRate
        - sessionDuration
        - retentionDay7
        - revenuePerUser

    Duration: minimum 7 days
    Sample size: 1000+ users per group
    Significance: p < 0.05

// Analysis
if GroupB.retention > GroupA.retention AND p < 0.05:
    → Ship variant B (enemies with less HP)
```

**Principles of A/B testing in games:**

1. Change only one variable per test.
2. Random assignment — no selection bias.
3. Wait for statistical significance before deciding.
4. Measure long-term metrics (retention), not just immediate ones (completion rate).

**Sources:** [A/B Testing in Mobile Games — Superscale](https://superscale.com/a-b-testing-in-mobile-games/) · [Game Analytics — Generalist Programmer](https://generalistprogrammer.com/game-analytics) · [Automatic Playtesting via Active Learning — arXiv](https://ar5iv.labs.arxiv.org/html/1908.01417)

---

## Part 4 — Data-Driven Procedural Content

### 4.1 ScriptableObjects as Generation Rules

```csharp
// Define generation rules as Inspector-editable assets
[CreateAssetMenu(fileName = "NewLevelGenConfig", menuName = "PCG/Level Generation Config")]
public class LevelGenConfig : ScriptableObject
{
    [Header("Dimensions")]
    public int width = 100;
    public int height = 100;

    [Header("BSP Settings")]
    public int minRoomSize = 8;
    public int maxBSPDepth = 5;
    [Range(0.3f, 0.7f)]
    public float splitRatioMin = 0.4f;
    [Range(0.3f, 0.7f)]
    public float splitRatioMax = 0.6f;

    [Header("Cellular Automata Overlay")]
    public bool useCaveOverlay = true;
    [Range(0f, 1f)]
    public float caveFillPercent = 0.45f;
    public int caSmoothing = 4;

    [Header("Enemy Spawning")]
    public EnemySpawnTable[] enemyTables;  // By biome/zone
    public AnimationCurve difficultyBudgetByRoom;

    [Header("Loot")]
    public LootTable[] lootTables;
    public float baseDropRate = 0.15f;
    public int pityThreshold = 20;
}
```

### 4.2 Config-Driven Wave Spawner

```csharp
[CreateAssetMenu(fileName = "NewWaveConfig", menuName = "PCG/Wave Config")]
public class WaveConfig : ScriptableObject
{
    public WaveData[] waves;
}

[System.Serializable]
public class WaveData
{
    public string waveName;
    public float delayBeforeWave = 3f;
    public float spawnInterval = 0.5f;
    public SpawnEntry[] enemies;
    public WaveModifier[] modifiers;  // Ex: +20% speed, +50% HP
}

[System.Serializable]
public class SpawnEntry
{
    public EnemyConfig enemyConfig;  // Another ScriptableObject
    public int count;
    public SpawnPattern pattern;     // Enum: Sequential, Simultaneous, Random
}

[System.Serializable]
public class WaveModifier
{
    public StatType stat;            // HP, Speed, Damage, etc.
    public ModifierType type;        // Additive, Multiplicative
    public float value;
}

// Runtime: WaveSpawner reads the config and executes
public class WaveSpawner : MonoBehaviour
{
    public WaveConfig config;
    private int currentWave = 0;

    IEnumerator RunWave(WaveData wave)
    {
        yield return new WaitForSeconds(wave.delayBeforeWave);

        foreach (var entry in wave.enemies)
        {
            for (int i = 0; i < entry.count; i++)
            {
                var enemy = Spawn(entry.enemyConfig);
                ApplyModifiers(enemy, wave.modifiers);

                if (entry.pattern == SpawnPattern.Sequential)
                    yield return new WaitForSeconds(wave.spawnInterval);
            }
        }
    }
}
```

### 4.3 Modular Difficulty System

```csharp
[CreateAssetMenu(fileName = "NewDifficultyProfile", menuName = "PCG/Difficulty Profile")]
public class DifficultyProfile : ScriptableObject
{
    [Header("Base Scaling")]
    public AnimationCurve hpCurve;        // X = normalized level, Y = multiplier
    public AnimationCurve damageCurve;
    public AnimationCurve speedCurve;

    [Header("Adaptive Difficulty")]
    public bool useAdaptive = true;
    public float adaptiveWeight = 0.3f;   // How much the system reacts to player performance
    public float targetDeathRate = 0.15f;  // 15% of runs should result in death

    // Calculates total multiplier given level + player performance
    public float GetMultiplier(StatType stat, int level, float playerPerformance)
    {
        float normalizedLevel = (float)level / maxLevel;

        AnimationCurve curve = stat switch
        {
            StatType.HP     => hpCurve,
            StatType.Damage => damageCurve,
            StatType.Speed  => speedCurve,
            _ => AnimationCurve.Linear(0, 1, 1, 1)
        };

        float baseMultiplier = curve.Evaluate(normalizedLevel);

        if (useAdaptive)
        {
            // playerPerformance: 0 = dying a lot, 1 = dominating
            // If the player is dominating, increase difficulty
            float adaptiveOffset = (playerPerformance - 0.5f) * 2f * adaptiveWeight;
            baseMultiplier *= (1f + adaptiveOffset);
        }

        return baseMultiplier;
    }
}
```

**Complete data-driven flow:**

```
DifficultyProfile (SO)   →  Defines scaling curves
WaveConfig (SO)          →  Defines composition of each wave
EnemyConfig (SO)         →  Defines base stats of each enemy
LootTable (SO)           →  Defines drops and rarities
LevelGenConfig (SO)      →  Defines map generation parameters

All editable in the Inspector by designers, without touching code.
CSV export/import for bulk editing and versioning.
```

---

## References and Sources

**Procedural Generation:**

- [WFC Original — mxgmn/GitHub](https://github.com/mxgmn/WaveFunctionCollapse)
- [WFC Tips and Tricks — BorisTheBrave](https://www.boristhebrave.com/2020/02/08/wave-function-collapse-tips-and-tricks/)
- [BSP Dungeons in Unity — Wayline](https://www.wayline.io/blog/procedural-dungeons-bsp-unity)
- [BSP Dungeon Generation — Romain Beaudon](http://www.rombdn.com/blog/2018/01/12/random-dungeon-bsp-unity/)
- [Cellular Automata Caves — Bronson Zgeb](https://bronsonzgeb.com/index.php/2022/01/30/procedural-generation-with-cellular-automata/)
- [Procedural Cave Generation — Unity Discussions](https://discussions.unity.com/t/procedural-cave-generation/566852)
- [Unity Mathf.PerlinNoise](https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html)
- [Math Noises in Unity — Mina Pêcheux](https://medium.com/geekculture/how-to-use-math-noises-for-procedural-generation-in-unity-c-44902a21d8e)
- [2D Procedural Generation with ScriptableObjects — GameDeveloper](https://www.gamedeveloper.com/design/2d-procedural-generation-in-unity-with-scriptableobjects)

**Mathematical Balancing:**

- [The Mathematics of Game Balance — UserWise](https://blog.userwise.io/blog/the-mathematics-of-game-balance)
- [The Mathematics of Balance — Department of Play](https://departmentofplay.net/the-mathematics-of-balance/)
- [Fire Rate, DPS, and TTK — Game Balance Dissected](https://gamebalancing.wordpress.com/2015/03/14/fire-rate-dps-and-ttk/)
- [Video Game Balance Guide — GameDesignSkills](https://gamedesignskills.com/game-design/game-balance/)
- [Designing Fair Randomness — Bad Luck Protection](https://medium.com/@niklasvmoers/designing-fair-and-fun-randomness-in-video-games-via-bad-luck-protection-48f2c2262cfa)
- [Loot Drop Best Practices — GameDeveloper](https://www.gamedeveloper.com/design/loot-drop-best-practices)
- [Loot Distributions Guide — Wintermute Digital](https://wintermutedigital.com/post/probcdf/)

**Economy and Analytics:**

- [Designing Balanced In-Game Economy — Unity](https://unity.com/how-to/design-balanced-in-game-economy-guide-part-3)
- [Value Chains — Lost Garden](https://lostgarden.com/2021/12/12/value-chains/)
- [Sinks & Faucets — 1kx Network](https://medium.com/1kxnetwork/sinks-faucets-lessons-on-designing-effective-virtual-game-economies-c8daf6b88d05)
- [Game Economy Balancing — Stanislav Stankovic](https://medium.com/ironsource-levelup/how-to-think-when-balancing-a-game-700dc8e27a00)
- [A/B Testing in Mobile Games — Superscale](https://superscale.com/a-b-testing-in-mobile-games/)
- [Automatic Playtesting via Active Learning — arXiv](https://ar5iv.labs.arxiv.org/html/1908.01417)
- [Game Analytics Guide — Generalist Programmer](https://generalistprogrammer.com/game-analytics)
