# Prefab — Quick Guide (CS2 Mods)
This is a companion to [**PrefabSystemGuide**](https://github.com/River-Mochi/CS2-Templates/blob/main/docs/PrefabSystemGuide.md) which has better code examples.

---

## Maxim

Vanilla baseline = the original default values included with the game.<br>
Prefab `*Data` values (what mods usually edit) *may* match vanilla by chance, but **shouldn't be trusted as baseline.**

---

## Minimal baseline

```csharp
PrefabSystem prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();

// Baseline values are conveniently stored in `PrefabBase`
if (!prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
    return;

// authoring components = vanilla truth
if (!prefabBase.TryGetExactly(out Game.Prefabs.DeathcareFacility dcAuthoring))
    return;

float baseRate = dcAuthoring.m_ProcessingRate;  // vanilla baseline
```

---

## Example authoring components & fields

| Authoring component (PrefabBase) | Key fields (vanilla baseline) |
|---|---|
| `Game.Prefabs.DeathcareFacility` | `m_ProcessingRate`, `m_StorageCapacity` |
| `Game.Prefabs.Workplace` | `m_Workplaces`, `m_MinimumWorkersLimit` |

*Notice: there is **no** `Data` at the end of any of these names.*

---

## Example ECS `*Data` components (changeable) you write on prefab-entities

| ECS component | Key fields (scaled values you write) |
|---|---|
| `Game.Prefabs.DeathcareFacilityData` | `m_ProcessingRate`, `m_StorageCapacity` |
| `Game.Prefabs.WorkplaceData` | `m_MaxWorkers`, `m_MinimumWorkersLimit` |

>*Notice there **is** `Data` at the end of all these ECS names.*

---

## Minimal Write to prefab

```csharp
// Edit *Data named components using baseline values (from PrefabBase).
// `baseRate` and other items from the snippet above.

DeathcareFacilityData dc = EntityManager.GetComponentData<DeathcareFacilityData>(prefabEntity);
dc.m_ProcessingRate = baseRate * scalar;      // scale from true vanilla baseline
EntityManager.SetComponentData(prefabEntity, dc);
```
> This writes to the *Data on the prefab-entity. New placed buildings will use it;<br>
> Existing buildings rely on runtime/instance values that need some refresh trigger depending on what you changed.

---
## Why workers example is “special”
Worker limits are **runtime instance components** (ex: `Game.Companies.WorkProvider.m_MaxWorkers`) and instance-entities don't just hot-update when you edit the prefab `*Data` items.

### So:
- Editing `WorkplaceData` is safe for all **new** buildings (and other prefab type names ending in `Data`)
- For Existing buildings to change values, something needs to trigger the game job to update the runtime instance like `WorkProvider.m_MaxWorkers`.
    - restarting the game usually doesn’t force that refresh.
    
#### Option 1 Player action (easiest)
  - rebuild building
  - add/remove extension building or upgrade items (ex: cold storage)
  - These actions trigger the game's own job to run -> reads the `WorkplaceData` you altered -> refreshes the employee m_MaxWorkers -> building panel shows increase number.

#### Option 2 Custom code
- If you can find the specific game job using iLSpy, copy the same method used to complete Option 1, then run it when you like (ex: Options UI slider to trigger it)

#### Option 3 Harmony patch code.
- Sometimes easier than option 2 as there are 1000's of code lines to research and you might not find a lucky hook.
- Could be brittle on game patch days.

---

## “When do I mutate runtime instance components?”
Only if you fully understand:
- which systems read them, what caches exist, what invariant you might violate (rigorous research using Scene Explorer mod and special iLSpy or DnSpyEX apps).
- Do you really want to add a Harmony patch layer?
- Consider: mutating runtime components that are calculated by the game is riskier with side effects.
  - In the example of max workers, asking the player to rebuild the building to refresh values is easier and safe (all their new buildings are already handled by changing `WorkplaceData` in the easier method).

---

### Bonus thing
**ECS = Entity Component System**
- **Entity**     = an ID (a "thing")
- **Component**  = data attached to that ID (e.g. a struct)
- **System**     = code that reads/writes components
