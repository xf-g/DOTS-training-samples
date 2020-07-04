﻿using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace HighwayRacer
{
    [UpdateAfter(typeof(SegmentizeSys))]
    public class BucketizeSys : SystemBase
    {
        private int nSegments;

        public BucketizedCars BucketizedCars;

        protected override void OnCreate()
        {
            base.OnCreate();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            BucketizedCars.Dispose();
        }

        private int nCars;

        protected override void OnUpdate()
        {
            var nSegments = RoadSys.nSegments;
            if (nSegments != this.nSegments)
            {
                if (this.BucketizedCars.IsCreated)
                {
                    this.BucketizedCars.Dispose();
                }
                
                this.BucketizedCars = new BucketizedCars(nSegments);
                this.nSegments = nSegments;
            }

            if (RoadSys.numCars != nCars)
            {
                nCars = RoadSys.numCars;
                Debug.Log(" num cars = "+  nCars);
            }
            
            this.BucketizedCars.Clear();

            var SegmentizedCars = this.BucketizedCars;

            Entities.WithNone<MergingLeft, MergingRight>().ForEach(
                (Entity ent, in TrackPos trackPos, in Speed speed, in Lane lane, in TrackSegment trackSegment) =>
                {
                    SegmentizedCars.AddCar(lane.Val, trackSegment, trackPos, speed, nSegments); // todo: can we ensure this gets inlined?
                }).Run();

            Entities.WithAll<MergingLeft>().ForEach((Entity ent, in TrackPos trackPos, in Speed speed, in Lane lane, in TrackSegment trackSegment) =>
            {
                SegmentizedCars.AddCar(lane.Val, trackSegment, trackPos, speed, nSegments);
                SegmentizedCars.AddCar(lane.Val - 1, trackSegment, trackPos, speed, nSegments); // lane to right (the old lane)
            }).Run();

            Entities.WithAll<MergingRight>().ForEach((Entity ent, in TrackPos trackPos, in Speed speed, in Lane lane, in TrackSegment trackSegment) =>
            {
                SegmentizedCars.AddCar(lane.Val, trackSegment, trackPos, speed, nSegments);
                SegmentizedCars.AddCar(lane.Val + 1, trackSegment, trackPos, speed, nSegments); // lane to left (the old lane)
            }).Run();
        }
    }
}
