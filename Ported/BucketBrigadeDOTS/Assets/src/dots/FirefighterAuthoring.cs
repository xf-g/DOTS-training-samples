﻿using Unity.Entities;
using Unity.Mathematics;

public struct Firefighter : IComponentData
{
}

public struct Target : IComponentData
{
    public float2 Value;
}

public struct FirefighterPositionInLine : IComponentData
{
    public float Value;
}