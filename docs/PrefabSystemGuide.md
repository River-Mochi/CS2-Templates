# PrefabSystem “Source of Truth” in Cities: Skylines II (CO API)

This note is for CS2 modders who change things like **capacities / rates / special values (workers, vehicles, storage, etc.)** and want the results to be:

- **Correct** (true vanilla baselines / game defaults)
- **Compatible** (other mods can coexist)
- **Predictable** (players know when changes apply immediately vs needing a rebuild)

**No time to read?** [Quick Guide](https://github.com/River-Mochi/CS2-Templates/blob/main/docs/PrefabQuickGuide.md#prefabsystem-source-of-truth--quick-guide-cs2-modding)

---

**TL;DR mental model**

- **Baseline** = `PrefabBase` authoring (via `PrefabSystem.TryGetPrefab(...)`)
- **What mods usually edit** = prefab-entity `*Data` components (`WithAll<PrefabData>()`)
- **What gameplay uses right now** = instance-side runtime components (sometimes copied once + cached/serialized)

> **Scene Explorer** mod is recommended to see values live in-game: https://mods.paradoxplaza.com/mods/74285/Windows  
> Examples below use **Deathcare** from **Magic Hearse** (just an example—this pattern applies to many prefab types).

---

## 3 “things” that get mixed up (a lot)

In CS2 there are **three layers** that *sound* similar but behave differently:

### 1) Prefab Entity (ECS entity with `PrefabData`)
- The ECS representation of a prefab.
- Instances reference this via `PrefabRef.m_Prefab`.
- This is where prefab-side ECS `*Data` components live (things mods often write):
  - `DeathcareFacilityData`, `WorkplaceData`, etc.
- **Important:** prefab entities are **mutable**. The game + mods can change them during a session.

### 2) `PrefabBase` (authoring object) — the real vanilla baseline
- The authoring ScriptableObject for the prefab.
- Holds authored “vanilla truth” fields (the ones you want as baseline).
- You can also grab authoring “components” off it via `prefabBase.TryGet(out T)`.

### 3) Instance Entity (placed building/vehicle/citizen in the world)
- This is the thing the player placed (or the sim spawned).
- Has a `PrefabRef` pointing to a **prefab entity**.
- Has runtime components used by simulation **right now** (often cached/serialized).
- Some instance-side values **do not hot-update** just because you changed the prefab.

---

## Why `PrefabRef` is NOT the vanilla baseline

`PrefabRef.m_Prefab` only tells you **which prefab entity** the instance came from.

That prefab entity can already be modified by:
- the game (upgrades/extensions that combine stats)
- other mods
- your mod on earlier runs

So using prefab-entity `*Data` as “baseline” risks:
- **double-scaling**
- wrong “restore to vanilla”
- racing another mod’s edits

**Rule of thumb**
- ✅ Baseline = `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` → authoring fields  
- ❌ Baseline = reading `*Data` from the prefab entity referenced by `PrefabRef`

---

## Concrete components you can look up (from DNSpy)

### Authoring components on `PrefabBase` (vanilla fields)

**`Game.Prefabs.DeathcareFacility` (authoring)**
- `m_HearseCapacity`
- `m_StorageCapacity`
- `m_ProcessingRate`
- `m_LongTermStorage`

DNSpy clue: `DeathcareFacility.Initialize(...)` copies these into `DeathcareFacilityData` on the prefab entity.

**`Game.Prefabs.Hearse` (authoring)**
- `m_CorpseCapacity`

DNSpy clue: `Hearse.Initialize(...)` writes `HearseData(m_CorpseCapacity)`.

> Deathcare is just an easy example because it has obvious numbers. The same authoring→data pattern shows up all over the game.

### ECS `*Data` on prefab entities (things you usually write)

**`Game.Prefabs.DeathcareFacilityData`**
- `m_HearseCapacity`
- `m_StorageCapacity`
- `m_ProcessingRate`
- `m_LongTermStorage`

(Also note: `DeathcareFacilityData.Combine(...)` adds capacities/rates when combining data—this matters with upgrades/extensions.)

---

## Recommended pattern (safe + compatible)

### Step 1 — Read vanilla from `PrefabBase` authoring (baseline)
```csharp
PrefabSystem prefabSystem = World.DefaultGameObjectInjectionWorld
    .GetExistingSystemManaged<PrefabSystem>();

// prefabEntity is the ECS prefab entity (ex: from PrefabRef.m_Prefab)
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

// Authoring component lives on PrefabBase, not the prefab entity
if (!prefabBase.TryGet(out Game.Prefabs.DeathcareFacility authoring))
    return;

// True vanilla baseline examples
float baseRate = authoring.m_ProcessingRate;
int baseHearses = authoring.m_HearseCapacity;
```

**Gotcha:** `m_HearseCapacity` exists on `DeathcareFacility` authoring (DNSpy confirms it).  
If a project errors on `authoring.m_HearseCapacity`, it usually means the authoring type is wrong (not `Game.Prefabs.DeathcareFacility`) or the variable named `authoring` is a different component.

---

### Step 2 — Write scaled values onto ECS `*Data` on prefab entities
```csharp
foreach ((RefRW<Game.Prefabs.DeathcareFacilityData> dc, Entity e) in SystemAPI
    .Query<RefRW<Game.Prefabs.DeathcareFacilityData>>()
    .WithAll<PrefabData>()
    .WithEntityAccess())
{
    // Baseline: PrefabBase authoring
    if (!prefabSystem.TryGetPrefab(e, out PrefabBase prefabBase))
        continue;
    if (!prefabBase.TryGet(out Game.Prefabs.DeathcareFacility authoring))
        continue;

    float scaledRate = authoring.m_ProcessingRate * scalar;

    dc.ValueRW.m_ProcessingRate = scaledRate;

    // Example: scale hearse capacity too
    dc.ValueRW.m_HearseCapacity = math.max(1, (int)math.round(authoring.m_HearseCapacity * scalar));
}
```

Why this is the sweet spot:
- edits **one place** (prefab entity) for **all instances**
- avoids stomping instance-side caches directly
- plays nicer with other mods (especially if you also store/apply markers)

---

### Step 3 — For “special” values, use a controlled strategy (marker + restore)
Workers are a classic “special” case:
- prefab-side `WorkplaceData` changes are correct and safe
- but some instance-side worker limits are cached (often created once, then serialized)

A safer approach than brute-forcing all instances every frame:
- store what was applied (marker component)
- restore only if the current value still matches the marker (avoid clobbering other mods)
- do apply as a **one-shot** (on setting change / button), not per-frame

---

## When do existing instances actually “refresh”?

Two separate mechanisms exist:

### A) “Prefab *Data is read live” systems
Some simulation systems read prefab `*Data` each tick, so changing prefab `*Data` appears to apply immediately.

### B) “Prefab gets replaced” path (real prefab swap)
DNSpy shows `PrefabSystem.UpdatePrefab(...)` can create a **new prefab entity** and then call `ReplacePrefabSystem.ReplacePrefab(oldPrefab, newPrefab, sourceInstance)`.

`ReplacePrefabSystem` then:
- rewrites **PrefabRef** across the world from old→new
- updates buffers like `SubObject`, `SubNet`, `SubArea`, `SubMesh`, etc.
- marks instances `Updated`
- later runs `FinalizeReplaces()` which can recreate areas/nets/lanes when geometry changed

**Important modder note:** most runtime mods are not calling `PrefabSystem.UpdatePrefab(...)`.  
So if a value is cached on instances, editing prefab `*Data` may not force a rebuild.

---

## Don’t do this (baseline bug)
```csharp
// DON'T DO THIS: uses prefab-entity data as "vanilla baseline"
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;
var baseData = dcLookup[prefabEntity];       // might already be modified by game/mods
float scaled = baseData.m_ProcessingRate * scalar; // double-scaling risk
```

## Do this (correct baseline)
```csharp
// DO THIS: baseline from PrefabBase authoring
Entity prefabEntity = prefabRefLookup[instance].m_Prefab;

if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

if (!prefabBase.TryGet(out Game.Prefabs.DeathcareFacility authoring))
    return;

float baseRate = authoring.m_ProcessingRate;
float scaled = baseRate * scalar;
```

---

## Quick verify in-game (Scene Explorer)

Scene Explorer lets beginners see the three layers in one place:

- **PrefabBase authoring** (vanilla truth)
- **Prefab entity** (`PrefabData` entity with `DeathcareFacilityData`, `WorkplaceData`, etc.)
- **Placed instance** (runtime components like `WorkProvider` etc.)

---

## Quick reference

### Baseline vs data vs runtime
| Layer | What it is | Good for | Not good for |
|---|---|---|---|
| `PrefabBase` authoring | “real prefab definition” | true vanilla baseline | writing runtime effects |
| Prefab entity (`PrefabData`) | ECS prefab representation | writing scaled `*Data` | using as baseline |
| Instance entity | placed building/vehicle | inspecting current behavior | reading vanilla defaults |

### “Applies immediately?” rule of thumb
| What to change | Where to usually write | Applies to existing stuff instantly? |
|---|---|---|
| processing / storage / fleet caps | prefab `*Data` (ex: `DeathcareFacilityData`) | often yes |
| geometry-ish stuff (subnets/subareas) | prefab updates that trigger replacement | depends; replacement path exists |
| workers max/min | `WorkplaceData` on prefab | often **needs** rebuild/upgrade/extension event |
| instance runtime values | instance components | yes, but risky (compat + invariants) |
