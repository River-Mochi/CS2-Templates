# PrefabSystem “Source of Truth” in Cities: Skylines II (CO API)

This note is for CS2 modders who change things like **capacities / rates / counts** and want results to be:

- **Correct** (true vanilla baselines / game defaults)
- **Compatible** (other mods can coexist)
- **Predictable** (players know when changes apply immediately vs needing a refresh)

**No time to read?** See: `PrefabQuickGuide.md`

---

## TL;DR mental model

- **True vanilla baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring component fields**
- **What mods usually edit** = prefab-entity ECS `*Data` components (`WithAll<PrefabData>()`)
- **What simulation uses right now** = instance/runtime components (often cached/serialized; not always hot-updated)

---

## The 3 layers that get mix up

### 1) Prefab entity (ECS entity with `PrefabData`)

The ECS representation of a prefab.

- Often referenced by `PrefabRef.m_Prefab` from instances.
- Holds prefab-side ECS `*Data` components mods commonly edit (e.g., `DeathcareFacilityData`, `WorkplaceData`).
- **Mutable** during a session.
- Can be **replaced** by the game when prefab definitions update.

### 2) PrefabBase (authoring object) — the baseline source of truth

The authoring “truth” for vanilla values.

```csharp
PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
    return;

// true vanilla baseline (authoring)
int baseHearses = authoring.m_HearseCapacity;
float baseRate = authoring.m_ProcessingRate;
```

- **Treat PrefabBase authoring fields as “true vanilla baseline.”**
- This is the safest baseline for scaling/restoring without double-scaling.

### 3) Instance entity (placed building / vehicle / citizen) 

- Has `PrefabRef` pointing at the prefab entity.
- Has runtime components used by simulation now.
- Often carries **cached / computed / serialized** values.
- Details in InstanceEntities.md
---

## What `PrefabRef` really is (and what it is not)

`PrefabRef` is just:

```csharp
public struct PrefabRef : IComponentData
{
    public Entity m_Prefab; // prefab entity
}
```

So:

- ✅ tells which **prefab entity** an instance currently references
- ❌ does **not** mean “vanilla baseline”
- ❌ does **not** guarantee stability: prefab entities can be **replaced** and references can be repointed

**Compatibility rule:** avoid treating `PrefabRef.m_Prefab` as “permanent identity” across time.

---

## Why `PrefabRef.m_Prefab` is NOT a safe vanilla baseline

If code does this:

```csharp
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;
var data = dcLookup[prefabEntity]; // DeathcareFacilityData on prefab entity
```

That `data` can already be modified by:

- upgrades/extensions combining stats
- other mods
- earlier runs of the same mod

So using prefab-entity `*Data` as baseline risks **double scaling** and **wrong restores**.

### Rule of thumb

- ✅ Baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring fields
- ❌ Baseline = reading prefab-entity `*Data` through `PrefabRef`

---

## Concrete: Deathcare + Hearse fields (common confusion)

### Authoring (PrefabBase) — vanilla baseline

| Authoring component | Baseline fields |
|---|---|
| `Game.Prefabs.DeathcareFacility` | `m_HearseCapacity`, `m_StorageCapacity`, `m_ProcessingRate`, `m_LongTermStorage` |
| `Game.Prefabs.Hearse` | `m_CorpseCapacity` |

**Do not mix these:**
- Facility fleet slots: `DeathcareFacility.m_HearseCapacity`
- Vehicle payload: `Hearse.m_CorpseCapacity`

### Prefab entity ECS `*Data` (commonly edited)

| ECS component | Fields typically edited |
|---|---|
| `Game.Prefabs.DeathcareFacilityData` | `m_HearseCapacity`, `m_StorageCapacity`, `m_ProcessingRate`, `m_LongTermStorage` |
| `Game.Prefabs.HearseData` | `m_CorpseCapacity` |

---

## Recommended pattern (safe + compatible)

### Step 1 — read vanilla from PrefabBase authoring

```csharp
PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
    return;

int baseHearses = authoring.m_HearseCapacity;
float baseRate = authoring.m_ProcessingRate;
```

### Step 2 — write scaled values onto prefab entity `*Data` (PrefabData entities)

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

### Step 3 — runtime-cached values need a deliberate strategy

Some values used by simulation are **instance-side runtime components** and may not hot-update from prefab edits.

- Prefab edits remain correct and safe for **new** instances.
- Existing instances may require a **refresh event** (rebuild, add/remove upgrade/extension).
- Avoid per-frame instance mutation for performance and compatibility.

---

## WRONG vs RIGHT baseline examples

### WRONG (double-scaling risk)

```csharp
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;
var baseData = dcLookup[prefabEntity];           // may already be modified
dcLookup[prefabEntity] = new DeathcareFacilityData
{
    m_ProcessingRate = baseData.m_ProcessingRate * scalar
};
```

### RIGHT (authoring truth)

```csharp
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;

PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility authoring))
    return;

float scaled = authoring.m_ProcessingRate * scalar;
```

---

## Quick reference tables

### Baseline vs data vs runtime

| Layer | What it is | Good for | Not good for |
|---|---|---|---|
| `PrefabBase` authoring | real prefab definition | true vanilla baseline | direct runtime mutation |
| Prefab entity (`PrefabData`) | ECS prefab representation | writing scaled `*Data` | baseline (unless intentionally “scale effective”) |
| Instance entity | placed building/vehicle | inspecting current behavior | reading vanilla defaults |

### “Applies immediately?” rule of thumb

| Change type | Usually written where | Existing instances update instantly? |
|---|---|---|
| processing/storage/fleet | prefab `*Data` (e.g., `DeathcareFacilityData`) | often yes |
| workers max/min | prefab `WorkplaceData` | often needs refresh event |
| instance runtime components | instance components | yes, but riskier |
