using System;
using UnityEngine;

[Serializable]
public struct SVector3Int
{
    public int x;
    public int y;
    public int z;

    public SVector3Int(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public override string ToString()
        => $"[{x}, {y}, {z}]";

    public static implicit operator Vector3Int(SVector3Int s)
        => new Vector3Int(s.x, s.y, s.z);

    public static implicit operator SVector3Int(Vector3Int v)
        => new SVector3Int(v.x, v.y, v.z);

    public static implicit operator Vector3(SVector3Int s)
        => new Vector3Int(s.x, s.y, s.z);

    public static implicit operator SVector3Int(Vector3 v)
        => new SVector3Int((int)v.x, (int)v.y, (int)v.z);
}