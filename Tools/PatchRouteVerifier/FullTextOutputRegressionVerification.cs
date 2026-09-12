using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyFullTextOutputRegression()
            {
                Console.WriteLine("[TEXT] Full profile exact output regression");
                if (string.IsNullOrWhiteSpace(_baselineOutputPath))
                {
                    Fail("full text regression requires --baseline-output <pre-change-full-output>");
                    return;
                }
                if (!Directory.Exists(_baselineOutputPath))
                {
                    Fail("baseline output directory is missing: {0}", _baselineOutputPath);
                    return;
                }
                if (!Directory.Exists(_output))
                {
                    Fail("current output directory is missing: {0}", _output);
                    return;
                }

                Dictionary<string, string> baselineFiles = GetTextOutputFiles(_baselineOutputPath);
                Dictionary<string, string> currentFiles = GetTextOutputFiles(_output);
                bool equal = true;

                foreach (string name in baselineFiles.Keys)
                {
                    string currentPath;
                    if (!currentFiles.TryGetValue(name, out currentPath))
                    {
                        Fail("full text regression missing current file: {0}", name);
                        equal = false;
                        continue;
                    }

                    string baselineHash = ComputeSha256(baselineFiles[name]);
                    string currentHash = ComputeSha256(currentPath);
                    if (!string.Equals(baselineHash, currentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Fail(
                            "full text regression hash mismatch {0}: baseline {1}, current {2}",
                            name,
                            baselineHash,
                            currentHash);
                        equal = false;
                    }
                }

                foreach (string name in currentFiles.Keys)
                {
                    if (!baselineFiles.ContainsKey(name))
                    {
                        Fail("full text regression has unexpected current file: {0}", name);
                        equal = false;
                    }
                }

                if (baselineFiles.Count == 0)
                {
                    Fail("baseline output contains no 0a0000 text archive files");
                    return;
                }

                if (equal)
                {
                    Pass("full text output matches {0} baseline files byte-for-byte", baselineFiles.Count);
                }
            }

            private static Dictionary<string, string> GetTextOutputFiles(string directory)
            {
                Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] paths = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < paths.Length; i++)
                {
                    string name = Path.GetFileName(paths[i]);
                    if (name.StartsWith(TextPrefix + ".", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("orig." + TextPrefix + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        files[name] = paths[i];
                    }
                }
                return files;
            }

            private static string ComputeSha256(string path)
            {
                using (FileStream stream = File.OpenRead(path))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
        }
    }
}
