using System;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The aberration field decentre vectors - Thompson's <c>sigma_j</c>, one per surface.
///
/// <para>Thompson (2005) Sec. 3 defines it geometrically: <c>sigma_j</c> points to where the
/// centre of surface <c>j</c>'s own aberration field lands in the image plane, and is "the
/// projection of a line connecting the center of the pupil for the surface of interest with the
/// center of curvature of that surface to the image plane". The effective field height of that
/// surface is then his Eq. (3.1),</para>
/// <code>
///     H_Aj = H - sigma_j
/// </code>
/// <para>and every displacement vector in <see cref="NatField"/> follows that sense.</para>
///
/// <h3>The paraxial route, and the divergence in it</h3>
///
/// <para>Thompson gives the definition but not a formula in tilt and decentre; that is
/// Buchroeder's, reproduced in the appendix of Gu (2020) as Eqs. (21)-(34). For a single
/// perturbed surface <c>k</c>, with equivalent tilt <c>E_k = T_k + c_k D_k</c>,</para>
/// <code>
///     sigma_k(j) = 0                            j &lt; k
///     sigma_k(k) = E_k / ibar_k
///     sigma_k(j) = E_k xi_j / ibar_j            j &gt; k
///
///     xi_j = (i_j ybar_k - ibar_j y_k) dn_k / H
/// </code>
/// <para>and several perturbed surfaces superpose. Surfaces ahead of the perturbation contribute
/// nothing, because light has not reached it yet.</para>
///
/// <para><b><c>sigma</c> itself diverges where the chief-ray incidence vanishes</b>, and that is
/// not a defect of the formula. A surface the chief ray strikes normally has its aberration
/// field centre at infinity - but it also contributes no coma and no astigmatism, because
/// <c>W131</c> carries one factor of <c>ibar</c> and <c>W222</c> two. The products that appear in
/// the theory are finite; only the vector on its own is not.</para>
///
/// <para>So this type reports BOTH: <see cref="Sigma"/> for a report to print, and
/// <see cref="Reduced"/> - <c>nu_j = ibar_j sigma_j</c> - which carries no division at all and is
/// what <see cref="NatField"/> actually builds the aberration field from.</para>
/// </summary>
public sealed class SigmaVector
{
    /// <summary>The aberration field decentre vector of each surface. May be large or infinite
    /// where the chief-ray incidence vanishes; see the note on this type.</summary>
    public Vec2[] Sigma { get; }

    /// <summary>
    /// <c>nu_j = ibar_j sigma_j</c>. Formed without any division, so it is finite everywhere,
    /// and it is what the field vectors are built from.
    /// </summary>
    public Vec2[] Reduced { get; }

    /// <summary>Chief-ray incidence at each surface, kept because the field sums need it.</summary>
    public Scalar[] ChiefIncidence { get; }

    /// <summary>Marginal-ray incidence at each surface.</summary>
    public Scalar[] MarginalIncidence { get; }

    /// <summary>
    /// The SECOND field decentre vector a figured surface carries - Thompson (2009) Eq. (11).
    ///
    /// <para>An aspheric surface has two aberration field centres, not one. The spherical base
    /// curve is centred by <see cref="Sigma"/>, an angle over an angle, because its aberration
    /// depends on the angle of incidence. The aspheric departure is not: treated as a zero-power
    /// plate after Burch, its contribution depends only on WHERE the optical axis ray crosses
    /// it, so</para>
    /// <code>
    ///     sigma_asph = delta beta* / ybar
    /// </code>
    /// <para>- the displacement of the optical axis ray from the aspheric vertex, over the
    /// chief-ray height. The two are genuinely different: on Thompson's telescope they are
    /// <c>(0.0220889, 0.0966082)</c> and <c>(0.0540453, 0.108097)</c>, a factor of two apart.
    /// Using the spherical one for both would put every node of a conic telescope in the wrong
    /// place.</para>
    ///
    /// <para>Zero on an unfigured surface, which carries no aspheric contribution to centre.</para>
    /// </summary>
    public Vec2[] SigmaAspheric { get; }

    /// <summary>
    /// <c>ybar_j sigma_asph,j</c> - the reduced form, carrying no division, for the same reason
    /// <see cref="Reduced"/> exists.
    /// </summary>
    public Vec2[] ReducedAspheric { get; }

    /// <summary>
    /// <c>G_j = n_j^2 y_j d(u/n)_j</c> - the Seidel kernel with both incidences divided out.
    /// Shared with <see cref="Sensitivity"/>, which needs exactly the same quantity.
    /// </summary>
    public Scalar[] Kernel { get; }

    /// <summary>
    /// Surfaces where <c>sigma</c> could not be formed because the chief-ray incidence was too
    /// near zero. The reduced form is still exact at those surfaces, so the aberration field is
    /// unaffected; only the printed vector is missing.
    /// </summary>
    public int[] SigmaSuppressedAt { get; }

    private SigmaVector(Vec2[] sigma, Vec2[] reduced, Scalar[] i, Scalar[] ibar, Scalar[] g,
                        int[] suppressed, Vec2[] sigmaAsph, Vec2[] reducedAsph)
    {
        Sigma = sigma; Reduced = reduced;
        MarginalIncidence = i; ChiefIncidence = ibar; Kernel = g;
        SigmaSuppressedAt = suppressed;
        SigmaAspheric = sigmaAsph; ReducedAspheric = reducedAsph;
    }

    /// <summary>Whether this surface carries figuring - a conic or any aspheric term.</summary>
    private static bool IsFigured(Surface s)
    {
        if (SMath.Abs(s.Conic) > 1e-15) return true;
        if (s.AsphericCoefficients != null)
            foreach (var a in s.AsphericCoefficients) if (SMath.Abs(a) > 1e-20) return true;
        return false;
    }

    /// <summary>The stop surface, or the last optical one if the design names none.</summary>
    private static int StopSurface(OpticalSystem system, int last)
    {
        for (int j = 1; j <= last; j++) if (system.Surfaces[j].IsStop) return j;
        return last;
    }

    /// <summary>Computes the field decentre vectors of a perturbed system.</summary>
    public static SigmaVector Compute(OpticalSystem system, Scalar[] indices, ParaxialResult p)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (p == null) throw new ArgumentNullException(nameof(p));

        int count = system.Surfaces.Count;
        int last = system.LastOpticalSurface();

        var sigma = new Vec2[count];
        var reduced = new Vec2[count];
        var sigmaAsph = new Vec2[count];
        var reducedAsph = new Vec2[count];
        var i = new Scalar[count];
        var ibar = new Scalar[count];
        var g = new Scalar[count];
        var suppressed = new System.Collections.Generic.List<int>();

        Scalar invariant = p.LagrangeInvariant;

        for (int j = 1; j <= last; j++)
        {
            Scalar nBefore = p.N[j - 1];
            Scalar nAfter = p.N[j];
            if (SMath.Abs(nBefore) < 1e-15 || SMath.Abs(nAfter) < 1e-15) continue;

            Scalar c = system.Surfaces[j].VertexCurvature;
            Scalar y = p.Y[j];
            Scalar u = p.U[j - 1];

            i[j] = y * c + u;
            ibar[j] = p.Ybar[j] * c + p.Ubar[j - 1];
            g[j] = nBefore * nBefore * y * (p.U[j] / nAfter - u / nBefore);
        }

        if (SMath.Abs(invariant) < 1e-15)
            return new SigmaVector(sigma, reduced, i, ibar, g, Array.Empty<int>(),
                                   sigmaAsph, reducedAsph);

        // Walk the optical axis ray through the system, accumulating its state as it goes.
        //
        // Thompson (2009) Eq. (10). The OAR is a linear combination of the nominal chief and
        // marginal rays, and its coefficients are the running decentres of the object field and
        // of the pupil - his Eqs. (3) and (4), which are Gu's (17) and (18). At each surface the
        // OAR's height and inclination relative to the mechanical axis follow from those by his
        // Eqs. (5) and (6), and sigma is then the OAR's angle of incidence on the local surface
        // over the nominal chief-ray incidence.
        //
        // The accumulation happens AFTER the surface, which is what makes the OAR unperturbed at
        // and before the first moved surface - Gu's Eqs. (22) and (23) - and displaced beyond it.
        // The pupil decentre the optical axis ray must START with. Thompson's sigma*.
        //
        // The optical axis ray is defined by passing through the centre of the STOP, so when the
        // stop has itself been moved - or when anything before it has - the ray is displaced
        // before it reaches the first surface. Locating the entrance pupil is therefore a
        // BOUNDARY-value problem, object centre at one end and stop centre at the other, and
        // starting the accumulation at zero solves only half of it.
        //
        // It needs no iteration. Accumulate the perturbations ahead of the stop, then ask what
        // initial pupil decentre puts the ray on the stop's own centre:
        //
        //     ybar#_OAR(stop) = ybar_stop qAcc + y_stop (eAcc + e0)  =  decentre of the stop
        //
        // which is one division by the marginal ray height at the stop - a height that cannot
        // vanish, since the stop is where the marginal ray is at full aperture.
        var qAcc = Vec2.Zero;   // decentre of the object field centre, normalised
        var eAcc = Vec2.Zero;   // decentre of the pupil centre, normalised

        int stop = StopSurface(system, last);
        if (stop >= 1 && SMath.Abs(p.Y[stop]) > 1e-12)
        {
            var qAhead = Vec2.Zero;
            var eAhead = Vec2.Zero;
            for (int j = 1; j < stop; j++)
            {
                var sj = system.Surfaces[j];
                if (!sj.IsPerturbed) continue;
                Scalar cj = sj.VertexCurvature;
                var bj = new Vec2(sj.TiltY + cj * sj.DecenterX, sj.TiltX + cj * sj.DecenterY);
                Scalar dnj = p.N[j] - p.N[j - 1];
                qAhead += (p.Y[j] * dnj / invariant) * bj;
                eAhead -= (p.Ybar[j] * dnj / invariant) * bj;
            }

            var stopCentre = new Vec2(system.Surfaces[stop].DecenterX,
                                      system.Surfaces[stop].DecenterY);
            eAcc = (1.0 / p.Y[stop])
                 * (stopCentre - p.Ybar[stop] * qAhead - p.Y[stop] * eAhead);
        }

        for (int j = 1; j <= last; j++)
        {
            var surf = system.Surfaces[j];
            Scalar c = surf.VertexCurvature;

            // The equivalent tilt: the angle locating this surface's centre of curvature
            // relative to the mechanical axis. A decentre is a tilt about the centre of
            // curvature, so for a spherical surface the two enter only through T + cD, and a
            // tilt about x swings the surface in the y meridian.
            var beta = new Vec2(surf.TiltY + c * surf.DecenterX,
                                surf.TiltX + c * surf.DecenterY);

            // Where the optical axis ray is, and which way it is going, at this surface.
            Scalar u = p.U[j - 1], ubar = p.Ubar[j - 1];
            Scalar y = p.Y[j], ybar = p.Ybar[j];

            var uOar = ubar * qAcc + u * eAcc;
            var yOar = ybar * qAcc + y * eAcc;

            // Its angle of incidence on the local surface, and hence sigma. Kept as the
            // NUMERATOR as well, which carries no division and so stays finite where the chief
            // ray strikes a surface normally.
            reduced[j] = -uOar - c * yOar + beta;

            // And the aspheric cap's own centre, Thompson Eq. (11): where the optical axis ray
            // crosses this surface, relative to the aspheric vertex. A zero-power plate is
            // centred by a POSITION, not by an angle of incidence, which is why this is a
            // different vector from the one above and not a multiple of it.
            if (IsFigured(surf))
                reducedAsph[j] = new Vec2(surf.DecenterX, surf.DecenterY) - yOar;

            // Carry the perturbation forward. A surface that is not moved changes nothing.
            if (surf.IsPerturbed)
            {
                Scalar dn = p.N[j] - p.N[j - 1];
                qAcc += (y * dn / invariant) * beta;
                eAcc -= (ybar * dn / invariant) * beta;
            }
        }

        for (int j = 1; j <= last; j++)
        {
            if (SMath.Abs(ibar[j]) > 1e-12)
            {
                sigma[j] = new Vec2(reduced[j].X / ibar[j], reduced[j].Y / ibar[j]);
            }

            // The aspheric one divides by the CHIEF RAY HEIGHT instead, which vanishes at a
            // stop rather than at normal incidence - a different surface, and a different
            // reason to be careful.
            Scalar ybarj = p.Ybar[j];
            if (SMath.Abs(ybarj) > 1e-12)
                sigmaAsph[j] = new Vec2(reducedAsph[j].X / ybarj, reducedAsph[j].Y / ybarj);
            else if (reduced[j].MagnitudeSquared > 0.0)
            {
                suppressed.Add(j);
            }
        }

        return new SigmaVector(sigma, reduced, i, ibar, g, suppressed.ToArray(),
                               sigmaAsph, reducedAsph);
    }
}
