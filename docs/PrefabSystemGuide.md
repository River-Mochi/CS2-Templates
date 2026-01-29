# PrefabSystem “Source of Truth” in Cities: Skylines II (CO API)

This note is for CS2 modders who change things like **capacities / rates and special values like worker counts** and want the results to be:
  - **Correct** (true vanilla baselines / game defaults)
  - **Compatible** (other mods can coexist)
  - **Predictable** (players know when changes apply immediately vs needing a new building)

**No time to read?** [Quick Guide](https://github.com/River-Mochi/CS2-Templates/blob/main/docs/PrefabQuickGuide.md#prefabsystem-source-of-truth--quick-guide-cs2-modding)

---

**TL;DR mental model**
  - **Baseline** = `PrefabBase` authoring (via `PrefabSystem.TryGetPrefab(...)`)
  - **What mods usually edit** = prefab-entity `*Data` components (`WithAll<PrefabData>()`)
  - **What gameplay uses right now** = instance-side runtime components (often) cached / serialized

> [Scene Explorer mod](https://mods.paradoxplaza.com/mods/74285/Windows) is recommended to see these values clearly in-game.  
> Examples below are mainly from the [Magic Hearse](https://mods.paradoxplaza.com/mods/123497/Windows) mod but apply to general prefabs.  
> See **InstanceEntities.md** for more instance runtime examples (special cases like workers).

---

## 3 different “things” that often get mixed up

In CS2 there are **three layers** that sound similar but behave very differently:

1) **Prefab Entity** (ECS entity with `PrefabData`)
- ECS representation of a prefab.
  - Often referenced by `PrefabRef.m_Prefab` from an instance.
  - Frequently stores ECS prefab-side `*Data` components mods edit (ex: `DeathcareFacilityData`, `WorkplaceData`).
- **Important:** prefab entities are **mutable**. Game + mods can change them during a session.

2) **PrefabBase (Authoring object)** — the real baseline
- Authoring object that represents what the prefab “is” in vanilla.
- Accessed via:
  ```csharp
  PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
  if (prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase)) { ... }
  ```
- **Treat PrefabBase authoring fields as the “true vanilla baseline.”**

3) **Instance Entity** (the placed building / vehicle / citizen)
- The thing that exists in the city.
- Has a `PrefabRef` component pointing at the **prefab entity** (the instance itself is not “PrefabRef”).
- Has runtime components used by simulation **now**.
- Often carries **cached / computed / serialized** values that do not always hot-update.

---

## Why `PrefabRef` is NOT the “true vanilla baseline”

`PrefabRef.m_Prefab` only tells which **prefab entity** the instance came from.

The prefab entity can already be modified by:
- the game itself (ex: upgrades/extensions combining stats)
- other mods
- earlier runs of the same mod

So using prefab-entity components as “baseline” tends to produce **double-scaling** or **wrong restore values**.

**Rule of thumb**
- ✅ Baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring component fields  
- ❌ Baseline = reading `*Data` from the prefab entity referenced by `PrefabRef`

---

## Concrete: real components & fields you can cite

### Authoring components (PrefabBase) — true vanilla values
These live on `PrefabBase` and contain authored values.

Examples (deathcare is just one example):
- **`Game.Prefabs.DeathcareFacility` (authoring)**
  - `m_ProcessingRate`
  - `m_StorageCapacity`
  - `m_HearseCapacity`
- **`Game.Prefabs.Workplace` (authoring)**
  - `m_Workplaces` (baseline max workers)
  - `m_MinimumWorkersLimit`

### ECS `*Data` components (often on prefab entities)
These are the ECS components mods typically write when scaling:

- **`Game.Prefabs.DeathcareFacilityData`**
  - `m_ProcessingRate`
  - `m_StorageCapacity`
  - `m_HearseCapacity`
- **`Game.Prefabs.WorkplaceData`**
  - `m_MaxWorkers`
  - `m_MinimumWorkersLimit`

### Runtime / instance-side components (placed entities)
Often what the simulation uses for current behavior:

- **`Game.Companies.WorkProvider` (instance-side)**
  - `m_MaxWorkers` (runtime value; not always copied live from prefab changes)

---

## Recommended pattern (safe & compatible)

### Step 1 — Read vanilla from PrefabBase
```csharp
PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
    return;

// true baseline examples
float baseRate = authoring.m_ProcessingRate;
int baseHearses = authoring.m_HearseCapacity;
```

### Step 2 — Write scaled values onto ECS `*Data` on prefab entities
```csharp
PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

foreach ((RefRW<Game.Prefabs.DeathcareFacilityData> dc, Entity prefabEntity) in SystemAPI
    .Query<RefRW<Game.Prefabs.DeathcareFacilityData>>()
    .WithAll<PrefabData>()
    .WithEntityAccess())
{
    if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
        continue;

    if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
        continue;

    dc.ValueRW.m_ProcessingRate = authoring.m_ProcessingRate * scalar;
    dc.ValueRW.m_HearseCapacity = (int)math.round(authoring.m_HearseCapacity * scalar);
}
```

- Alternate example (Tree Controller):  
  https://github.com/yenyang/Tree_Controller/blob/56752932a92eb5d0632ecedda499c61157722da2/Tree_Controller/Systems/ModifyVegetationPrefabsSystem.cs#L35

### Step 3 — Workers are “special”: plan a refresh story
Worker limits often live in instance runtime components (ex: `Game.Companies.WorkProvider`) and may not hot-update.

Practical guidance for players:
- **New buildings**: correct from editing `WorkplaceData`
- **Existing buildings**: often needs a refresh event:
  - rebuild building
  - add/remove extension or upgrade
- restarting usually doesn’t force the refresh

---

## How to verify in-game (quick)

Use Scene Explorer (entity inspector):
- **PrefabBase authoring** (baseline truth)
- **Prefab entity** (`PrefabData` entity, shows `*Data` components)
- **Placed instance** (runtime components like `WorkProvider`)

---

## WRONG vs RIGHT examples

### WRONG baseline (common bug)
```csharp
// WRONG: uses prefab-entity data as vanilla baseline
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;
var baseData = dcLookup[prefabEntity]; // might already be modified
var scaled = baseData.m_ProcessingRate * scalar; // double-scaling risk
```

### RIGHT baseline
```csharp
// RIGHT: baseline from PrefabBase authoring
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;

PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
    return;

float baseRate = authoring.m_ProcessingRate;
float scaled = baseRate * scalar;
```

---

## Quick reference

### Baseline vs data vs runtime
| Layer | What it is | Good for | Not good for |
|---|---|---|---|
| `PrefabBase` authoring | “Real prefab definition” | true vanilla baseline | writing runtime effects |
| Prefab entity (`PrefabData`) | ECS representation | writing scaled `*Data` | using as baseline |
| Instance entity | placed building/vehicle | inspecting current behavior | reading vanilla defaults |

### “Applies immediately?” rule of thumb
| What to change | Where to usually write | Applies to existing buildings instantly? |
|---|---|---|
| processing/storage/fleet | prefab `*Data` (ex: `DeathcareFacilityData`) | often yes / instant |
| workers max/min | prefab `WorkplaceData` | often needs refresh event |
| runtime instance values | instance components (rare) | yes, but risky |
