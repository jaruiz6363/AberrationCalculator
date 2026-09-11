using System;

using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Variables;

namespace AberrationCalculator.Optimize.Algorithms;

/// <summary>
/// How far each variable has to move to matter.
///
/// <para><b>Why this is needed at all.</b> A search that steps every variable by the same
/// fraction is meaningless on a lens, because the variables are not the same kind of thing. A
/// curvature lives near 0.02 reciprocal millimetres and a thickness near ten - three orders
/// apart, in different units. One step size cannot serve both: it is either far too small to
/// move the curvature or large enough to turn the lens inside out. Every pattern search and
/// every random perturbation in this optimiser therefore works in units of the scale computed
/// here, not in the variables' own units.</para>
///
/// <para><b>Where the scale comes from.</b> From the Jacobian. The natural step for a variable
/// is the one that changes the merit function by some set amount, and with an exact analytic
/// Jacobian in hand that is simply that amount divided by the length of the variable's column.
/// A variable the merit is very sensitive to gets a small step; one it barely notices gets a
/// large one. This is a use the derivatives can be put to that a finite-difference optimiser
/// cannot easily match, because it would have to spend a whole extra Jacobian to find out how
/// big a step to take.</para>
///
/// <para><b>And a physical ceiling on it.</b> A variable with almost no influence would be given
/// an almost unbounded step by that rule, which is right about the merit function and wrong
/// about the lens - a thickness is not free to become a kilometre because the spot does not care.
/// So each scale is capped by what the parameter can plausibly be asked to do on THIS design:
/// curvatures against the focal length, thicknesses against the total track. The cap is also
/// the fallback where a variable turns out to have no gradient at all.</para>
/// </summary>
public static class Scaling
{
    /// <summary>
    /// A step for each variable, in that variable's own units.
    /// </summary>
    /// <param name="meritChange">
    /// How much the merit should move over one such step. A twentieth is a step big enough to
    /// explore with and small enough not to destroy a working design.
    /// </param>
    public static double[] Build(MeritFunction merit, double meritChange = 0.05)
    {
        if (merit == null) throw new ArgumentNullException(nameof(merit));

        var design = merit.Design;
        int n = design.Variables.Count;
        var scale = new double[n];
        var ceiling = PhysicalCeilings(design);

        var r = merit.Evaluate(true);
        int m = r.Ok ? r.Residuals.Length : 0;

        for (int j = 0; j < n; j++)
        {
            double norm = 0.0;
            for (int i = 0; i < m; i++) norm += r.Jacobian[i, j] * r.Jacobian[i, j];
            norm = Math.Sqrt(norm);

            double fromMerit = norm > 1e-300 ? meritChange / norm : double.PositiveInfinity;
            scale[j] = Math.Min(fromMerit, ceiling[j]);

            if (!(scale[j] > 0.0) || double.IsInfinity(scale[j])) scale[j] = ceiling[j];
        }
        return scale;
    }

    /// <summary>
    /// The largest step each variable can sensibly be asked to take on this design, from its
    /// geometry alone.
    /// </summary>
    public static double[] PhysicalCeilings(Design design)
    {
        if (design == null) throw new ArgumentNullException(nameof(design));

        var system = design.System;
        int n = design.Variables.Count;
        var ceiling = new double[n];

        // The two lengths every ceiling is expressed in: how strongly this lens bends light,
        // and how long it is.
        double efl = 0.0, track = 0.0;
        try
        {
            var p = design.Probe(-1).Paraxial(design.PrimaryWave);
            efl = Math.Abs(p.Efl.Value);
            for (int i = 1; i < system.Surfaces.Count - 1; i++)
                track += Math.Abs(system.Surfaces[i].Thickness);
        }
        catch (Exception)
        {
            // A design that cannot even be traced still needs scales, or the search has no way
            // to step off it and find one that can.
        }

        if (!(efl > 1e-6) || double.IsInfinity(efl)) efl = 100.0;
        if (!(track > 1e-6)) track = 10.0;

        for (int j = 0; j < n; j++)
        {
            var v = design.Variables[j];
            ceiling[j] = v.Kind switch
            {
                // A quarter of the curvature a single surface would need to do the whole job.
                VariableKind.Curvature => 0.25 / efl,

                // A twentieth of the instrument's length.
                VariableKind.Thickness => Math.Max(0.05 * track, 0.05),

                _ => 1.0,
            };

            // A bounded variable can never usefully step further than its own interval.
            if (v.IsBounded && !double.IsNegativeInfinity(v.Min) && !double.IsPositiveInfinity(v.Max))
                ceiling[j] = Math.Min(ceiling[j], 0.5 * (v.Max - v.Min));
        }
        return ceiling;
    }
}
