using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>What figuring one surface would do, at third order.</summary>
public sealed class AsphereCandidate
{
    /// <summary>Surface index, as the prescription numbers it.</summary>
    public int Surface;

    /// <summary>Marginal ray height there. Schulz's <c>h</c>.</summary>
    public Scalar MarginalHeight;

    /// <summary>Chief ray height there. Schulz's <c>H</c>.</summary>
    public Scalar ChiefHeight;

    /// <summary>
    /// <c>H/h</c>, the Delano ratio, and the whole of what decides WHICH aberrations an
    /// asphere here can reach. NaN where the marginal ray height vanishes.
    /// </summary>
    public Scalar Ratio;

    /// <summary>True where <c>h</c> vanishes, so the ratio is not formed.</summary>
    public bool AtImage;

    /// <summary>
    /// What one unit of r^4 coefficient adds to each Seidel sum. These are EXACT derivatives,
    /// not differences: the aspheric contribution is linear in the coefficient.
    /// Petzval is absent because figuring cannot reach it.
    /// </summary>
    public Scalar DS1, DS2, DS3, DS5;

    /// <summary>The same for one unit of CONIC constant, which is how most designs state it.</summary>
    public Scalar DS1Conic, DS2Conic, DS3Conic, DS5Conic;

    /// <summary>
    /// The r^4 coefficient that would drive each sum to zero on its own, or NaN where this
    /// surface has no leverage on that sum at all.
    /// </summary>
    public Scalar NullS1, NullS2, NullS3, NullS5;
}

/// <summary>
/// Which surface to figure, and what figuring it would buy - the design question this program
/// could always answer backwards and never forwards.
///
/// <para>Everything else here takes a design and says what is wrong with it, per surface. This
/// asks the other way round: given that an asphere is expensive and you can afford one, where
/// does it do the most good? The two questions share their arithmetic and have opposite
/// directions.</para>
///
/// <para><b>The whole of the answer is H/h</b>, the ratio of the chief-ray to the marginal-ray
/// height at the surface. <see cref="SeidelCoefficients"/> already forms it - as a local named
/// <c>ratio</c> - because the aspheric contribution to the five sums is</para>
///
/// <code>
///     dS1 = 8 (n' - n) a4 h^4,   dS2 = dS1 (H/h),   dS3 = dS1 (H/h)^2,   dS5 = dS1 (H/h)^3
/// </code>
///
/// <para>so one number fixes the whole pattern. Small <c>|H/h|</c> and the surface reaches
/// spherical aberration and little else; large and it reaches distortion; in between it moves
/// all four by comparable amounts. That is G. Schulz's reading of the Delano diagram, *Progress
/// in Optics* XXV (1988) Sec. 3.3, and this class is that diagram evaluated rather than drawn.
/// </para>
///
/// <para><b>Petzval is untouched and that is not an omission.</b> `S4` depends on the surface
/// curvature and the index step alone; no deformation of the surface can move it. Schulz says the
/// same in Sec. 3.3 - the condition "cannot be influenced by asphericities" - and this program's
/// own aspheric block leaves the column blank for the same reason. It is the one thing an asphere
/// cannot buy, and it is why a design can need a glass change rather than a figure.</para>
///
/// <para><b>The sensitivities are exact.</b> The contribution above is LINEAR in <c>a4</c>, so
/// the derivative is the contribution at unit coefficient and there is nothing to difference and
/// no step size to choose. That is a property of the third order only; at fifth and seventh the
/// figuring enters nonlinearly and this would have to be differentiated properly.</para>
/// </summary>
public static class AspherePlacement
{
    /// <summary>
    /// Evaluate every optical surface as a candidate. <paramref name="indices"/> is the index
    /// in each region, as <see cref="SeidelCoefficients"/> takes it.
    /// </summary>
    public static List<AsphereCandidate> Screen(OpticalSystem system, IReadOnlyList<Scalar> indices,
                                                ParaxialResult paraxial, SeidelResult seidel)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        if (seidel == null) throw new ArgumentNullException(nameof(seidel));

        var rows = new List<AsphereCandidate>();
        int last = system.LastOpticalSurface();

        for (int j = 1; j <= last; j++)
        {
            var surf = system.Surfaces[j];
            Scalar y = j < paraxial.Y.Length ? paraxial.Y[j] : 0.0;
            Scalar ybar = j < paraxial.Ybar.Length ? paraxial.Ybar[j] : 0.0;

            Scalar nBefore = j - 1 < indices.Count ? indices[j - 1] : 1.0;
            Scalar nAfter = j < indices.Count ? indices[j] : 1.0;

            bool atImage = Math.Abs(y) <= 1e-12;
            Scalar ratio = atImage ? double.NaN : ybar / y;

            // The contribution at unit r^4 coefficient, which IS the derivative.
            Scalar d1 = 8.0 * (nAfter - nBefore) * y * y * y * y;
            Scalar d2 = atImage ? 0.0 : d1 * ratio;
            Scalar d3 = atImage ? 0.0 : d2 * ratio;
            Scalar d5 = atImage ? 0.0 : d3 * ratio;

            // A conic K is an r^4 coefficient of K c^3 / 8, so its sensitivity is the same
            // numbers scaled. Reported because most designs state a conic, not a polynomial.
            Scalar c = surf.VertexCurvature;
            Scalar toConic = c * c * c / 8.0;

            var row = new AsphereCandidate
            {
                Surface = j,
                MarginalHeight = y,
                ChiefHeight = ybar,
                Ratio = ratio,
                AtImage = atImage,
                DS1 = d1, DS2 = d2, DS3 = d3, DS5 = d5,
                DS1Conic = d1 * toConic, DS2Conic = d2 * toConic,
                DS3Conic = d3 * toConic, DS5Conic = d5 * toConic,
                NullS1 = Null(seidel.TotalS1, d1),
                NullS2 = Null(seidel.TotalS2, d2),
                NullS3 = Null(seidel.TotalS3, d3),
                NullS5 = Null(seidel.TotalS5, d5),
            };
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// The coefficient that drives a sum to zero, or NaN where the surface has no leverage on it
    /// - which is a real answer and not a failure, so it is not reported as a very large number.
    /// </summary>
    private static Scalar Null(Scalar total, Scalar derivative)
        => Math.Abs(derivative) <= 1e-30 ? double.NaN : -total / derivative;

    /// <summary>
    /// How this surface's leverage divides across the four sums, normalised to the largest -
    /// the profile <c>1 : |H/h| : (H/h)^2 : |H/h|^3</c> rescaled.
    ///
    /// <para><b>Normalised rather than ranked against the design's current aberrations</b>, and
    /// the first version of this did the latter. Dividing each sensitivity by the size of the sum
    /// it moves looks like the useful comparison and is a trap: an aberration the design has
    /// already corrected has a total near zero, so it scores near infinity and wins the ranking
    /// precisely because there is nothing left to correct. The four Seidel sums are in one
    /// measure and are directly comparable, so the honest statement is the bare profile, and what
    /// to do with it is the reader's judgement and not this program's.</para>
    ///
    /// <para>Returns an empty array where the surface has no leverage at all - no index step, or
    /// the marginal ray at zero height.</para>
    /// </summary>
    public static Scalar[] ReachProfile(AsphereCandidate r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));

        var raw = new[] { Math.Abs(r.DS1), Math.Abs(r.DS2), Math.Abs(r.DS3), Math.Abs(r.DS5) };
        Scalar largest = 0.0;
        foreach (var v in raw) if (v > largest) largest = v;
        if (largest <= 1e-30) return Array.Empty<Scalar>();

        for (int i = 0; i < raw.Length; i++) raw[i] /= largest;
        return raw;
    }

    /// <summary>The screen as a readable block.</summary>
    public static string Render(OpticalSystem system, IReadOnlyList<Scalar> indices,
                                ParaxialResult paraxial, SeidelResult seidel)
    {
        var rows = Screen(system, indices, paraxial, seidel);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.AppendLine("WHERE AN ASPHERE WOULD ACT");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Every other table here says what is wrong with the design. This one asks the");
        sb.AppendLine("question the other way round: an asphere costs money, so if you can afford one,");
        sb.AppendLine("where does it do the most good? Third order only.");
        sb.AppendLine();
        sb.AppendLine("H/h - the chief-ray height over the marginal-ray height - decides the whole");
        sb.AppendLine("pattern, because figuring contributes to the four sums as 1, H/h, (H/h)^2 and");
        sb.AppendLine("(H/h)^3 of one another. Small: the surface reaches spherical aberration and little");
        sb.AppendLine("else. Large: distortion. In between: all four together. G. Schulz, Progress in");
        sb.AppendLine("Optics XXV (1988), Sec. 3.3, reading the Delano diagram.");
        sb.AppendLine();
        sb.AppendLine(string.Format(inv, "{0,-5} {1,10} {2,10} {3,10}   {4}",
            "Surf", "h", "H", "H/h", "leverage, normalised    S1     S2     S3     S5"));
        foreach (var r in rows)
        {
            var profile = ReachProfile(r);
            string reach = profile.Length == 0
                ? "no leverage - no index step, or h = 0"
                : string.Format(inv, "                     {0,6:0.000} {1,6:0.000} {2,6:0.000} {3,6:0.000}",
                    profile[0], profile[1], profile[2], profile[3]);
            sb.AppendLine(string.Format(inv, "{0,-5} {1,10:0.####} {2,10:0.####} {3,10}   {4}",
                r.Surface, r.MarginalHeight, r.ChiefHeight,
                r.AtImage ? "-" : r.Ratio.ToString("0.####", inv), reach));
        }
        sb.AppendLine();
        sb.AppendLine("The leverage columns are the four sensitivities scaled to the largest of them, so");
        sb.AppendLine("they say how a surface DIVIDES its effect and not how much it has. They are not");
        sb.AppendLine("ranked against what the design currently suffers from: an aberration already");
        sb.AppendLine("corrected has a total near zero and would win any such ranking precisely because");
        sb.AppendLine("there is nothing left to correct. Which of them is worth having is your call.");

        sb.AppendLine();
        sb.AppendLine("The r^4 coefficient that would drive each sum to zero on its own. One asphere");
        sb.AppendLine("buys one of these, and the other three move with it - read across to see what");
        sb.AppendLine("the choice costs elsewhere. A dash means this surface has no leverage on that");
        sb.AppendLine("sum at all, which is an answer rather than a failure.");
        sb.AppendLine();
        sb.AppendLine(string.Format(inv, "{0,-5} {1,16} {2,16} {3,16} {4,16}",
            "Surf", "null S1", "null S2", "null S3", "null S5"));
        foreach (var r in rows)
            sb.AppendLine(string.Format(inv, "{0,-5} {1,16} {2,16} {3,16} {4,16}",
                r.Surface, Num(r.NullS1), Num(r.NullS2), Num(r.NullS3), Num(r.NullS5)));

        sb.AppendLine();
        sb.AppendLine("PETZVAL IS ABSENT AND THAT IS NOT AN OMISSION. S4 depends on the surface");
        sb.AppendLine("curvatures and the index steps alone, and no deformation of a surface can move");
        sb.AppendLine("it. It is the one thing figuring cannot buy, and it is why a flat field can need");
        sb.AppendLine("a glass change or another element rather than an asphere.");
        sb.AppendLine();
        sb.AppendLine("AND THIS IS THE THIRD ORDER. The contribution above is exactly linear in the");
        sb.AppendLine("coefficient, so these are exact derivatives with no step size to choose. The");
        sb.AppendLine("fifth and seventh orders are not linear in it, will move when the figure is");
        sb.AppendLine("applied, and are not screened here. Treat this as where to START looking.");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string Num(Scalar v)
        => double.IsNaN(v) || double.IsInfinity(v)
         ? "-" : v.ToString("0.000000000E+00", CultureInfo.InvariantCulture);
}
