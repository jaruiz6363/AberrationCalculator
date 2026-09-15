using System;
using System.Collections.Generic;
using System.Text;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Variables;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// Designs this optimiser can evaluate, checked once before anything runs.
///
/// <para><b>This file used to be called SphericalOnly, and refused every figured design.</b> The
/// reason was real while it lasted: Buchdahl's aspheric seventh order needs an arrangement he
/// never published, and this repository's reconstruction of it was one real rays rejected, by up
/// to a factor of four on <c>tau20</c>. Descending a quantity wrong by a factor of four is not
/// slow, it is aimed wrongly. That arrangement is now established - all twenty tau against
/// Forbes' series trace to between 2E-13 and 2E-10 on every figured design, the rays agreeing
/// with both, and an independent transcription in <c>macros/BUCH7_ASPH.ZPL</c> reproducing
/// <c>FORBES.ZPL</c> inside OpticStudio. See <c>docs/verification.md</c>.</para>
///
/// <para><b>The second reason given for the refusal was cost, and measurement did not support
/// it.</b> The argument was that routing to the aspheric tertiary would put a test for figuring
/// inside the evaluation loop. It does not: <c>TertiaryCoefficients.Attach</c> makes that choice
/// once per evaluation, from data it has already computed, and the branch is not per surface and
/// not inside the arithmetic. What a figured design does cost is the aspheric scheme itself,
/// which is genuinely more work than Buchdahl's table - and a spherical design never pays it,
/// because the routing sends spheres down the same path they always took, bit for bit.</para>
///
/// <para><b>What remains refused is one case, and it is a real one.</b> A FIGURED FLAT FACING
/// COLLIMATED LIGHT has an identically zero marginal incidence, so the incidence ratio is
/// infinite and the finite coefficients arrive only after terms in different powers of it
/// cancel. Core handles it by running the whole chain again in Laurent series arithmetic with
/// that surface's curvature as the variable - and that route exists in Core alone. In the
/// differentiating build the call compiles away to nothing, so the optimiser would get a value
/// that is right and a derivative that is silently not. Refusing it here is the only place that
/// can be caught, because nothing downstream knows the difference.</para>
/// </summary>
public static class SupportedDesign
{
    /// <summary>The line the report prints about where the coefficients came from.</summary>
    public const string Explanation =
        "Seventh order by BUCHDAHL's computing scheme, which is closed-form sums over the "
      + "paraxial ray data and the fastest route there is. A figured surface takes the aspheric "
      + "arrangement of his Sec. 85, which agrees with Forbes' series trace on all twenty "
      + "tertiary coefficients to 2E-10 or better; a spherical design takes his own published "
      + "table, bit for bit as it always did.";

    /// <summary>
    /// Throws unless the design is one the evaluation loop can carry, figuring included.
    ///
    /// <para>Called once, before the first evaluation.</para>
    /// </summary>
    public static void Require(Design design)
    {
        if (design == null) throw new ArgumentNullException(nameof(design));

        // Checked against the design AS IT STANDS. Whether the optimiser could later drive a
        // curvature to zero and create a figured flat cannot be answered here and is not worth
        // guessing at; what this catches is the design a designer actually opens with one
        // already in it, which is the case that exists.
        var series = SeriesOnly(design);
        if (series.Count == 0) return;

        var sb = new StringBuilder();
        sb.Append("Surface ");
        sb.Append(series.Count == 1 ? series[0].ToString() : Join(series));
        sb.Append(series.Count == 1 ? " is a FIGURED FLAT facing collimated light"
                                    : " are FIGURED FLATS facing collimated light");
        sb.Append(", and this optimiser cannot evaluate one.\n\n");
        sb.Append("There the marginal incidence is identically zero, the incidence ratio is "
                + "infinite, and the finite coefficients arrive only after terms carrying "
                + "different powers of it cancel. The analysis side reaches them by running the "
                + "whole chain again in Laurent series arithmetic with that surface's curvature "
                + "as the variable; that route is not in the differentiating build, so the "
                + "optimiser would get a correct value and a silently wrong derivative.\n\n");
        sb.Append("Bend the surface and it is exact again - at R = 100 the two routes agree to "
                + "1.6E-12. Or analyse it without optimising: `abcalc <lens>` and `--forbes` "
                + "handle this case at every order they report.");
        throw new NotSupportedException(sb.ToString());
    }

    /// <summary>
    /// The figured flats in collimated light, or an empty list when the design cannot be traced
    /// at all.
    ///
    /// <para>A design that will not trace is not refused HERE. It has a fault the optimiser's own
    /// first evaluation will report far better than this check could, and swallowing it into a
    /// message about aspheric flats would be actively misleading.</para>
    /// </summary>
    private static List<int> SeriesOnly(Design design)
    {
        try
        {
            var system = design.System;
            var indices = design.Indices(design.PrimaryWave);
            var paraxial = Core.RayTrace.ParaxialTrace.Trace(system, indices, design.MaxField);
            return Core.Aberrations.TertiaryCoefficients.SeriesOnlySurfaces(system, indices,
                                                                           paraxial);
        }
        catch (Exception)
        {
            return new List<int>();
        }
    }

    private static string Join(List<int> values)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(i == values.Count - 1 ? " and " : ", ");
            sb.Append(values[i]);
        }
        return sb.ToString();
    }
}
