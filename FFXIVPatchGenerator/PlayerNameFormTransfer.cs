using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal enum PlayerNameFormTransferStatus
    {
        // The global row never addresses the player by first or last name only.
        NotApplicable,
        // Korean name references were rewritten to the global first/last name forms.
        Applied,
        // The forms cannot be mapped safely (mixed forms with a different reference count, or a
        // particle whose subject depends on a condition); the Korean row is kept as-is.
        SkippedAmbiguous,
        // A SeString could not be parsed safely; the Korean row is kept as-is.
        SkippedUnparsed
    }

    // Korean server names are a single word, so Korean text always prints the full player name
    // (<String(gstr(1))>). Global text instead uses <Split(<String(gstr(1))>, " ", 1|2)> to call the
    // player by first or last name depending on the speaker. This copies those forms from the global
    // row onto the Korean row. The subject of a Korean particle macro (Josa/JosaRo) follows the name
    // form displayed just before it, so the particle is chosen for the name that is actually printed.
    internal static class PlayerNameFormTransfer
    {
        private const byte MacroStart = 0x02;
        private const byte MacroEnd = 0x03;
        private const byte JosaMacro = 0x0D;
        private const byte JosaRoMacro = 0x0E;
        private const byte SplitMacro = 0x2C;
        private const byte StringExpression = 0xFF;
        private const byte SplitSeparator = 0x20;

        // <String(gstr(1))>: the local player's full name.
        private static readonly byte[] FullNameToken = { 0x02, 0x29, 0x03, 0xEB, 0x02, 0x03 };

        public static PlayerNameFormTransferStatus Apply(byte[] global, byte[] korean, out byte[] result)
        {
            result = korean;
            if (global == null || korean == null || !ContainsFullNameToken(global) || !ContainsFullNameToken(korean))
            {
                return PlayerNameFormTransferStatus.NotApplicable;
            }

            // Each entry is the exact global Split token for a reference, or null for a full-name reference.
            List<byte[]> globalForms = new List<byte[]>();
            if (!CollectGlobalForms(global, 0, global.Length, globalForms))
            {
                return PlayerNameFormTransferStatus.SkippedUnparsed;
            }

            bool hasSplit = false;
            for (int i = 0; i < globalForms.Count; i++)
            {
                if (globalForms[i] != null)
                {
                    hasSplit = true;
                    break;
                }
            }

            if (!hasSplit)
            {
                return PlayerNameFormTransferStatus.NotApplicable;
            }

            int visibleReferences;
            if (!CountKoreanReferences(korean, 0, korean.Length, false, out visibleReferences))
            {
                return PlayerNameFormTransferStatus.SkippedUnparsed;
            }

            bool uniform = AllFormsEqual(globalForms);
            if (!uniform && visibleReferences != globalForms.Count)
            {
                return PlayerNameFormTransferStatus.SkippedAmbiguous;
            }

            FormAssigner assigner = new FormAssigner(globalForms, uniform);
            byte[] rewritten;
            if (!TryRewrite(korean, 0, korean.Length, false, assigner, out rewritten))
            {
                return assigner.Ambiguous
                    ? PlayerNameFormTransferStatus.SkippedAmbiguous
                    : PlayerNameFormTransferStatus.SkippedUnparsed;
            }

            result = rewritten;
            return PlayerNameFormTransferStatus.Applied;
        }

        private static bool CollectGlobalForms(byte[] bytes, int start, int end, List<byte[]> forms)
        {
            int cursor = start;
            while (cursor < end)
            {
                if (bytes[cursor] != MacroStart)
                {
                    cursor++;
                    continue;
                }

                MacroBounds macro;
                bool isNameSplit;
                if (!TryReadMacro(bytes, cursor, end, out macro) || !TryClassifySplit(bytes, macro, out isNameSplit))
                {
                    return false;
                }

                if (IsFullNameToken(bytes, macro))
                {
                    forms.Add(null);
                }
                else if (isNameSplit)
                {
                    forms.Add(Slice(bytes, macro.Offset, macro.NextOffset - macro.Offset));
                }
                else if (!ForEachStringArgument(bytes, macro, delegate(int argStart, int argEnd, int argIndex)
                {
                    return CollectGlobalForms(bytes, argStart, argEnd, forms);
                }))
                {
                    return false;
                }

                cursor = macro.NextOffset;
            }

            return true;
        }

        private static bool CountKoreanReferences(byte[] bytes, int start, int end, bool insideJosaSubject, out int visible)
        {
            int count = 0;
            int cursor = start;
            while (cursor < end)
            {
                if (bytes[cursor] != MacroStart)
                {
                    cursor++;
                    continue;
                }

                MacroBounds macro;
                bool isNameSplit;
                if (!TryReadMacro(bytes, cursor, end, out macro) || !TryClassifySplit(bytes, macro, out isNameSplit))
                {
                    visible = 0;
                    return false;
                }

                if (IsFullNameToken(bytes, macro) || isNameSplit)
                {
                    // An existing Split is kept but still occupies a reference slot.
                    if (!insideJosaSubject)
                    {
                        count++;
                    }
                }
                else
                {
                    bool isJosa = IsJosa(macro);
                    int nested = 0;
                    if (!ForEachStringArgument(bytes, macro, delegate(int argStart, int argEnd, int argIndex)
                    {
                        int argCount;
                        if (!CountKoreanReferences(bytes, argStart, argEnd, insideJosaSubject || (isJosa && argIndex == 0), out argCount))
                        {
                            return false;
                        }

                        nested += argCount;
                        return true;
                    }))
                    {
                        visible = 0;
                        return false;
                    }

                    count += nested;
                }

                cursor = macro.NextOffset;
            }

            visible = count;
            return true;
        }

        private static bool TryRewrite(byte[] bytes, int start, int end, bool insideJosaSubject, FormAssigner assigner, out byte[] rewritten)
        {
            rewritten = null;
            MemoryStream output = new MemoryStream(end - start + 32);
            int cursor = start;
            while (cursor < end)
            {
                if (bytes[cursor] != MacroStart)
                {
                    output.WriteByte(bytes[cursor]);
                    cursor++;
                    continue;
                }

                MacroBounds macro;
                bool isNameSplit;
                if (!TryReadMacro(bytes, cursor, end, out macro) || !TryClassifySplit(bytes, macro, out isNameSplit))
                {
                    return false;
                }

                if (IsFullNameToken(bytes, macro))
                {
                    byte[] form;
                    if (insideJosaSubject)
                    {
                        if (!assigner.TryGetJosaSubjectForm(out form))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        form = assigner.NextVisible();
                        assigner.Displayed = DisplayedName.Of(form);
                    }

                    if (form == null)
                    {
                        output.Write(bytes, macro.Offset, macro.NextOffset - macro.Offset);
                    }
                    else
                    {
                        output.Write(form, 0, form.Length);
                    }
                }
                else if (isNameSplit)
                {
                    // Korean text that already picks a form keeps it, and later particles follow it.
                    if (!insideJosaSubject)
                    {
                        assigner.ConsumeVisible();
                        assigner.Displayed = DisplayedName.Of(Slice(bytes, macro.Offset, macro.NextOffset - macro.Offset));
                    }

                    output.Write(bytes, macro.Offset, macro.NextOffset - macro.Offset);
                }
                else
                {
                    byte[] rebuilt;
                    if (!TryRewriteMacro(bytes, macro, insideJosaSubject, assigner, out rebuilt))
                    {
                        return false;
                    }

                    output.Write(rebuilt, 0, rebuilt.Length);
                }

                cursor = macro.NextOffset;
            }

            rewritten = output.ToArray();
            return true;
        }

        private static bool TryRewriteMacro(byte[] bytes, MacroBounds macro, bool insideJosaSubject, FormAssigner assigner, out byte[] rebuilt)
        {
            rebuilt = null;
            bool isJosa = IsJosa(macro);
            bool changed = false;
            List<byte> payload = new List<byte>(macro.PayloadLength + 16);
            int cursor = macro.PayloadOffset;
            int payloadEnd = macro.PayloadOffset + macro.PayloadLength;
            int argIndex = 0;

            // String arguments are treated as alternatives (If/Switch branches, particle variants):
            // only one of them is printed, so each starts from the same displayed name.
            DisplayedName before = assigner.Displayed;
            List<DisplayedName> outcomes = new List<DisplayedName>();
            while (cursor < payloadEnd)
            {
                int expressionLength;
                if (!TryMeasureExpression(bytes, cursor, payloadEnd, out expressionLength))
                {
                    return false;
                }

                int contentStart;
                int contentLength;
                if (TryReadStringExpression(bytes, cursor, payloadEnd, out contentStart, out contentLength))
                {
                    bool josaSubject = isJosa && argIndex == 0;
                    assigner.Displayed = before;
                    byte[] content;
                    if (!TryRewrite(bytes, contentStart, contentStart + contentLength, insideJosaSubject || josaSubject, assigner, out content))
                    {
                        return false;
                    }

                    if (!josaSubject)
                    {
                        outcomes.Add(assigner.Displayed);
                    }

                    if (!BytesEqual(bytes, contentStart, contentLength, content))
                    {
                        changed = true;
                    }

                    payload.Add(StringExpression);
                    AppendUInt(payload, checked((uint)content.Length));
                    payload.AddRange(content);
                }
                else
                {
                    for (int i = 0; i < expressionLength; i++)
                    {
                        payload.Add(bytes[cursor + i]);
                    }
                }

                cursor += expressionLength;
                argIndex++;
            }

            assigner.Displayed = outcomes.Count == 0 ? before : assigner.Merge(outcomes);

            if (!changed)
            {
                // Keep the original encoding byte-for-byte when nothing inside the macro changed.
                rebuilt = Slice(bytes, macro.Offset, macro.NextOffset - macro.Offset);
                return true;
            }

            List<byte> result = new List<byte>(payload.Count + 8);
            result.Add(MacroStart);
            result.Add(macro.Type);
            AppendUInt(result, checked((uint)payload.Count));
            result.AddRange(payload);
            result.Add(MacroEnd);
            rebuilt = result.ToArray();
            return true;
        }

        private delegate bool StringArgumentVisitor(int start, int end, int argIndex);

        private static bool ForEachStringArgument(byte[] bytes, MacroBounds macro, StringArgumentVisitor visitor)
        {
            int cursor = macro.PayloadOffset;
            int payloadEnd = macro.PayloadOffset + macro.PayloadLength;
            int argIndex = 0;
            while (cursor < payloadEnd)
            {
                int expressionLength;
                if (!TryMeasureExpression(bytes, cursor, payloadEnd, out expressionLength))
                {
                    return false;
                }

                int contentStart;
                int contentLength;
                if (TryReadStringExpression(bytes, cursor, payloadEnd, out contentStart, out contentLength) &&
                    !visitor(contentStart, contentStart + contentLength, argIndex))
                {
                    return false;
                }

                cursor += expressionLength;
                argIndex++;
            }

            return true;
        }

        private static bool IsJosa(MacroBounds macro)
        {
            return macro.Type == JosaMacro || macro.Type == JosaRoMacro;
        }

        private static bool IsFullNameToken(byte[] bytes, MacroBounds macro)
        {
            return BytesEqual(bytes, macro.Offset, macro.NextOffset - macro.Offset, FullNameToken);
        }

        // Recognizes <Split("<String(gstr(1))>", " ", 1|2)>. Returns false when a Split over the player
        // name has malformed or unexpected remaining arguments, so the caller keeps the row unchanged.
        private static bool TryClassifySplit(byte[] bytes, MacroBounds macro, out bool isNameSplit)
        {
            isNameSplit = false;
            if (macro.Type != SplitMacro)
            {
                return true;
            }

            int end = macro.PayloadOffset + macro.PayloadLength;
            int nameStart;
            int nameLength;
            if (!TryReadStringExpression(bytes, macro.PayloadOffset, end, out nameStart, out nameLength) ||
                !BytesEqual(bytes, nameStart, nameLength, FullNameToken))
            {
                // Not a player-name Split; its arguments are validated like any other macro.
                return true;
            }

            int separatorStart;
            int separatorLength;
            if (!TryReadStringExpression(bytes, nameStart + nameLength, end, out separatorStart, out separatorLength) ||
                separatorLength != 1 ||
                bytes[separatorStart] != SplitSeparator)
            {
                return false;
            }

            uint index;
            int indexLength;
            int indexOffset = separatorStart + separatorLength;
            if (!TryReadUInt(bytes, indexOffset, end, out index, out indexLength) ||
                (index != 1 && index != 2) ||
                indexOffset + indexLength != end)
            {
                return false;
            }

            isNameSplit = true;
            return true;
        }

        private static bool AllFormsEqual(List<byte[]> forms)
        {
            for (int i = 1; i < forms.Count; i++)
            {
                if (!FormsEqual(forms[0], forms[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FormsEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return BytesEqual(left, 0, left.Length, right);
        }

        private static bool ContainsFullNameToken(byte[] bytes)
        {
            for (int i = 0; i + FullNameToken.Length <= bytes.Length; i++)
            {
                if (BytesEqual(bytes, i, FullNameToken.Length, FullNameToken))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadMacro(byte[] bytes, int offset, int end, out MacroBounds macro)
        {
            macro = new MacroBounds();
            if (offset + 3 > end || bytes[offset] != MacroStart)
            {
                return false;
            }

            uint length;
            int lengthSize;
            if (!TryReadUInt(bytes, offset + 2, end, out length, out lengthSize))
            {
                return false;
            }

            int payloadOffset = offset + 2 + lengthSize;
            if (length > (uint)(end - payloadOffset))
            {
                return false;
            }

            int payloadEnd = payloadOffset + (int)length;
            if (payloadEnd >= end || bytes[payloadEnd] != MacroEnd)
            {
                return false;
            }

            macro.Offset = offset;
            macro.Type = bytes[offset + 1];
            macro.PayloadOffset = payloadOffset;
            macro.PayloadLength = (int)length;
            macro.NextOffset = payloadEnd + 1;
            return true;
        }

        private static bool TryReadStringExpression(byte[] bytes, int offset, int end, out int contentStart, out int contentLength)
        {
            contentStart = 0;
            contentLength = 0;
            if (offset >= end || bytes[offset] != StringExpression)
            {
                return false;
            }

            uint length;
            int lengthSize;
            if (!TryReadUInt(bytes, offset + 1, end, out length, out lengthSize))
            {
                return false;
            }

            int start = offset + 1 + lengthSize;
            if (length > (uint)(end - start))
            {
                return false;
            }

            contentStart = start;
            contentLength = (int)length;
            return true;
        }

        private static bool TryMeasureExpression(byte[] bytes, int offset, int end, out int length)
        {
            length = 0;
            if (offset >= end)
            {
                return false;
            }

            byte marker = bytes[offset];
            if ((marker > 0x00 && marker < 0xD0) || (marker >= 0xF0 && marker <= 0xFE))
            {
                uint ignored;
                return TryReadUInt(bytes, offset, end, out ignored, out length);
            }

            if ((marker >= 0xD0 && marker <= 0xDF) || marker == 0xEC)
            {
                length = 1;
                return true;
            }

            if (marker >= 0xE0 && marker <= 0xE5)
            {
                int left;
                int right;
                if (!TryMeasureExpression(bytes, offset + 1, end, out left) ||
                    !TryMeasureExpression(bytes, offset + 1 + left, end, out right))
                {
                    return false;
                }

                length = 1 + left + right;
                return true;
            }

            if (marker >= 0xE8 && marker <= 0xEB)
            {
                int operand;
                if (!TryMeasureExpression(bytes, offset + 1, end, out operand))
                {
                    return false;
                }

                length = 1 + operand;
                return true;
            }

            if (marker == StringExpression)
            {
                int contentStart;
                int contentLength;
                if (!TryReadStringExpression(bytes, offset, end, out contentStart, out contentLength))
                {
                    return false;
                }

                length = contentStart + contentLength - offset;
                return true;
            }

            return false;
        }

        private static bool TryReadUInt(byte[] bytes, int offset, int end, out uint value, out int length)
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
            int required = 1;
            if ((flags & 0x08) != 0) required++;
            if ((flags & 0x04) != 0) required++;
            if ((flags & 0x02) != 0) required++;
            if ((flags & 0x01) != 0) required++;
            if (offset + required > end)
            {
                return false;
            }

            int cursor = offset + 1;
            if ((flags & 0x08) != 0) value |= (uint)(bytes[cursor++] << 24);
            if ((flags & 0x04) != 0) value |= (uint)(bytes[cursor++] << 16);
            if ((flags & 0x02) != 0) value |= (uint)(bytes[cursor++] << 8);
            if ((flags & 0x01) != 0) value |= bytes[cursor++];
            length = required;
            return true;
        }

        private static void AppendUInt(List<byte> output, uint value)
        {
            if (value < 0xCF)
            {
                output.Add((byte)(value + 1));
                return;
            }

            int markerIndex = output.Count;
            output.Add(0);
            byte marker = 0xF0;
            for (int shift = 24; shift >= 0; shift -= 8)
            {
                byte part = (byte)(value >> shift);
                if (part != 0)
                {
                    marker |= (byte)(1 << (shift / 8));
                    output.Add(part);
                }
            }

            output[markerIndex] = (byte)(marker - 1);
        }

        private static bool BytesEqual(byte[] bytes, int offset, int length, byte[] expected)
        {
            if (length != expected.Length || offset < 0 || offset + length > bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < length; i++)
            {
                if (bytes[offset + i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] Slice(byte[] bytes, int offset, int length)
        {
            byte[] slice = new byte[length];
            Buffer.BlockCopy(bytes, offset, slice, 0, length);
            return slice;
        }

        private struct MacroBounds
        {
            public int Offset;
            public byte Type;
            public int PayloadOffset;
            public int PayloadLength;
            public int NextOffset;
        }

        private enum DisplayedNameKind
        {
            // No player name has been printed yet.
            None,
            // The last printed player name is known; Form is its token (null for the full name).
            Known,
            // The last printed player name depends on a condition.
            Uncertain
        }

        private sealed class DisplayedName
        {
            public static readonly DisplayedName None = new DisplayedName(DisplayedNameKind.None, null);
            public static readonly DisplayedName Uncertain = new DisplayedName(DisplayedNameKind.Uncertain, null);

            public readonly DisplayedNameKind Kind;
            public readonly byte[] Form;

            private DisplayedName(DisplayedNameKind kind, byte[] form)
            {
                Kind = kind;
                Form = form;
            }

            public static DisplayedName Of(byte[] form)
            {
                return new DisplayedName(DisplayedNameKind.Known, form);
            }

            public bool SameAs(DisplayedName other)
            {
                return Kind == other.Kind && (Kind != DisplayedNameKind.Known || FormsEqual(Form, other.Form));
            }
        }

        // Hands out the global form for each Korean name reference in document order, and tracks the
        // name form printed most recently so a particle subject can follow it.
        private sealed class FormAssigner
        {
            private readonly List<byte[]> _forms;
            private readonly bool _uniform;
            private int _nextVisible;

            public DisplayedName Displayed = DisplayedName.None;
            public bool Ambiguous;

            public FormAssigner(List<byte[]> forms, bool uniform)
            {
                _forms = forms;
                _uniform = uniform;
            }

            public byte[] NextVisible()
            {
                byte[] form = _uniform ? _forms[0] : (_nextVisible < _forms.Count ? _forms[_nextVisible] : null);
                _nextVisible++;
                return form;
            }

            public void ConsumeVisible()
            {
                _nextVisible++;
            }

            public bool TryGetJosaSubjectForm(out byte[] form)
            {
                DisplayedName subject = Resolve(Displayed);
                if (subject.Kind == DisplayedNameKind.Known)
                {
                    form = subject.Form;
                    return true;
                }

                form = null;
                Ambiguous = true;
                return false;
            }

            public DisplayedName Merge(List<DisplayedName> outcomes)
            {
                DisplayedName first = Resolve(outcomes[0]);
                for (int i = 1; i < outcomes.Count; i++)
                {
                    if (!first.SameAs(Resolve(outcomes[i])))
                    {
                        return DisplayedName.Uncertain;
                    }
                }

                return first;
            }

            // With a single global form, a particle before any printed name still takes that form.
            private DisplayedName Resolve(DisplayedName name)
            {
                return name.Kind == DisplayedNameKind.None && _uniform ? DisplayedName.Of(_forms[0]) : name;
            }
        }
    }
}
