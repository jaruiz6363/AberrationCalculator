extern alias Ad;

using System;
using System.Collections.Generic;

using AdM = Ad::AberrationCalculator.Core.Models;
using AdR = Ad::AberrationCalculator.Core.RayTrace;
using AdA = Ad::AberrationCalculator.Core.Aberrations;
using Dual = Ad::AberrationCalculator.Core.Ad.Dual;

namespace AberrationCalculator.Optimize.Evaluation;

/// <summary>
/// One pass of the aberration chain, seeded on one variable, with everything an operand might
/// ask of it computed on demand and kept.
///
/// <para>A probe is cheap to make and expensive to interrogate, so nothing is computed until
/// something asks for it. Two operands wanting the coefficients at the same wavelength get the
/// same Buchdahl run; two wanting the same ray get the same trace. A probe is built per
/// variable and thrown away, so the caching is per column of the Jacobian - which is exactly
/// the scope over which those answers are the same.</para>
///
/// <para>Every quantity that comes out is a <see cref="Dual"/>: the value, and its exact
/// derivative with respect to the one variable this probe was seeded on.</para>
/// </summary>
public sealed class DesignProbe
{
    private readonly AdM.OpticalSystem _sys;
    private readonly Dual[][] _indices;          // [wavelength][surface]
    private readonly double _maxField;
    private readonly int _primary;

    private readonly Dictionary<long, AdR.ParaxialResult> _paraxial = new();
    private readonly Dictionary<int, AdA.BuchdahlTerms> _coefficients = new();
    private readonly Dictionary<long, AdR.RealRayTrace.SurfaceHit[]> _rays = new();
    private Dual[]? _apertureRadius;

    // The plain-double design, present only on a probe that has been asked for VALUES and no
    // derivative. When it is here the chain runs in ordinary arithmetic and the results are
    // lifted into duals with zero derivatives - see DoubleToDual for why that is worth doing.
    private readonly Core.Models.OpticalSystem? _core;
    private readonly double[][]? _coreIndices;

    public DesignProbe(AdM.OpticalSystem system, Dual[][] indices, double maxField,
                       int primaryWavelength)
    {
        _sys = system ?? throw new ArgumentNullException(nameof(system));
        _indices = indices ?? throw new ArgumentNullException(nameof(indices));
        _maxField = maxField;
        _primary = primaryWavelength;
    }

    /// <summary>
    /// A probe for a pass that wants values and no derivative.
    ///
    /// <para>It still answers in dual numbers, because the operands are written once and read
    /// one set of types; every derivative it returns is zero, which is the truth for a pass that
    /// was seeded on nothing.</para>
    /// </summary>
    public DesignProbe(AdM.OpticalSystem system, Dual[][] indices, double maxField,
                       int primaryWavelength,
                       Core.Models.OpticalSystem core, double[][] coreIndices)
        : this(system, indices, maxField, primaryWavelength)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
        _coreIndices = coreIndices ?? throw new ArgumentNullException(nameof(coreIndices));
    }

    public AdM.OpticalSystem System => _sys;

    /// <summary>Number of wavelengths in the evaluation set; at least one.</summary>
    public int WaveCount => _indices.Length;

    /// <summary>Index of the reference wavelength.</summary>
    public int PrimaryWave => _primary;

    /// <summary>The largest field the design defines, in the units its field type names.</summary>
    public double MaxField => _maxField;

    public int LastOpticalSurface => _sys.LastOpticalSurface();

    public int ImageSurface => _sys.Surfaces.Count - 1;

    /// <summary>The paraxial trace at one wavelength, taken at the maximum field.</summary>
    public AdR.ParaxialResult Paraxial(int wave) => Paraxial(wave, _maxField);

    /// <summary>The paraxial trace at one wavelength and one field.</summary>
    public AdR.ParaxialResult Paraxial(int wave, double field)
    {
        long key = ((long)Clamp(wave) << 32) ^ (uint)field.GetHashCode();
        if (_paraxial.TryGetValue(key, out var cached)) return cached;

        var p = _core != null
            ? DoubleToDual.Paraxial(Core.RayTrace.ParaxialTrace.Trace(
                  _core, _coreIndices![Clamp(wave)], field))
            : AdR.ParaxialTrace.Trace(_sys, _indices[Clamp(wave)], field);

        _paraxial[key] = p;
        return p;
    }

    /// <summary>
    /// The thirty-seven transverse aberration coefficients at one wavelength, by Buchdahl's
    /// computing scheme.
    ///
    /// <para>There is no route to choose here and no test for figuring, because a design that
    /// would need either never reaches this far - <see cref="SphericalOnly"/> refuses it before
    /// the first evaluation. That is the point of the restriction. This is the innermost loop of
    /// the optimiser, run once per variable per iteration, and a question settled once outside it
    /// is a question not asked tens of thousands of times inside it.</para>
    /// </summary>
    public AdA.BuchdahlTerms Coefficients(int wave)
    {
        int w = Clamp(wave);
        if (_coefficients.TryGetValue(w, out var cached)) return cached;

        // Nine tenths of an evaluation is the tertiary scheme below. On a value-only probe it
        // runs in ordinary arithmetic, which measured ten times faster, and is lifted after.
        if (_core != null)
        {
            var pd = Core.RayTrace.ParaxialTrace.Trace(_core, _coreIndices![w], _maxField);
            var bd = Core.Aberrations.BuchdahlCoefficients.Compute(_core, pd);
            Core.Aberrations.TertiaryCoefficients.Attach(_core, _coreIndices[w], pd, bd, _maxField);

            var lifted = DoubleToDual.Coefficients(bd.Totals);
            _coefficients[w] = lifted;
            return lifted;
        }

        var p = Paraxial(w);
        var b = AdA.BuchdahlCoefficients.Compute(_sys, p);
        AdA.TertiaryCoefficients.Attach(_sys, _indices[w], p, b, _maxField);

        _coefficients[w] = b.Totals;
        return b.Totals;
    }

    /// <summary>
    /// A real ray, kept at every surface it met. <paramref name="py"/> and
    /// <paramref name="px"/> are pupil coordinates as fractions of the pupil radius,
    /// meridional and sagittal; the chief ray is (0, 0) and the upper marginal ray (1, 0).
    /// </summary>
    /// <param name="atParaxialFocus">
    /// Where the ray is caught. False - the default for a merit function - uses the image
    /// surface the design defines, which is ONE plane shared by every wavelength. True uses
    /// each colour's own paraxial focus, which is what the aberration coefficients are referred
    /// to but is a different plane for every wavelength: measuring lateral colour there would
    /// fold the axial colour into it.
    /// </param>
    public AdR.RealRayTrace.SurfaceHit[] Ray(int wave, double fieldDeg, double py, double px,
                                             bool atParaxialFocus = false)
    {
        int w = Clamp(wave);
        long key = ((long)w << 48) ^ ((long)fieldDeg.GetHashCode() << 24)
                 ^ ((long)py.GetHashCode() * 31) ^ (uint)px.GetHashCode()
                 ^ (atParaxialFocus ? 1L << 62 : 0L);
        if (_rays.TryGetValue(key, out var cached)) return cached;

        // The paraxial trace the launch is built on has to be the one at THIS ray's field, or
        // the pupil it is aimed through belongs to a different field point.
        if (_core != null)
        {
            var pd = Core.RayTrace.ParaxialTrace.Trace(_core, _coreIndices![w], fieldDeg);
            var lifted = DoubleToDual.Ray(Core.RayTrace.RealRayTrace.TraceRecord(
                _core, _coreIndices[w], pd, fieldDeg, py, px, atParaxialFocus));
            _rays[key] = lifted;
            return lifted;
        }

        var p = Paraxial(w, fieldDeg);
        var hits = AdR.RealRayTrace.TraceRecord(_sys, _indices[w], p, fieldDeg, py, px,
                                                atParaxialFocus);
        _rays[key] = hits;
        return hits;
    }

    /// <summary>
    /// The paraxial beam half-height at each surface: |marginal| + |chief| at the maximum
    /// field, in the reference colour.
    ///
    /// <para>This is the clear aperture a surface has to have, and it is where the
    /// edge-thickness and diameter-to-thickness operands are measured. Taking it from the trace
    /// rather than from the semi-diameter the file declared is the point: a declared
    /// semi-diameter is a constant, and it would tell the optimiser that thinning a lens costs
    /// nothing at its edge when in fact the beam is still the size it was.</para>
    /// </summary>
    public Dual ApertureRadius(int surface)
    {
        if (_apertureRadius == null)
        {
            var p = Paraxial(_primary);
            int n = _sys.Surfaces.Count;
            var r = new Dual[n];
            for (int i = 0; i < n; i++)
            {
                Dual y = i < p.Y.Length ? p.Y[i] : default;
                Dual yb = i < p.Ybar.Length ? p.Ybar[i] : default;
                r[i] = Abs(y) + Abs(yb);
            }
            _apertureRadius = r;
        }
        return surface >= 0 && surface < _apertureRadius.Length ? _apertureRadius[surface] : default;
    }

    private static Dual Abs(Dual d) => d.Value >= 0.0 ? d : -d;

    private int Clamp(int wave) => wave < 0 ? _primary
                                 : wave >= _indices.Length ? _indices.Length - 1 : wave;
}
