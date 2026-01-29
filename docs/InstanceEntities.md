# Instance Entities

## Why does a prefab change apply instantly for some sliders, but the same method can not be used for workers?

Important:

- Changing **processing rate / storage / some capacity** slider in Options UI can take effect immediately.
- Changing items like **workers** often needs a “rebuild” (rebuild the building, add/remove extension/upgrade) before the building shows true new worker limit.
- Restarting the game usually does **not** force that update.

### What’s happening (simple)

Some values are read from **prefab** `*Data` frequently (or affect newly spawned behavior quickly).
Examples: processing rate, storage capacity, vehicle capacity.

Other values feed into **instance runtime components** that are computed/cached and not automatically invalidated when you edit prefab data.
Workers are an example: the sim may use a cached per-building value (ex: `WorkProvider.m_MaxWorkers`) that isn’t recomputed just because `WorkplaceData` changed.
Upgrades/extensions also complicate this: the game may combine multiple sources into a final runtime worker limit.

### Practical takeaway
Example, if the mod scales something like worker counts:
- Write scaled worker values onto the prefab (`WorkplaceData`), but:
  - Existing buildings might not fully update until a refresh event:
    - rebuild the building, or
    - add/remove an extension, or a building upgrade (when available)
- Avoid “mutate everything at runtime” unless the full dependency chain is understood (it’s easy to break invariants or stomp other mods).


### Derived runtime state (instance-side, cached)
Some simulation values are stored on *instance entities* as runtime components.  
These values may be **computed/cached** from prefab data and saved into the savegame, so they might **not update immediately** when you change prefab values.

**Example: Workers**
- Prefab authoring (baseline): `Game.Prefabs.Workplace` (`m_Workplaces`, `m_MinimumWorkersLimit`)
- Prefab ECS data (what mods often write): `Game.Prefabs.WorkplaceData` (`m_MaxWorkers`, `m_MinimumWorkersLimit`)
- Instance runtime state (what simulation may use): `Game.Companies.WorkProvider` (`m_MaxWorkers`)
  
**Why do new buildings get the update but not the existing ones?**
If a runtime worker limit is serialized per building, scaling workers on the prefab often requires 
a refresh trigger (new building change) to recompute instance-side values.
- This means a slider to adjust workers with `WorkplaceData` will change new buildings but not existing ones.
- Can't update `Companies.WorkProvider` directly because it's calculated in a **burst job**.

#### 3 ways to get buildings to update instantly for special values
1. Harmony patch: makes the mod more brittle on game patch days, but may be the only way.
2. Rigorous research of the decompiled code to find the exact method used and copy it. Then the burst job for `Companies.WorkProvider` will read your new value.
    - one-shot method on slider movement helps avoid fighting the burst job and will update **existing** buildings.
    - also still need `WorkplaceData` change to take care of all **new** buildings.
    - iLSpy or DnSpyEx are apps to decompile and study code.
3. Ask the player to do it: avoid all effort of 1 or 2 by asking the player to simply make new buildings to see the slider changes for "special" values.

>Changes persist on saves when the mod is removed but is harmless if steps are done correctly. Player can delete old buildings and all new buildings will be vanilla values.

