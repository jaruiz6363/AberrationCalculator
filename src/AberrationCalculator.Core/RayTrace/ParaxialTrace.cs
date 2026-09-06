using System;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.RayTrace;

/// <summary>
/// The paraxial marginal and chief rays through a system, plus the first-order quantities
/// that come with them.
///
/// Everything the aberration coefficients need arrives here: at each surface the two rays
/// give (y, u) and (ybar, ubar), and those four numbers with the refractive indices are
/// what the third-, fifth- and seventh-order sums are built from. The pupils, focal length
/// and F/number fall out of the same trace rather than being computed separately, so they
/// cannot disagree with the rays.
/// </summary>
public sealed class ParaxialResult
{
    /// <summary>Marginal-ray height at each surface, indexed like <see cref="OpticalSystem.Surfaces"/>.</summary>
    public double[] Y { get; init; } = Array.Empty<double>();

    /// <summary>Marginal-ray slope in the medium AFTER each surface.</summary>
    public double[] U { get; init; } = Array.Empty<double>();

    /// <summary>Chief-ray height at each surface.</summary>
    public double[] Ybar { get; init; } = Array.Empty<double>();

    /// <summary>Chief-ray slope in the medium after each surface.</summary>
    public double[] Ubar { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Refractive index of the medium after each surface, signed: the sign flips at every
    /// mirror so that a folded system traces with the same recurrence as a straight one.
    /// </summary>
    public double[] N { get; init; } = Array.Empty<double>();

    /// <summary>Effective focal length.</summary>
    public double Efl { get; init; }

    /// <summary>Back focal length: last surface to the paraxial focus of a collimated beam.</summary>
    public double Bfl { get; init; }

    /// <summary>Entrance pupil diameter, whichever way the file stated its aperture.</summary>
    public double Epd { get; init; }

    /// <summary>Entrance pupil position, measured from the first surface, positive to the right.</summary>
    public double EntrancePupilPosition { get; init; }

    /// <summary>
    /// Exit pupil position, measured from the IMAGE plane and positive to the right - the
    /// convention every program this one reads files from uses when it reports the number,
    /// so the two can be compared directly.
    /// </summary>
    public double ExitPupilPosition { get; init; }

    /// <summary>
    /// Exit pupil position measured from the last surface instead. Same pupil; this is the
    /// reference the aberration sums work in, and it avoids re-deriving it there.
    /// </summary>
    public double ExitPupilFromLastSurface { get; init; }

    /// <summary>Exit pupil diameter.</summary>
    public double ExitPupilDiameter { get; init; }

    /// <summary>Image-space F/number, EFL/EPD for an object at infinity.</summary>
    public double FNumber { get; init; }

    /// <summary>Chief-ray height at the image surface: the paraxial image height of this field.</summary>
    public double ImageHeight { get; init; }

    /// <summary>
    /// Distance from the last surface to where the paraxial marginal ray crosses the axis.
    /// This is where the image actually is, which need not be where the file put its image
    /// surface - a design saved at best focus for one conjugate and then used at another
    /// will differ, and the gap between this and the image surface is that defocus.
    /// </summary>
    public double ParaxialFocusDistance { get; init; }

    /// <summary>
    /// Chief-ray height at the paraxial focus. Programs that report a "paraxial image
    /// height" for a finite conjugate mean this one; <see cref="ImageHeight"/> is the same
    /// ray measured at the image surface the file defines.
    /// </summary>
    public double ParaxialImageHeight { get; init; }

    /// <summary>
    /// Transverse magnification, from the marginal ray as n*u / (n'*u'). A property of
    /// the conjugates, so it is defined for any finite-conjugate system whether or not it
    /// has an off-axis field and however its fields are stated. Zero for an object at
    /// infinity, where magnification does not apply.
    /// </summary>
    public double Magnification { get; init; }

    /// <summary>
    /// Lagrange invariant, n(ubar*y - u*ybar). Conserved by the paraxial equations, so a
    /// varying value would mean the trace is wrong - see <see cref="InvariantDrift"/>.
    /// </summary>
    public double LagrangeInvariant { get; init; }

    /// <summary>
    /// Largest relative departure of the Lagrange invariant from its object-space value
    /// across the system. A self-check on the trace: it should sit at rounding level.
    /// </summary>
    public double InvariantDrift { get; init; }

    /// <summary>True when the object is at infinity.</summary>
    public bool InfiniteConjugate { get; init; }
}

/// <summary>Traces the two paraxial reference rays.</summary>
public static class ParaxialTrace
{
    /// <summary>Object distances at or beyond this are treated as infinite.</summary>
    private const double InfiniteObject = 1e12;

    /// <summary>
    /// Traces the marginal and chief rays for one field point.
    /// </summary>
    /// <param name="system">The lens. Surface 0 is the object, the last surface the image.</param>
    /// <param name="n">Index after each surface, from <c>IndexResolver.Build</c> at one wavelength.</param>
    /// <param name="field">
    /// The field point, in the units <see cref="OpticalSystem.FieldType"/> names: degrees of
    /// object angle, or object height in lens units.
    /// </param>
    public static ParaxialResult Trace(OpticalSystem system, double[] n, double field)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (n == null) throw new ArgumentNullException(nameof(n));

        int count = system.Surfaces.Count;
        if (count < 2) throw new InvalidOperationException("A system needs at least an object and an image surface.");
        if (n.Length < count) throw new ArgumentException("Index array is shorter than the surface list.", nameof(n));

        // Reflection is carried in the sign of the index rather than by turning the geometry
        // around: after an odd number of mirrors the ray runs the other way, and negating n
        // (against the already-negative thicknesses such a file stores) reproduces that
        // exactly while leaving one recurrence to trace forwards.
        var ns = new double[count];
        double sign = 1.0;
        for (int i = 0; i < count; i++)
        {
            if (system.Surfaces[i].IsMirror) sign = -sign;
            ns[i] = sign * Math.Abs(n[i]);
        }

        int last = system.LastOpticalSurface();
        int stop = system.StopSurfaceIndex;
        if (stop < 1 || stop > last) stop = last;      // no stop marked: the last surface bounds the beam

        double objectThickness = system.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectThickness) || Math.Abs(objectThickness) >= InfiniteObject;
        double t0 = infinite ? 0.0 : Math.Abs(objectThickness);

        // Two basis rays span every paraxial ray, so each ray this method needs is found by
        // solving a linear combination rather than by aiming and iterating.
        var basisA = Propagate(system, ns, 1.0, 0.0, last);     // unit height, parallel
        var basisB = Propagate(system, ns, 0.0, 1.0, last);     // on axis, unit slope

        // The chief ray crosses the axis at the stop; where a ray must have come from in
        // object space to do that is the entrance pupil.
        double aStop = basisA.Y[stop], bStop = basisB.Y[stop];
        double entrancePupil = Math.Abs(aStop) > 1e-15 ? bStop / aStop : 0.0;

        // Focal length from the system's POWER: EFL = n_object / phi, with
        // phi = -n' u' / y. Written with the reduced angle n'u' rather than u' alone, this
        // is the number every design program prints, and the two agree exactly whenever
        // image space is air. They part company on a design whose last medium is glass -
        // -y/u' would then report the rear focal length in that glass, a factor n' larger.
        double omegaLast = ns[last] * basisA.U[last];
        double efl = Math.Abs(omegaLast) > 1e-15 ? -ns[0] / omegaLast : double.PositiveInfinity;
        double bfl = Math.Abs(basisA.U[last]) > 1e-15 ? -basisA.Y[last] / basisA.U[last] : double.PositiveInfinity;

        double epd = EntrancePupilDiameter(system, efl, t0, entrancePupil, infinite, ns[0]);

        // Marginal ray: from the axial object point to the edge of the entrance pupil. With
        // the object at infinity that is simply a parallel ray at the pupil edge.
        double yMarg, uMarg;
        if (infinite)
        {
            yMarg = 0.5 * epd;
            uMarg = 0.0;
        }
        else
        {
            double objectToPupil = t0 + entrancePupil;
            uMarg = Math.Abs(objectToPupil) > 1e-15 ? 0.5 * epd / objectToPupil : 0.0;
            yMarg = uMarg * t0;
        }

        // Chief ray: from the edge of the field through the centre of the entrance pupil.
        double yChief, uChief;
        if (!infinite && system.FieldType == FieldType.ObjectHeight)
        {
            double objectToPupil = t0 + entrancePupil;
            uChief = Math.Abs(objectToPupil) > 1e-15 ? -field / objectToPupil : 0.0;
            yChief = field + uChief * t0;
        }
        else
        {
            uChief = Math.Tan(field * Math.PI / 180.0);
            yChief = -uChief * entrancePupil;
        }

        var marginal = Propagate(system, ns, yMarg, uMarg, count - 1);
        var chief = Propagate(system, ns, yChief, uChief, count - 1);

        // The invariant is conserved by the paraxial equations, so tracking it costs nothing
        // and catches a bad index array or a mis-signed mirror.
        double h0 = ns[0] * (chief.U[0] * marginal.Y[1] - marginal.U[0] * chief.Y[1]);
        double drift = 0.0;
        if (Math.Abs(h0) > 1e-15)
        {
            for (int i = 1; i <= last; i++)
            {
                double hi = ns[i] * (chief.U[i] * marginal.Y[i] - marginal.U[i] * chief.Y[i]);
                drift = Math.Max(drift, Math.Abs((hi - h0) / h0));
            }
        }

        // The pupil is found relative to the last surface, then reported relative to the
        // image plane, which is where every program that prints this number measures from.
        double lastToImage = 0.0;
        for (int i = last; i < count - 1; i++)
        {
            double t = system.Surfaces[i].Thickness;
            if (!double.IsInfinity(t) && !double.IsNaN(t)) lastToImage += t;
        }
        // The exit pupil is the image of the stop: a property of the lens, not of the field.
        // Deriving it from the traced chief ray would lose it on a design whose only field
        // is on axis, where that ray is identically zero - so it comes from a pupil ray of
        // unit slope aimed through the stop, which exists whatever the fields are.
        var pupilRay = Propagate(system, ns, -entrancePupil, 1.0, last);
        double exitPupil = Math.Abs(pupilRay.U[last]) > 1e-15
            ? -pupilRay.Y[last] / pupilRay.U[last]
            : double.PositiveInfinity;

        // Its diameter is the marginal ray's own height where that pupil sits.
        double exitPupilDia = double.IsInfinity(exitPupil)
            ? double.PositiveInfinity
            : 2.0 * Math.Abs(marginal.Y[last] + marginal.U[last] * exitPupil);
        // Where the image actually forms, and the chief-ray height there.
        double focusDistance = Math.Abs(marginal.U[last]) > 1e-15
            ? -marginal.Y[last] / marginal.U[last]
            : double.PositiveInfinity;
        double paraxialImageHeight = double.IsInfinity(focusDistance)
            ? chief.Y[count - 1]
            : chief.Y[last] + chief.U[last] * focusDistance;
        // Transverse magnification from the MARGINAL ray: m = n*u / (n'*u').
        //
        // Magnification is a property of the conjugates, not of the field. Deriving it
        // from the chief ray as (image height / object height) needs a non-zero object
        // height to divide by, so it collapses to nothing on an on-axis-only design and
        // on any system whose fields are stated as angles - both of which are finite
        // conjugates with a perfectly well-defined magnification. The marginal-ray form
        // has no such dependency and agrees with the chief-ray ratio wherever that ratio
        // is computable.
        double magnification = 0.0;
        if (!infinite)
        {
            double omegaObject = ns[0] * marginal.U[0];
            double omegaImage  = ns[last] * marginal.U[last];
            if (Math.Abs(omegaImage) > 1e-15) magnification = omegaObject / omegaImage;
        }

        return new ParaxialResult
        {
            Y = marginal.Y,
            U = marginal.U,
            Ybar = chief.Y,
            Ubar = chief.U,
            N = ns,
            Efl = efl,
            Bfl = bfl,
            Epd = epd,
            EntrancePupilPosition = entrancePupil,
            ExitPupilPosition = double.IsInfinity(exitPupil) ? exitPupil : exitPupil - lastToImage,
            ExitPupilFromLastSurface = exitPupil,
            ExitPupilDiameter = exitPupilDia,
            FNumber = Math.Abs(epd) > 1e-15 ? efl / epd : double.PositiveInfinity,
            ImageHeight = chief.Y[count - 1],
            ParaxialFocusDistance = focusDistance,
            ParaxialImageHeight = paraxialImageHeight,
            Magnification = magnification,
            LagrangeInvariant = h0,
            InvariantDrift = drift,
            InfiniteConjugate = infinite,
        };
    }

    /// <summary>
    /// The entrance pupil diameter, whichever way the file chose to state the aperture.
    /// F/number and object-space NA are both converted here so that everything downstream
    /// sees one quantity.
    /// </summary>
    private static double EntrancePupilDiameter(OpticalSystem system, double efl, double t0,
                                                double entrancePupil, bool infinite, double n0)
    {
        switch (system.Aperture.Type)
        {
            case ApertureType.FNumber:
                double fno = system.Aperture.Value;
                return fno > 1e-12 ? Math.Abs(efl) / fno : 0.0;

            case ApertureType.ObjectSpaceNA:
                // NA = n sin(theta); paraxially the marginal slope is NA/n, and the pupil it
                // fills is that slope carried from the object to the entrance pupil.
                if (infinite) return 0.0;            // an object-space NA means nothing from infinity
                double u = system.Aperture.Value / Math.Abs(n0 == 0.0 ? 1.0 : n0);
                return 2.0 * u * (t0 + entrancePupil);

            case ApertureType.EPD:
            default:
                return system.Aperture.Value;
        }
    }

    /// <summary>
    /// Runs the paraxial recurrence from object space through surface
    /// <paramref name="through"/>, starting from a ray at height <paramref name="y1"/> on
    /// the first surface with object-space slope <paramref name="u0"/>.
    ///
    /// Reduced angles (omega = n*u) are used inside the loop because refraction is then a
    /// subtraction and transfer a multiplication, with no division by an index a mirror may
    /// have made negative. The slopes returned are ordinary u = omega/n.
    /// </summary>
    private static (double[] Y, double[] U) Propagate(OpticalSystem system, double[] ns,
                                                      double y1, double u0, int through)
    {
        int count = system.Surfaces.Count;
        var y = new double[count];
        var u = new double[count];

        // The surface-0 entries describe the ray in object space, which is what the
        // aberration sums and the invariant check expect to find there.
        u[0] = u0;
        y[0] = double.IsInfinity(system.Surfaces[0].Thickness)
            ? 0.0
            : y1 - u0 * Math.Abs(system.Surfaces[0].Thickness);

        double omega = ns[0] * u0;
        if (count > 1) y[1] = y1;

        int limit = Math.Min(through, count - 1);
        for (int i = 1; i <= limit; i++)
        {
            double power = SurfacePower(system.Surfaces[i], ns[i - 1], ns[i]);

            omega -= y[i] * power;
            u[i] = omega / ns[i];

            if (i + 1 < count)
            {
                double t = system.Surfaces[i].Thickness;
                if (double.IsInfinity(t) || double.IsNaN(t)) t = 0.0;
                y[i + 1] = y[i] + t * u[i];
            }
        }

        // Past the requested surface nothing refracts, so the ray keeps its last state and
        // callers that asked only for the optical part still get a fully filled array.
        for (int i = limit + 1; i < count; i++) u[i] = u[limit];

        return (y, u);
    }

    /// <summary>
    /// Refracting power of one surface. A real surface takes it from its curvature and the
    /// index step; an ideal paraxial surface states it directly as a focal length; the
    /// index-transparent surface types have none.
    /// </summary>
    private static double SurfacePower(Surface s, double nBefore, double nAfter)
    {
        switch (s.Type)
        {
            case SurfaceType.Paraxial:
                return Math.Abs(s.FocalLength) > 1e-15 ? nBefore / s.FocalLength : 0.0;

            case SurfaceType.CoordinateBreak:
            case SurfaceType.Abcd:
            case SurfaceType.Unsupported:
                return 0.0;

            default:
                return s.VertexCurvature * (nAfter - nBefore);
        }
    }
}
