# Instance Entities (runtime state, caches, refresh triggers)

This is a companion to [**PrefabSystemGuide.md**](https://github.com/River-Mochi/CS2-Templates/blob/main/docs/PrefabSystemGuide.md)

Most mods edit **prefab entities** (entities with PrefabData) by writing to components with *Data at end of the name.  
Gameplay, however, often reads **instance entities** (the placed building/vehicle/citizen/etc.) that may hold **cached / computed / serialized** runtime state.

Result: some Options UI menu sliders changing building/vehicle (prefab) details show instant changes on existing prefabs, while other values only update on **newly created** buildings or after a trigger (adding/removing a building extension or upgrade item). 

---

## Quick glossary

| Term | Meaning | Typical mod action |
|---|---|---|
| **PrefabBase** (authoring) | Vanilla authored object (source-of-truth baseline) | Read baseline via `PrefabSystem.TryGetPrefab(...)` |
| **Prefab entity** (`PrefabData`) | ECS entity that represents the prefab and holds `*Data` components | Write scaled `*Data` |
| **Instance entity** | Placed things have `PrefabRef.m_Prefab that points to a prefab entity | Inspect current runtime behavior; avoid blind edits |

**Key link:** instance → prefab entity via `PrefabRef.m_Prefab`.

---

## Why some prefab changes feel instant (and others don’t)

### Pattern A: “reads prefab data often”
Some systems read prefab `*Data` frequently, or the effect is naturally visible once the next sim tick uses the new value.

- Typical examples: some capacities, rates, dispatch limits, storage, fleet counts  
- Result: editing prefab `*Data` **often** shows up quickly (sometimes immediately)

### Pattern B: “instance caches / computed state”
Other systems compute a per-instance value and store it on the instance entity (and sometimes serialize it into the save).  
Prefab edits then affect **new instances**, but **existing** ones can remain unchanged until a refresh trigger happens.

- Typical examples: worker limits (common), upgrade/extension combined stats, derived runtime limits
- Result: prefab edits do **not reliably** update existing instances without a refresh

---

## What “cached / computed / serialized” usually looks like

Instance-side runtime state often has one or more of these properties:

- **Computed**: derived from prefab data + upgrades/extensions + efficiency + policies
- **Cached**: stored on the instance so the sim doesn’t recompute every time
- **Serialized**: saved into the savegame so it persists across loads

None of those are “bad”; they’re normal sim engineering tradeoffs. They *do* mean that editing prefab data alone may not invalidate existing runtime state.

---

## Worker limits case study (common pain point)

### Typical layers

| Layer | Example type | Example fields |
|---|---|---|
| PrefabBase authoring | `Game.Prefabs.Workplace` | `m_Workplaces`, `m_MinimumWorkersLimit` |
| Prefab entity `*Data` | `Game.Prefabs.WorkplaceData` | `m_MaxWorkers`, `m_MinimumWorkersLimit` |
| Instance runtime | `Game.Companies.WorkProvider` | `m_MaxWorkers` (runtime) |

### What tends to happen
- Editing `WorkplaceData` on the prefab often affects **new** buildings
- Existing buildings may keep their current `WorkProvider` runtime state until something triggers a recompute
- Writing `WorkProvider` directly can “work”, but it is **high risk**: other systems/jobs may recompute/overwrite it, and incorrect values can break invariants or fight other mods

---

## How to tell if a value is instance-cached

### Fast in-game test (Scene Explorer)
1. Pick a placed building/vehicle.
2. Inspect **instance entity** runtime component value.
3. Inspect **prefab entity** `*Data` value.
4. Change the setting (edit prefab `*Data`).
5. Re-check the instance value.

If prefab value changes but instance stays the same, the value is very likely **instance-cached** or **recomputed only on events**.

### dnSpy tips
If a struct/component implements `ISerializable`, it’s a strong clue it can be stored in saves.  
This does not prove “cached”, but it raises the probability it may persist in saves.

---

## Refresh triggers that often cause recompute

Exact triggers vary by system, but these commonly cause rebuild/reinit paths:

| Trigger | Typical result |
|---|---|
| Building placed (new instance) | Prefab values copied/initialized for that instance |
| Upgrade toggled / extension added/removed | Combine/recompute stats for that instance |
| Building rebuild (delete + place again) | Forces a clean init using current prefab state |
| Some service/system-specific “refresh” method | Recomputes runtime data (best-case, but must be located per system) |

This is why “rebuild / change extension / toggle upgrade” is often the most reliable player-facing instruction.

---

## Recommended mod strategies (ordered by safety)

### Strategy 1 — Prefab-only + player guidance (highest compatibility)
- Edit prefab `*Data` for correct future behavior
- For instance-cached values, tell players a refresh is needed (rebuild / upgrade/extension change)

Best when stability and compatibility matter most.

### Strategy 2 — Prefab edits + system-driven refresh (best quality if available)
- Edit prefab `*Data`
- Locate the game system/method that recomputes the instance runtime value
- Trigger it **once** on setting change (not per-frame)

This is ideal, but requires targeted research and careful integration.

### Strategy 3 — Direct instance runtime edits (works fast, highest risk)
- Write to instance runtime components (e.g., `WorkProvider`)
- Must avoid fighting other systems and must preserve invariants
- Must be one-shot/event-driven and guarded with markers to avoid stomping other mods

Use only when the dependency chain is understood.

### Strategy 4 — Patching (Harmony) (powerful, brittle)
- Patch the method that computes the runtime value or the refresh trigger
- Can be the only route for some values
- More likely to break on game updates

---

## Compatibility rules of thumb (don’t stomp)

### Marker pattern
When writing a value that other mods might also change, store a marker so restore only happens when safe.

- Store “applied-by-mod” + “applied-value”
- On restore, only revert if the current value still matches the applied marker

This prevents undoing another mod’s later change.

### One-shot, not per-frame
Even when a value is “safe enough” to set:
- do it on setting change / load / explicit action
- avoid continuous writes that fight simulation systems/jobs

---

## Short code examples

### 1) Detect mismatch: instance runtime vs prefab data (conceptual)
This is a debugging pattern to confirm “instance-cached” behavior.

```csharp
// Pseudocode example; type names vary.
foreach (var (prefabRef, workProvider, entity) in SystemAPI
    .Query<RefRO<Game.Prefabs.PrefabRef>, RefRO<Game.Companies.WorkProvider>>()
    .WithEntityAccess())
{
    Entity prefabEntity = prefabRef.ValueRO.m_Prefab;

    if (!SystemAPI.HasComponent<Game.Prefabs.WorkplaceData>(prefabEntity))
        continue;

    var prefabWork = SystemAPI.GetComponent<Game.Prefabs.WorkplaceData>(prefabEntity);

    int prefabMax = prefabWork.m_MaxWorkers;                // prefab entity value
    int instanceMax = workProvider.ValueRO.m_MaxWorkers;    // instance runtime value

    // If prefabMax changes but instanceMax stays the same, the value is instance-cached or event-recomputed.
}
```

### 2) Safe stance: prefab edit + “refresh needed” message
```csharp
// Edit prefab entity WorkplaceData (new buildings reflect changes)
// Then communicate: existing buildings update after rebuild/upgrade/extension change.
```

### 3) “System-driven refresh” placeholder
```csharp
// Best-case approach:
// - Identify the CO system that recomputes the runtime component from prefab state
// - Call/trigger it once when settings change
// Details are system-specific and belong next to the mod that uses it.
```

---

## Practical player-facing wording (short)

When a value is instance-cached (workers are common):

> “This setting affects new buildings immediately. Existing buildings update after a refresh (rebuild the building, or add/remove an upgrade/extension). Restarting the game usually won’t refresh existing buildings.”

---

## Summary table

| Goal | Typical approach | Risk |
|---|---|---|
| Correct vanilla baseline | Read from `PrefabBase` authoring | Low |
| New instances use new values | Write prefab entity `*Data` | Low |
| Existing instances update instantly | Trigger a real recompute path | Medium |
| Existing instances update instantly (brute force) | Write instance runtime components | High |
| Force through game internals | Patch (Harmony) | High (update-brittle) |

