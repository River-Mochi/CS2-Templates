## Step 2 — Write scaled values onto ECS *Data on prefab entities
This is where the mod actually changes the prefab entity (entities with PrefabData) by writing to *Data components.

### Option 2 Compact ECS style

```csharp
// Same as Option 1, just using Unity.Entities ECS RefRW<T> query style.
// Tradeoff: denser, harder to trace errors.

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
Differences vs Option 1:
- Option 1 is probably easier for beginners.
- No ToEntityArray / NativeArray lifetime to manage.
- Writes through RefRW<T> (so there’s no explicit “get struct copy → modify → SetComponentData” step).
