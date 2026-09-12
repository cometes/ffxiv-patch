using System;
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
