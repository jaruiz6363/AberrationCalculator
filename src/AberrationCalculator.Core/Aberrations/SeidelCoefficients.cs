using System;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>Third-order sums, per surface and totalled.</summary>
public sealed class SeidelResult
{
    /// <summary>Spherical aberration contribution of each surface.</summary>
    public double[] S1 { get; init; } = Array.Empty<double>();

    /// <summary>Coma.</summary>
    public double[] S2 { get; init; } = Array.Empty<double>();

    /// <summary>Astigmatism.</summary>
    public double[] S3 { get; init; } = Array.Empty<double>();

    /// <summary>Petzval field curvature.</summary>
    public double[] S4 { get; init; } = Array.Empty<double>();

    /// <summary>Distortion.</summary>
    public double[] S5 { get; init; } = Array.Empty<double>();

    /// <summary>Longitudinal (axial) chromatic aberration.</summary>
    public double[] CL { get; init; } = Array.Empty<double>();

    /// <summary>Transverse (lateral) chromatic aberration.</summary>
    public double[] CT { get; init; } = Array.Empty<double>();

    public double TotalS1 { get; init; }
    public double TotalS2 { get; init; }
    public double TotalS3 { get; init; }
    public double TotalS4 { get; init; }
    public double TotalS5 { get; init; }
    public double TotalCL { get; init; }
    public double TotalCT { get; init; }

    /// <summary>
    /// Surfaces where the distortion term had to be suppressed because the marginal-ray
    /// refraction invariant A was zero there. See the note in the calculator.
    /// </summary>
    public int[] DistortionSuppressedAt { get; init; } = Array.Empty<int>();

    // ── The aspheric share of each sum ──────────────────────────────────────────────────
    //
    // The figuring's contribution ALONE, already included in S1..S5 above and repeated here
    // so that it can be separated again. Zero on an unfigured surface.
    //
    // Nodal aberration theory is what wants this. An aspheric surface has TWO aberration field
    // centres - one for the spherical base curve and one for the aspheric cap, which behaves as
    // a zero-power plate and so is centred by where the optical axis ray CROSSES it rather than
    // by an angle of incidence. Summing them needs the two contributions apart, which no other
    // consumer of this type has ever had a reason to ask for.

    /// <summary>Aspheric share of the spherical aberration sum, per surface.</summary>
    public double[] S1Aspheric { get; init; } = Array.Empty<double>();

    /// <summary>Aspheric share of coma.</summary>
    public double[] S2Aspheric { get; init; } = Array.Empty<double>();

    /// <summary>Aspheric share of astigmatism.</summary>
    public double[] S3Aspheric { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Aspheric share of distortion. There is no aspheric share of PETZVAL: that term depends
    /// only on the surface's curvature and index step, and figuring changes neither.
    /// </summary>
    public double[] S5Aspheric { get; init; } = Array.Empty<double>();
}

/// <summary>
/// The Seidel (third-order) aberration coefficients, surface by surface.
///
/// These are sums over the paraxial marginal and chief rays, so everything needed comes
/// from <see cref="ParaxialTrace"/> and the refractive indices. The per-surface breakdown
/// is the point of computing them at all: a spot diagram says the design is soft, the
/// coefficients say which surface is making it soft.
///
/// Formulation follows the standard treatment (Welford, <i>Aberrations of Optical
/// Systems</i>, ch. 8). Per surface, with the marginal ray (y, u) and chief ray
/// (ybar, ubar) and the refraction invariants
///
///   A    = n (y c + u)          the marginal ray's n*i
///   Abar = n (ybar c + ubar)    the chief ray's n*ibar
///
/// the contributions are
///
///   S1 = -A^2 y d(u/n)          S2 = -A Abar y d(u/n)      S3 = -Abar^2 y d(u/n)
///   S4 = -H^2 c d(1/n)          S5 = (Abar/A)(S3 + S4)
///   CL = -A y d(dn/n)           CT = -Abar y d(dn/n)
///
/// where d(x) is the change in x across the surface and dn = n_short - n_long.
/// </summary>
public static class SeidelCoefficients
{
    /// <summary>
    /// Computes the third-order sums.
    /// </summary>
    /// <param name="system">The lens.</param>
    /// <param name="n">Index after each surface at the primary wavelength.</param>
    /// <param name="nShort">Index after each surface at the short wavelength (F line).</param>
    /// <param name="nLong">Index after each surface at the long wavelength (C line).</param>
    /// <param name="p">The paraxial trace, which supplies both rays and the invariant.</param>
    public static SeidelResult Compute(OpticalSystem system, double[] n, double[] nShort, double[] nLong,
                                       ParaxialResult p)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (p == null) throw new ArgumentNullException(nameof(p));

        int count = system.Surfaces.Count;
        int last = system.LastOpticalSurface();

        var s1 = new double[count]; var s2 = new double[count]; var s3 = new double[count];
        var a1 = new double[count]; var a2 = new double[count]; var a3 = new double[count];
        var a5 = new double[count];
        var s4 = new double[count]; var s5 = new double[count];
        var cl = new double[count]; var ct = new double[count];
        var suppressed = new System.Collections.Generic.List<int>();

        double H = p.LagrangeInvariant;

        for (int j = 1; j <= last; j++)
        {
            var surf = system.Surfaces[j];

            // The signed indices from the trace, so a mirror's reversal is already carried.
            double nBefore = p.N[j - 1];
            double nAfter  = p.N[j];
            if (Math.Abs(nBefore) < 1e-15 || Math.Abs(nAfter) < 1e-15) continue;

            double c = surf.VertexCurvature;

            // (y, u) at this surface: the height here, the slope in the medium BEFORE it.
            double y    = p.Y[j];
            double u    = p.U[j - 1];
            double ybar = p.Ybar[j];
            double ubar = p.Ubar[j - 1];

            double A    = nBefore * (y * c + u);
            double Abar = nBefore * (ybar * c + ubar);

            double dUoverN  = p.U[j] / nAfter - u / nBefore;
            double dOneOverN = 1.0 / nAfter - 1.0 / nBefore;

            s1[j] = -A * A * y * dUoverN;
            s2[j] = -A * Abar * y * dUoverN;
            s3[j] = -Abar * Abar * y * dUoverN;
            s4[j] = -H * H * c * dOneOverN;

            // Distortion carries a 1/A. A is zero only when the marginal ray meets the
            // surface at normal incidence, where the surface contributes no spherical,
            // coma or astigmatism either - but the Petzval part of S5 is genuinely
            // singular there, so it is reported as suppressed rather than as a number.
            if (Math.Abs(A) > 1e-12)
            {
                s5[j] = (Abar / A) * (s3[j] + s4[j]);
            }
            else
            {
                s5[j] = 0.0;
                if (Math.Abs(s4[j]) > 1e-15) suppressed.Add(j);
            }

            // Aspheric figuring adds to every term except Petzval, which depends only on
            // the surface's curvature and index step. A conic of constant K departs from
            // the sphere by K c^3 r^4 / 8 to fourth order; an explicit r^4 coefficient
            // adds directly to that.
            double a4 = 0.0;
            if (Math.Abs(surf.Conic) > 1e-15) a4 += surf.Conic * c * c * c / 8.0;
            if (surf.AsphericCoefficients.Length > 1) a4 += surf.AsphericCoefficients[1];
            if (Math.Abs(a4) > 1e-30)
            {
                double sAsph = 8.0 * (nAfter - nBefore) * a4 * y * y * y * y;
                double ratio = Math.Abs(y) > 1e-15 ? ybar / y : 0.0;

                // Kept separately as well as added in - see the note on SeidelResult.
                a1[j] = sAsph;
                a2[j] = sAsph * ratio;
                a3[j] = sAsph * ratio * ratio;
                a5[j] = sAsph * ratio * ratio * ratio;

                s1[j] += a1[j];
                s2[j] += a2[j];
                s3[j] += a3[j];
                s5[j] += a5[j];
            }

            // Chromatic terms use the dispersion of the medium after each surface.
            double dispBefore = DispersionAt(j - 1, nShort, nLong);
            double dispAfter  = DispersionAt(j, nShort, nLong);
            double dDispOverN = dispAfter / nAfter - dispBefore / nBefore;

            cl[j] = -A * y * dDispOverN;
            ct[j] = -Abar * y * dDispOverN;
        }

        return new SeidelResult
        {
            S1 = s1, S2 = s2, S3 = s3, S4 = s4, S5 = s5, CL = cl, CT = ct,
            TotalS1 = Sum(s1), TotalS2 = Sum(s2), TotalS3 = Sum(s3), TotalS4 = Sum(s4),
            TotalS5 = Sum(s5), TotalCL = Sum(cl), TotalCT = Sum(ct),
            DistortionSuppressedAt = suppressed.ToArray(),
            S1Aspheric = a1, S2Aspheric = a2, S3Aspheric = a3, S5Aspheric = a5,
        };
    }

    private static double DispersionAt(int i, double[] nShort, double[] nLong)
    {
        if (nShort == null || nLong == null) return 0.0;
        if (i < 0 || i >= nShort.Length || i >= nLong.Length) return 0.0;
        return nShort[i] - nLong[i];
    }

    private static double Sum(double[] v)
    {
        double t = 0.0;
        foreach (double x in v) t += x;
        return t;
    }
}
