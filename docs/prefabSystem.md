# PrefabSystem “Source of Truth” in Cities: Skylines II (CO API)

This note is for CS2 modders who change **capacities / rates / worker counts** and want the results to be:
- **Correct** (true vanilla baselines)
- **Compatible** (other mods can coexist)
- **Predictable** (players know when changes apply immediately vs needing a refresh)

---

## 3 different “things” that often get mixed up

In CS2 you will run into **three** layers that *sound* similar but behave differently:

1) **Prefab Entity** (ECS entity with `PrefabData`)
- This is the ECS representation of a prefab.
- Often referenced by `PrefabRef.m_Prefab` from an instance.
- Frequently stores runtime data like `*Data` components (ex: `DeathcareFacilityData`, `WorkplaceData`).

2) **PrefabBase (Authoring object)** — the real baseline
- The game’s authoring object that represents what the prefab “is” in vanilla.
- Accessed via:
  ```csharp
  PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
  if (prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase)) { ... }
  ```
- **This is what you should treat as “true vanilla baseline.”**

3) **Instance Entity** (the placed building / vehicle / citizen in the world)
- Has `PrefabRef` pointing at the prefab entity.
- Has runtime components that may or may not update when the prefab changes.

---

## Why `PrefabRef` is NOT the “true vanilla baseline”

`PrefabRef.m_Prefab` only tells you *which prefab entity* the instance came from.

The prefab entity can already be modified by:
- the game itself (upgrades combining stats, simulation adjustments)
- other mods
- your own mod on earlier runs

So using prefab-entity components as “baseline” tends to produce **double-scaling** or **wrong restore values**.

**Rule of thumb**
- ✅ Baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring component fields  
- ❌ Baseline = reading `*Data` from the prefab entity referenced by `PrefabRef`

---

## Concrete: real components & fields you can cite

### Authoring components (PrefabBase) — true vanilla values
These live on `PrefabBase` and contain the “what the prefab was authored as” values.

Examples:

**`Game.Prefabs.DeathcareFacility` (authoring)**
- `m_ProcessingRate`
- `m_HearseCapacity`
- `m_StorageCapacity`
- `m_LongTermStorage`

**`Game.Prefabs.Workplace` (authoring)**
- `m_Workplaces` (baseline max workers)
- `m_MinimumWorkersLimit`

### ECS `*Data` components (often on prefab entities)
These are the ECS components you typically write to when scaling:

**`Game.Prefabs.DeathcareFacilityData`**
- `m_ProcessingRate`
- `m_HearseCapacity`
- `m_StorageCapacity`
- `m_LongTermStorage`

**`Game.Prefabs.WorkplaceData`**
- `m_MaxWorkers`
- `m_MinimumWorkersLimit`

### Runtime / instance-side components (placed entities)
These are often what the simulation actually uses for *current behavior*:

**`Game.Companies.WorkProvider` (instance-side, common)**
- `m_MaxWorkers` (game-maintained runtime value; not always “copied live” from prefab changes)

---

## The “why does X apply immediately but workers don’t?” thing

Important:

- Changing **processing rate / storage / hearse capacity** can appear to take effect immediately.
- Changing **workers** often needs a “refresh” (rebuild, upgrade, add/remove extension) before the building behaves like it updated.
- Restarting the game usually does **not** force that refresh.

### What’s happening

Some values are read from **prefab data** by simulation systems frequently (or affect newly spawned behavior quickly).
Examples: processing rate, storage capacity, hearse capacity.

Other values are used to produce or update **instance runtime components** that are not fully “hot-reloaded.”
Workers are often represented by instance-side systems that compute or cache a runtime worker limit (ex: `WorkProvider.m_MaxWorkers`).
Existing buildings may not re-run the “recompute my workforce limit” path just because you edited the prefab.

### Practical takeaway
Example, if the mod scales worker counts:
- **Best practice:** write scaled worker values onto the prefab (`WorkplaceData`), but:
  - Existing buildings might not fully update until a refresh event:
    - rebuild the building, or
    - add/remove an extension, or
    - add/remove an upgrade (when available)
- Avoid “mutate everything at runtime” unless you really know the full dependency chain (it’s easy to break simulation invariants).

---

## Recommended pattern (safe & compatible)

### Step 1 — Read vanilla from PrefabBase
```csharp
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGet(out DeathcareFacility authoring))
    return;

// true baseline
float baseRate = authoring.m_ProcessingRate;
int baseHearses = authoring.m_HearseCapacity;
```

### Step 2 — Write scaled values onto ECS `*Data` on prefab entities
```csharp
foreach ((RefRW<DeathcareFacilityData> dc, Entity e) in SystemAPI
    .Query<RefRW<DeathcareFacilityData>>()
    .WithAll<PrefabData>()
    .WithEntityAccess())
{
    // authoring baseline via PrefabBase here...
    dc.ValueRW.m_ProcessingRate = scaled;
}
```

### Step 3 — If altering workers, include a refresh note or a controlled restore strategy
If you scale workers, it’s good to:
- store what you applied (marker component), and
- only restore if the current values still match your marker (prevents stomping another mod’s changes).

This is exactly why marker components exist.

---

## WRONG vs RIGHT examples

### WRONG baseline (common bug)
```csharp
// WRONG: uses prefab-entity data as vanilla baseline
Entity prefab = prefabRefLookup[instance].m_Prefab;
var baseData = dcLookup[prefab]; // might already be modified!
var scaled = baseData.m_ProcessingRate * scalar;
```

Why it fails:
- If another mod already changed `DeathcareFacilityData`, you double-scale.
- “Restore defaults” restores to an already-modified value.

### RIGHT baseline
```csharp
// RIGHT: baseline from PrefabBase authoring
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGet(out DeathcareFacility authoring))
    return;

float baseRate = authoring.m_ProcessingRate;
float scaled = baseRate * scalar;
```

---
### Derived runtime state (instance-side, cached)
Some simulation values are stored on *instance entities* as runtime components.  
These values may be **computed/cached** from prefab data and saved into the savegame, so they might **not update immediately** when you change prefab values.

**Example: Workers**
- Prefab authoring (baseline): `Game.Prefabs.Workplace` (`m_Workplaces`, `m_MinimumWorkersLimit`)
- Prefab ECS data (what mods often write): `Game.Prefabs.WorkplaceData` (`m_MaxWorkers`, `m_MinimumWorkersLimit`)
- Instance runtime state (what simulation may use): `Game.Companies.WorkProvider` (`m_MaxWorkers`) — **serializable per instance**

`WorkProvider` implements `ISerializable` and writes/reads `m_MaxWorkers`, meaning existing buildings can carry worker limits as persisted runtime state.  
So scaling workers on the prefab may require a refresh trigger (rebuild / upgrade / extension change) to recompute instance-side values.

## Quick reference tables

### Baseline vs data vs runtime
| Layer | What it is | Good for | Not good for |
|---|---|---|---|
| `PrefabBase` authoring | “Real prefab definition” | true vanilla baseline | writing runtime effects |
| Prefab entity (`PrefabData`) | ECS representation | writing scaled `*Data` | using as baseline |
| Instance entity | placed building/vehicle | inspecting current behavior | reading vanilla defaults |

### “Applies immediately?” rule of thumb
| What you change | Where you usually write | Applies to existing buildings instantly? |
|---|---|---|
| processing/storage/fleet | `DeathcareFacilityData` on prefab | often yes / quickly visible |
| workers max/min | `WorkplaceData` on prefab | **often needs refresh** (rebuild/upgrade/extension) |
| runtime worker provider | `WorkProvider` on instances | yes, but risky (compatibility + invariants) |

---

## Warnings for runtime components
> Worker limits are partially cached on existing buildings. After changing worker scaling, rebuild the building or add/remove an upgrade/extension to refresh. Restarting the game usually won’t refresh runtime component limits.
