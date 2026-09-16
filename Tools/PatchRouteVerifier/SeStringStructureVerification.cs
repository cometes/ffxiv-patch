using System;
using System.Text;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static class SeStringStructureInspector
    {
        public static bool TryBuildSignature(byte[] bytes, out string signature, out string error)
        {
            signature = string.Empty;
            error = string.Empty;
            if (bytes == null || bytes.Length == 0)
            {
                return true;
            }

            StringBuilder builder = new StringBuilder();
            if (!AppendPayloads(bytes, 0, bytes.Length, builder, out error))
            {
                signature = builder.ToString();
                return false;
            }

            int rsvCount = CountRsvTokens(bytes);
            if (rsvCount > 0)
            {
                builder.Append("|rsv:");
                builder.Append(rsvCount);
            }

            signature = builder.ToString();
            return true;
        }

        private static bool AppendPayloads(
            byte[] bytes,
            int start,
            int end,
            StringBuilder builder,
            out string error)
        {
            error = string.Empty;
            int index = start;
            while (index < end)
            {
                if (bytes[index] == 0x02)
                {
                    SePayload payload;
                    if (!TryReadPayload(bytes, index, end, out payload))
                    {
                        error = "invalid payload at byte " + index;
                        return false;
                    }

                    builder.Append("|m:");
                    builder.Append(payload.Type.ToString("x"));
                    index = payload.NextOffset;
                    continue;
                }
                if (bytes[index] == 0x03)
                {
                    error = "orphan payload terminator at byte " + index;
                    return false;
                }

                index++;
            }

            return true;
        }

        private static bool TryReadPayload(byte[] bytes, int offset, int end, out SePayload payload)
        {
            payload = new SePayload();
            if (offset < 0 || offset >= end || bytes[offset] != 0x02)
            {
                return false;
            }

            int cursor = offset + 1;
            uint type;
            int typeLength;
            if (!TryReadExpressionUInt32(bytes, cursor, end, out type, out typeLength))
            {
                return false;
            }
            cursor += typeLength;

            uint payloadLength;
            int payloadLengthLength;
            if (!TryReadExpressionUInt32(bytes, cursor, end, out payloadLength, out payloadLengthLength))
            {
                return false;
            }
            cursor += payloadLengthLength;

            if (payloadLength > int.MaxValue || cursor > end - (int)payloadLength - 1)
            {
                return false;
            }

            int payloadEnd = cursor + (int)payloadLength;
            if (payloadEnd >= end || bytes[payloadEnd] != 0x03)
            {
                return false;
            }

            payload.Type = type;
            payload.PayloadOffset = cursor;
            payload.PayloadLength = (int)payloadLength;
            payload.NextOffset = payloadEnd + 1;
            return true;
        }

        internal static bool TryReadExpressionUInt32(
            byte[] bytes,
            int offset,
            int end,
            out uint value,
            out int length)
        {
            value = 0;
            length = 0;
            if (offset < 0 || offset >= end)
            {
                return false;
            }

            byte marker = bytes[offset];
            if (marker > 0x00 && marker < 0xD0)
            {
                value = (uint)(marker - 1);
                length = 1;
                return true;
            }
            if (marker < 0xF0 || marker > 0xFE)
            {
                return false;
            }

            byte flags = (byte)(marker + 1);
            int requiredLength = 1;
            if ((flags & 0x08) != 0) requiredLength++;
            if ((flags & 0x04) != 0) requiredLength++;
            if ((flags & 0x02) != 0) requiredLength++;
            if ((flags & 0x01) != 0) requiredLength++;
            if (offset > end - requiredLength)
            {
                return false;
            }

            int cursor = offset + 1;
            if ((flags & 0x08) != 0) value |= (uint)(bytes[cursor++] << 24);
            if ((flags & 0x04) != 0) value |= (uint)(bytes[cursor++] << 16);
            if ((flags & 0x02) != 0) value |= (uint)(bytes[cursor++] << 8);
            if ((flags & 0x01) != 0) value |= bytes[cursor++];
            length = requiredLength;
            return true;
        }

        private static int CountRsvTokens(byte[] bytes)
        {
            int count = 0;
            for (int i = 0; i <= bytes.Length - 5; i++)
            {
                if (bytes[i] == (byte)'_' &&
                    bytes[i + 1] == (byte)'r' &&
                    bytes[i + 2] == (byte)'s' &&
                    bytes[i + 3] == (byte)'v' &&
                    bytes[i + 4] == (byte)'_')
                {
                    count++;
                    i += 4;
                }
            }
            return count;
        }

        private struct SePayload
        {
            public uint Type;
            public int PayloadOffset;
            public int PayloadLength;
            public int NextOffset;
        }
    }
}
