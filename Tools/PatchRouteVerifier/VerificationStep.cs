using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private sealed class VerificationStep
            {
                public readonly string Name;
                public readonly Action Run;
                public readonly bool RunByDefault;

                public VerificationStep(string name, Action run, bool runByDefault = true)
                {
                    Name = name;
                    Run = run;
                    RunByDefault = runByDefault;
                }
            }
        }
    }
}
