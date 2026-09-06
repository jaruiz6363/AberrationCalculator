using System;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.RayTrace;

/// <summary>
/// An exact skew ray trace: no series, no small-angle approximation, no aberration theory.
///
/// <para><b>Why this exists.</b> Every traced number in this repository has until now come from
/// outside it, which makes the coefficients checkable only against files someone pasted in. The
/// real purpose is bigger: a transverse aberration polynomial can be INVERTED. Trace real rays,
/// separate the orders by how they scale, and each coefficient falls out on its own rather than
/// collapsed into an RMS spot where errors cancel. Hopkins (JOSA 66, 405) did exactly that
/// against ACCOS V and found agreement to roundoff. See <see cref="CoefficientInversion"/>.</para>
///
/// <para>Object at infinity only. A finite conjugate throws rather than quietly tracing the
/// wrong thing - see the note in the calculator's docs about ArbitraryRay being a placeholder.
/// </para>
/// </summary>
public static class RealRayTrace
{
    /// <summary>Where a traced ray landed, and whether it got there.</summary>
    public readonly record struct Landing(double Y, double Z, bool Ok);

    /// <summary>
    /// Traces one ray to the image plane and returns its intercept.
    ///
    /// <para>The ray is launched at the paraxial entrance pupil, crossing it at the fractional
    /// coordinates given - no ray aiming, which is the convention the traced fans this is
    /// checked against were measured under.</para>
    /// </summary>
    /// <param name="py">Pupil coordinate along y, as a fraction of the pupil radius.</param>
    /// <param name="pz">Pupil coordinate along the perpendicular, same units.</param>
    /// <param name="fieldDeg">Field angle in degrees, in the y-z meridian.</param>
    /// <param name="atParaxialFocus">
    /// Where to catch the ray. True puts it on the paraxial image plane, which is what the
    /// aberration coefficients are referred to and what the traced fans this is checked
    /// against were measured on; false uses the plane the file itself specifies. The two
    /// differ by a real defocus on any design optimised to best focus, and confusing them
    /// shows up as an error strictly linear in the pupil.
    /// </param>
    public static Landing Trace(OpticalSystem system, double[] indices, ParaxialResult paraxial,
                                double fieldDeg, double py, double pz,
                                bool atParaxialFocus = true)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        if (!double.IsInfinity(system.Surfaces[0].Thickness))
            throw new NotSupportedException(
                "RealRayTrace handles an object at infinity only; this system has a finite " +
                "conjugate, and tracing it as though collimated would be silently wrong.");

        int last = system.LastOpticalSurface();
        double epr = 0.5 * paraxial.Epd;
        double alpha = fieldDeg * Math.PI / 180.0;

        // Direction cosines. The field is in the y-z meridian, so the ray tilts in y only.
        double dx = 0.0, dy = Math.Sin(alpha), dz = Math.Cos(alpha);

        // Launch on the entrance pupil plane, which sits EntrancePupilPosition to the right of
        // surface 1, then walk BACK to surface 1's vertex plane so the first transfer below is
        // the ordinary one.
        double ep = paraxial.EntrancePupilPosition;
        double x = pz * epr, y = py * epr, z = ep;
        double back = -z / dz;
        x += back * dx; y += back * dy; z = 0.0;

        return TraceFrom(system, indices, paraxial, x, y, dx, dy, dz, atParaxialFocus);
    }

    /// <summary>
    /// Traces a ray given explicitly at SURFACE 1's VERTEX PLANE, rather than by field angle
    /// and pupil coordinate.
    ///
    /// <para>This is what a finite conjugate needs. The field-and-pupil form above has to
    /// assume the ray is collimated in object space in order to know its direction, which is
    /// true only for an object at infinity; given the ray itself there is nothing left to
    /// assume, and the trace below never asks where the object is.</para>
    ///
    /// <para>Surface 1's vertex plane is also <see cref="Forbes.ForbesTrace"/>'s input base
    /// plane, so a ray expressed for one is expressed for the other without conversion.</para>
    /// </summary>
    /// <param name="x">Sagittal height at surface 1's vertex plane.</param>
    /// <param name="y">Meridional height at surface 1's vertex plane.</param>
    /// <param name="dx">Direction cosines, which need not be normalised.</param>
    public static Landing TraceFrom(OpticalSystem system, double[] indices,
                                    ParaxialResult paraxial,
                                    double x, double y, double dx, double dy, double dz,
                                    bool atParaxialFocus = true)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));

        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-300) return new Landing(0, 0, false);
        dx /= len; dy /= len; dz /= len;

        int last = system.LastOpticalSurface();
        double z = 0.0;
        double nBefore = indices.Length > 0 ? indices[0] : 1.0;

        for (int i = 1; i <= last; i++)
        {
            var s = system.Surfaces[i];
            if (!Intersect(s, ref x, ref y, ref z, dx, dy, dz)) return new Landing(0, 0, false);

            double nAfter = i < indices.Length ? indices[i] : 1.0;
            if (!Refract(s, x, y, nBefore, nAfter, ref dx, ref dy, ref dz))
                return new Landing(0, 0, false);
            nBefore = nAfter;

            // Into the next surface's vertex frame.
            double t = s.Thickness;
            if (double.IsInfinity(t) || double.IsNaN(t)) return new Landing(0, 0, false);
            z -= t;
        }

        // Transfer to the image plane. z is measured from the vertex plane of the surface
        // after the last optical one, so the paraxial focus sits at the difference between the
        // back focal length and the last thickness.
        // ParaxialFocusDistance and not Bfl. Bfl is where a COLLIMATED beam comes to focus,
        // which is the paraxial image only when the object is at infinity; on a 250 mm Cooke
        // triplet the two are 11.76 mm apart, and catching the rays there would be measuring a
        // defocused spot. They are equal to the bit at infinite conjugate, so this changes
        // nothing that was previously reachable - Trace refuses a finite conjugate outright,
        // and only TraceFrom can get here with one.
        double target = atParaxialFocus
            ? paraxial.ParaxialFocusDistance - system.Surfaces[last].Thickness
            : 0.0;
        double tImage = (target - z) / dz;
        return new Landing(y + tImage * dy, x + tImage * dx, true);
    }

    /// <summary>
    /// Advances the ray to its intersection with the surface, by Newton iteration on the sag.
    /// The starting guess is the flat-surface crossing, which is exact for a plane and close
    /// for anything this program handles.
    /// </summary>
    private static bool Intersect(Surface s, ref double x, ref double y, ref double z,
                                  double dx, double dy, double dz)
    {
        if (Math.Abs(dz) < 1e-14) return false;

        double t = -z / dz;
        for (int k = 0; k < 64; k++)
        {
            double xi = x + t * dx, yi = y + t * dy, zi = z + t * dz;
            double r = Math.Sqrt(xi * xi + yi * yi);
            double sag = s.Sag(r);
            if (double.IsNaN(sag)) return false;

            double f = zi - sag;
            if (Math.Abs(f) < 1e-13)
            {
                x = xi; y = yi; z = zi;
                return true;
            }

            // d/dt of (z - sag(r)) = dz - S'(r) * (x dx + y dy) / r
            double sp = SagSlope(s, r);
            double drdt = r > 1e-14 ? (xi * dx + yi * dy) / r : 0.0;
            double d = dz - sp * drdt;
            if (Math.Abs(d) < 1e-14) return false;
            t -= f / d;
        }
        return false;
    }

    /// <summary>dz/dr of the sag: the conic part in closed form, then the polynomial.</summary>
    private static double SagSlope(Surface s, double r)
    {
        double slope = 0.0;
        double c = s.Curvature;
        if (Math.Abs(c) > 1e-15)
        {
            double disc = 1.0 - (1.0 + s.Conic) * c * c * r * r;
            if (disc <= 0.0) return double.NaN;
            slope = c * r / Math.Sqrt(disc);
        }

        var a = s.AsphericCoefficients;
        double rp = r;                                   // r, then r^3, r^5 ...
        for (int k = 0; k < a.Length; k++)
        {
            slope += 2.0 * (k + 1) * a[k] * rp;
            rp *= r * r;
        }
        return slope;
    }

    /// <summary>
    /// Snell's law in vector form. The surface normal comes from the implicit form
    /// F = z - S(r), whose gradient is (-S'(r) x/r, -S'(r) y/r, 1).
    /// </summary>
    private static bool Refract(Surface s, double x, double y, double nBefore, double nAfter,
                                ref double dx, ref double dy, ref double dz)
    {
        if (Math.Abs(nAfter) < 1e-15) return false;

        double r = Math.Sqrt(x * x + y * y);
        double sp = SagSlope(s, r);
        if (double.IsNaN(sp)) return false;

        double nx = 0.0, ny = 0.0, nz = 1.0;
        if (r > 1e-14) { nx = -sp * x / r; ny = -sp * y / r; }
        double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        nx /= len; ny /= len; nz /= len;

        double mu = nBefore / nAfter;
        double cosI = -(dx * nx + dy * ny + dz * nz);
        // Normal must oppose the ray, or the geometry below picks the wrong root.
        if (cosI < 0.0) { nx = -nx; ny = -ny; nz = -nz; cosI = -cosI; }

        double k = 1.0 - mu * mu * (1.0 - cosI * cosI);
        if (k < 0.0) return false;                        // total internal reflection
        double cosT = Math.Sqrt(k);

        dx = mu * dx + (mu * cosI - cosT) * nx;
        dy = mu * dy + (mu * cosI - cosT) * ny;
        dz = mu * dz + (mu * cosI - cosT) * nz;

        double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        dx /= d; dy /= d; dz /= d;
        return true;
    }
}
