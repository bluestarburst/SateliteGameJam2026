using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using UnityEngine;

/// <summary>
/// Manages voice playback for a specific remote player.
/// Each remote speaker gets their own VoiceRemotePlayer with isolated audio buffer.
/// Supports positional 3D audio when attached to player avatar.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VoiceRemotePlayer : MonoBehaviour
{
    [SerializeField] private float playbackLatencySeconds = 0.1f;
    
    private AudioSource audioSource;
    private SteamId senderSteamId;
    
    private float[] audioclipBuffer;
    private int audioclipBufferSize;
    private int audioPlayerPosition;
    private int playbackBuffer;
    
    private Queue<PendingAudioBuffer> pendingBuffers = new Queue<PendingAudioBuffer>();
    private MemoryStream uncompressedStream;
    
    /// <summary>
    /// Initializes this voice player for a specific remote sender.
    /// </summary>
    public void Initialize(SteamId senderId)
    {
        senderSteamId = senderId;

        audioSource = GetComponent<AudioSource>();
        uncompressedStream = new MemoryStream();

        int optimalRate = (int)SteamUser.OptimalSampleRate;
        audioclipBufferSize = optimalRate * 5; // 5 seconds buffer
        audioclipBuffer = new float[audioclipBufferSize];

        audioSource.clip = AudioClip.Create($"Voice_{senderId}", optimalRate * 2, 1, optimalRate, true, OnAudioRead, null);
        audioSource.loop = true;
        audioSource.spatialBlend = 1.0f; // 3D audio
        audioSource.Play();
    }
    
    /// <summary>
    /// Receives and queues compressed voice data from the remote player.
    /// </summary>
    public void ReceiveVoiceData(byte[] compressed, int length)
    {
        try
        {
            var compressedStream = new MemoryStream(compressed, 0, length);
            uncompressedStream.Position = 0;
            
            int uncompressedWritten = SteamUser.DecompressVoice(compressedStream, length, uncompressedStream);
            
            if (uncompressedWritten > 0)
            {
                byte[] outputBuffer = new byte[uncompressedWritten];
                uncompressedStream.Position = 0;
                uncompressedStream.Read(outputBuffer, 0, uncompressedWritten);
                
                pendingBuffers.Enqueue(new PendingAudioBuffer
                {
                    ReadTime = Time.time + playbackLatencySeconds,
                    Buffer = outputBuffer,
                    Size = uncompressedWritten
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to process voice from {senderSteamId}: {e}");
        }
    }
    
    private void Update()
    {
        // Pending buffer processing
        while (pendingBuffers.Count > 0 && pendingBuffers.Peek().ReadTime <= Time.time)
        {
            var pending = pendingBuffers.Dequeue();
            WriteToPlaybackBuffer(pending.Buffer, pending.Size);
        }
    }
    
    /// <summary>
    /// Writes uncompressed audio samples to the playback ring buffer.
    /// </summary>
   private void WriteToPlaybackBuffer(byte[] uncompressed, int size)
    {
        for (int i = 0; i < size; i += 2)
        {
            if (i + 1 >= size) break;
            
            short sample = (short)(uncompressed[i] | (uncompressed[i + 1] << 8));
            audioclipBuffer[audioPlayerPosition] = sample / 32767.0f;
            
            audioPlayerPosition = (audioPlayerPosition + 1) % audioclipBufferSize;
            playbackBuffer++;
        }
    }
    
    /// <summary>
    /// Unity callback for audio playback - reads from ring buffer.
    /// </summary>
    private void OnAudioRead(float[] data)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = 0;
            
            if (playbackBuffer > 0)
            {
                int readPosition = (audioPlayerPosition - playbackBuffer + audioclipBufferSize) % audioclipBufferSize;
                data[i] = audioclipBuffer[readPosition];
                playbackBuffer--;
            }
        }
    }
    
    private void OnDestroy()
    {
        Debug.Log($"Cleaning up VoiceRemotePlayer for {senderSteamId}");
        uncompressedStream?.Dispose();
    }
    
    private class PendingAudioBuffer
    {
        public float ReadTime { get; set; }
        public byte[] Buffer { get; set; }
        public int Size { get; set; }
    }
}
