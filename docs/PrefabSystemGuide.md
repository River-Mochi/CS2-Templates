# PrefabSystem “Source of Truth” in Cities: Skylines II (CO API)

This note is for CS2 modders who change things like **capacities / rates and special values like worker counts** and want the results to be:
  - **Correct** (true vanilla baselines / game defaults)
  - **Compatible** (other mods can coexist)
  - **Predictable** (players know when changes apply immediately vs needing a new building)

**TL;DR mental model**
  - **Baseline** = `PrefabBase` authoring (via `PrefabSystem.TryGetPrefab(...)`)
  - **What mods usually edit** = prefab-entity `*Data` components (`WithAll<PrefabData>()`)
  - **What gameplay uses right now** = instance-side runtime components (often cached / serialized)

> [Scene Explorer mod](https://mods.paradoxplaza.com/mods/74285/Windows) is recommended to see these values more clearly in-game. <br>
> Examples below are mainly from the [Magic Hearse](https://mods.paradoxplaza.com/mods/123497/Windows) mod.

---

## 3 different “things” that often get mixed up

In CS2 you run into **three** layers that *sound* similar but behave differently:

1) **Prefab Entity** (ECS entity with `PrefabData`)
- This is the ECS representation of a prefab.
  - Often referenced by `PrefabRef.m_Prefab` from an instance.
  - Frequently stores runtime-ish data like `*Data` components (ex: `DeathcareFacilityData`, `WorkplaceData`).
- **Important:** prefab entities are **mutable**. The game and mods can change them during a session.

2) **PrefabBase (Authoring object)** — the real baseline
  - The game’s authoring object that represents what the prefab “is” in vanilla.
  - Accessed via:
  ```csharp
  PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
  if (prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase)) { ... }
  ```
  - **Treat as “true vanilla baseline.”**

3) **Instance Entity** (the placed building / vehicle / citizen in the world)
  - Has `PrefabRef` pointing at the prefab entity.
  - Has runtime components that may or may not update when the prefab changes.
  - Often carries **cached / computed / serialized** values used by simulation right now.

---

## Why `PrefabRef` is NOT the “true vanilla baseline”

`PrefabRef.m_Prefab` only tells you *which prefab entity* the instance came from.

The prefab entity can already be modified by:
- the game itself (ex: upgrades/extensions combining stats)
- other mods
- your own mod on earlier runs

So using prefab-entity components as “baseline” tends to produce **double-scaling** or **wrong restore values**.

**Rule of thumb**
- ✅ Baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring component fields  
- ❌ Baseline = reading `*Data` from the prefab entity referenced by `PrefabRef`

---

## Concrete: real components & fields you can cite

### Authoring components (PrefabBase) — true vanilla values
These live on `PrefabBase` and contain the “authored” values.

Examples:

**`Game.Prefabs.DeathcareFacility` (authoring)**
- `m_ProcessingRate`
- `m_StorageCapacity`

**`Game.Prefabs.Workplace` (authoring)**
- `m_Workplaces` (baseline max workers)
- `m_MinimumWorkersLimit`

### ECS `*Data` components (often on prefab entities)
These are the ECS components you typically write to when scaling:

**`Game.Prefabs.DeathcareFacilityData`**
- `m_ProcessingRate`
- `m_StorageCapacity`

**`Game.Prefabs.WorkplaceData`**
- `m_MaxWorkers`
- `m_MinimumWorkersLimit`

### Runtime / instance-side components (placed entities)
These are often what the simulation actually uses for *current behavior*:

**`Game.Companies.WorkProvider` (instance-side)**
- `m_MaxWorkers` (game-maintained runtime value; not always “copied live” from prefab changes)

---

## Why does a prefab change apply instantly for some sliders, but the same method can not be used for workers?

Important:

- Changing **processing rate / storage / some capacity** slider in Options UI can take effect immediately.
- Changing items like **workers** often needs a “rebuild” (rebuild the building, add/remove extension/upgrade) before the building shows true new worker limit.
- Restarting the game usually does **not** force that update.

### What’s happening (simple)

Some values are read from **prefab** `*Data` frequently (or affect newly spawned behavior quickly).
Examples: processing rate, storage capacity, vehicle capacity.

Other values feed into **instance runtime components** that are computed/cached and not automatically invalidated when you edit prefab data.
Workers are an example: the sim may use a cached per-building value (ex: `WorkProvider.m_MaxWorkers`) that isn’t recomputed just because `WorkplaceData` changed.
Upgrades/extensions also complicate this: the game may combine multiple sources into a final runtime worker limit.

### Practical takeaway
Example, if the mod scales something like worker counts:
- Write scaled worker values onto the prefab (`WorkplaceData`), but:
  - Existing buildings might not fully update until a refresh event:
    - rebuild the building, or
    - add/remove an extension, or add/remove a building upgrade (when available)
- Avoid “mutate everything at runtime” unless the full dependency chain is understood (it’s easy to break invariants or stomp other mods).

---

## Recommended pattern (safe & compatible)

### Step 1 — Read vanilla from PrefabBase
```csharp
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGet(out DeathcareFacility authoring))
    return;

// true baseline examples
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

- **Alternate method** from [Tree mod](https://github.com/yenyang/Tree_Controller/blob/56752932a92eb5d0632ecedda499c61157722da2/Tree_Controller/Systems/ModifyVegetationPrefabsSystem.cs#L35)

### Step 3 — For special items like workers, add a controlled restore strategy (marker)
If you scale workers, it’s possible to:
- store what you applied (marker component), and
- only restore if the current values still match the marker (prevents stomping another mod’s changes).
- consider one-shot apply (ex: setting change) and not per-frame (bad for performance)

---

### How to verify in-game (quick)
Use Scene Explorer mod (entity inspector) and typically CTRL+E and click on any building:
- PrefabBase authoring (source-of-truth baseline)
- Prefab entity (`PrefabData` entity, click on any crematorium: `WorkplaceData`, `DeathcareFacilityData`, etc.)
- Placed building instance (`WorkProvider`, plus anything else)

---

## WRONG vs RIGHT examples

### WRONG baseline (potential bug)
```csharp
// WRONG: uses prefab-entity data as vanilla baseline
Entity prefab = prefabRefLookup[instance].m_Prefab;
var baseData = dcLookup[prefab]; // might already be modified!
var scaled = baseData.m_ProcessingRate * scalar;
```

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
- Instance runtime state (what simulation may use): `Game.Companies.WorkProvider` (`m_MaxWorkers`) — commonly persists on reboot
  
If a runtime worker limit is serialized per building instance, scaling workers on the prefab often requires 
a refresh trigger (new building change) to recompute instance-side values.
- This means a slider to adjust workers will not instant update on the building
- we can't update `Companies.WorkProvider` directly because it's calculated in a **burst job**.

#### 3 Methods to get buildings to update
1. Harmony patch: makes the mod more brittle on game patch days, but may be the only way.
2. Rigurous research of the decompiled code to find the exact method used and copy it. Then the burst job for Companies.WorkProvider will read your new value.
    - one-shot method on slider movement helps avoid fighting the burst job and will update **existing** buildings.
    - also still need `WorkplaceData` change to take care of all **new** buildings.
    - risks doing this, carefully check the logic.
3. Avoid all this by asking the player to simply update to new buildings to get the changes for this one slider.
  
nota bene: changes persist on saves when the mod is removed but is harmless, player can delete old buildings and all new buildings will be vanilla values.

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
| processing/storage/fleet | prefab *Data components (ex: DeathcareFacilityData) | often yes / instant |
| workers max/min | ex: `WorkplaceData` on prefab | **often needs** (rebuild/add extension) |
| runtime worker provider | `WorkProvider` on instances | yes, but risky (compatibility + invariants) |

---

## Example Warnings for runtime components
> Worker limits are partially cached on existing buildings. After changing worker scaling, rebuild the building or add/remove an upgrade/extension to refresh.
> Restarting the game usually won’t refresh runtime component limits.
