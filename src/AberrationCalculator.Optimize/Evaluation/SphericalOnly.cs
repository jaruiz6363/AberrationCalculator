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
/// <para><b>Why the restriction, and what it is NOT.</b> The aberration coefficients come from
/// Buchdahl's computing scheme, which is closed-form sums over the paraxial ray data - no trace,
/// no fit, no linear solve - and is the fastest route to a seventh-order coefficient there is.
/// It handles a conic or an even asphere correctly at third and fifth order. At SEVENTH order a
/// figured system needs an arrangement Buchdahl never published as a table, and for a long time
/// this repository's reconstruction of it was one real rays rejected, by up to a factor of four
/// on tau20. That was the reason for this file, and IT IS NO LONGER THE REASON: the arrangement
/// has since been completed and agrees with Forbes' series trace on all twenty tau to between
/// 2E-13 and 2E-10 on every figured design, with the rays agreeing with both. See
/// <c>docs/verification.md</c>.</para>
///
/// <para><b>What remains is a cost, not a doubt.</b> The aspheric routine is compiled into the
/// dual-number build alongside the spherical one and could be differentiated today. Carrying
/// both means every evaluation asks which route it is on, and asks whether each surface is
/// figured, in the middle of the arithmetic - and the figured route costs a second, dual run of
/// the whole scheme. Those questions are answered once, here, or they are answered tens of
/// thousands of times a second. Until the figured route is wired through deliberately, with its
/// own Jacobian check, the refusal stands - but it now stands on speed and on work not yet done,
/// which is a different thing from standing on a number known to be wrong, and a reader deciding
/// whether to trust this program should not be told the second when the first is true.</para>
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
      + "can become figured, which is what this optimiser requires - not because his aspheric "
      + "tertiary is in doubt, it agrees with Forbes to 2E-10 or better, but because routing to "
      + "it would put a test for figuring inside the evaluation loop and cost a second run of "
      + "the scheme.";

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
            sb.Append("The coefficients come from Buchdahl's scheme. Its aspheric SEVENTH "
                    + "order needs an arrangement he never published, and this repository has "
                    + "one that agrees with Forbes' series trace to 2E-10 or better - so this "
                    + "is not a doubt about the number. It is that routing to it would put a "
                    + "test for figuring inside the evaluation loop and cost a second run of "
                    + "the scheme, and that route has not been wired through and Jacobian-"
                    + "checked yet.\n\n");
            sb.Append("Remove the figuring, or analyse the design without optimising it - "
                    + "`abcalc <lens>` and `--forbes` handle conics and even aspheres at every "
                    + "order they report.");
            throw new NotSupportedException(sb.ToString());
        }

        // A spherical design must not be able to ACQUIRE figuring either. The kinds that would
        // do it now exist - VariableKind carries Conic and the three even-asphere terms, and
        // every layer under this one reads, writes, seeds, scales and files them - but the
        // evaluation loop has not yet been routed to the aspheric tertiary, so a design that
        // became figured mid-run would be descending the spherical arrangement on a surface
        // that is no longer spherical. That is the one failure this file exists to prevent, and
        // it is the last thing standing between here and a figured optimisation.
        if (variables != null)
            foreach (var v in variables.Items)
                if (v.Figures)
                    throw new NotSupportedException(
                        $"Variable {v.Name} would figure surface {v.Surface}, and this optimiser "
                      + "is still spherical-only.\n\n"
                      + "The variable itself is fully built - it reads and writes, it carries a "
                      + "bound, it seeds the differentiating chain and it has a step scale of "
                      + "its own. What is not built is the routing: the evaluation loop computes "
                      + "the tertiary through Buchdahl's spherical table, and a surface that "
                      + "acquired a conic mid-run would be descending the wrong arrangement "
                      + "without anything saying so.\n\n"
                      + "Vary CV and TH for now. Analysing a figured design is unaffected - "
                      + "`abcalc <lens>` and `--forbes` handle conics and even aspheres at every "
                      + "order they report.");
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
