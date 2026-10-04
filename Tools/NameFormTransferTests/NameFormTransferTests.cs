using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    // Regression cases for PlayerNameFormTransfer, built from synthetic SeStrings.
    // Run through Scripts\test-name-forms.ps1.
    internal static class NameFormTransferTests
    {
        private const byte IfMacro = 0x08;
        private const byte JosaMacro = 0x0D;
        private const byte JosaRoMacro = 0x0E;
        private const byte SplitMacro = 0x2C;

        // <String(gstr(1))>
        private static readonly byte[] N = { 0x02, 0x29, 0x03, 0xEB, 0x02, 0x03 };
        private static readonly byte[] S1 = Split(1);
        private static readonly byte[] S2 = Split(2);
        // gnum(4): the player's gender, used as an If condition.
        private static readonly byte[] Condition = { 0xE9, 0x05 };

        private static int _failures;
        private static int _passed;

        private static int Main()
        {
            // Basic forms.
            Expect("uniform first name",
                S1, Text("어서 와, ", N, "."),
                PlayerNameFormTransferStatus.Applied, Text("어서 와, ", S1, "."));
            Expect("uniform last name with Josa",
                S2, Cat(N, Josa(JosaMacro, N, "가", "이")),
                PlayerNameFormTransferStatus.Applied, Cat(S2, Josa(JosaMacro, S2, "가", "이")));
            Expect("uniform last name with JosaRo",
                S2, Cat(N, Josa(JosaRoMacro, N, "로", "으로")),
                PlayerNameFormTransferStatus.Applied, Cat(S2, Josa(JosaRoMacro, S2, "로", "으로")));
            Expect("mixed forms mapped in order",
                Cat(S1, Text("……"), S2), Cat(N, Text("…… "), N),
                PlayerNameFormTransferStatus.Applied, Cat(S1, Text("…… "), S2));
            Expect("mixed forms keep the Josa subject on the preceding full name",
                Cat(S1, Text("!? "), N), Cat(N, Text("!? "), N, Josa(JosaMacro, N, "라", "이라")),
                PlayerNameFormTransferStatus.Applied, Cat(S1, Text("!? "), N, Josa(JosaMacro, N, "라", "이라")));
            Expect("mixed forms with a different reference count stay full",
                Cat(S2, Text("。"), S1), Cat(N, Text(", 저길 봐라.")),
                PlayerNameFormTransferStatus.SkippedAmbiguous, Cat(N, Text(", 저길 봐라.")));
            Expect("global without first/last forms is not applicable",
                Cat(N), Cat(N, Josa(JosaMacro, N, "가", "이")),
                PlayerNameFormTransferStatus.NotApplicable, Cat(N, Josa(JosaMacro, N, "가", "이")));

            // Review [P2]: a Josa subject after a conditional must not be pinned to the last branch.
            Expect("conditional branches with different forms before Josa stay full",
                If(S1, S2), Cat(If(N, N), Josa(JosaMacro, N, "가", "이")),
                PlayerNameFormTransferStatus.SkippedAmbiguous, Cat(If(N, N), Josa(JosaMacro, N, "가", "이")));
            Expect("conditional branches with different forms before JosaRo stay full",
                If(S1, S2), Cat(If(N, N), Josa(JosaRoMacro, N, "로", "으로")),
                PlayerNameFormTransferStatus.SkippedAmbiguous, Cat(If(N, N), Josa(JosaRoMacro, N, "로", "으로")));
            Expect("conditional branches without Josa are mapped",
                If(S1, S2), If(N, N),
                PlayerNameFormTransferStatus.Applied, If(S1, S2));
            Expect("conditional branches with one form before Josa are mapped",
                S1, Cat(If(N, N), Josa(JosaMacro, N, "가", "이")),
                PlayerNameFormTransferStatus.Applied, Cat(If(S1, S1), Josa(JosaMacro, S1, "가", "이")));
            Expect("Josa inside a branch follows the name in that branch",
                If(S1, S2), If(Cat(N, Josa(JosaMacro, N, "가", "이")), Cat(N, Josa(JosaMacro, N, "가", "이"))),
                PlayerNameFormTransferStatus.Applied,
                If(Cat(S1, Josa(JosaMacro, S1, "가", "이")), Cat(S2, Josa(JosaMacro, S2, "가", "이"))));

            // Review [P2]: an existing Korean Split is kept and its particle follows it.
            Expect("existing Split keeps its form for Josa",
                S1, Cat(S2, Josa(JosaMacro, N, "가", "이")),
                PlayerNameFormTransferStatus.Applied, Cat(S2, Josa(JosaMacro, S2, "가", "이")));
            Expect("existing Split keeps its form for JosaRo",
                S1, Cat(S2, Josa(JosaRoMacro, N, "로", "으로")),
                PlayerNameFormTransferStatus.Applied, Cat(S2, Josa(JosaRoMacro, S2, "로", "으로")));

            // Review [P2]: a malformed Split must not be copied into Korean text.
            Expect("global Split with an expression missing its operand is unparsed",
                Hex("02 2C 0A FF 07 02 29 03 EB 02 03 EB 03"), Cat(N),
                PlayerNameFormTransferStatus.SkippedUnparsed, Cat(N));
            Expect("global Split without an index is unparsed",
                Macro(SplitMacro, Cat(Str(N), Str(Text(" ")))), Cat(N),
                PlayerNameFormTransferStatus.SkippedUnparsed, Cat(N));
            Expect("global Split with an unexpected index is unparsed",
                Macro(SplitMacro, Cat(Str(N), Str(Text(" ")), Int(3))), Cat(N),
                PlayerNameFormTransferStatus.SkippedUnparsed, Cat(N));
            Expect("global Split with a different separator is unparsed",
                Macro(SplitMacro, Cat(Str(N), Str(Text("・")), Int(1))), Cat(N),
                PlayerNameFormTransferStatus.SkippedUnparsed, Cat(N));
            Expect("global Split with trailing arguments is unparsed",
                Macro(SplitMacro, Cat(Str(N), Str(Text(" ")), Int(1), Int(1))), Cat(N),
                PlayerNameFormTransferStatus.SkippedUnparsed, Cat(N));
            Expect("malformed Korean Split is unparsed",
                S1, Cat(N, Macro(SplitMacro, Cat(Str(N), Str(Text(" "))))),
                PlayerNameFormTransferStatus.SkippedUnparsed, Cat(N, Macro(SplitMacro, Cat(Str(N), Str(Text(" "))))));

            // Length re-encoding past the one-byte integer range.
            string longText = new string('가', 80);
            Expect("long nested string lengths are re-encoded",
                S1, If(Cat(Text(longText), N), Text("")),
                PlayerNameFormTransferStatus.Applied, If(Cat(Text(longText), S1), Text("")));

            Console.WriteLine();
            Console.WriteLine("{0} passed, {1} failed", _passed, _failures);
            return _failures == 0 ? 0 : 1;
        }

        private static void Expect(string name, byte[] global, byte[] korean, PlayerNameFormTransferStatus expectedStatus, byte[] expected)
        {
            byte[] actual;
            PlayerNameFormTransferStatus status = PlayerNameFormTransfer.Apply(global, korean, out actual);
            bool ok = status == expectedStatus && Same(actual, expected);
            if (ok)
            {
                _passed++;
                Console.WriteLine("PASS  {0}", name);
                return;
            }

            _failures++;
            Console.WriteLine("FAIL  {0}", name);
            Console.WriteLine("      status   {0} (expected {1})", status, expectedStatus);
            Console.WriteLine("      actual   {0}", BitConverter.ToString(actual ?? new byte[0]));
            Console.WriteLine("      expected {0}", BitConverter.ToString(expected));
        }

        private static byte[] Split(uint index)
        {
            return Macro(SplitMacro, Cat(Str(N), Str(Text(" ")), Int(index)));
        }

        private static byte[] If(byte[] whenTrue, byte[] whenFalse)
        {
            return Macro(IfMacro, Cat(Condition, Str(whenTrue), Str(whenFalse)));
        }

        private static byte[] Josa(byte type, byte[] subject, string withoutFinal, string withFinal)
        {
            return Macro(type, Cat(Str(subject), Str(Text(withoutFinal)), Str(Text(withFinal))));
        }

        private static byte[] Macro(byte type, byte[] payload)
        {
            List<byte> output = new List<byte> { 0x02, type };
            AppendUInt(output, (uint)payload.Length);
            output.AddRange(payload);
            output.Add(0x03);
            return output.ToArray();
        }

        private static byte[] Str(byte[] content)
        {
            List<byte> output = new List<byte> { 0xFF };
            AppendUInt(output, (uint)content.Length);
            output.AddRange(content);
            return output.ToArray();
        }

        private static byte[] Int(uint value)
        {
            List<byte> output = new List<byte>();
            AppendUInt(output, value);
            return output.ToArray();
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

        private static byte[] Text(string value)
        {
            return System.Text.Encoding.UTF8.GetBytes(value);
        }

        // Concatenates SeString parts; string parts are UTF-8 text.
        private static byte[] Text(params object[] parts)
        {
            return Cat(parts);
        }

        private static byte[] Cat(params object[] parts)
        {
            List<byte> output = new List<byte>();
            foreach (object part in parts)
            {
                string text = part as string;
                output.AddRange(text != null ? Text(text) : (byte[])part);
            }

            return output.ToArray();
        }

        private static byte[] Hex(string value)
        {
            string[] parts = value.Split(' ');
            byte[] bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                bytes[i] = Convert.ToByte(parts[i], 16);
            }

            return bytes;
        }

        private static bool Same(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
