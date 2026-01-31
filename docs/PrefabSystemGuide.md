# PrefabSystem in Cities: Skylines II

This note is for CS2 modders who change things like **capacities / rates and special values** and want the results to be:
- **Correct** (true vanilla baselines / game defaults)
- **Compatible** (other mods can coexist)
- **Predictable** (players know when changes apply immediately vs needing a new building)

**No time to read?** [Quick Guide](https://github.com/River-Mochi/CS2-Templates/blob/main/docs/PrefabQuickGuide.md#prefabsystem-source-of-truth--quick-guide-cs2-modding)

> [Scene Explorer mod](https://mods.paradoxplaza.com/mods/74285/Windows) is recommended to inspect entities in-game.  
> Examples use Magic Hearse mod but the pattern applies to *any* prefab type.

---

### TL;DR
- **Baseline (true vanilla)** = `PrefabBase` authoring fields (via `PrefabSystem.TryGetPrefab(...)`)
- **What mods usually edit** = prefab-entity `*Data` components (`WithAll<PrefabData>()`)
- **What gameplay uses right now** = instance-side runtime components (often game computed / cached / serialized)

---

## 3 different “things” that often get mixed up

In CS2 you run into **three layers** that *sound* similar but behave differently:
### 1) PrefabBase (authoring) — the real baseline
- Authoring object that represents what the prefab “is” in vanilla.
- Accessed via `PrefabSystem.TryGetPrefab(...)`:

```csharp
PrefabSystem prefabSystem =
    World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;
```

- **Treat PrefabBase authoring fields as the “true vanilla baseline.”**
- PrefabBase includes the raw field value that asset creator used on that prefab.
- This makes restore logic correct and prevents double multiplier scaling.
  
### 2) Prefab-Entity (ECS entity with `PrefabData`)
- ECS representation of a prefab.
- Often referenced by `PrefabRef.m_Prefab` from an instance.
- Stores ECS prefab-side `*Data` components that **mods commonly edit** (ex: `DeathcareFacilityData`, `WorkplaceData`).
- **Important:** prefab entities are **mutable**. Different mods can change them during a session.
- Not everything from PrefabBase to PrefabData is one-to-one (not all provide easy tuning knobs).

### 3) Instance Entity (placed building / vehicle / citizen)
- The thing that exists in the city right now.
- `PrefabRef` points to a **prefab-entity** (`PrefabRef.m_Prefab`), not `PrefabBase`.
- Has runtime components used by simulation right now.
- Most known runtime values do **not** hot-update just because the prefab entity changed.
  - (ex: workers: instance-side `Game.Companies.WorkProvider.m_MaxWorkers`)
  - needs extra code to trigger an instant update or a player action (ex: place a new building).

> See **InstanceEntities.md** for detailed instance-side / runtime examples.

---

## Why `PrefabRef` is NOT the “true vanilla baseline”

`PrefabRef.m_Prefab` only tells you *which prefab entity* the instance came from.
That prefab entity is commonly modified by mods.

So using prefab-entity `*Data` as “baseline” could produce **double-scaling** or **wrong restore values**.

**Rule of thumb**
- ✅ Baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring component fields
- ❌ Baseline = reading `*Data` from the prefab entity referenced by `PrefabRef`

---

## Recommended pattern (safe & compatible)

### Step 1 — Read vanilla from PrefabBase (authoring)
```csharp
PrefabSystem prefabSystem =
    World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
    return;

float baseRate = authoring.m_ProcessingRate;  // true baseline example
```

### Step 2 — Write scaled values onto ECS `*Data` on prefab entities

This is where the mod actually **changes the prefab-entity** (entities with `PrefabData`) by writing to `*Data` components.

#### Option 1: classic SystemAPI style  (query → get entities → foreach loop)

```csharp
// Classic Style SystemAPI.QueryBuilder 
using Game.Prefabs;        // PrefabSystem, PrefabBase, PrefabData, DeathcareFacilityData
using Unity.Entities;      // Entity, SystemAPI
...
// For each prefab entity: read ProcessingRate from PrefabBase, then write the scaled value into DeathcareFacilityData.
EntityQuery query = SystemAPI.QueryBuilder()
    .WithAll<PrefabData, DeathcareFacilityData>()
    .Build();

using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

foreach (Entity prefabEntity in entities)
{
    // 1) Read vanilla baseline from PrefabBase authoring (not from *Data).
    if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
        continue;

    if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
        continue;

    // 2) Write new scaled value onto a copy of prefab entity's *Data component.
    DeathcareFacilityData dc = EntityManager.GetComponentData<DeathcareFacilityData>(prefabEntity);
    dc.m_ProcessingRate = authoring.m_ProcessingRate * scalar;

    // 3) Writes updated copy back to the entity.
    EntityManager.SetComponentData(prefabEntity, dc);
}
```

>**Example real mod using Option 1 (with different ways to change Prefabs):** [Tree Controller](https://github.com/yenyang/Tree_Controller/blob/master/Tree_Controller/Systems/ModifyVegetationPrefabsSystem.cs#L21)

---

### Option 2: compact ECS style (advanced)
- Same results as Option 1, uses [Unity.Entities ECS <RefRW<T>>](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/systems-systemapi-query.html)

```csharp
// Compact Unity.Entities ECS query style SystemAPI.Query<RefRW<T>>()
using Game.Prefabs;        // PrefabSystem, PrefabBase, PrefabData, DeathcareFacilityData
using Unity.Entities;      // Entity, SystemAPI, RefRW

foreach ((RefRW<DeathcareFacilityData> dc, Entity prefabEntity) in SystemAPI
    .Query<RefRW<DeathcareFacilityData>>()
    .WithAll<PrefabData>()          // prefab entities only
    .WithEntityAccess())            // exposes prefabEntity in the loop
{
    // Vanilla baseline from PrefabBase authoring
    if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
        continue;

    if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
        continue;

    dc.ValueRW.m_ProcessingRate = authoring.m_ProcessingRate * scalar;
}
```
Differences vs Option 1:
- Classic Option 1 is probably easier for beginners; Option 2 is denser and harder to trace errors.
- No ToEntityArray / NativeArray lifetime to manage.
- Writes through RefRW<T> (so there’s no “get struct copy → modify → SetComponentData” step).

---

### Step 3 — Restore Strategy / Custom "Marker" component
Special case: if changing something like Workers, consider:
- store what was applied (add a custom component)
- restore only if current values still match the marker (prevents stomping other mods)
- players often run multiple mods that can change the same values.
- apply on change events (Options UI / load), not per-frame

```csharp
// Example marker snippet: store what this mod last applied.
// Stored on the same prefab entity that has WorkplaceData.
private struct WorkplaceMarker : IComponentData
{
    public int AppliedMax; // last max-workers value written by this mod
}

// After writing WorkplaceData, write/update the marker too.
WorkplaceMarker marker = new WorkplaceMarker
{
    AppliedMax = scaledMax,
};

// Marker enables "restore only if it still matches" later,
// so another mod's changes don't get overwritten by accident.
bool hasMarker = SystemAPI.HasComponent<WorkplaceMarker>(prefabEntity); // already tracked?
if (hasMarker)
{ 
EntityManager.SetComponentData(prefabEntity, marker); // update existing marker
}
else
{
    EntityManager.AddComponent(prefabEntity, marker); // add marker first time
}
```
This is just a brief example of custom component markers with prefabs. Hopefully, someone writes a more extensive article.

**Advanced (optional): EntityCommandBuffer (ECB)**
- When adding components to a lot of entities simulatanously, instead of calling `EntityManager.AddComponentData(...)` inside the loop, queue the write with an ECB (`ecb.SetComponent(...)`).
- This batches writes and avoids immediate write sync points; useful when causing structural changes on lots of entities.
- Typical pattern: create the ECB from a phase barrier (ex: `ModificationEndBarrier`), to not stall the main thread and queue a lot commands to run in bulk.
  
```csharp
// ... get an ECB from a barrier (recommended) or create one manually.
EntityCommandBuffer ecb = m_Barrier.CreateCommandBuffer(); // e.g., ModificationEndBarrier

// ... inside the foreach after computing dc ...
ecb.AddComponent(prefabEntity, dc); // instead of EntityManager.AddComponent(prefabEntity, dc);
```
> **Example ECB** from [Tree Controller mod](https://github.com/yenyang/Tree_Controller/blob/master/Tree_Controller/Systems/ModifyVegetationPrefabsSystem.cs#L157)
> 
> See Unity Docs on [Optimizing for Structural Changes](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/optimize-structural-changes.html)
---
## Quick Baseline vs Direct write sample

### DO this for true vanilla baseline

Use this for restore accuracy and to avoid double-scaling.

```csharp

// Baseline version: read authored vanilla (PrefabBase), then scale into *Data.
// safe for restore or % scaling sliders

Entity prefabEntity = prefabRefLookup[instance].m_Prefab; // PrefabRef points to the prefab *entity* (ECS)

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
    return;

float baseRate = authoring.m_ProcessingRate;   // vanilla authored baseline
float scaledRate = baseRate * scalar;          // apply settings scalar
```

### DO this for Direct prefab write

Use this to set a `*Data` value (don't care about baseline check).

```csharp
// Direct prefab write: set prefab-entity *Data to a fixed value (no vanilla baseline).
// ECB queues the write now; the game applies it later (batched) at the barrier playback.

Entity prefabEntity = prefabRefLookup[instance].m_Prefab; // the prefab entity to edit

if (!EntityManager.HasComponent<DeathcareFacilityData>(prefabEntity))
    return; // prefab doesn't have this data, nothing to change

DeathcareFacilityData dc = EntityManager.GetComponentData<DeathcareFacilityData>(prefabEntity); // struct copy
dc.m_ProcessingRate = 12f; // absolute override example

EntityManager.SetComponentData(prefabEntity, dc); // queue: write this updated data back.
```

---

## Avoid Errors with  `SystemAPI.Query()` (Prefab vs Runtime types)

Some names exist in two different “layers”, and only the **runtime ECS component** version can be used in `SystemAPI.Query()` / `.WithAll<T>()`.

### The gotcha: same name, different layer

- `Game.Buildings.DeathcareFacility` = **runtime ECS components** ✅  valid in `SystemAPI.Query()` / `.WithAll<T>()`
- `Game.Prefabs.DeathcareFacility`  = **PrefabBase authoring types** ❌ not ECS components → **cannot** go in `.WithAll<T>()`

❌ Ambiguous: if the file has `using Game.Prefabs;` this code can bind to the wrong type:
```csharp
    EntityQuery q = SystemAPI.QueryBuilder()
        .WithAll<DeathcareFacility>()    // Makes confusing compile errors.
        .Build();
```

✅  Fix: fully-qualify the ECS type in query:

```csharp
    EntityQuery q = SystemAPI.QueryBuilder()
        .WithAll<Game.Buildings.DeathcareFacility>()
        .Build();
```

### What *is* valid to query for prefabs?
Prefab entities are still entities, so querying them is fine, the *Data* at the end of the name is the key.

✅ **Prefab entities**: entities that have `Game.Prefabs.PrefabData`  
✅ **Prefab `*Data` ECS components**: types like `Game.Prefabs.DeathcareFacilityData`

Example (valid):

```csharp
EntityQuery prefabQ = SystemAPI.QueryBuilder()
    .WithAll<Game.Prefabs.PrefabData, Game.Prefabs.DeathcareFacilityData>() // prefab entities with this *Data name
    .Build();
```

>Notes:<br>
>See the [Unity user manual (1.3*)](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/systems-systemapi.html) for complete details on SystemAPI queries.<br>
>This is a Prefabs article and this section is only meant to highlight one common SystemAPI error with prefabs.

---

## Quick summary

### Baseline vs data vs runtime (real examples)

| Layer | What it is | Good for | Not good for | Examples |
|:---|:---|:----|:---|:---|
| `PrefabBase` authoring | Real prefab definition | true vanilla baseline / restore | writing runtime effects | `Game.Prefabs.DeathcareFacility.m_ProcessingRate`<br>`Game.Prefabs.Workplace.m_Workplaces` |
| Prefab entity (`PrefabData`) | ECS representation of the prefab | writing scaled `*Data` values | using as baseline (can be modified) | `Game.Prefabs.DeathcareFacilityData.m_ProcessingRate`<br>`Game.Prefabs.WorkplaceData.m_MaxWorkers` |
| Instance entity | placed building / vehicle / citizen | inspecting current behavior | reading vanilla defaults | `Game.Companies.WorkProvider.m_MaxWorkers` *(runtime/cached)* |

> **Note:**
- Instance-side values like `WorkProvider.m_MaxWorkers` are not known to hot-update from prefab edits.
- Hence, editing prefab `WorkplaceData.m_MaxWorkers` applies to **new** buildings.


