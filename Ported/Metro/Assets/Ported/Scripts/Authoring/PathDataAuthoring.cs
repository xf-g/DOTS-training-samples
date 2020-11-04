﻿using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public class PathDataAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] private Color pathColour;
    [SerializeField] private RailMarkerType[] railMarkerTypes;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddSharedComponentData(entity, new ID {Value = transform.GetSiblingIndex()});

        var builder = new BlobBuilder(Allocator.Temp);
        ref var pathData = ref builder.ConstructRoot<PathData>();

        var halfMarkerCount = transform.childCount;
        var totalMarkerCount = halfMarkerCount * 2;

        // Initialize Arrays
        float3[] positions = new float3[totalMarkerCount];
        var markerTypesB = builder.Allocate(ref pathData.MarkerTypes, totalMarkerCount);
        float3[] handlesIn = new float3[totalMarkerCount];
        float3[] handlesOut = new float3[totalMarkerCount];
        float[] distances = new float[totalMarkerCount];

        // Outbound Positions
        for (var c = 0; c < transform.childCount; c++)
            positions[c] = transform.GetChild(c).transform.position;

        RebuildHandle(positions, ref handlesIn, ref handlesOut, halfMarkerCount);

        float halfDistance = BezierHelpers.MeasurePath(positions, handlesIn, handlesOut, halfMarkerCount, out var tempDistances);
        for (var p = 0; p < halfMarkerCount; p++)
        {
            distances[p] = tempDistances[p];
        }

        for (int p = halfMarkerCount - 1; p >= 0; p--)
        {
            var position = positions[p];
            var distance = distances[p];
            var perpPosition = BezierHelpers.GetPointPerpendicularOffset(position, distance, positions, handlesIn,
                handlesOut, distances, halfDistance, Globals.BEZIER_PLATFORM_OFFSET);

            positions[halfMarkerCount + p - halfMarkerCount] = perpPosition;
        }

        RebuildHandle(positions, ref handlesIn, ref handlesOut, totalMarkerCount);

        // Marker types
        for (var m = 0; m < halfMarkerCount; m++)
        {
            // Outbound
            markerTypesB[m] = (int) railMarkerTypes[m];

            // Return
            markerTypesB[halfMarkerCount + m] = (int)railMarkerTypes[halfMarkerCount - 1 - m];
        }

        // Total path distance
        pathData.TotalDistance = BezierHelpers.MeasurePath(positions, handlesIn, handlesOut, totalMarkerCount, out tempDistances);

        // Marker distances
        for (var d = 0; d < totalMarkerCount; d++)
            distances[d] = tempDistances[d];

        var positionsB = builder.Allocate(ref pathData.Positions, totalMarkerCount);
        var handlesInB = builder.Allocate(ref pathData.HandlesIn, totalMarkerCount);
        var handlesOutB = builder.Allocate(ref pathData.HandlesOut, totalMarkerCount);
        var distancesB = builder.Allocate(ref pathData.Distances, totalMarkerCount);

        for (var p = 0; p < halfMarkerCount; p++)
        {
            positionsB[p] = positions[p];
            handlesInB[p] = handlesIn[p];
            handlesOutB[p] = handlesOut[p];
            distancesB[p] = distances[p];
        }

        // Path colour
        pathData.Colour = new float3(pathColour.r, pathColour.g, pathColour.b);

        dstManager.AddComponentData(entity, new PathRef
        {
            Data = builder.CreateBlobAssetReference<PathData>(Allocator.Persistent)
        });
    }

    private void RebuildHandle(float3[] positions, ref float3[] handlesIn, ref float3[] handlesOut, int size)
    {
        // Outbound Handles
        for (var p = 1; p < size - 1; p++)
        {
            var currentPosition = positions[p];
            var previousPosition = positions[p - 1];
            var nextPosition = positions[p + 1];

            var offsetPosition = nextPosition - previousPosition;
            var handleIn = BezierHelpers.GetHandleIn(currentPosition, offsetPosition);
            var handleOut = BezierHelpers.GetHandleOut(currentPosition, offsetPosition);

            handlesIn[p] = handleIn;
            handlesOut[p] = handleOut;
        }
        // Point 0
        {
            var currentPosition = positions[0];
            var ptA = positions[1];
            var ptB = positions[0];

            var offsetPosition = ptA - ptB;
            var handleIn = BezierHelpers.GetHandleIn(currentPosition, offsetPosition);
            var handleOut = BezierHelpers.GetHandleOut(currentPosition, offsetPosition);

            handlesIn[0] = handleIn;
            handlesOut[0] = handleOut;
        }
        // Last Point
        {
            int last = size - 1;
            var currentPosition = positions[last];
            var ptA = positions[last];
            var ptB = positions[last - 1];

            var offsetPosition = ptA - ptB;
            var handleIn = BezierHelpers.GetHandleIn(currentPosition, offsetPosition);
            var handleOut = BezierHelpers.GetHandleOut(currentPosition, offsetPosition);

            handlesIn[last] = handleIn;
            handlesOut[last] = handleOut;
        }
    }

    private void OnDrawGizmos()
    {
        Debug.Log("Gizmos");

        Gizmos.color = pathColour;
        for (var c = 0; c < transform.childCount; c++)
        {
            var currentPosition = transform.GetChild(c).position;
            Gizmos.DrawSphere(currentPosition, 1f);

            if (c == transform.childCount - 1)
                break;

            Gizmos.DrawLine(currentPosition, transform.GetChild(c + 1).position);
        }
    }
}