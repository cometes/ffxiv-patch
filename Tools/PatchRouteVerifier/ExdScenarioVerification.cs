using System;
using System.Collections.Generic;
using System.IO;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly byte[] KefkaAutoTranslateOpenIcon =
            {
                0x02, 0x12, 0x02, 0x37, 0x03
            };
            private static readonly byte[] KefkaAutoTranslateCloseIcon =
            {
                0x02, 0x12, 0x02, 0x38, 0x03
            };
            private static readonly byte[] KefkaFlattenedOpenMarker =
            {
                0x20, 0x20, 0x20, 0x37, 0x20
            };
            private static readonly byte[] KefkaFlattenedCloseMarker =
            {
                0x20, 0x20, 0x20, 0x38, 0x20
            };

            private void VerifyCharacterSelectJobSubtitle()
            {
                Console.WriteLine("[EXD] Character-select Korean primary and base-language job subtitle");
                if (_koreanText == null)
                {
                    Fail("Character-select verification requires a staged Korean source backup");
                    return;
                }
                byte[] primary = GetFirstStringBytes(_patchedText, "Lobby", 1974, _language);
                byte[] koreanPrimary = GetFirstStringBytes(_koreanText, "Lobby", 1974, _sourceLanguage);
                if (koreanPrimary.Length == 0 || !BytesEqual(primary, koreanPrimary))
                {
                    Fail("Lobby#1974 must retain the Korean level/job primary");
                }
                byte[] cleanSubtitle = GetFirstStringBytes(_cleanText, "Lobby", 1975, _language);
                byte[] subtitle = GetFirstStringBytes(_patchedText, "Lobby", 1975, _language);
                byte[] koreanSubtitle = GetFirstStringBytes(_koreanText, "Lobby", 1975, _sourceLanguage);
                if (koreanSubtitle.Length != 0)
                {
                    if (!BytesEqual(subtitle, koreanSubtitle))
                    {
                        Fail("Lobby#1975 must prefer a nonempty Korean source subtitle");
                    }
                    else
                    {
                        Pass("Lobby#1975 uses the nonempty Korean source subtitle");
                    }
                    return;
                }
                if (cleanSubtitle.Length == 0)
                {
                    if (!BytesEqual(subtitle, cleanSubtitle))
                    {
                        Fail("Lobby#1975/{0} must preserve the empty base subtitle", _language);
                    }
                    else
                    {
                        Pass("Lobby#1975/{0} preserves the empty base subtitle", _language);
                    }
                    return;
                }

                ExcelHeader header = ExcelHeader.Parse(_cleanText.ReadFile("exd/ClassJob.exh"));
                SortedDictionary<uint, byte[]> names = new SortedDictionary<uint, byte[]>();
                foreach (ExcelPageDefinition page in header.Pages)
                {
                    ExcelDataFile file = ExcelDataFile.Parse(_cleanText.ReadFile(BuildExdPath(
                        "ClassJob", page.StartId, _language, header.HasLanguage(LanguageToId(_language)))));
                    foreach (ExcelDataRow row in file.Rows)
                    {
                        names.Add(row.RowId, file.GetStringBytesByColumnOffset(row, header, 0));
                    }
                }
                if (names.Count == 0)
                {
                    Fail("Character-select subtitle verification found no clean ClassJob names");
                    return;
                }
                uint lastRow = 0;
                foreach (KeyValuePair<uint, byte[]> pair in names)
                {
                    byte[] actual = EvaluateJobSubtitle(subtitle, pair.Key);
                    if (!BytesEqual(actual, pair.Value))
                    {
                        Fail("Lobby#1975/{0} job {1} does not render its base-language ClassJob name", _language, pair.Key);
                    }
                    lastRow = pair.Key;
                }
                for (uint rowId = 0; rowId <= lastRow + 1; rowId++)
                {
                    if (!names.ContainsKey(rowId) && EvaluateJobSubtitle(subtitle, rowId).Length != 0)
                    {
                        Fail("Lobby#1975 unknown job {0} must not resolve a translated name", rowId);
                    }
                }
                Pass("Lobby#1975/{0} evaluated {1} clean job names and absent-key boundaries", _language, names.Count);
            }

            private byte[] EvaluateJobSubtitle(byte[] text, uint jobId)
            {
                if (text.Length == 0 || text[0] != 0x02)
                {
                    return text;
                }
                int cursor = 2;
                uint bodyLength = ReadJobSubtitleUInt(text, ref cursor);
                int end = checked(cursor + (int)bodyLength);
                if (end != text.Length - 1 || text[end] != 0x03)
                {
                    throw new InvalidDataException("Invalid character-select subtitle payload bounds.");
                }
                byte[] selected = null;
                if (text[1] == 0x28)
                {
                    byte[] sheet = ReadJobSubtitleString(text, ref cursor);
                    uint row = ReadJobSubtitleNumber(text, ref cursor, jobId);
                    uint column = ReadJobSubtitleUInt(text, ref cursor);
                    if (System.Text.Encoding.UTF8.GetString(sheet) != "ClassJob" || column != 0)
                    {
                        throw new InvalidDataException("Unsupported character-select subtitle Sheet reference.");
                    }
                    selected = GetFirstStringBytes(_patchedText, "ClassJob", row, _language);
                }
                else if (text[1] == 0x08)
                {
                    byte comparison = text[cursor++];
                    uint left = ReadJobSubtitleNumber(text, ref cursor, jobId);
                    uint right = ReadJobSubtitleNumber(text, ref cursor, jobId);
                    if (comparison != 0xE2 && comparison != 0xE4)
                    {
                        throw new InvalidDataException("Unsupported character-select subtitle condition.");
                    }
                    byte[] whenTrue = ReadJobSubtitleString(text, ref cursor);
                    byte[] whenFalse = ReadJobSubtitleString(text, ref cursor);
                    selected = (comparison == 0xE2 ? left <= right : left == right) ? whenTrue : whenFalse;
                }
                else if (text[1] == 0x09)
                {
                    uint choice = ReadJobSubtitleNumber(text, ref cursor, jobId);
                    uint index = 1;
                    while (cursor < end)
                    {
                        byte[] value = ReadJobSubtitleString(text, ref cursor);
                        if (index == choice || (index == 1 && choice == 0))
                        {
                            selected = value;
                        }
                        index++;
                    }
                }
                if (selected == null || cursor != end)
                {
                    throw new InvalidDataException("Unresolved character-select subtitle branch.");
                }
                return EvaluateJobSubtitle(selected, jobId);
            }

            private static byte[] ReadJobSubtitleString(byte[] bytes, ref int cursor)
            {
                if (cursor >= bytes.Length || bytes[cursor++] != 0xFF)
                {
                    throw new InvalidDataException("Expected character-select subtitle string expression.");
                }
                int length = checked((int)ReadJobSubtitleUInt(bytes, ref cursor));
                if (length > bytes.Length - cursor)
                {
                    throw new InvalidDataException("Character-select subtitle string is out of bounds.");
                }
                byte[] result = new byte[length];
                Buffer.BlockCopy(bytes, cursor, result, 0, length);
                cursor += length;
                return result;
            }

            private static uint ReadJobSubtitleNumber(byte[] bytes, ref int cursor, uint jobId)
            {
                if (cursor < bytes.Length && bytes[cursor] == 0xE8)
                {
                    cursor++;
                    if (ReadJobSubtitleUInt(bytes, ref cursor) != 1)
                    {
                        throw new InvalidDataException("Unexpected character-select subtitle parameter.");
                    }
                    return jobId;
                }
                return ReadJobSubtitleUInt(bytes, ref cursor);
            }

            private static uint ReadJobSubtitleUInt(byte[] bytes, ref int cursor)
            {
                uint value;
                int length;
                if (!SeStringStructureInspector.TryReadExpressionUInt32(bytes, cursor, bytes.Length, out value, out length))
                {
                    throw new InvalidDataException("Invalid character-select subtitle integer.");
                }
                cursor += length;
                return value;
            }

            private void VerifyCompactTimeRows()
            {
                Console.WriteLine("[EXD] Compact time labels");
                ExpectTextContains("Addon", 44, "m");
                ExpectTextContains("Addon", 45, "h");
                ExpectTextContains("Addon", 49, "s");
                ExpectTextContains("Addon", 2338, "h");
                ExpectTextContains("Addon", 2338, "m");
                ExpectTextContains("Addon", 6166, "h");
                ExpectTextContains("Addon", 6166, "m");
                ExpectTextContains("Addon", 876, "\uE028");
                ExpectTextNotContains("Addon", 876, "분");
                ExpectText("Addon", 8291, "5m");
                ExpectText("Addon", 8292, "10m");
                ExpectText("Addon", 8293, "30m");
                ExpectText("Addon", 8294, "60m");
                ExpectTextNotContains("Addon", 8291, "분");
                ExpectTextNotContains("Addon", 8292, "분");
                ExpectTextNotContains("Addon", 8293, "분");
                ExpectTextNotContains("Addon", 8294, "분");
            }

            private void VerifyWorldVisitRows()
            {
                Console.WriteLine("[EXD] World visit labels");
                ExpectText("Addon", 12510, "서버 텔레포");
                ExpectText("Addon", 12511, "서버 텔레포");
                ExpectText("Addon", 12520, "서버 텔레포 예약 신청 중");
                ExpectText("Addon", 12524, "서버 텔레포");
                ExpectText("Addon", 12537, "서버 텔레포");
            }

            private void VerifyBozjaEntranceRows()
            {
                Console.WriteLine("[EXD] Bozja entrance custom talk");

                ExpectAnyTextColumnContains("custom/006/ctsmycentrance_00673", 1, "입장하기");
                ExpectAnyTextColumnContains("custom/006/ctsmycentrance_00673", 2, "남부 보즈야 전선");
                ExpectAnyTextColumnContains("custom/006/ctsmycentrance_00673", 3, "취소");
                ExpectAnyTextColumnNotContains("custom/006/ctsmycentrance_00673", 1, "突入する");
                ExpectAnyTextColumnNotContains("custom/006/ctsmycentrance_00673", 2, "南方ボズヤ戦線");

                ExpectAnyTextColumnContains("custom/007/ctsmycentrancenormal_00705", 3, "레지스탕스 랭크");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancenormal_00705", 14, "현재의");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancenormal_00705", 23, "미시야");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancenormal_00705", 25, "모험가님");
                ExpectAnyTextColumnNotContains("custom/007/ctsmycentrancenormal_00705", 3, "レジスタンスランク");
                ExpectAnyTextColumnNotContains("custom/007/ctsmycentrancenormal_00705", 23, "ミーシィヤ");

                ExpectAnyTextColumnContains("custom/007/ctsmycentrancehard_00706", 1, "입장하기");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancehard_00706", 3, "이야기 듣기");
                ExpectAnyTextColumnContains("custom/007/ctsmycentrancehard_00706", 5, "초고를 읽어");
                ExpectAnyTextColumnNotContains("custom/007/ctsmycentrancehard_00706", 1, "突入する");
                ExpectAnyTextColumnNotContains("custom/007/ctsmycentrancehard_00706", 3, "話を聞く");
            }

            private void VerifyRsvAutoTranslateDelimiters()
            {
                Console.WriteLine("[EXD] Korean RSV listing resolution");
                if (_koreanText == null)
                {
                    Fail("InstanceContentTextData#45500 requires a staged Korean source backup");
                    return;
                }

                byte[] source = GetFirstStringBytes(
                    _koreanText,
                    "InstanceContentTextData",
                    45500,
                    _sourceLanguage);
                RsvResolutionResult resolution = _rsvResolver.Resolve(source);
                if (resolution.ResolvedTokens != 1 || resolution.UnresolvedTokens != 0)
                {
                    Fail(
                        "InstanceContentTextData#45500 Korean RSV listing resolution was {0} resolved, {1} unresolved",
                        resolution.ResolvedTokens,
                        resolution.UnresolvedTokens);
                    return;
                }

                byte[] actual = GetFirstStringBytes(
                    _patchedText,
                    "InstanceContentTextData",
                    45500,
                    _language);
                ExpectBytes(
                    "InstanceContentTextData#45500/" + _language + "/Korean-RSV-listing",
                    actual,
                    resolution.Bytes);
                int openIcons = CountByteSequence(actual, KefkaAutoTranslateOpenIcon);
                int closeIcons = CountByteSequence(actual, KefkaAutoTranslateCloseIcon);
                int flattenedMarkers =
                    CountByteSequence(actual, KefkaFlattenedOpenMarker) +
                    CountByteSequence(actual, KefkaFlattenedCloseMarker);
                if (RsvStringResolver.ContainsRsvToken(actual) ||
                    openIcons != 2 ||
                    closeIcons != 2 ||
                    flattenedMarkers != 0)
                {
                    Fail(
                        "InstanceContentTextData#45500 marker shape is invalid: open={0}, close={1}, flattened={2}",
                        openIcons,
                        closeIcons,
                        flattenedMarkers);
                }
                else
                {
                    Pass("InstanceContentTextData#45500 restores two native Icon(54/55) pairs");
                }

                byte[] followingSource = GetFirstStringBytes(
                    _koreanText, "InstanceContentTextData", 45501, _sourceLanguage);
                RsvResolutionResult followingResolution = _rsvResolver.Resolve(followingSource);
                if (followingResolution.UnresolvedTokens != 0 ||
                    followingResolution.Bytes == null || followingResolution.Bytes.Length == 0)
                {
                    Fail("InstanceContentTextData#45501 Korean source could not be resolved");
                    return;
                }
                ExpectBytes(
                    "InstanceContentTextData#45501/" + _language + "/Korean-RSV-listing",
                    GetFirstStringBytes(_patchedText, "InstanceContentTextData", 45501, _language),
                    followingResolution.Bytes);
            }

            private static int CountByteSequence(byte[] bytes, byte[] pattern)
            {
                if (bytes == null || pattern == null || pattern.Length == 0 || bytes.Length < pattern.Length)
                {
                    return 0;
                }

                int count = 0;
                for (int offset = 0; offset <= bytes.Length - pattern.Length; offset++)
                {
                    bool matches = true;
                    for (int index = 0; index < pattern.Length; index++)
                    {
                        if (bytes[offset + index] != pattern[index])
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        count++;
                        offset += pattern.Length - 1;
                    }
                }

                return count;
            }


        }
    }
}
