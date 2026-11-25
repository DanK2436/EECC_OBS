using System;

namespace EeccStudio.Core.WebRtc
{
    /// <summary>
    /// Paquet RTP (Real-Time Transport Protocol - RFC 3550)
    /// Utilisé pour dépaqueter les flux H.264 (RFC 6184) et Opus (RFC 7587) reçus d'EECC Caméra.
    /// </summary>
    public sealed class RtpPacket
    {
        public const int MinHeaderLength = 12;

        public byte Version { get; }
        public bool HasPadding { get; }
        public bool HasExtension { get; }
        public byte CsrcCount { get; }
        public bool Marker { get; }
        public byte PayloadType { get; }
        public ushort SequenceNumber { get; }
        public uint Timestamp { get; }
        public uint Ssrc { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public RtpPacket(
            byte version,
            bool hasPadding,
            bool hasExtension,
            byte csrcCount,
            bool marker,
            byte payloadType,
            ushort sequenceNumber,
            uint timestamp,
            uint ssrc,
            ReadOnlyMemory<byte> payload)
        {
            Version = version;
            HasPadding = hasPadding;
            HasExtension = hasExtension;
            CsrcCount = csrcCount;
            Marker = marker;
            PayloadType = payloadType;
            SequenceNumber = sequenceNumber;
            Timestamp = timestamp;
            Ssrc = ssrc;
            Payload = payload;
        }

        public static bool TryParse(ReadOnlyMemory<byte> buffer, out RtpPacket? packet)
        {
            packet = null;
            if (buffer.Length < MinHeaderLength)
                return false;

            var span = buffer.Span;

            // Octet 0 : V (2b), P (1b), X (1b), CC (4b)
            var b0 = span[0];
            var version = (byte)((b0 >> 6) & 0x03);
            if (version != 2) // Standard RTP version 2
                return false;

            var hasPadding = (b0 & 0x20) != 0;
            var hasExtension = (b0 & 0x10) != 0;
            var csrcCount = (byte)(b0 & 0x0F);

            // Octet 1 : M (1b), PT (7b)
            var b1 = span[1];
            var marker = (b1 & 0x80) != 0;
            var payloadType = (byte)(b1 & 0x7F);

            // Octets 2-3 : Numéro de séquence (Big Endian)
            var sequenceNumber = (ushort)((span[2] << 8) | span[3]);

            // Octets 4-7 : Horodatage (Big Endian)
            var timestamp = (uint)((span[4] << 24) | (span[5] << 16) | (span[6] << 8) | span[7]);

            // Octets 8-11 : SSRC (Big Endian)
            var ssrc = (uint)((span[8] << 24) | (span[9] << 16) | (span[10] << 8) | span[11]);

            var headerLength = MinHeaderLength + (csrcCount * 4);
            if (buffer.Length < headerLength)
                return false;

            // Gestion de l'extension d'en-tête (X)
            if (hasExtension)
            {
                if (buffer.Length < headerLength + 4)
                    return false;
                var extLength = (span[headerLength + 2] << 8) | span[headerLength + 3];
                headerLength += 4 + (extLength * 4);
                if (buffer.Length < headerLength)
                    return false;
            }

            var payloadLength = buffer.Length - headerLength;
            if (hasPadding && payloadLength > 0)
            {
                var padCount = span[buffer.Length - 1];
                payloadLength -= padCount;
                if (payloadLength < 0)
                    return false;
            }

            var payload = buffer.Slice(headerLength, payloadLength);

            packet = new RtpPacket(
                version,
                hasPadding,
                hasExtension,
                csrcCount,
                marker,
                payloadType,
                sequenceNumber,
                timestamp,
                ssrc,
                payload);

            return true;
        }
    }
}
