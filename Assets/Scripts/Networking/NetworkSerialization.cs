using System;
using UnityEngine;

/// <summary>
/// Utility class for serializing and deserializing network data.
/// Provides methods to write/read primitives, vectors, and quaternions to/from byte arrays.
/// </summary>
public static class NetworkSerialization
{
    // Write primitives
    public static void WriteUInt(byte[] buffer, ref int offset, uint value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 4);
        offset += 4;
    }
    
    public static void WriteULong(byte[] buffer, ref int offset, ulong value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 8);
        offset += 8;
    }
    
    public static void WriteFloat(byte[] buffer, ref int offset, float value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 4);
        offset += 4;
    }
    
    public static void WriteVector3(byte[] buffer, ref int offset, Vector3 value)
    {
        WriteFloat(buffer, ref offset, value.x);
        WriteFloat(buffer, ref offset, value.y);
        WriteFloat(buffer, ref offset, value.z);
    }
    
    public static void WriteQuaternion(byte[] buffer, ref int offset, Quaternion value)
    {
        // Compress to 3 floats (smallest 3 components)
        // Or full 4 floats for simplicity
        WriteFloat(buffer, ref offset, value.x);
        WriteFloat(buffer, ref offset, value.y);
        WriteFloat(buffer, ref offset, value.z);
        WriteFloat(buffer, ref offset, value.w);
    }
    
    // Read primitives
    public static uint ReadUInt(byte[] buffer, ref int offset)
    {
        uint value = BitConverter.ToUInt32(buffer, offset);
        offset += 4;
        return value;
    }
    
    public static ulong ReadULong(byte[] buffer, ref int offset)
    {
        ulong value = BitConverter.ToUInt64(buffer, offset);
        offset += 8;
        return value;
    }
    
    public static float ReadFloat(byte[] buffer, ref int offset)
    {
        float value = BitConverter.ToSingle(buffer, offset);
        offset += 4;
        return value;
    }
    
    public static Vector3 ReadVector3(byte[] buffer, ref int offset)
    {
        return new Vector3(
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset)
        );
    }
    
    public static Quaternion ReadQuaternion(byte[] buffer, ref int offset)
    {
        return new Quaternion(
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset)
        );
    }
}