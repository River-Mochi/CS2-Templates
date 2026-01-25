# PrefabSystem Source-of-Truth — Cheat Sheet (CS2 Modding)

---

## The rule
**True vanilla baseline comes from `PrefabSystem.TryGetPrefab(...)` → `PrefabBase` authoring components.**  
Do **not** treat prefab-entity `*Data` components reached via `PrefabRef` as baseline.

---

## Minimal “RIGHT baseline” snippet

```csharp
PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

// prefabEntity is the ECS prefab entity (often from PrefabRef.m_Prefab)
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

// authoring components = vanilla truth
if (!prefabBase.TryGet(out Game.Prefabs.DeathcareFacility dcAuthoring))
    return;

float baseRate = dcAuthoring.m_ProcessingRate;
int baseHearses = dcAuthoring.m_HearseCapacity;
```

---

## Common authoring components & fields (examples)

| Authoring component (PrefabBase) | Key fields (vanilla baseline) |
|---|---|
| `Game.Prefabs.DeathcareFacility` | `m_ProcessingRate`, `m_HearseCapacity`, `m_StorageCapacity`, `m_LongTermStorage` |
| `Game.Prefabs.Workplace` | `m_Workplaces`, `m_MinimumWorkersLimit` |

---

## Common ECS `*Data` components you write (on prefab entities)

| ECS component | Key fields (scaled values you write) |
|---|---|
| `Game.Prefabs.DeathcareFacilityData` | `m_ProcessingRate`, `m_HearseCapacity`, `m_StorageCapacity`, `m_LongTermStorage` |
| `Game.Prefabs.WorkplaceData` | `m_MaxWorkers`, `m_MinimumWorkersLimit` |

---

## Why workers are “special”
Worker limits are **runtime instance components** (example: `Game.Companies.WorkProvider.m_MaxWorkers`) that don’t always hot-update when you edit the prefab.

**So:**
- editing `WorkplaceData` is correct and safe
- but players may need a **refresh event** for existing buildings:
  - rebuild building
  - add/remove extension
  - add/remove upgrade
- restarting the game usually doesn’t force that refresh

---

## WRONG snippet (don’t do this)

```csharp
Entity prefab = prefabRefLookup[instance].m_Prefab;
var baseData = dcLookup[prefab]; // already modified by game/mods
var scaled = baseData.m_ProcessingRate * scalar; // double-scaling risk
```

---

## “When do I mutate runtime instance components?”
Only if you fully understand:
- which systems read them
- what caches exist
- what invariant you might violate

If you don’t, stick to prefab `*Data` + give players refresh instructions.
