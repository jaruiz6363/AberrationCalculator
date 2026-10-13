using System;
using System.Collections.Generic;
using System.Linq;
using AberrationCalculator.Core.Analysis;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>The geometric MTF by Kidger's summation, against the formulas it rests on and against LensHH-LT.</summary>
public class GeometricMtfTests
{
    private sealed record Setup(OpticalSystem System, double[][] Indices, ParaxialResult Paraxial);

    private static Setup Load(string name, Action<OpticalSystem>? change = null)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        change?.Invoke(sys);
        var indices = sys.Wavelengths.Select(w => IndexResolver.Build(sys, catalog, w.Value)).ToArray();
        var paraxial = ParaxialTrace.Trace(sys, indices[sys.PrimaryWavelengthIndex], sys.Fields.Max(f => Math.Abs(f.Y)));
        return new Setup(sys, indices, paraxial);
    }

    // ── The pieces ──────────────────────────────────────────────────────────────────────

    /// <summary>Every ray stands for the same area: the mean of ρ² over the pattern is the disc's, ½, and it is symmetric.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public void ThePupilPatternIsEvenAndSymmetric(int rings)
    {
        var pupil = GeometricMtf.Pupil(rings);
        Assert.Equal(0, pupil.Length % 4);
        Assert.Equal(0.5, pupil.Average(p => p.Py * p.Py + p.Px * p.Px), 12);
        Assert.Equal(0.0, pupil.Sum(p => p.Py), 9);
        Assert.Equal(0.0, pupil.Sum(p => p.Px), 9);
        Assert.Equal(pupil.Sum(p => p.Py * p.Py), pupil.Sum(p => p.Px * p.Px), 9);
        Assert.All(pupil, p => Assert.True(p.Py * p.Py + p.Px * p.Px < 1.0));
    }

    /// <summary>A point has an MTF of 1; two equal points d apart, |cos πfd|, along the line joining them only.</summary>
    [Fact]
    public void TheSumIsTheSpotsFourierTransform()
    {
        var point = new[] { new GeometricMtf.Spot(3.0, -2.0, 1.0) };
        Assert.Equal(new GeometricMtf.Modulation(1.0, 1.0), GeometricMtf.Geometric(point, 37.0));

        const double d = 0.01;
        var pair = new[] { new GeometricMtf.Spot(0.2, 5.0, 2.0), new GeometricMtf.Spot(0.2, 5.0 + d, 2.0) };
        foreach (double f in new[] { 10.0, 25.0, 50.0, 80.0 })
        {
            var m = GeometricMtf.Geometric(pair, f);
            Assert.Equal(Math.Abs(Math.Cos(Math.PI * f * d)), m.Tangential, 12);
            Assert.Equal(1.0, m.Sagittal, 12);
        }
    }

    /// <summary>The circle's MTF where it is known exactly, and zero from the cutoff on.</summary>
    [Fact]
    public void ThePerfectLensMtfOfACircularPupil()
    {
        Assert.Equal(1.0, GeometricMtf.DiffractionLimit(0.0, 100.0), 12);
        Assert.Equal(2.0 / Math.PI * (Math.PI / 3.0 - 0.5 * Math.Sqrt(0.75)), GeometricMtf.DiffractionLimit(50.0, 100.0), 12);
        Assert.Equal(0.0, GeometricMtf.DiffractionLimit(100.0, 100.0));
        Assert.Equal(0.0, GeometricMtf.DiffractionLimit(150.0, 100.0));
    }

    /// <summary>
    /// An annular pupil's MTF is the overlap of the annulus with itself shifted by 2v radii, over
    /// its area; counted on a fine grid, it is O'Neill's formula.
    /// </summary>
    [Theory]
    [InlineData(0.3)]
    [InlineData(0.6)]
    public void ThePerfectLensMtfOfAnAnnularPupilIsItsAutocorrelation(double e)
    {
        const int n = 1200;
        double h = 2.0 / n;
        var inside = new List<(double X, double Y)>();
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                double x = -1.0 + (i + 0.5) * h, y = -1.0 + (j + 0.5) * h, r2 = x * x + y * y;
                if (r2 <= 1.0 && r2 >= e * e) inside.Add((x, y));
            }
        foreach (double v in new[] { 0.05, 0.15, 0.25, 0.35, 0.5, 0.65, 0.8, 0.95 })
        {
            double s = 2.0 * v;
            int both = inside.Count(p => { double x = p.X - s, r2 = x * x + p.Y * p.Y; return r2 <= 1.0 && r2 >= e * e; });
            Assert.Equal((double)both / inside.Count, GeometricMtf.DiffractionLimit(v, 1.0, e), 2);
        }
        Assert.Equal(GeometricMtf.DiffractionLimit(0.3, 1.0), GeometricMtf.DiffractionLimit(0.3, 1.0, 0.0));
    }

    // ── A lens ──────────────────────────────────────────────────────────────────────────

    /// <summary>On axis the cutoff is 2 NA/λ: the Cooke triplet's real marginal ray, as LensHH-LT has it (365.23 cy/mm at 0.55 µm).</summary>
    [Fact]
    public void TheCutoffOnAxisIsTwiceTheNaOverTheWavelength()
    {
        var s = Load("CookeTriplet");
        var r = GeometricMtf.Compute(s.System, s.Indices, s.Paraxial, 0.0, new[] { 10.0 }, new MtfOptions { Wavelength = 1 });
        Assert.Equal(365.23, r.CutoffTangential, 1);
        Assert.Equal(r.CutoffTangential, r.CutoffSagittal, 9);
        Assert.Equal(0.0, r.Obscuration);
        Assert.Equal(r.Mtf[0].Tangential, r.Mtf[0].Sagittal, 12);
    }

    /// <summary>
    /// LensHH-LT's Kidger MTF of the Cooke triplet at 0.55 µm with 200 rings, where its own ray
    /// pattern has converged: within 0.003 at every field.
    /// </summary>
    [Theory]
    [InlineData(0.0, 43.8273, 0.601320, 0.601320)]
    [InlineData(0.0, 87.6546, 0.156128, 0.156128)]
    [InlineData(14.0, 42.5265, 0.070052, 0.148285)]
    [InlineData(14.0, 85.0530, 0.004806, 0.043083)]
    [InlineData(20.0, 41.1794, 0.088157, 0.104839)]
    [InlineData(20.0, 82.3588, 0.036808, 0.085307)]
    public void TheTripletMatchesLensHHLT(double field, double frequency, double tangential, double sagittal)
    {
        var s = Load("CookeTriplet");
        var r = GeometricMtf.Compute(s.System, s.Indices, s.Paraxial, field, new[] { frequency }, new MtfOptions { Rings = 60, Wavelength = 1 });
        Assert.InRange(r.Mtf[0].Tangential - tangential, -0.003, 0.003);
        Assert.InRange(r.Mtf[0].Sagittal - sagittal, -0.003, 0.003);
    }

    /// <summary>
    /// With LensHH-LT's own ray pattern - rings at the outer edge of each equal-area zone - the
    /// polychromatic MTF is LensHH-LT's at its default 15 rings: the tracing, the weighting of the
    /// wavelengths and the diffraction limit are the same, and only the pattern differs. Its edge
    /// rings converge slowly, which is why this one centres them.
    /// </summary>
    [Fact]
    public void WithLensHHLTsPatternThePolychromaticMtfIsLensHHLTs()
    {
        var s = Load("CookeTriplet");
        var tracer = GeometricMtf.DefaultTracer(s.System, s.Indices, s.Paraxial);
        const int rings = 15, arms = 32;
        double total = s.System.Wavelengths.Sum(w => w.Weight);
        var spots = new List<GeometricMtf.Spot>();
        for (int w = 0; w < s.System.Wavelengths.Count; w++)
            for (int ring = 1; ring <= rings; ring++)
                for (int arm = 0; arm < arms; arm++)
                {
                    double rho = Math.Sqrt((double)ring / rings), theta = 2.0 * Math.PI * arm / arms;
                    var ray = tracer(14.0, w, rho * Math.Sin(theta), rho * Math.Cos(theta));
                    if (ray.Ok) spots.Add(new GeometricMtf.Spot(ray.X, ray.Y, s.System.Wavelengths[w].Weight / total));
                }
        double step = 406.1 / 50.0;
        var lt = new (double F, double T, double S)[]
        {
            (step, 0.766697, 0.892129), (2 * step, 0.403928, 0.673953), (3 * step, 0.208466, 0.439454),
            (5 * step, 0.056639, 0.186042), (8 * step, 0.010164, 0.147502),
        };
        var limit = GeometricMtf.Compute(s.System, s.Indices, s.Paraxial, 14.0, lt.Select(p => p.F).ToArray()).DiffractionLimit;
        for (int k = 0; k < lt.Length; k++)
        {
            var g = GeometricMtf.Geometric(spots, lt[k].F);
            Assert.InRange(g.Tangential * limit[k].Tangential - lt[k].T, -0.003, 0.003);
            Assert.InRange(g.Sagittal * limit[k].Sagittal - lt[k].S, -0.003, 0.003);
        }
    }

    /// <summary>Kidger's pattern converges fast: 15 rings are within 0.003 of 120.</summary>
    [Fact]
    public void FifteenRingsAreEnough()
    {
        var s = Load("CookeTriplet");
        var frequencies = new[] { 10.0, 20.0, 40.0, 80.0 };
        var coarse = GeometricMtf.Compute(s.System, s.Indices, s.Paraxial, 14.0, frequencies, new MtfOptions { Rings = 15 });
        var fine = GeometricMtf.Compute(s.System, s.Indices, s.Paraxial, 14.0, frequencies, new MtfOptions { Rings = 120 });
        for (int k = 0; k < frequencies.Length; k++)
        {
            Assert.InRange(coarse.Mtf[k].Tangential - fine.Mtf[k].Tangential, -0.003, 0.003);
            Assert.InRange(coarse.Mtf[k].Sagittal - fine.Mtf[k].Sagittal, -0.003, 0.003);
        }
    }

    /// <summary>A fixed semi-diameter that cuts into the beam stops rays and narrows the cutoff; left to follow the beam, it stops none.</summary>
    [Fact]
    public void AFixedSemiDiameterVignettes()
    {
        var open = Load("CookeTriplet");
        double epr = 0.5 * Math.Abs(open.Paraxial.Epd);
        int front = open.System.StopSurfaceIndex == 1 ? 2 : 1;
        var clipped = Load("CookeTriplet", sys =>
        {
            sys.Surfaces[front].SemiDiameter = 0.8 * epr;
            sys.Surfaces[front].SemiDiameterMode = SemiDiameterMode.Fixed;
        });
        var automatic = Load("CookeTriplet", sys =>
        {
            sys.Surfaces[front].SemiDiameter = 0.8 * epr;
            sys.Surfaces[front].SemiDiameterMode = SemiDiameterMode.Auto;
        });
        var f = new[] { 20.0 };
        var a = GeometricMtf.Compute(open.System, open.Indices, open.Paraxial, 0.0, f);
        var b = GeometricMtf.Compute(clipped.System, clipped.Indices, clipped.Paraxial, 0.0, f);
        var c = GeometricMtf.Compute(automatic.System, automatic.Indices, automatic.Paraxial, 0.0, f);

        Assert.Equal(a.RaysTraced, a.RaysArrived);
        Assert.True(b.RaysArrived < b.RaysTraced);
        Assert.Equal(a.RaysArrived, c.RaysArrived);
        Assert.InRange(b.CutoffTangential / a.CutoffTangential, 0.75, 0.85);
    }

    [Fact]
    public void AnAfocalSystemIsRefused()
    {
        var s = Load("CookeTriplet", sys => sys.IsAfocal = true);
        Assert.Throws<NotSupportedException>(() => GeometricMtf.Compute(s.System, s.Indices, s.Paraxial, 0.0, new[] { 10.0 }));
    }
}
