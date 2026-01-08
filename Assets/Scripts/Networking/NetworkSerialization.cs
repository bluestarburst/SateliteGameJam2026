using System;
using UnityEngine;

/// <summary>
/// Utility class for serializing and deserializing network data.
/// Provides methods to write/read primitives, vectors, and quaternions to/from byte arrays.
/// </summary>
public static class NetworkSerialization
{
    private static void EnsureWriteSpace(byte[] buffer, int offset, int size, string label)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset + size > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), $"Not enough space to write {label} (need {size} bytes at offset {offset}, buffer length {buffer.Length})");
    }

    private static void EnsureReadSpace(byte[] buffer, int offset, int size, string label)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset + size > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), $"Not enough data to read {label} (need {size} bytes at offset {offset}, buffer length {buffer.Length})");
    }

    // Write primitives
    public static void WriteUInt(byte[] buffer, ref int offset, uint value)
    {
        EnsureWriteSpace(buffer, offset, 4, "uint");
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 4);
        offset += 4;
    }
    
    public static void WriteULong(byte[] buffer, ref int offset, ulong value)
    {
        EnsureWriteSpace(buffer, offset, 8, "ulong");
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 8);
        offset += 8;
    }
    
    public static void WriteFloat(byte[] buffer, ref int offset, float value)
    {
        EnsureWriteSpace(buffer, offset, 4, "float");
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
        value = Quaternion.Normalize(value);
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
        EnsureReadSpace(buffer, offset, 4, "uint");
        uint value = BitConverter.ToUInt32(buffer, offset);
        offset += 4;
        return value;
    }
    
    public static ulong ReadULong(byte[] buffer, ref int offset)
    {
        EnsureReadSpace(buffer, offset, 8, "ulong");
        ulong value = BitConverter.ToUInt64(buffer, offset);
        offset += 8;
        return value;
    }
    
    public static float ReadFloat(byte[] buffer, ref int offset)
    {
        EnsureReadSpace(buffer, offset, 4, "float");
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
        Quaternion q = new Quaternion(
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset)
        );

        float sqrMag = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (sqrMag < 1e-6f)
        {
            return Quaternion.identity;
        }

        return Quaternion.Normalize(q);
    }
}