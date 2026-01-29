# PrefabSystem “Source of Truth” in Cities: Skylines II (CO API)

This note is for CS2 modders who change things like **capacities / rates and special values like worker counts** and want the results to be:
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
  
### 2) Prefab Entity (ECS entity with `PrefabData`)
- ECS representation of a prefab.
- Often referenced by `PrefabRef.m_Prefab` from an instance.
- Stores ECS prefab-side `*Data` components that **mods commonly edit** (ex: `DeathcareFacilityData`, `WorkplaceData`).
- **Important:** prefab entities are **mutable**. Game + mods can change them during a session.
- Not everything from PrefabBase to PrefabData is one-to-one (not all provide easy tuning knobs)

### 3) Instance Entity (placed building / vehicle / citizen)
- The thing that exists in the city right now.
- `PrefabRef` points to a **prefab entity** (`PrefabRef.m_Prefab`), not `PrefabBase`.
- Has runtime components used by simulation right now (often game computed/ cached / serialized).
- Most all known of these runtime values do **not** hot-update just because the prefab entity changed.
  - (ex: workers: instance-side `Game.Companies.WorkProvider.m_MaxWorkers`)
  - needs extra code to trigger an instant update or a player action (ex: place a new building).

> See **InstanceEntities.md** for detailed instance-side / runtime examples.

---

## Why `PrefabRef` is NOT the “true vanilla baseline”

`PrefabRef.m_Prefab` only tells you *which prefab entity* the instance came from.
That prefab entity can and is commonly modified by mods.

So using prefab-entity `*Data` as “baseline” could produce **double-scaling** or **wrong restore values**.

**Rule of thumb**
- ✅ Baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring component fields
- ❌ Baseline = reading `*Data` from the prefab entity referenced by `PrefabRef`

---
## Tiny but important: how `TryGetPrefab(...)` works

The engine keeps an internal list of authoring prefabs (`PrefabSystem` has a list like `m_Prefabs`).
Each prefab entity has `PrefabData`, which stores **an index into that list**.

So this call:

```csharp
prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase)
```

is basically:
- read `PrefabData.m_Index` from `prefabEntity`
- return `m_Prefabs[m_Index]`

That “index bridge” is why `TryGetPrefab(...)` is the baseline hook for vanilla values.

---


## Concrete: real components & fields (examples)

### Authoring components (PrefabBase) — true vanilla values
These live on `PrefabBase` and contain the authored defaults.

**`Game.Prefabs.DeathcareFacility` (authoring)**
- `m_ProcessingRate`, `m_StorageCapacity`

**`Game.Prefabs.Workplace` (authoring)**
- `m_Workplaces` (baseline max workers)
- `m_MinimumWorkersLimit`

### ECS `*Data` components (on prefab entities)
These are what mods usually write to when scaling for example:

**`Game.Prefabs.DeathcareFacilityData`**
- `m_ProcessingRate`, `m_StorageCapacity`

**`Game.Prefabs.WorkplaceData`**
- `m_MaxWorkers`, `m_MinimumWorkersLimit`

### Runtime / instance-side components (placed entities)
Often what simulation uses *right now*:

**`Game.Companies.WorkProvider` (instance-side example)**
- `m_MaxWorkers`

- runtime value; normally not hot-updated from prefab edits in Options UI
- Means that updating `WokplaceData` above only applies to new buildings but not existing ones.
- `Workprovider` is game computed and needs extra code to make "instant" changes on a building
- or a player action to naturally trigger the game job (build new building or add an extension triggers game to run job and read `WorkplaceData` again).

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

// true baseline examples
float baseRate = authoring.m_ProcessingRate;
int baseHearses = authoring.m_HearseCapacity;
```

### Step 2 — Write scaled values onto ECS `*Data` on prefab entities

This is where the mod actually **changes the prefab entity** (entities with `PrefabData`) by writing to `*Data` components.

#### Option 1: explicit `EntityQuery` + `NativeArray<Entity>` loop

```csharp
// Step 2: iterate prefab entities that have DeathcareFacilityData.
// Build query → get entities → foreach loop.

EntityQuery query = SystemAPI.QueryBuilder()
    .WithAll<PrefabData, DeathcareFacilityData>() // prefab entities only + the data component to edit
    .Build();

NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

foreach (Entity prefabEntity in entities)
{
    // 1) Read vanilla baseline from PrefabBase authoring (NOT from prefab-entity *Data)
    if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
        continue;

    if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
        continue;

    float baseRate = authoring.m_ProcessingRate;     // vanilla authored baseline
    float scaledRate = baseRate * scalar;            // apply settings scalar

    int baseStorage = authoring.m_StorageCapacity;   // vanilla authored baseline
    int scaledStorage = Math.Max(1, (int)Math.Round(baseStorage * scalar));

    // 2) Write scaled values onto the prefab entity's *Data component
    DeathcareFacilityData dc = EntityManager.GetComponentData<DeathcareFacilityData>(prefabEntity);
    dc.m_ProcessingRate = scaledRate;
    dc.m_StorageCapacity = scaledStorage;
    EntityManager.SetComponentData(prefabEntity, dc);
}
entities.Dispose();
```

#### Option 2: `SystemAPI.Query<RefRW<T>>()` (compact ECS style)

```csharp
// Same logic as above, just using RefRW<T> query style.
// foreach header is denser, no dispose needed.

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
    dc.ValueRW.m_StorageCapacity = Math.Max(1, (int)Math.Round(authoring.m_StorageCapacity * scalar));
}
```

**Option 3 (EntityManager loop style):** [Tree Controller](https://github.com/yenyang/Tree_Controller/blob/56752932a92eb5d0632ecedda499c61157722da2/Tree_Controller/Systems/ModifyVegetationPrefabsSystem.cs#L33)

---
## Baseline examples

### DON'T do this for baseline (double-scale trap)
```csharp
// Common method for assigning values; bad if intent is correct scaled baseline.
Entity prefab = prefabRefLookup[instance].m_Prefab;
var baseData = dcLookup[prefab]; // danger, might already be modified!
float scaled = baseData.m_ProcessingRate * scalar; // double-scaling risk
```

### DO this (true vanilla baseline)
```csharp
// RIGHT: baseline from PrefabBase authoring (vanilla)
Entity prefabEntity = prefabRefLookup[instance].m_Prefab; // PrefabRef points to the *prefab entity* (ECS)

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
    return;
// baseRate = vanilla truth, scaled = computed result
float baseRate = authoring.m_ProcessingRate;  // Read vanilla baseline from authoring fields
float scaled = baseRate * scalar;  // Apply scalar from settings.
```

---

## Quick reference

### Baseline vs data vs runtime
| Layer | What it is | Good for | Not good for |
|---|---|---|---|
| `PrefabBase` authoring | Real prefab definition | true vanilla baseline | writing runtime effects |
| Prefab entity (`PrefabData`) | ECS representation | writing scaled `*Data` | using as baseline |
| Instance entity | placed building/vehicle | inspecting current behavior | reading vanilla defaults |

### “Applies immediately?” rule of thumb (real examples)
| What to change | Where to usually write | Applies to existing buildings instantly? |
|---|---|---|
| processing/storage | prefab `*Data` components (ex: `DeathcareFacilityData`) | yes / easiest |
| workers max/min | `WorkplaceData` on prefab | Often needs a trigger: new building/extension/upgrade |
| runtime worker provider | `WorkProvider` on instances | yes, but risky (compatibility + invariants) |
