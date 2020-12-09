﻿using System.Xml;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(BarSpawningSystem))]
public class BarMovementSytem : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<TornadoSettings>();
        RequireSingletonForUpdate<Tornado>();
    }

    protected override void OnUpdate()
    {
        var settings = this.GetSingleton<TornadoSettings>();
        var tornadoTranslation = EntityManager.GetComponentData<Translation>(GetSingletonEntity<Tornado>());

        Random random = new Random(1234);

        //float tornadoFader = math.clamp(tornadoFader + Time.DeltaTime / 10f, 0f, 1f);
        float tornadoFader = 0.5f;
        float invDamping = 1f - settings.Damping;
        float deltaTime = Time.DeltaTime;

        Entities.ForEach((Node node, ref Translation translation) =>
        {
            if (node.anchor)
            {
                float3 start = translation.Value;

                node.oldPosition.y += .01f;

                float2 tornadoForce = new float2(tornadoTranslation.Value.x + (math.sin(translation.Value.y / 5f + deltaTime / 4f) * 3f) - translation.Value.x,
                    tornadoTranslation.Value.z - translation.Value.z);

                float tornadoDist = math.length(tornadoForce);

                tornadoForce = math.normalize(tornadoForce);

                if (tornadoDist < settings.TornadoMaxForceDistance)
                {
                    float force = (1f - tornadoDist / settings.TornadoMaxForceDistance);
                    float yFader = math.clamp(1f - translation.Value.y / settings.TornadoHeight, 0f, 1f);
                    force *= tornadoFader * settings.TornadoForce * random.NextFloat(-0.3f, 1.3f);
                    float forceY = settings.TornadoUpForce;
                    node.oldPosition.y -= forceY * force;
                    float forceX = -tornadoForce.y + tornadoForce.x * settings.TornadoInwardForce * yFader;
                    float forceZ = tornadoForce.x + tornadoForce.y * settings.TornadoInwardForce * yFader;
                    node.oldPosition.x -= forceX * force;
                    node.oldPosition.z -= forceZ * force;
                }

                translation.Value += (translation.Value - node.oldPosition) * invDamping;
                node.oldPosition = start;

                if (translation.Value.y < 0f)
                {
                    translation.Value.y = 0f;
                    node.oldPosition.y = -translation.Value.y;
                    node.oldPosition.xz += (translation.Value.xz - node.oldPosition.xz) * settings.Friction;
                }

            }
        }).Run();

        var ecb = new EntityCommandBuffer( Unity.Collections.Allocator.Temp );

        Entities.ForEach((in DynamicBuffer<Constraint> constraints) =>
        {
            for (int i = 0; i < constraints.Length; i++)
            {
                Constraint constraint = constraints[i];

                Node point1 = GetComponent<Node>(constraint.pointA);
                float3 point1Pos = GetComponent<Translation>(constraint.pointA).Value;
                Node point2 = GetComponent<Node>(constraint.pointB);
                float3 point2Pos = GetComponent<Translation>(constraint.pointB).Value;

                float3 d = point2Pos - point1Pos;
                float dist = math.length(d);
                float extraDist = dist - constraint.distance;

                float3 push = (d / dist * extraDist) * .5f;

                if (point1.anchor == false && point2.anchor == false)
                {
                    point1Pos += push;
                    point2Pos -= push;
                }
                else if (point1.anchor)
                {
                    point2Pos -= push * 2f;
                }
                else if (point2.anchor)
                {
                    point1Pos += push * 2f;
                }

                if (math.abs(extraDist) > settings.BreakResistance)
                {
                    if (point2.neighborCount > 1)
                    {
                        point2.neighborCount--;

                        var newPoint = point2;
                        newPoint.neighborCount = 1;

                        Entity newPointEntity = ecb.CreateEntity();
                        ecb.AddComponent(newPointEntity, newPoint);
                        ecb.AddComponent(newPointEntity, new Translation() { Value = point2Pos });

                        constraint.pointB = newPointEntity;
                    }
                    else if (point1.neighborCount > 1)
                    {
                        point1.neighborCount--;

                        var newPoint = point1;
                        newPoint.neighborCount = 1;

                        Entity newPointEntity = ecb.CreateEntity();
                        ecb.AddComponent(newPointEntity, newPoint);
                        ecb.AddComponent(newPointEntity, new Translation() { Value = point1Pos });

                        constraint.pointA = newPointEntity;
                    }
                }
            }
        }).Run();

        ecb.Playback(EntityManager);
    }
}