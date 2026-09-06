using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Models;

using AberrationCalculator.Core.Enums;

namespace AberrationCalculator.Core.Glass;

/// <summary>
/// Turns each surface's material into a refractive index at a wavelength.
///
/// The result is indexed by surface and holds the index of the medium AFTER that surface,
/// which is what a ray-trace recurrence wants: leaving surface i, the ray travels in
/// index[i] to reach surface i+1.
/// </summary>
public static class IndexResolver
{
    /// <summary>d, F and C lines in micrometres, for model-glass dispersion.</summary>
    private const double LambdaD = 0.5875618, LambdaF = 0.4861327, LambdaC = 0.6562725;

    /// <summary>
    /// Index after each surface at <paramref name="lambdaUm"/>. Air is 1. A mirror keeps the
    /// index of the medium it sits in — reflection is handled by the trace reversing
    /// direction, not by negating the index here, so that this array stays a description of
    /// the media rather than of the ray's history.
    /// </summary>
    public static double[] Build(OpticalSystem system, GlassCatalog catalog, double lambdaUm,
                                 IList<string>? unresolved = null)
    {
        int n = system.Surfaces.Count;
        var idx = new double[n];

        for (int i = 0; i < n; i++)
        {
            var s = system.Surfaces[i];

            if (s.ModelIndexEnabled && s.ModelNd > 0.0)
            {
                idx[i] = ModelIndex(s.ModelNd, s.ModelVd, s.ModelDPgF, lambdaUm);
                continue;
            }

            if (string.IsNullOrEmpty(s.Material) || s.IsMirror)
            {
                // Air, or a mirror in whatever medium precedes it.
                idx[i] = s.IsMirror && i > 0 ? idx[i - 1] : 1.0;
                continue;
            }

            var g = catalog.Find(s.Material, system.GlassCatalogs);
            if (g == null)
            {
                // Not a name any loaded catalog knows. Before giving up, read it as a glass
                // code: whole prescriptions are written that way, and treating one as air
                // would silently flatten the lens instead of reporting a problem.
                if (GlassCode.TryParse(s.Material, out double codeNd, out double codeVd))
                {
                    idx[i] = ModelIndex(codeNd, codeVd, 0.0, lambdaUm);
                    continue;
                }

                unresolved?.Add(s.Material!);
                idx[i] = 1.0;                       // unknown glass behaves as air, and is reported
                continue;
            }

            double v = g.IndexAt(lambdaUm);
            if (double.IsNaN(v) || v <= 0.0)
            {
                unresolved?.Add(s.Material!);
                idx[i] = g.Nd > 0.0 ? g.Nd : 1.0;   // fall back to the catalog's own nd
                continue;
            }

            idx[i] = v;
        }

        return idx;
    }

    /// <summary>
    /// Index of a "model" glass given only nd, Vd and optionally the relative partial
    /// dispersion dPgF.
    ///
    /// With Vd alone the dispersion is fixed by two points (F and C) and interpolated
    /// linearly in 1/λ², which is the usual first approximation. dPgF adds curvature: it
    /// says how far the glass sits off the normal line, so a quadratic term in 1/λ² is
    /// fitted to reproduce it. Reduces to the linear form when dPgF is zero.
    /// </summary>
    public static double ModelIndex(double nd, double vd, double dPgF, double lambdaUm)
    {
        if (lambdaUm <= 0.0 || nd <= 0.0) return double.NaN;
        if (Math.Abs(vd) < 1e-9) return nd;              // no dispersion information

        double nF_nC = (nd - 1.0) / vd;                  // principal dispersion

        double x  = 1.0 / (lambdaUm * lambdaUm);
        double xd = 1.0 / (LambdaD * LambdaD);
        double xF = 1.0 / (LambdaF * LambdaF);
        double xC = 1.0 / (LambdaC * LambdaC);

        // Linear in 1/λ² through (xF, nF) and (xC, nC), anchored so n(λd) = nd.
        double slope = nF_nC / (xF - xC);
        double n = nd + slope * (x - xd);

        if (Math.Abs(dPgF) > 1e-12)
        {
            // dPgF is the departure of (ng − nF)/(nF − nC) from the normal line. Add a
            // quadratic term that produces exactly that departure at the g line and
            // vanishes at d, so nd is preserved.
            const double LambdaG = 0.4358343;
            double xg = 1.0 / (LambdaG * LambdaG);
            double curv = dPgF * nF_nC / ((xg - xF) * (xg - xd));
            n += curv * (x - xd) * (x - xF);
        }

        return n;
    }
}
