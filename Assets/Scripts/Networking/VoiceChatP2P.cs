using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using UnityEngine;

/// <summary>
/// Voice chat over Steam P2P using Facepunch Steamworks.
/// Records voice, compresses it, sends via P2P channel, then decompresses and plays on remote clients.
/// Attach to a GameObject in your gameplay scene alongside P2PGameObject.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VoiceChatP2P : MonoBehaviour
{
    [Header("Voice Settings")]
    [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;
    [SerializeField] private bool alwaysRecord = false; // For testing; use push-to-talk in production
    [SerializeField] private int voiceChannel = 2; // Separate P2P channel for voice data

    [Header("Playback")]
    [SerializeField] private float playbackLatencySeconds = 0.1f; // Buffer before playback starts

    private AudioSource audioSource;
    private MemoryStream voiceStream;
    private MemoryStream uncompressedStream;
    private MemoryStream compressedStream;

    // Audio playback buffers
    private float[] audioclipBuffer;
    private int audioclipBufferSize;
    private int audioPlayerPosition;
    private int playbackBuffer;

    private Queue<PendingAudioBuffer> pendingBuffers = new();

    private SteamId OpponentId => SteamManager.Instance != null ? SteamManager.Instance.OpponentSteamId : default;
    private bool isLocalPlayerActive => SteamManager.Instance != null && !SteamManager.Instance.LobbyPartnerDisconnected;

    private class PendingAudioBuffer
    {
        public float ReadTime { get; set; }
        public byte[] Buffer { get; set; }
        public int Size { get; set; }
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("VoiceChatP2P requires an AudioSource component.");
            enabled = false;
            return;
        }

        voiceStream = new MemoryStream();
        uncompressedStream = new MemoryStream();
        compressedStream = new MemoryStream();

        // Setup playback
        int optimalRate = (int)SteamUser.OptimalSampleRate;
        audioclipBufferSize = optimalRate * 5; // 5 seconds buffer
        audioclipBuffer = new float[audioclipBufferSize];

        audioSource.clip = AudioClip.Create("VoiceData", optimalRate * 2, 1, optimalRate, true, OnAudioRead, null);
        audioSource.loop = true;
        audioSource.Play();
    }

    private void Update()
    {
        // Recording: capture voice and send via P2P
        if (isLocalPlayerActive)
        {
            bool shouldRecord = alwaysRecord || Input.GetKey(pushToTalkKey);
            SteamUser.VoiceRecord = shouldRecord;

            if (SteamUser.HasVoiceData)
            {
                voiceStream.Position = 0;
                int compressedRead = SteamUser.ReadVoiceData(voiceStream);
                if (compressedRead > 0)
                {
                    voiceStream.Position = 0;
                    byte[] compressedData = new byte[compressedRead];
                    voiceStream.Read(compressedData, 0, compressedRead);

                    // Send compressed voice via P2P on dedicated voice channel
                    SendVoicePacket(compressedData, compressedRead);
                }
            }
        }

        // Receiving: poll for voice packets on voice channel
        while (SteamNetworking.IsP2PPacketAvailable(voiceChannel))
        {
            var packet = SteamNetworking.ReadP2PPacket(voiceChannel);
            if (packet.HasValue)
            {
                HandleIncomingVoicePacket(packet.Value.Data, packet.Value.Data.Length);
            }
        }

        // Playback: process pending buffers when it's time
        while (pendingBuffers.Count > 0 && pendingBuffers.Peek().ReadTime <= Time.time)
        {
            var pending = pendingBuffers.Dequeue();
            WriteToPlaybackBuffer(pending.Buffer, pending.Size);
        }
    }

    private void SendVoicePacket(byte[] compressed, int length)
    {
        bool sent = SteamNetworking.SendP2PPacket(OpponentId, compressed, length, voiceChannel, P2PSend.UnreliableNoDelay);
        if (!sent)
        {
            Debug.Log("Failed to send voice packet");
        }
    }

    private void HandleIncomingVoicePacket(byte[] compressed, int length)
    {
        try
        {
            compressedStream.Position = 0;
            compressedStream.Write(compressed, 0, length);
            compressedStream.Position = 0;

            uncompressedStream.Position = 0;
            int uncompressedWritten = SteamUser.DecompressVoice(compressedStream, length, uncompressedStream);

            if (uncompressedWritten > 0)
            {
                // Copy to avoid shared buffer issues
                byte[] outputBuffer = new byte[uncompressedWritten];
                uncompressedStream.Position = 0;
                uncompressedStream.Read(outputBuffer, 0, uncompressedWritten);

                // Queue for playback with latency buffer
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
            Debug.LogError($"Failed to process incoming voice packet: {e}");
        }
    }

    private void WriteToPlaybackBuffer(byte[] uncompressed, int size)
    {
        // Convert 16-bit PCM bytes to float samples
        for (int i = 0; i < size; i += 2)
        {
            if (i + 1 >= size) break;

            short sample = (short)(uncompressed[i] | (uncompressed[i + 1] << 8));
            audioclipBuffer[audioPlayerPosition] = sample / 32767.0f;

            audioPlayerPosition = (audioPlayerPosition + 1) % audioclipBufferSize;
            playbackBuffer++;
        }
    }

    private void OnAudioRead(float[] data)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = 0; // Start with silence

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
        SteamUser.VoiceRecord = false;
        voiceStream?.Dispose();
        uncompressedStream?.Dispose();
        compressedStream?.Dispose();
    }
}
