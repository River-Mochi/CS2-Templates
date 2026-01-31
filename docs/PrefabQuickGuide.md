# PrefabSystem Source-of-Truth — Quick Guide (CS2 Modding)

---

## The rule

Vanilla baseline = what the game sets the buildings/vehicles/citizens to be.<br>
Prefab-entity with `*Data` name endings -> reached via `PrefabRef` are not vanilla baseline values and can be altered.


---

## Minimal baseline snippet

```csharp
PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

// vanilla baseline values comes from `TryGetPrefab(...)` → `PrefabBase`.  
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

// authoring components = vanilla truth
if (!prefabBase.TryGet(out Game.Prefabs.DeathcareFacility dcAuthoring))
    return;

float baseRate = dcAuthoring.m_ProcessingRate;    // copy of the real base number not altered by any mod.
```

---

## Example authoring components & fields

| Authoring component (PrefabBase) | Key fields (vanilla baseline) |
|---|---|
| `Game.Prefabs.DeathcareFacility` | `m_ProcessingRate`, `m_StorageCapacity` |
| `Game.Prefabs.Workplace` | `m_Workplaces`, `m_MinimumWorkersLimit` |

---

## Example ECS `*Data` components (changeable) you write on prefab entities

| ECS component | Key fields (scaled values you write) |
|---|---|
| `Game.Prefabs.DeathcareFacilityData` | `m_ProcessingRate`, `m_StorageCapacity` |
| `Game.Prefabs.WorkplaceData` | `m_MaxWorkers`, `m_MinimumWorkersLimit` |

---

## Danger snippet

```csharp
Entity prefab = prefabRefLookup[instance].m_Prefab;
var baseData = dcLookup[prefab]; // already modified by game/mods
var scaled = baseData.m_ProcessingRate * scalar; // double-scaling risk
```

---
## Why workers example is “special”
Worker limits are **runtime instance components** (ex: `Game.Companies.WorkProvider.m_MaxWorkers`) that don’t always hot-update when you edit the prefab.

**So:**
- editing `WorkplaceData` is correct and safe for all **new** buildings (or other things inside of names ending in *Data)
- but for Existing buildings to change values, something needs to trigger the game job to update the runtime instance like `WorkProvider.m_MaxWorkers`.
  - restarting the game usually doesn’t force that refresh
Option 1 Player action (easiest)
  - rebuild building
  - add/remove extension
  - add/remove upgrade (ex: cold storage)
Option 2 Custom code
- If you can find the specific game job using iLSpy, copy the code method, and then run it when you like (ex: Options UI slider to trigger it)
Option 3 Harmony patch code.
- Sometimes easier than option 2 as there are 1000's of code lines to research.
- Could be brittle on game patch days.

---

## “When do I mutate runtime instance components?”
Only if you fully understand:
- which systems read them, what caches exist, what invariant you might violate (rigorous research using Scene Explorer mod and special iLSpy or DnSpyEX apps).
- Do you really want to add a Harmony patch layer?
- Consider: mutating runtime components that are calculated by the game is riskier with side effects.
  - In the example of max workers, asking the player to rebuild the building to refresh values is easier and safe (all their new buildings are already handled by changing `WorkplaceData` in the easier method).
