using System;
using Game.Messages;
using Google.Protobuf;
using UnityEngine;

namespace Cabo.Client.Network
{
    /// <summary>
    /// Handles [4-byte big-endian length][protobuf bytes] framing.
    /// Accumulates received bytes, extracts complete frames, deserializes ServerMessage.
    /// Also provides static Encode to serialize ClientMessage to bytes for sending.
    /// </summary>
    public sealed class MessageCodec
    {
        private byte[] receiveBuffer = Array.Empty<byte>();

        /// <summary>
        /// Feed received raw bytes into the codec.
        /// Calls onMessage for each complete ServerMessage decoded.
        /// Must be called on the same thread consistently (typically main thread via dispatcher).
        /// </summary>
        public void FeedBytes(byte[] data, Action<ServerMessage> onMessage)
        {
            // Append new data to buffer
            var newBuffer = new byte[receiveBuffer.Length + data.Length];
            if (receiveBuffer.Length > 0)
                Array.Copy(receiveBuffer, 0, newBuffer, 0, receiveBuffer.Length);
            Array.Copy(data, 0, newBuffer, receiveBuffer.Length, data.Length);
            receiveBuffer = newBuffer;

            // Try to extract complete frames
            while (receiveBuffer.Length >= 4)
            {
                int payloadLength = ReadBigEndianInt32(receiveBuffer, 0);
                if (payloadLength < 0)
                {
                    Debug.LogError($"[MessageCodec] Invalid payload length: {payloadLength}, resetting buffer");
                    receiveBuffer = Array.Empty<byte>();
                    return;
                }

                int frameLength = 4 + payloadLength;
                if (receiveBuffer.Length < frameLength)
                    break; // Incomplete frame — wait for more data

                // Extract payload
                var payload = new byte[payloadLength];
                Array.Copy(receiveBuffer, 4, payload, 0, payloadLength);

                // Deserialize
                try
                {
                    var message = ServerMessage.Parser.ParseFrom(payload);
                    onMessage?.Invoke(message);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MessageCodec] Parse error: {ex.Message}");
                }

                // Remove processed frame from buffer
                int remaining = receiveBuffer.Length - frameLength;
                if (remaining > 0)
                {
                    var truncated = new byte[remaining];
                    Array.Copy(receiveBuffer, frameLength, truncated, 0, remaining);
                    receiveBuffer = truncated;
                }
                else
                {
                    receiveBuffer = Array.Empty<byte>();
                    break;
                }
            }
        }

        /// <summary>
        /// Serialize a ClientMessage to bytes ready for TCP send.
        /// Returns [4-byte big-endian length][protobuf bytes]
        /// </summary>
        public static byte[] Encode(ClientMessage message)
        {
            var payload = message.ToByteArray();
            var frame = new byte[4 + payload.Length];
            WriteBigEndianInt32(frame, 0, payload.Length);
            Array.Copy(payload, 0, frame, 4, payload.Length);
            return frame;
        }

        /// <summary>
        /// Deserialize raw bytes (full frame payload WITHOUT 4-byte length prefix)
        /// into a ServerMessage.
        /// </summary>
        public static ServerMessage Decode(byte[] payload)
        {
            return ServerMessage.Parser.ParseFrom(payload);
        }

        private static int ReadBigEndianInt32(byte[] buf, int offset)
        {
            return (buf[offset] << 24)
                 | (buf[offset + 1] << 16)
                 | (buf[offset + 2] << 8)
                 | buf[offset + 3];
        }

        private static void WriteBigEndianInt32(byte[] buf, int offset, int value)
        {
            buf[offset]     = (byte)((value >> 24) & 0xFF);
            buf[offset + 1] = (byte)((value >> 16) & 0xFF);
            buf[offset + 2] = (byte)((value >>  8) & 0xFF);
            buf[offset + 3] = (byte)( value        & 0xFF);
        }

        public void Reset()
        {
            receiveBuffer = Array.Empty<byte>();
        }
    }
}
