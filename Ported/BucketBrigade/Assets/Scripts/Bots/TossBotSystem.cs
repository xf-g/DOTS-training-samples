﻿using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class TossBotSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = Time.DeltaTime;
        var botSpeed = EntityManager.GetComponentData<BotConfig>(GetSingletonEntity<BotConfig>()).botSpeed;
        // Go to default position if no pickup/carrying happening
        Entities
            .WithName("Toss_GoToTarget")
            .WithAll<BotTypeToss>()
            .WithNone<CarriedBucket>()
            .WithNone<TargetBucket>()
            .ForEach((Entity e, ref Translation translation, in BrigadeGroup group) =>
                {
                    Brigade brigade = GetComponent<Brigade>(group.Value);
                    if (!UtilityFunctions.FlatOverlapCheck(brigade.fireTarget, translation.Value))
                    {
                        translation.Value = UtilityFunctions.BotHeightCorrect(translation.Value + 
                                                                              math.normalize(brigade.fireTarget - translation.Value) * botSpeed * deltaTime);
                    }
                }
            ).Schedule();
        
        // Go to target bucket
        Entities
            .WithName("Toss_BucketPickup")
            .WithAll<BotTypeToss>()
            .WithAll<TargetBucket>()
            .WithNone<CarriedBucket>()
            .WithStructuralChanges()
            .ForEach((Entity e, ref Translation translation, in TargetBucket targetBucket) =>
                {
                    var bucketTranslation = EntityManager.GetComponentData<Translation>(targetBucket.Value);
                    translation.Value = UtilityFunctions.BotHeightCorrect(translation.Value +
                                                                          math.normalize(bucketTranslation.Value - translation.Value) * botSpeed * deltaTime);
                    if (UtilityFunctions.FlatOverlapCheck(translation.Value, bucketTranslation.Value))
                    {
                        EntityManager.AddComponentData(e, new CarriedBucket()
                        {
                            Value = targetBucket.Value
                        });
                        EntityManager.RemoveComponent<TargetBucket>(e);
                    }
                }
            ).Run();
        
        // Carry & Deliver bucket
        Entities
            .WithName("Toss_BucketCarryAndToss")
            .WithAll<BotTypeToss>()
            .WithAll<CarriedBucket>()
            .WithStructuralChanges()
            .ForEach((Entity e, ref Translation translation, in CarriedBucket carriedBucket, in BrigadeGroup brigadeGroup, in NextBot nextBot) =>
                {
                    Brigade brigade = EntityManager.GetComponentData<Brigade>(brigadeGroup.Value);
                    var fireTarget = brigade.fireTarget;
                    translation.Value = UtilityFunctions.BotHeightCorrect(translation.Value + math.normalize(fireTarget - translation.Value) * botSpeed * deltaTime);
                    if (UtilityFunctions.FlatOverlapCheck(translation.Value, fireTarget))
                    {
                        // Next bot targets bucket
                        EntityManager.AddComponentData(nextBot.Value, new TargetBucket
                        {
                            Value = carriedBucket.Value
                        });
                        
                        // Next bot owns bucket 
                        EntityManager.AddComponentData(carriedBucket.Value, new Owner
                        {
                            Value = nextBot.Value
                        });
                        
                        // Empty bucket
                        EntityManager.SetComponentData(carriedBucket.Value, new Water()
                        {
                            capacity = 1,
                            volume = 0
                        });
                        
                        EntityManager.RemoveComponent<CarriedBucket>(e);
                        
                        // Splash for the FireSplashSystem; only if we had an actual valid target
                        if (brigade.fireEntity != Entity.Null)
                        {
                            EntityManager.AddComponentData(brigade.fireEntity, new Splash());
                        }
                    }
                }
            ).Run();
    }
}
