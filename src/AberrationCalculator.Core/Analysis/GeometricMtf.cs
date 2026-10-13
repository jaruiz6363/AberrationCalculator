using System;
using System.Collections.Generic;
using System.Linq;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Analysis;

/// <summary>
/// The geometric MTF by Kidger's direct summation (Fundamental Optical Design, SPIE 2002,
/// section 4.5.3): for rays spread evenly over the pupil, landing at (x, y) on the image plane,
///
/// <code>
///   MTF_S(f) = |Σ w exp(-2πi f x)| / Σ w        MTF_T(f) = |Σ w exp(-2πi f y)| / Σ w
/// </code>
///
/// <para>Each is the modulus of the Fourier transform of the spot's line spread function, taken
/// straight from the rays with no binning and no FFT. The modulus discards where the spot is, so
/// no reference point is needed and a spot that only moves keeps its MTF. Only rays that arrive
/// are summed; several wavelengths are summed together, each ray weighted by its wavelength's
/// weight, so lateral colour spreads the spot as it should.</para>
///
/// <para><b>The diffraction limit.</b> As λ → 0 this is exact. At finite λ it overstates a
/// nearly perfect lens, whose spot shrinks to a point and whose geometric MTF stays near 1 to any
/// frequency. Kidger's correction, which LensHH-LT applies too, multiplies by the MTF of a
/// perfect lens of the same aperture: right for large aberrations and for very small ones, and
/// pessimistic in between. <see cref="MtfOptions.DiffractionLimited"/> chooses it (the default).
/// The perfect lens's cutoff, sagittal and tangential, comes from the spread of direction cosines
/// in the arriving beam (<see cref="Cutoffs"/>), so it narrows off axis and where the beam is
/// vignetted.</para>
///
/// <para>The rays come from a <see cref="Tracer"/>, so a caller with its own aiming and apertures -
/// a wavefront calculation whose rays the MTF should share - passes its own. <see cref="DefaultTracer"/>
/// aims as the file asks and stops rays at fixed semi-diameters and obscurations.</para>
/// </summary>
public static class GeometricMtf
{
    /// <summary>One ray at the image plane: where it landed and its direction cosines there.</summary>
    public readonly record struct ImageRay(bool Ok, double X, double Y, double L, double M, double N);

    /// <summary>
    /// Traces the ray of wavelength <paramref name="wavelength"/> (an index into the system's
    /// wavelengths) and field <paramref name="field"/> (in the system's field units) through the
    /// pupil point (<paramref name="py"/>, <paramref name="px"/>), meridional then sagittal, as
    /// fractions of the pupil's radius.
    /// </summary>
    public delegate ImageRay Tracer(double field, int wavelength, double py, double px);

    /// <summary>A ray's landing point on the image plane, and its weight in the sums.</summary>
    public readonly record struct Spot(double X, double Y, double Weight);

    /// <summary>The MTF in the sagittal (x) and tangential (y) directions.</summary>
    public readonly record struct Modulation(double Sagittal, double Tangential);

    /// <summary>
    /// Kidger's ray pattern (his Fig. 4.25): <paramref name="rings"/> rings of equal area, each
    /// cut into the same number of equal sectors, one ray at the centre of each cell - so every ray
    /// stands for the same area of the pupil. Each ring has 2 × rings sectors, rounded up to a
    /// multiple of 4, so the pattern is symmetric about both axes and on axis the sagittal and
    /// tangential MTF agree. Unlike Kidger, the whole pupil is traced: the sums are not symmetric
    /// in x for an off-axis coma, even when the lens is.
    /// </summary>
    public static (double Py, double Px)[] Pupil(int rings)
    {
        if (rings < 1) throw new ArgumentOutOfRangeException(nameof(rings), "at least one ring");
        int arms = 2 * rings;
        if (arms % 4 != 0) arms += 4 - arms % 4;
        var points = new (double, double)[rings * arms];
        int k = 0;
        for (int ring = 0; ring < rings; ring++)
        {
            double rho = Math.Sqrt((ring + 0.5) / rings);
            for (int arm = 0; arm < arms; arm++)
            {
                double theta = 2.0 * Math.PI * (arm + 0.5) / arms;
                points[k++] = (rho * Math.Sin(theta), rho * Math.Cos(theta));
            }
        }
        return points;
    }

    /// <summary>The geometric MTF of a spot at one spatial frequency, in cycles per unit of x and y.</summary>
    public static Modulation Geometric(IReadOnlyList<Spot> spots, double frequency)
    {
        if (spots == null) throw new ArgumentNullException(nameof(spots));
        double sr = 0.0, si = 0.0, tr = 0.0, ti = 0.0, total = 0.0;
        double k = -2.0 * Math.PI * frequency;
        foreach (var s in spots)
        {
            double ps = k * s.X, pt = k * s.Y;
            sr += s.Weight * Math.Cos(ps); si += s.Weight * Math.Sin(ps);
            tr += s.Weight * Math.Cos(pt); ti += s.Weight * Math.Sin(pt);
            total += s.Weight;
        }
        if (!(total > 0.0)) return new Modulation(double.NaN, double.NaN);
        return new Modulation(Math.Sqrt(sr * sr + si * si) / total, Math.Sqrt(tr * tr + ti * ti) / total);
    }

    /// <summary>
    /// The MTF of a perfect lens with a circular pupil, or an annular one whose central obscuration
    /// is <paramref name="obscuration"/> of its radius, at <paramref name="frequency"/> for a
    /// cutoff of <paramref name="cutoff"/> (O'Neill 1956). An elliptical pupil has the circle's MTF
    /// along each axis with that axis's cutoff, which is what lets an off-axis beam use it.
    /// </summary>
    public static double DiffractionLimit(double frequency, double cutoff, double obscuration = 0.0)
    {
        if (!(cutoff > 0.0)) return 0.0;
        double v = Math.Abs(frequency) / cutoff;
        if (v >= 1.0) return 0.0;
        double e = Math.Min(Math.Max(obscuration, 0.0), 0.999999);
        double a = Circle(v);
        if (e == 0.0) return a;
        double e2 = e * e;
        double b = v < e ? e2 * Circle(v / e) : 0.0;
        double c;
        if (v <= (1.0 - e) / 2.0) c = -2.0 * e2;
        else if (v >= (1.0 + e) / 2.0) c = 0.0;
        else
        {
            double phi = Math.Acos(Math.Min(Math.Max((1.0 + e2 - 4.0 * v * v) / (2.0 * e), -1.0), 1.0));
            c = 2.0 * e / Math.PI * Math.Sin(phi) + (1.0 + e2) / Math.PI * phi
                - 2.0 * (1.0 - e2) / Math.PI * Math.Atan((1.0 + e) / (1.0 - e) * Math.Tan(phi / 2.0)) - 2.0 * e2;
        }
        return Math.Max(0.0, (a + b + c) / (1.0 - e2));
    }

    private static double Circle(double v) =>
        v >= 1.0 ? 0.0 : 2.0 / Math.PI * (Math.Acos(v) - v * Math.Sqrt(1.0 - v * v));

    /// <summary>
    /// The extent of the arriving beam in direction-cosine space, times the image index: the
    /// diameter of the pupil, sagittal and tangential, as the image plane sees it. Divided by a
    /// vacuum wavelength it is the perfect lens's cutoff in cycles per unit length on the image
    /// plane, since direction cosines are the Fourier conjugates of image-plane position; on axis
    /// it is 2 NA. Each rim is found along the pupil's axes: the ray at the rim, or if that is
    /// stopped, the outermost ray that arrives. Also the central obscuration, as a fraction of the
    /// pupil's sagittal radius, from the innermost ray that arrives.
    /// </summary>
    public static (double Sagittal, double Tangential, double Obscuration) Cutoffs(Tracer tracer, double field, int wavelength, double imageIndex)
    {
        if (tracer == null) throw new ArgumentNullException(nameof(tracer));
        ImageRay? Rim(double cy, double cx)
        {
            var ray = tracer(field, wavelength, cy, cx);
            if (ray.Ok) return ray;
            double lo = 0.0, hi = 1.0;
            ImageRay? best = null;
            for (int i = 0; i < 30; i++)
            {
                double mid = 0.5 * (lo + hi);
                var r = tracer(field, wavelength, mid * cy, mid * cx);
                if (r.Ok) { best = r; lo = mid; } else hi = mid;
            }
            return best;
        }
        var (up, down, right, left) = (Rim(1.0, 0.0), Rim(-1.0, 0.0), Rim(0.0, 1.0), Rim(0.0, -1.0));
        double t = up is { } u && down is { } d ? Math.Abs(u.M - d.M) * imageIndex : 0.0;
        double s = right is { } r1 && left is { } l1 ? Math.Abs(r1.L - l1.L) * imageIndex : 0.0;

        double obscuration = 0.0;
        if (!tracer(field, wavelength, 0.0, 0.0).Ok && right is { })
        {
            double lo = 0.0, hi = 1.0;
            for (int i = 0; i < 30; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (tracer(field, wavelength, 0.0, mid).Ok) hi = mid; else lo = mid;
            }
            obscuration = hi;
        }
        return (s, t, obscuration);
    }

    /// <summary>
    /// The geometric MTF of one field at the given frequencies, in cycles per unit length of the
    /// image plane (cycles/mm for a lens in millimetres). Afocal systems are not supported.
    /// </summary>
    /// <param name="indices">Each wavelength's refractive indices, indexed like the surfaces.</param>
    /// <param name="paraxial">The primary wavelength's paraxial trace, which places the pupil.</param>
    /// <param name="tracer">How rays are traced; <see cref="DefaultTracer"/> if null.</param>
    public static MtfResult Compute(OpticalSystem system, IReadOnlyList<double[]> indices, ParaxialResult paraxial,
                                    double field, IReadOnlyList<double> frequencies, MtfOptions? options = null,
                                    Tracer? tracer = null)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (frequencies == null) throw new ArgumentNullException(nameof(frequencies));
        if (system.IsAfocal) throw new NotSupportedException("the geometric MTF of an afocal system is not supported");
        if (indices.Count != system.Wavelengths.Count) throw new ArgumentException("one index array per wavelength", nameof(indices));
        options ??= new MtfOptions();
        tracer ??= DefaultTracer(system, indices, paraxial);

        int primary = Math.Max(0, system.PrimaryWavelengthIndex);
        var waves = options.Wavelength is int only ? new[] { only } : Enumerable.Range(0, system.Wavelengths.Count).ToArray();
        double weightSum = waves.Sum(w => system.Wavelengths[w].Weight);
        if (!(weightSum > 0.0)) throw new ArgumentException("the wavelengths have no weight");

        var pupil = Pupil(options.Rings);
        var spots = new List<Spot>(pupil.Length * waves.Length);
        foreach (int w in waves)
        {
            double weight = system.Wavelengths[w].Weight / weightSum;
            foreach (var (py, px) in pupil)
            {
                var ray = tracer(field, w, py, px);
                if (ray.Ok) spots.Add(new Spot(ray.X, ray.Y, weight));
            }
        }

        // The beam's extent is the aperture's, so it is measured once, at the primary wavelength,
        // and each wavelength's cutoff is that over its own wavelength.
        double imageIndex = Math.Abs(indices[primary][system.LastOpticalSurface()]);
        var (extentS, extentT, obscuration) = Cutoffs(tracer, field, primary, imageIndex);
        double Cutoff(double extent, int w) => extent / (system.Wavelengths[w].Value * 1e-3);

        var geometric = new Modulation[frequencies.Count];
        var limit = new Modulation[frequencies.Count];
        var mtf = new Modulation[frequencies.Count];
        for (int i = 0; i < frequencies.Count; i++)
        {
            double f = frequencies[i];
            geometric[i] = spots.Count == 0 ? new Modulation(double.NaN, double.NaN) : Geometric(spots, f);
            double ls = 0.0, lt = 0.0;
            foreach (int w in waves)
            {
                double weight = system.Wavelengths[w].Weight / weightSum;
                ls += weight * DiffractionLimit(f, Cutoff(extentS, w), obscuration);
                lt += weight * DiffractionLimit(f, Cutoff(extentT, w), obscuration);
            }
            limit[i] = new Modulation(ls, lt);
            mtf[i] = options.DiffractionLimited
                ? new Modulation(geometric[i].Sagittal * ls, geometric[i].Tangential * lt)
                : geometric[i];
        }
        return new MtfResult(field, frequencies.ToArray(), mtf, geometric, limit,
                             Cutoff(extentS, primary), Cutoff(extentT, primary), obscuration,
                             pupil.Length * waves.Length, spots.Count);
    }

    /// <summary>
    /// <see cref="Compute(OpticalSystem, IReadOnlyList{double[]}, ParaxialResult, double, IReadOnlyList{double}, MtfOptions?, Tracer?)"/>
    /// with the indices from <paramref name="catalog"/> and the paraxial trace done here.
    /// </summary>
    public static MtfResult Compute(OpticalSystem system, GlassCatalog catalog, double field,
                                    IReadOnlyList<double> frequencies, MtfOptions? options = null)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        var indices = system.Wavelengths.Select(w => IndexResolver.Build(system, catalog, w.Value)).ToArray();
        int primary = Math.Max(0, system.PrimaryWavelengthIndex);
        double maxField = system.Fields.Count == 0 ? 0.0 : system.Fields.Max(f => Math.Abs(f.Y));
        var paraxial = ParaxialTrace.Trace(system, indices[primary], maxField);
        return Compute(system, indices, paraxial, field, frequencies, options);
    }

    /// <summary>
    /// The usual tracer. Rays are launched at the primary wavelength's paraxial entrance pupil, or,
    /// when the file asks for ray aiming, aimed at the real stop that the primary wavelength's
    /// axial beam fills (<see cref="StopAimer"/>), the pupil coordinates then being fractions of
    /// its radius. A ray outside a fixed semi-diameter (times the clear aperture), or inside an
    /// obscuration, is stopped; semi-diameters left to follow the beam stop nothing.
    /// </summary>
    public static Tracer DefaultTracer(OpticalSystem system, IReadOnlyList<double[]> indices, ParaxialResult paraxial)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        int primary = Math.Max(0, system.PrimaryWavelengthIndex);
        int stop = system.StopSurfaceIndex, last = system.LastOpticalSurface(), image = system.Surfaces.Count - 1;

        StopAimer?[] aimers = new StopAimer?[indices.Count];
        bool aim = system.RayAiming != RayAimingMode.Off && stop >= 1 && stop <= last;
        if (aim)
        {
            double height = StopAimer.RealAxialHeight(system, indices[primary], paraxial) ?? paraxial.Y[stop];
            for (int w = 0; w < indices.Count; w++)
                aimers[w] = StopAimer.ForSystem(system, indices[w], paraxial, stopHeight: height);
        }

        var sd = new double[system.Surfaces.Count];
        for (int i = 1; i <= last; i++)
        {
            var s = system.Surfaces[i];
            if (i != stop && s.SemiDiameter > 0.0 && s.SemiDiameterMode == SemiDiameterMode.Fixed)
                sd[i] = s.SemiDiameter * Math.Max(0.0, s.ClearAperturePercent) / 100.0;
        }

        return (field, w, py, px) =>
        {
            double ly = py, lx = px;
            if (aim)
            {
                if (aimers[w]!.Launch(field, py, px) is not (double ay, double ax)) return default;
                (ly, lx) = (ay, ax);
            }
            RealRayTrace.SurfaceHit[] hits;
            try { hits = RealRayTrace.TraceRecord(system, indices[w], paraxial, field, ly, lx, atParaxialFocus: false); }
            catch (InvalidOperationException) { return default; }
            var end = hits[image];
            if (!end.Ok) return default;
            for (int i = 1; i < image; i++)
            {
                double r = Math.Sqrt(hits[i].X * hits[i].X + hits[i].Y * hits[i].Y);
                if (sd[i] > 0.0 && r > sd[i] * (1.0 + 1e-9)) return default;
                double obscuration = system.Surfaces[i].ObscurationRadius;
                if (obscuration > 0.0 && r < obscuration) return default;
            }
            return new ImageRay(true, end.X, end.Y, end.L, end.M, end.N);
        };
    }
}

/// <summary>How <see cref="GeometricMtf.Compute(OpticalSystem, IReadOnlyList{double[]}, ParaxialResult, double, IReadOnlyList{double}, MtfOptions?, GeometricMtf.Tracer?)"/> samples and corrects.</summary>
public sealed record MtfOptions
{
    /// <summary>Rings of Kidger's pattern; the rays are 2 rings² or a little more, per wavelength.</summary>
    public int Rings { get; init; } = 15;

    /// <summary>Multiply by a perfect lens's MTF (Kidger's correction), as LensHH-LT does.</summary>
    public bool DiffractionLimited { get; init; } = true;

    /// <summary>One wavelength, by index; every wavelength, weighted, if null.</summary>
    public int? Wavelength { get; init; }
}

/// <summary>The geometric MTF of one field.</summary>
/// <param name="Mtf">The MTF reported: the geometric, times the diffraction limit if asked for.</param>
/// <param name="Geometric">The geometric MTF alone.</param>
/// <param name="DiffractionLimit">A perfect lens's MTF with the same aperture, weighted over the wavelengths.</param>
/// <param name="CutoffSagittal">The perfect lens's cutoff at the primary wavelength, sagittal.</param>
/// <param name="CutoffTangential">The same, tangential.</param>
/// <param name="Obscuration">The central obscuration as a fraction of the pupil's radius.</param>
/// <param name="RaysTraced">Rays launched, over every wavelength.</param>
/// <param name="RaysArrived">Of them, those that reached the image.</param>
public sealed record MtfResult(double Field, IReadOnlyList<double> Frequencies,
                               IReadOnlyList<GeometricMtf.Modulation> Mtf,
                               IReadOnlyList<GeometricMtf.Modulation> Geometric,
                               IReadOnlyList<GeometricMtf.Modulation> DiffractionLimit,
                               double CutoffSagittal, double CutoffTangential, double Obscuration,
                               int RaysTraced, int RaysArrived);
