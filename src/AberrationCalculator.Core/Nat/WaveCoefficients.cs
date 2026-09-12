using System;
using AberrationCalculator.Core.Aberrations;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The third-order wave aberration coefficients nodal aberration theory is written in, from the
/// Seidel sums this program already computes.
///
/// <para>NAT speaks <c>Wklm</c> - Thompson's notation, and Buchdahl's before him - where this
/// program reports Seidel sums <c>S1..S5</c>. The bridge is arithmetic and exact:</para>
/// <code>
///     W040 = S1/8     W131 = S2/2     W222 = S3/2     W220P = S4/4     W311 = S5/2
///     W220M = W220P + W222/2
/// </code>
///
/// <para>That this is the right bridge can be checked without leaving the repository. Gu 2020
/// Eq. (9) writes the coma coefficient as</para>
/// <code>
///     W131 = -(1/2) i ibar y n^2 d(u/n)
/// </code>
/// <para>and <see cref="SeidelCoefficients"/> computes <c>s2 = -A Abar y dUoverN</c> with
/// <c>A = n i</c> and <c>Abar = n ibar</c>, which is <c>-n^2 i ibar y d(u/n)</c>. The two differ
/// by the factor of two and nothing else. The same substitution against Gu Eq. (14) gives
/// <c>W222 = S3/2</c>.</para>
///
/// <para><b>Nothing here is a fit or an approximation.</b> Third-order aberration is additive
/// over surfaces, there is no induced part and no aspheric reconstruction at this order, so the
/// per-surface conversion is as sound as the Seidel sums are - which is to say validated.</para>
///
/// <para><b>Medial versus Petzval.</b> NAT's field curvature term is the MEDIAL one, the average
/// of the tangential and sagittal focal surfaces, because that is what has a single node. The
/// Seidel S4 is the Petzval sum. They differ by half the astigmatism and confusing them is a
/// common way to get the field-curvature node in the wrong place.</para>
/// </summary>
public static class WaveCoefficients
{
    /// <summary>The third-order wave coefficients of one surface, or of a whole system.</summary>
    public readonly struct Third
    {
        /// <summary>Spherical aberration.</summary>
        public readonly Scalar W040;

        /// <summary>Coma.</summary>
        public readonly Scalar W131;

        /// <summary>Astigmatism.</summary>
        public readonly Scalar W222;

        /// <summary>Petzval field curvature.</summary>
        public readonly Scalar W220P;

        /// <summary>Distortion.</summary>
        public readonly Scalar W311;

        public Third(Scalar w040, Scalar w131, Scalar w222, Scalar w220p, Scalar w311)
        {
            W040 = w040; W131 = w131; W222 = w222; W220P = w220p; W311 = w311;
        }

        /// <summary>
        /// Medial field curvature, <c>W220P + W222/2</c>. This is the one NAT uses.
        /// </summary>
        public Scalar W220M => W220P + 0.5 * W222;
    }

    /// <summary>The wave coefficients of surface <paramref name="j"/>.</summary>
    public static Third OfSurface(SeidelResult seidel, int j)
    {
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));
        if (j < 0 || j >= seidel.S1.Length)
            throw new ArgumentOutOfRangeException(nameof(j), j, "no such surface");

        return new Third(seidel.S1[j] / 8.0, seidel.S2[j] / 2.0, seidel.S3[j] / 2.0,
                         seidel.S4[j] / 4.0, seidel.S5[j] / 2.0);
    }

    /// <summary>The wave coefficients of the whole system.</summary>
    public static Third OfSystem(SeidelResult seidel)
    {
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));
        return new Third(seidel.TotalS1 / 8.0, seidel.TotalS2 / 2.0, seidel.TotalS3 / 2.0,
                         seidel.TotalS4 / 4.0, seidel.TotalS5 / 2.0);
    }
}
