# PrefabSystem Source of Truth (Cities: Skylines II Modding)

This document explains **what “source of truth” means for prefab values** in Cities: Skylines II (CS2), and how to avoid the most common baseline mistakes when changing capacities, workers, rates, fleets, storage, etc.
It’s written for C# / ECS modders using the **Colossal Order (CO) API**.

---

## TL;DR (the rule you must follow)

> If you need **true vanilla baseline values**, read them from  
> `PrefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase)`  
> then read **authoring components** from `PrefabBase` (e.g., `Workplace`, `DeathcareFacility`).  
> **Do not** treat `PrefabRef -> ECS prefab *Data` as baseline.

---

## Why this exists

CS2 has multiple layers of data that look similar but mean different things:

- **Authoring prefab values** (asset baseline)
- **ECS prefab entity components** (mutable simulation-facing data)
- **Runtime instance components** (computed / affected by upgrades, budgets, simulation)

A lot of mod bugs come from accidentally using the wrong layer as “baseline”, causing:
- compounding scaling (`scaled = scaled * scalar` repeatedly),
- incorrect restores,
- incompatibilities with other mods,
- “vanilla baseline” drifting over time.

---

## Glossary

- **PrefabBase**: Managed object representing the prefab asset and its authoring components.
- **Prefab entity**: ECS entity marked with `PrefabData` that holds `*Data` components used by simulation.
- **Instance entity**: A placed building / citizen / vehicle entity in the live simulation.
- **PrefabRef**: ECS component on instances linking to a prefab entity (`PrefabRef.m_Prefab`).

---

## The 3 layers (the mental model)

### 1) Authoring prefab asset (true baseline)
**Source-of-truth for “vanilla defaults”.**

- Access via: `PrefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase)`
- Read authoring components from `PrefabBase`, e.g.:
  - `Game.Prefabs.Workplace`
    - `m_Workplaces`
    - `m_MinimumWorkersLimit`
  - `Game.Prefabs.DeathcareFacility`
    - `m_ProcessingRate`
    - `m_HearseCapacity`
    - `m_StorageCapacity`
    - `m_LongTermStorage`

✅ Use this for: **baseline scaling**, “restore vanilla”, and any logic that must not drift.

---

### 2) ECS prefab entity `*Data` (simulation-facing, mutable)
This is what the simulation uses at runtime for prefab behavior.

Examples:
- `Game.Prefabs.WorkplaceData`
  - `m_MaxWorkers`
  - `m_MinimumWorkersLimit`
- `Game.Prefabs.DeathcareFacilityData`
  - `m_ProcessingRate`
  - `m_HearseCapacity`
  - `m_StorageCapacity`
  - `m_LongTermStorage`

✅ Use this for: **writing your changes**, because the simulation reads these.

⚠️ Not safe as baseline because it may already be modified (by your mod or others), and may represent processed/combined values.

---

### 3) Runtime instance components (computed / situation-dependent)
This is per-placed-building / per-vehicle / per-citizen data, influenced by simulation.

Examples:
- `Game.Companies.WorkProvider`
  - `m_MaxWorkers` (runtime-computed; often what UI/status reflects)
- `Game.Buildings.DeathcareFacility`
  - `m_LongTermStoredCount` (instance state)
- Efficiency/budget effects, etc.

✅ Use this for: **status scanning**, reporting what’s actually happening.

❌ Never use this as “vanilla baseline”.

---

## Quick reference tables

### Baseline (Authoring) vs ECS Data vs Runtime

| Layer | Where | Read with | Examples | Use for |
|------:|------|-----------|----------|--------|
| **Authoring baseline** | `PrefabBase` (asset) | `PrefabSystem.TryGetPrefab(...)` then `prefabBase.TryGet(...)` | `Workplace.m_Workplaces`, `DeathcareFacility.m_ProcessingRate` | **True vanilla defaults**, baseline scaling, restore logic |
| **ECS prefab data** | Prefab entity (`PrefabData`) | `SystemAPI.Query<RefRW<...Data>>()` | `WorkplaceData.m_MaxWorkers`, `DeathcareFacilityData.m_HearseCapacity` | **Write** values that simulation uses |
| **Runtime instance** | Placed entities | component lookups / queries on instances | `WorkProvider.m_MaxWorkers`, `Building` state | Status/reporting, debugging actual live values |

---

## PrefabRef: what it is (and the trap)

`PrefabRef` is an ECS component usually like:

- `Entity m_Prefab;`

On an **instance** entity, `PrefabRef.m_Prefab` points to the **ECS prefab entity**, not to `PrefabBase`.

So this chain is the trap:

> Instance → `PrefabRef.m_Prefab` → read `*Data` → call it “vanilla baseline”

That’s wrong because `*Data` can already be modified, and may not match authoring defaults.

---

## The correct pattern (baseline read + ECS write)

### Step A: Read baseline from PrefabBase authoring

```csharp
using Game.Prefabs;     // PrefabSystem, PrefabBase, Workplace, DeathcareFacility
using Unity.Entities;

private PrefabSystem m_PrefabSystem = null!;

protected override void OnCreate()
{
    base.OnCreate();
    m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
}

private bool TryGetWorkplaceAuthoring(Entity prefabEntity, out Workplace workplace)
{
    workplace = null!;
    if (!m_PrefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
        return false;

    // Prefer TryGetExactly to avoid ambiguous matches.
    return prefabBase.TryGetExactly(out workplace);
}

private bool TryGetDeathcareAuthoring(Entity prefabEntity, out DeathcareFacility dc)
{
    dc = null!;
    if (!m_PrefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase))
        return false;

    return prefabBase.TryGet(out dc);
}
