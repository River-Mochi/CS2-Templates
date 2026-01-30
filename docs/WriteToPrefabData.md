## Step 2 — Write scaled values onto ECS *Data on prefab entities
This is where the mod actually changes the prefab entity (entities with PrefabData) by writing to *Data components.

### Option 2

```csharp
// Same as Option 1, just using Unity.Entities ECS RefRW<T> query style.
// Tradeoff: denser, harder to trace errors. For beginners, Option 1 is better

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

- Denser ECS query code can be harder for beginners to debug, because the compiler error often points at a symptom (missing type/namespace) rather than the real cause (a type mismatch or missing generic, etc.).
