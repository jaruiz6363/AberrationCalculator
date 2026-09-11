using System;
using System.Collections.Generic;
using System.Text;

using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Variables;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// This optimiser works on spherical surfaces, and says so before it starts rather than
/// discovering it partway.
///
/// <para><b>Why the restriction.</b> The aberration coefficients come from Buchdahl's computing
/// scheme, which is closed-form sums over the paraxial ray data - no trace, no fit, no linear
/// solve - and is the fastest route to a seventh-order coefficient there is. It handles a conic
/// or an even asphere correctly at third and fifth order. At SEVENTH order it does not: that
/// needs an aspheric arrangement Buchdahl never published, so this repository's is a
/// reconstruction, and real rays reject it - <c>docs/distortion-prediction.md</c> measures tau20
/// out by up to a factor of four on a figured design. An optimiser descending a quantity that is
/// wrong by a factor of four is not slow, it is pointed the wrong way.</para>
///
/// <para><b>Why refuse rather than fall back.</b> Forbes' series trace gets the figured tertiary
/// right and could be used instead, but carrying two routes means every evaluation asks which one
/// it is on - and worse, asks whether each surface is figured, in the middle of the arithmetic.
/// Those are the questions that have to be answered once, here, or they are answered tens of
/// thousands of times a second for no benefit. Refusing up front keeps the evaluation loop free
/// of them entirely.</para>
///
/// <para>The ANALYSIS side of this program is unaffected and still handles conics and even
/// aspheres at every order it reports - `abcalc &lt;lens&gt;` and `--forbes` are unchanged. It is
/// only the optimiser that is spherical.</para>
/// </summary>
public static class SphericalOnly
{
    /// <summary>The line the report prints about where the coefficients came from.</summary>
    public const string Explanation =
        "Seventh order by BUCHDAHL's computing scheme, which is closed-form sums over the "
      + "paraxial ray data and the fastest route there is. Every surface is spherical and none "
      + "can become figured, which is what this optimiser requires - his aspheric tertiary is a "
      + "reconstruction real rays reject, so a figured design is refused rather than optimised "
      + "against a number known to be wrong.";

    /// <summary>
    /// Throws unless the design is spherical throughout and stays that way.
    ///
    /// <para>Called once, before the first evaluation. Nothing downstream tests for figuring
    /// again.</para>
    /// </summary>
    public static void Require(OpticalSystem system, VariableSet? variables)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));

        var figured = new List<int>();
        int last = system.LastOpticalSurface();
        for (int i = 1; i <= last && i < system.Surfaces.Count; i++)
            if (system.Surfaces[i].IsFigured) figured.Add(i);

        if (figured.Count > 0)
        {
            var sb = new StringBuilder();
            sb.Append("This optimiser handles SPHERICAL surfaces only, and surface ");
            sb.Append(figured.Count == 1 ? figured[0].ToString() : Join(figured));
            sb.Append(figured.Count == 1 ? " is figured" : " are figured");
            sb.Append(" - a conic constant or an even-asphere coefficient is not zero.\n\n");
            sb.Append("The coefficients come from Buchdahl's scheme, whose aspheric SEVENTH "
                    + "order needs an arrangement he never published; the reconstruction of it "
                    + "in this repository is one real rays reject, by up to a factor of four. "
                    + "Optimising against that would not be slow, it would be aimed wrongly.\n\n");
            sb.Append("Remove the figuring, or analyse the design without optimising it - "
                    + "`abcalc <lens>` and `--forbes` handle conics and even aspheres at every "
                    + "order they report.");
            throw new NotSupportedException(sb.ToString());
        }

        // A design cannot acquire figuring either, because the variables that would do it do not
        // exist - VariableKind has no conic and no aspheric member. This is belt and braces
        // against someone adding one without reading this file.
        if (variables != null)
            foreach (var v in variables.Items)
                if (v.Kind != VariableKind.Curvature && v.Kind != VariableKind.Thickness)
                    throw new NotSupportedException(
                        $"Variable {v.Name} is of a kind this optimiser does not accept. Only "
                      + "curvature and thickness may be varied; figuring a surface would put the "
                      + "seventh order beyond what Buchdahl's scheme can reach.");
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
