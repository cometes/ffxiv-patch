using System;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class InventorySubtitleSpacingPatch
    {
        public const string InventoryUldPath = "ui/uld/Inventory.uld";
        public const string InventoryLargeUldPath = "ui/uld/InventoryLarge.uld";
        public const string InventoryExpansionUldPath = "ui/uld/InventoryExpansion.uld";
        public static readonly string[] UldPaths = new string[]
        {
            InventoryUldPath,
            InventoryLargeUldPath,
            InventoryExpansionUldPath
        };
        public const uint ComponentId = 1005;
        public const uint TitleNodeId = 3;
        public const uint SubtitleNodeId = 4;
        public const short SourceX = 13;
        public const short TargetX = 25;

        private const int UldHeaderSize = 16;
        private const int AtkHeaderMinSize = 36;
        private const int ListHeaderSize = 16;
        private const int NodeTypeOffset = 20;
        private const int NodeSizeOffset = 24;
        private const int NodeXOffset = 44;
        private const int TextExtraOffset = 88;
        private const int TextNodeSize = 112;
        private const uint WidgetId = 1;
        private const uint TitleRowId = 528;
        private const uint SubtitleRowId = 529;

        public static byte[] Apply(string path, byte[] sourceUld)
        {
            uint expectedInstanceNodeId;
            if (string.Equals(path, InventoryUldPath, StringComparison.OrdinalIgnoreCase))
            {
                expectedInstanceNodeId = 21;
            }
            else if (string.Equals(path, InventoryLargeUldPath, StringComparison.OrdinalIgnoreCase))
            {
                expectedInstanceNodeId = 75;
            }
            else if (string.Equals(path, InventoryExpansionUldPath, StringComparison.OrdinalIgnoreCase))
            {
                expectedInstanceNodeId = 146;
            }
            else
            {
                throw new ArgumentException("Unsupported inventory ULD path: " + (path ?? string.Empty), "path");
            }

            int nodeOffset;
            uint instanceNodeId;
            string error;
            if (!TryFindSubtitleTextNode(sourceUld, out nodeOffset, out instanceNodeId, out error))
            {
                throw new InvalidDataException(path + " inventory subtitle source contract failed: " + error);
            }
            if (instanceNodeId != expectedInstanceNodeId)
            {
                throw new InvalidDataException(path + " inventory window instance changed: expected node " +
                    expectedInstanceNodeId.ToString() + ", found " + instanceNodeId.ToString());
            }

            byte[] patched = (byte[])sourceUld.Clone();
            patched[nodeOffset + NodeXOffset] = (byte)TargetX;
            patched[nodeOffset + NodeXOffset + 1] = (byte)(TargetX >> 8);
            return patched;
        }

        public static bool TryFindSubtitleTextNode(byte[] uld, out int nodeOffset, out string error)
        {
            uint instanceNodeId;
            return TryFindSubtitleTextNode(uld, out nodeOffset, out instanceNodeId, out error);
        }

        private static bool TryFindSubtitleTextNode(
            byte[] uld,
            out int nodeOffset,
            out uint instanceNodeId,
            out string error)
        {
            nodeOffset = 0;
            instanceNodeId = 0;
            error = null;
            if (!HasRange(uld, 0, UldHeaderSize) || !HasMagic(uld, 0, "uldh"))
            {
                error = "invalid uldh header";
                return false;
            }

            int listOffset;
            if (!TryFindList(uld, 8, 16, "cohd", out listOffset, out error))
            {
                return false;
            }

            uint componentCount = Endian.ReadUInt32LE(uld, listOffset + 8);
            int componentMatches = 0;
            int titleMatches = 0;
            int subtitleMatches = 0;
            int subtitleOffset = 0;
            int entryOffset = listOffset + ListHeaderSize;
            for (uint componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                if (!HasRange(uld, entryOffset, ListHeaderSize))
                {
                    error = "component entry is out of range";
                    return false;
                }

                uint componentId = Endian.ReadUInt32LE(uld, entryOffset);
                uint nodeCount = Endian.ReadUInt32LE(uld, entryOffset + 8);
                int componentSize = Endian.ReadUInt16LE(uld, entryOffset + 12);
                int nodeRelativeOffset = Endian.ReadUInt16LE(uld, entryOffset + 14);
                if (nodeRelativeOffset < ListHeaderSize || componentSize < nodeRelativeOffset ||
                    !HasRange(uld, entryOffset, componentSize))
                {
                    error = "component entry has an invalid size or node offset";
                    return false;
                }

                if (componentId == ComponentId)
                {
                    componentMatches++;
                    if (nodeRelativeOffset != 48 || nodeCount != 12 || componentSize != 1284 ||
                        uld[entryOffset + 7] != 2 ||
                        Endian.ReadUInt32LE(uld, entryOffset + 16) != TitleNodeId ||
                        Endian.ReadUInt32LE(uld, entryOffset + 20) != SubtitleNodeId)
                    {
                        error = "component 1005 window type, size, or title/subtitle mapping changed";
                        return false;
                    }
                }

                int cursor = entryOffset + nodeRelativeOffset;
                int componentEnd = entryOffset + componentSize;
                for (uint nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    int nodeSize;
                    if (!TryReadNodeSize(uld, cursor, componentEnd, out nodeSize, out error))
                    {
                        return false;
                    }
                    if (componentId == ComponentId)
                    {
                        uint nodeId = Endian.ReadUInt32LE(uld, cursor);
                        if (nodeId == TitleNodeId || nodeId == SubtitleNodeId)
                        {
                            bool subtitle = nodeId == SubtitleNodeId;
                            if (!ValidateTextNode(uld, cursor, nodeSize, subtitle, out error))
                            {
                                return false;
                            }
                            if (subtitle)
                            {
                                subtitleMatches++;
                                subtitleOffset = cursor;
                            }
                            else
                            {
                                titleMatches++;
                            }
                        }
                    }
                    cursor += nodeSize;
                }
                if (cursor != componentEnd)
                {
                    error = "component node sizes do not match the component boundary";
                    return false;
                }
                entryOffset = componentEnd;
            }

            if (componentMatches != 1 || titleMatches != 1 || subtitleMatches != 1)
            {
                error = "expected one component/title/subtitle, found " + componentMatches.ToString() + "/" +
                    titleMatches.ToString() + "/" + subtitleMatches.ToString();
                return false;
            }
            if (!TryFindWindowInstance(uld, out instanceNodeId, out error))
            {
                return false;
            }

            nodeOffset = subtitleOffset;
            return true;
        }

        private static bool TryFindWindowInstance(byte[] uld, out uint instanceNodeId, out string error)
        {
            instanceNodeId = 0;
            int listOffset;
            if (!TryFindList(uld, 12, 24, "wdhd", out listOffset, out error))
            {
                return false;
            }

            uint widgetCount = Endian.ReadUInt32LE(uld, listOffset + 8);
            int widgetMatches = 0;
            int instanceMatches = 0;
            int cursor = listOffset + ListHeaderSize;
            for (uint widgetIndex = 0; widgetIndex < widgetCount; widgetIndex++)
            {
                if (!HasRange(uld, cursor, ListHeaderSize))
                {
                    error = "widget entry is out of range";
                    return false;
                }
                uint widgetId = Endian.ReadUInt32LE(uld, cursor);
                uint nodeCount = Endian.ReadUInt16LE(uld, cursor + 12);
                if (widgetId == WidgetId)
                {
                    widgetMatches++;
                }
                cursor += ListHeaderSize;
                for (uint nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    int nodeSize;
                    if (!TryReadNodeSize(uld, cursor, uld.Length, out nodeSize, out error))
                    {
                        return false;
                    }
                    if (Endian.ReadUInt32LE(uld, cursor + NodeTypeOffset) == ComponentId)
                    {
                        instanceMatches++;
                        instanceNodeId = Endian.ReadUInt32LE(uld, cursor);
                        if (widgetId != WidgetId || nodeSize != TextNodeSize ||
                            (instanceNodeId != 21 && instanceNodeId != 75 && instanceNodeId != 146) ||
                            Endian.ReadUInt32LE(uld, cursor + TextExtraOffset + 12) != TitleRowId ||
                            Endian.ReadUInt32LE(uld, cursor + TextExtraOffset + 16) != SubtitleRowId)
                        {
                            error = "inventory window instance identity or Addon 528/529 binding changed";
                            return false;
                        }
                    }
                    cursor += nodeSize;
                }
            }
            if (widgetMatches != 1 || instanceMatches != 1)
            {
                error = "expected one widget 1/window instance, found " + widgetMatches.ToString() + "/" +
                    instanceMatches.ToString();
                return false;
            }
            return true;
        }

        private static bool ValidateTextNode(byte[] uld, int offset, int size, bool subtitle, out string error)
        {
            error = null;
            if (size != TextNodeSize)
            {
                error = "inventory title/subtitle text node size changed";
                return false;
            }
            int extraOffset = offset + TextExtraOffset;
            uint nodeType = Endian.ReadUInt32LE(uld, offset + NodeTypeOffset);
            short x = unchecked((short)Endian.ReadUInt16LE(uld, offset + NodeXOffset));
            short y = unchecked((short)Endian.ReadUInt16LE(uld, offset + 46));
            ushort width = Endian.ReadUInt16LE(uld, offset + 48);
            ushort height = Endian.ReadUInt16LE(uld, offset + 50);
            uint textId = Endian.ReadUInt32LE(uld, extraOffset);
            byte alignment = uld[extraOffset + 8];
            byte fontId = uld[extraOffset + 10];
            byte fontSize = uld[extraOffset + 11];
            byte textFlags = uld[extraOffset + 16];
            byte sheetType = uld[extraOffset + 17];
            byte charSpacing = uld[extraOffset + 18];
            byte lineSpacing = uld[extraOffset + 19];
            byte textFlags2 = uld[extraOffset + 20];
            if (nodeType != 3 ||
                (subtitle ? x != SourceX && x != TargetX : x != 12) ||
                y != (subtitle ? 17 : 7) || width != (subtitle ? 46 : 86) || height != (subtitle ? 20 : 31) ||
                fontId != (subtitle ? 0 : 3) || fontSize != (subtitle ? 12 : 23) ||
                textId != 0 || alignment != 3 || textFlags != 128 || textFlags2 != 6 ||
                sheetType != 0 || charSpacing != 0 || lineSpacing != 0)
            {
                error = (subtitle ? "subtitle" : "title") + " text contract changed: type=" + nodeType.ToString() +
                    ", rect=" + x.ToString() + "," + y.ToString() + "," + width.ToString() + "," + height.ToString() +
                    ", text=" + textId.ToString() + ", align=" + alignment.ToString() +
                    ", font=" + fontId.ToString() + "/" + fontSize.ToString() +
                    ", flags=" + textFlags.ToString() + "/" + textFlags2.ToString() +
                    ", spacing=" + charSpacing.ToString() + "/" + lineSpacing.ToString() +
                    ", sheet=" + sheetType.ToString();
                return false;
            }
            return true;
        }

        private static bool TryFindList(
            byte[] uld, int atkPointerOffset, int listPointerOffset, string magic,
            out int listOffset, out string error)
        {
            listOffset = 0;
            error = null;
            uint atkOffsetValue = Endian.ReadUInt32LE(uld, atkPointerOffset);
            if (atkOffsetValue == 0 || atkOffsetValue > int.MaxValue ||
                !HasRange(uld, (int)atkOffsetValue, AtkHeaderMinSize) || !HasMagic(uld, (int)atkOffsetValue, "atkh"))
            {
                error = "invalid " + magic + " ATK offset or atkh header";
                return false;
            }
            int atkOffset = (int)atkOffsetValue;
            uint relativeOffset = Endian.ReadUInt32LE(uld, atkOffset + listPointerOffset);
            long offsetValue = (long)atkOffset + relativeOffset;
            if (relativeOffset == 0 || offsetValue > int.MaxValue ||
                !HasRange(uld, (int)offsetValue, ListHeaderSize) || !HasMagic(uld, (int)offsetValue, magic))
            {
                error = "invalid " + magic + " list offset or header";
                return false;
            }
            listOffset = (int)offsetValue;
            return true;
        }

        private static bool TryReadNodeSize(byte[] uld, int offset, int end, out int size, out string error)
        {
            size = 0;
            error = null;
            if (offset < 0 || offset > end - (NodeSizeOffset + 2) || !HasRange(uld, offset, NodeSizeOffset + 2))
            {
                error = "ULD node header is out of range";
                return false;
            }
            size = Endian.ReadUInt16LE(uld, offset + NodeSizeOffset);
            if (size < NodeSizeOffset + 2 || offset > end - size || !HasRange(uld, offset, size))
            {
                error = "ULD node has an invalid size";
                return false;
            }
            return true;
        }

        private static bool HasRange(byte[] data, int offset, int length)
        {
            return data != null && offset >= 0 && length >= 0 && offset <= data.Length - length;
        }

        private static bool HasMagic(byte[] data, int offset, string magic)
        {
            if (!HasRange(data, offset, magic.Length))
            {
                return false;
            }
            for (int i = 0; i < magic.Length; i++)
            {
                if (data[offset + i] != (byte)magic[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
