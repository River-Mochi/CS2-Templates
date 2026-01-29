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
- **What gameplay uses right now** = instance-side runtime components (often cached / serialized)

> [**Scene Explorer**](https://mods.paradoxplaza.com/mods/74285/Windows) mod is recommended to see live detailed entity values in-game.<br>
> Examples below uses [**Magic Hearse**](https://mods.paradoxplaza.com/mods/123497/Windows) mod.

---

## 3 different “things” that often get mixed up

In CS2 you run into **three** layers that *sound* similar but behave differently:

### 1) Prefab entity (ECS entity with `PrefabData`)
- ECS representation of a prefab.
- Many instances store a `PrefabRef` component that points to this prefab entity.
- This entity often holds prefab-side `*Data` components mods edit (example: `DeathcareFacilityData`, `WorkplaceData`).
- **Important:** prefab entities are **mutable**. The game and mods can change them during a session.

### 2) `PrefabBase` (authoring object) — the real baseline
- The game’s authoring object that represents what the prefab “is” in vanilla.
- `PrefabBase` stores authoring components and fields (example: `DeathcareFacility.m_ProcessingRate`).
- Accessed via `PrefabSystem.TryGetPrefab(...)`:

```csharp
PrefabSystem prefabSystem = World.DefaultGameObjectInjectionWorld
    .GetOrCreateSystemManaged<PrefabSystem>();

if (prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
{
    // prefabBase = vanilla baseline source
}
```

### 3) Instance entity (placed building / vehicle / citizen in the world)
- A placed thing in the simulation.
- Has a `PrefabRef` component.
  - `PrefabRef` points to a **prefab entity** (`PrefabRef.m_Prefab`).
  - `PrefabRef` is **not** “the instance entity”. It’s just a component on the instance.
- Has runtime components that may or may not update when the prefab changes.
- Often carries **cached / computed / serialized** values used by simulation right now.

---

## Why `PrefabRef` is NOT the “true vanilla baseline”

`PrefabRef.m_Prefab` only tells you *which prefab entity* the instance currently references.

The prefab entity can already be modified by:
- the game itself (example: upgrades/extensions combining stats)
- other mods
- the same mod on earlier runs

So using prefab-entity components as “baseline” tends to cause **double-scaling** or **wrong restore values**.

**Rule of thumb**
- ✅ Baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring component fields  
- ❌ Baseline = reading `*Data` from the prefab entity referenced by `PrefabRef`

---

## (Examples) Authoring → Prefab Data: how the game copies values

This is the main “why it works” bit in one sentence:

- Authoring components on `PrefabBase` **initialize** the prefab entity `*Data` components.

Example from DNSpy (`Game.Prefabs.DeathcareFacility.Initialize`):

```csharp
entityManager.SetComponentData(entity, new DeathcareFacilityData
{
    m_HearseCapacity = m_HearseCapacity,
    m_StorageCapacity = m_StorageCapacity,
    m_LongTermStorage = m_LongTermStorage,
    m_ProcessingRate = m_ProcessingRate
});
```

Meaning:
- **Authoring (`PrefabBase`)** has fields like `m_ProcessingRate`
- The game writes those into the **prefab entity** as `DeathcareFacilityData`
- Mods usually scale the `*Data` on the prefab entity (not the authoring object)

---

## (Examples) Concrete components & fields you can cite

Keep it simple. Two fields is plenty.

### Authoring components (`PrefabBase`) — true vanilla values
These live on `PrefabBase` and contain the authored defaults.

**`Game.Prefabs.DeathcareFacility` (authoring)**
- `m_ProcessingRate`
- `m_StorageCapacity`

**`Game.Prefabs.Workplace` (authoring)**
- `m_Workplaces`
- `m_MinimumWorkersLimit`

### ECS `*Data` components (usually on prefab entities)
These are what you typically write to when scaling:

**`Game.Prefabs.DeathcareFacilityData`**
- `m_ProcessingRate`
- `m_StorageCapacity`

**`Game.Prefabs.WorkplaceData`**
- `m_MaxWorkers`
- `m_MinimumWorkersLimit`

### Runtime / instance-side components (placed entities)
These are often what simulation uses for current behavior:

**`Game.Companies.WorkProvider` (instance-side)**
- `m_MaxWorkers` (game-maintained runtime value; not always “copied live” from prefab changes)

---

## Recommended pattern (safe & compatible)

### Step 1 — Read vanilla from `PrefabBase`
```csharp
PrefabSystem prefabSystem = World.DefaultGameObjectInjectionWorld
    .GetOrCreateSystemManaged<PrefabSystem>();

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGet(out Game.Prefabs.DeathcareFacility authoring))
    return;

// true baseline examples
float baseRate = authoring.m_ProcessingRate;
int baseStorage = authoring.m_StorageCapacity;
```

**Note:** `PrefabBase` is a ScriptableObject that holds a `components` list.  
`prefabBase.TryGet<T>(out T component)` searches the prefab + its authoring components list.

### Step 2 — Write scaled values onto ECS `*Data` on prefab entities
```csharp
foreach ((RefRW<Game.Prefabs.DeathcareFacilityData> dc, Entity prefabEntity) in SystemAPI
    .Query<RefRW<Game.Prefabs.DeathcareFacilityData>>()
    .WithAll<PrefabData>()
    .WithEntityAccess())
{
    if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
        continue;

    if (!prefabBase.TryGet(out Game.Prefabs.DeathcareFacility authoring))
        continue;

    float baseRate = authoring.m_ProcessingRate;
    dc.ValueRW.m_ProcessingRate = baseRate * scalar;
}
```

### Step 3 — Workers are “special”: plan a refresh + restore strategy
Workers are often based on instance-side runtime components and caches.

Practical approach that stays compatible:
- Scale `WorkplaceData` on the prefab entity (safe; affects new buildings)
- Tell players **existing** buildings may need a refresh event:
  - rebuild building
  - add/remove extension
  - add/remove upgrade (example: cold storage)

If doing a restore button:
- store a marker of what was applied
- only restore if the current values still match that marker (don’t stomp another mod)

---

## What happens when a prefab is “replaced” (why updates can propagate)

When the game updates or replaces a prefab entity, it uses **ReplacePrefabSystem**.

High-level behavior (from DNSpy):
- `PrefabSystem.UpdatePrefab(...)` queues an update
- `PrefabSystem.OnUpdate()` calls `ReplacePrefabSystem.ReplacePrefab(oldEntity, newEntity, sourceInstance)`
- `ReplacePrefabSystem` walks entities and:
  - swaps `PrefabRef.m_Prefab` from old to new
  - marks affected instances `Updated`
  - updates various buffers (`SubObject`, `SubNet`, `SubArea`, etc.)
- `ReplacePrefabSystem.FinalizeReplaces()` can rebuild areas/nets/lanes for objects when needed

Takeaway for modders:
- Changing prefab entities can propagate via `PrefabRef` updates.
- Runtime simulation components still might not recompute “the way you hoped” (workers are the classic case).

---

## How to verify in-game (quick)

Scene Explorer mod shows the three layers in one place:

- **PrefabBase authoring** (vanilla truth)
- **Prefab entity** (example: `DeathcareFacilityData`, `WorkplaceData`)
- **Placed instance** (example: `WorkProvider`)

---

## DON'T do this (double-scale trap)

```csharp
// DON'T: uses prefab-entity data as vanilla baseline
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;

var baseData = dcLookup[prefabEntity];         // might already be modified
var scaled  = baseData.m_ProcessingRate * s;   // double-scaling risk
```

## Do this (true vanilla baseline)

```csharp
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGet(out Game.Prefabs.DeathcareFacility authoring))
    return;

float baseRate = authoring.m_ProcessingRate;
float scaled   = baseRate * s;
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
| processing/storage/fleet | prefab `*Data` components (example: `DeathcareFacilityData`) | often yes / instant |
| workers max/min | `WorkplaceData` on prefab entity | **often needs** rebuild/upgrade/extension refresh |
| instance runtime worker provider | `WorkProvider` on instances | yes, but risky (compatibility + invariants) |
