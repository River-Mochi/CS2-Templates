# PrefabSystem in Cities: Skylines II

This note is for CS2 modders who change prefab values (capacities, rates, workers, etc.) and want the results to be:
- **Correct** (true vanilla baselines / game defaults)
- **Compatible** (coexist with most other mods)
- **Predictable** (clear when changes apply: existing vs new buildings)

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

- The prefab’s **vanilla default values** shipped with the game.

```csharp
PrefabSystem prefabSystem =
    World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))  // baseline
    return;
```

- Use PrefabBase fields as the baseline for scaling/restore (prevents double-scaling).
- PrefabBase includes the raw field value that asset creator used on that prefab.
  
### 2) Prefab-Entity (ECS entity with `PrefabData`)
- An entity tagged with `Game.Prefabs.PrefabData` that holds prefab-side components (often ends in **Data**).
- **Important:** mods commonly edit these (ex: School**Data**, Workplace**Data**).
- Not every PrefabBase field has a 1:1 prefab-side `*Data` equivalent (not all expose easy tuning knobs).

### 3) Instance Entity (placed building / vehicle / citizen)

- The thing (runtime component) that exists in the city simulation right now.
- `PrefabRef` points to a **prefab-entity** (`PrefabRef.m_Prefab`), not `PrefabBase`.
- Most known runtime values do **not** hot-update just because the prefab entity changed.
  - (ex: workers: instance-side `Game.Companies.WorkProvider.m_MaxWorkers`)
  - needs extra code to trigger an update of existing buildings or a player action (ex: place an extension).

> See [**InstanceEntities**](https://github.com/River-Mochi/CS2-Templates/blob/main/docs/InstanceEntities.md) for instance-side / runtime examples.

---

## Recommended pattern (safe & compatible)

### Step 1 — Read vanilla from PrefabBase
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

#### Option 2: compact ECS style (advanced)
- Same results as Option 1, uses [Unity.Entities ECS `RefRW<T>`](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/systems-systemapi-query.html)

```csharp
// Compact Unity.Entities ECS query style SystemAPI.Query<RefRW<T>>()

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

### Step 3 — Optional Restore Strategy / Custom "Marker" component (advanced)

Special Case: when changing values that other mods might also touch (ex: Workers), store what this mod applied on the same prefab entity. 
On restore, only revert when the current value still matches the marker (avoids stomping other mods).

- **Marker = last value applied by this mod. Restore only if it still matches**
- Apply on setting change/load, not per-frame.

```csharp
// Custom component: store what this mod last applied.
// Stored on the same prefab entity that has WorkplaceData.
private struct WorkplaceMarker : IComponentData
{
    public int AppliedMax; // last max-workers value this mod wrote.
}

// After writing WorkplaceData, write/update the marker too.
WorkplaceMarker marker = new WorkplaceMarker
{
    AppliedMax = scaledMax,
};

// Marker enables "restore only if it still matches" later, so another mod's changes don't get overwritten.
bool hasMarker = SystemAPI.HasComponent<WorkplaceMarker>(prefabEntity); // already tracked?
if (hasMarker)
{
    EntityManager.SetComponentData(prefabEntity, marker); // update existing marker
}
else
{
    EntityManager.AddComponentData(prefabEntity, marker); // add marker first time (structural change)
}
```
This is a brief example of custom component markers with prefabs. Hopefully, someone writes a more extensive article.<br>
Refer to: [Unity unmanaged components](https://docs.unity3d.com/Packages/com.unity.entities%401.4/manual/components-unmanaged.html) and [Unity user manual: EntityManager](https://docs.unity3d.com/Packages/com.unity.entities@1.3/api/Unity.Entities.EntityManager.html)

### Advanced (optional): EntityCommandBuffer (ECB)

**Structural changes** (like AddComponentData) can trigger **sync points** when repeated many times in a loop.<br>
Create the ECB from an appropriate barrier for the update phase (ex: ModificationEndBarrier) so playback is later at a predictable point.

- `EntityManager.AddComponentData(entity, data)` → **`ecb.AddComponent(entity, data)`**    // add + set initial value
- `EntityManager.SetComponentData(entity, data)` → **`ecb.SetComponent(entity, data)`**    // not structural, but can be batched

Note: `SetComponentData` is *not* a structural change; ECB is mainly the performance win for **add/remove**.

```csharp
// ECB variant: same logic as EntityManager, but structural work is queued and played back later.
EntityCommandBuffer ecb = m_Barrier.CreateCommandBuffer(); // e.g., ModificationEndBarrier

if (hasMarker)
{
    ecb.SetComponent(prefabEntity, marker);  // marker exists: queue update.
}
else
{
    ecb.AddComponent(prefabEntity, marker);  // marker missing: queue add.
}
```

> **Example ECB** from [Tree Controller mod](https://github.com/yenyang/Tree_Controller/blob/master/Tree_Controller/Systems/ModifyVegetationPrefabsSystem.cs#L157)<br>
> See Unity Docs on [Optimizing for Structural Changes](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/optimize-structural-changes.html)

---

## Quick Baseline vs Direct write sample

### DO this to get vanilla baseline

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

Entity prefabEntity = prefabRefLookup[instance].m_Prefab;

if (!EntityManager.HasComponent<DeathcareFacilityData>(prefabEntity))
    return;

DeathcareFacilityData dc = EntityManager.GetComponentData<DeathcareFacilityData>(prefabEntity);
dc.m_ProcessingRate = 12f;

EntityManager.SetComponentData(prefabEntity, dc); // writes immediately
```

---

## Avoid Errors with  `SystemAPI.Query()` (Prefab vs Runtime types)

Some names exist in two different “layers”, and only the **runtime ECS component** can be used in `SystemAPI.Query()` / `.WithAll<T>()`.

### The gotcha: same name, different layer

- `Game.Buildings.DeathcareFacility` = **runtime ECS components** ✅ valid in WithAll<T>()
- `Game.Prefabs.DeathcareFacility` = **PrefabBase authoring types** ❌ NOT ECS → **cannot** go in WithAll<T>()

❌ Ambiguous: if file has `using Game.Prefabs;` this can bind to the wrong type:

```csharp
    EntityQuery q = SystemAPI.QueryBuilder()
        .WithAll<DeathcareFacility>()    // Makes misleading compile errors.
        .Build();
```

✅ Fix: fully-qualify the ECS type:

```csharp
    EntityQuery q = SystemAPI.QueryBuilder()
        .WithAll<Game.Buildings.DeathcareFacility>()   // this is a good defense habit.
        .Build();
```

### What *is* valid to query for prefabs?
Prefab entities are still entities, so querying them is fine, the *Data* at the end of the name is the key.

✅ `Game.Prefabs.PrefabData`, `Game.Prefabs.PrefabRef` = **ECS components** ✅ valid in `WithAll<T>()`

Example (valid):

```csharp
EntityQuery prefabQ = SystemAPI.QueryBuilder()
    .WithAll<Game.Prefabs.PrefabData, Game.Prefabs.DeathcareFacilityData>() // prefab entities with *Data in the name.
    .Build();
```

>Notes:<br>
>See the [Unity user manual (1.3*)](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/systems-systemapi.html) for complete details on SystemAPI queries.<br>
>This section is only for highlighting one common SystemAPI error with prefabs, a more detailed SystemAPI guide is still needed.

---

## Quick summary

### Baseline vs data vs runtime (real examples)

| Item | What it is | Good for | Not good for | Examples |
|:---|:---|:---|:---|:---|
| `PrefabBase` authoring | Real prefab definition (vanilla-authored values) | true vanilla baseline / restore | changing live behavior directly | `Game.Prefabs.DeathcareFacility.m_ProcessingRate`<br>`Game.Prefabs.Workplace.m_Workplaces` |
| Prefab entity (`PrefabData`) | ECS, holds `*Data` components | writing `*Data` values | using `*Data` as “vanilla baseline” | `Game.Prefabs.DeathcareFacilityData.m_ProcessingRate`<br>`Game.Prefabs.WorkplaceData.m_MaxWorkers` |
| `PrefabRef` (instance link) | Instance component that points to the prefab entity<br>(field `m_Prefab`) | finding the prefab entity to edit | treating “prefab entity `*Data`” as baseline | `Game.Prefabs.PrefabRef.m_Prefab`<br>*(PrefabRef stores an entity handle in m_Prefab)* |
| Instance entity | Placed building / vehicle / citizen | inspecting current behavior | reading vanilla defaults | `Game.Companies.WorkProvider.m_MaxWorkers` *(runtime/cached)* |

> **Note:**
- `PrefabRef.m_Prefab` points to the **prefab entity**, not `PrefabBase`. Use `TryGetPrefab(...)` for vanilla baseline.
- **Instance-side** values like `WorkProvider.m_MaxWorkers` are not known to hot-update from prefab edits.
- Hence, editing prefab `WorkplaceData.m_MaxWorkers` applies to **new** buildings, and you need alternate methods to see changes in **existing** buildings.


