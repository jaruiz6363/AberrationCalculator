using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Buchdahl/Rimmer coefficients, checked two ways.
///
/// Against the ZEMAX oracle, which is the authority: the same lenses run through ZEMAX's
/// own FIFTHORD macro, per surface and in total. And against physics that must hold for
/// any correct implementation, so the tests still mean something in a clone without the
/// private reference data.
/// </summary>
public class BuchdahlCoefficientsTests
{
    /// <summary>
    /// The reference fixtures, which live in this repository.
    ///
    /// <para>They used to be found by walking up to fourteen directories looking for a
    /// separate, private repository. That made the suite quietly dependent on where this one
    /// happened to sit on disk: a fresh clone reported success while running seven fewer
    /// tests than a working copy, and said nothing about it. A test that disappears when it
    /// cannot find its data is worse than one that fails, because the number at the end still
    /// says everything passed.</para>
    /// </summary>
    private static string OracleDir() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "coefficient-reference");

    private static (BuchdahlResult R, ParaxialResult P) Run(string zmx)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(zmx, catalog);
        int pw = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[pw].Value);
        double field = 0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        return (BuchdahlCoefficients.Compute(sys, p), p);
    }

    public static IEnumerable<object[]> Fixtures()
    {
        // No silent empty set. If the fixtures are not there the suite should say so.
        var dir = OracleDir();
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException(
                "the coefficient reference fixtures are missing from " + dir +
                ". They are part of this repository; a clone should have them.");

        var files = Directory.GetFiles(dir, "*.buchdahl.json");
        if (files.Length == 0)
            throw new FileNotFoundException("no reference fixtures found in " + dir);

        foreach (var f in files)
            yield return new object[] { Path.GetFileName(f).Replace(".buchdahl.json", "") };
    }

    /// <summary>
    /// Every coefficient of every fixture against the recorded reference: the intrinsic part
    /// of each surface, the isolated aspheric part, and the system totals - which are neither
    /// the sum of the surfaces nor unscaled, so they exercise the induced corrections and the
    /// F/number convention as well as the per-surface arithmetic.
    ///
    /// <para>The reference is what the FIFTHORD macro reported for these designs, so this is
    /// a second implementation of the same published method agreeing with this one. It is a
    /// cross-check and not the authority: what establishes these coefficients is Buchdahl's
    /// own printed table, closed-form conic surfaces, inverse ray tracing and Forbes' series
    /// trace. See docs/references.md, and the README beside the fixtures.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void MatchesTheRecordedReference(string fixture)
    {
        if (fixture == null) return;                     // reference repo absent
        var dir = OracleDir()!;
        string zmx = Path.Combine(dir, fixture + ".zmx");
        var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, fixture + ".buchdahl.json"))).RootElement;
        var (r, _) = Run(zmx);

        void Compare(JsonElement expected, BuchdahlTerms got, string where)
        {
            foreach (var name in BuchdahlTerms.Names)
            {
                if (!expected.TryGetProperty(name, out var ev)) continue;
                double e = ev.GetDouble(), a = got[name];
                double scale = Math.Max(Math.Abs(e), 1e-9);
                Assert.True(Math.Abs(e - a) / scale < 1e-9,
                    $"{fixture} {where}.{name}: expected {e:E12}, got {a:E12}");
            }
        }

        foreach (var s in doc.GetProperty("surfaces").EnumerateObject())
            Compare(s.Value, r.Intrinsic[int.Parse(s.Name)], "surface " + s.Name);

        if (doc.TryGetProperty("aspheric", out var asph))
            foreach (var s in asph.EnumerateObject())
            {
                var got = r.Aspheric[int.Parse(s.Name)];
                Assert.True(got != null, $"{fixture}: expected an aspheric contribution at surface {s.Name}");
                Compare(s.Value, got!, "aspheric " + s.Name);
            }

        Compare(doc.GetProperty("totals"), r.Totals, "totals");

        Assert.Equal(doc.GetProperty("globals").GetProperty("fnum").GetDouble(), r.FNumber, 10);
        Assert.Equal(doc.GetProperty("globals").GetProperty("lagrange").GetDouble(), r.Lagrange, 10);
    }

    /// <summary>
    /// A conic and the pure r^4 coefficient that matches its c1 agree exactly at third
    /// order, because both produce the same fourth-power departure from the sphere. They
    /// must then diverge at fifth and seventh order, where the conic contributes a c2 and
    /// c3 the single polynomial term cannot.
    ///
    /// This is the check that the c1/c2/c3 chain is a chain and not three unrelated
    /// constants - and it needs no reference data beyond the two fixtures themselves.
    /// </summary>
    [Fact]
    public void AConicAndItsMatchedR4Term_AgreeAtThirdOrderAndDivergeAbove()
    {
        var dir = OracleDir();
        if (dir == null) return;

        var (conic, _) = Run(Path.Combine(dir, "F1_conic_singlet.zmx"));
        var (poly, _) = Run(Path.Combine(dir, "F2_a4_equivalent.zmx"));

        foreach (var name in new[] { "B", "F", "C", "Pi", "E" })
        {
            double a = conic.Totals[name], b = poly.Totals[name];
            Assert.True(Math.Abs(a - b) / Math.Max(Math.Abs(a), 1e-12) < 1e-10,
                $"third order must agree: {name} {a:E12} vs {b:E12}");
        }

        Assert.True(Math.Abs(conic.Totals.B5 - poly.Totals.B5)
                    / Math.Abs(conic.Totals.B5) > 1e-6,
            "fifth-order spherical must differ between a conic and a bare r^4 term");
    }

    /// <summary>
    /// With all three deformation coefficients matched, a conic and a sphere-plus-polynomial
    /// describe the same surface to seventh order, so every coefficient must agree.
    /// </summary>
    [Fact]
    public void AConicAndItsFullPolynomialEquivalent_AgreeAtEveryOrder()
    {
        var dir = OracleDir();
        if (dir == null) return;

        var (conic, _) = Run(Path.Combine(dir, "F1_conic_singlet.zmx"));
        var (poly, _) = Run(Path.Combine(dir, "F7_conic_as_polynomial.zmx"));

        foreach (var name in BuchdahlTerms.Names)
        {
            double a = conic.Totals[name], b = poly.Totals[name];
            double scale = Math.Max(Math.Abs(a), 1e-12);
            Assert.True(Math.Abs(a - b) / scale < 1e-9,
                $"{name} must agree: {a:E12} vs {b:E12}");
        }
    }

    /// <summary>
    /// A paraboloid has exactly zero third-order spherical aberration at infinite conjugate.
    /// The aspheric block must cancel the spherical block to rounding - physics, not a
    /// property of this implementation - and it also exercises the mirror index-sign path.
    /// </summary>
    [Fact]
    public void AParabolicMirrorCancelsItsOwnThirdOrderSpherical()
    {
        var dir = OracleDir();
        if (dir == null) return;

        var (r, _) = Run(Path.Combine(dir, "F4_parabolic_mirror.zmx"));

        double intrinsic = r.Intrinsic[1].B;
        double asph = r.Aspheric[1]!.B;
        Assert.True(Math.Abs(intrinsic) > 1e-6, "the sphere must contribute spherical aberration");
        Assert.Equal(-intrinsic, asph, 10);
        Assert.True(Math.Abs(r.Totals.B) < 1e-12 * Math.Max(1.0, Math.Abs(intrinsic)),
            $"a parabola's total third-order spherical must vanish, got {r.Totals.B:E12}");
    }

    /// <summary>
    /// The totals are deliberately NOT the sum of the per-surface intrinsic parts: they
    /// carry the aspheric contributions, the induced cross-surface corrections, and the
    /// F/number. A design with two aspheres and a mid-stop makes all three matter, so this
    /// records the distinction rather than letting a future reader "fix" it.
    /// </summary>
    [Fact]
    public void TotalsAreNotTheSumOfTheIntrinsicParts()
    {
        var dir = OracleDir();
        if (dir == null) return;

        var (r, _) = Run(Path.Combine(dir, "F6_triplet_two_aspheres.zmx"));

        double sumB5 = 0;
        foreach (var t in r.Intrinsic) if (t != null) sumB5 += t.B5;

        Assert.True(Math.Abs(sumB5 - r.Totals.B5) > 1e-9,
            "totals that equalled the intrinsic sum would mean the induced terms were lost");
    }

    static OpticalSystem OneAsphere(double curvature, double a2, double a4)
    {
        var sys = new OpticalSystem { Aperture = new Aperture(ApertureType.EPD, 20.0) };
        sys.Wavelengths.Add(new Wavelength(0.5875618, 1.0, true));
        sys.Fields.Add(new Field(2.0));
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface { Index = 1, Curvature = curvature, Thickness = 6.0,
                                       Material = "GLASS", IsStop = true,
                                       Type = SurfaceType.EvenAsphere });
        sys.Surfaces[1].AsphericCoefficients[0] = a2;
        sys.Surfaces[1].AsphericCoefficients[1] = a4;
        sys.Surfaces.Add(new Surface { Index = 2, Curvature = -0.005, Thickness = 76.0 });
        sys.Surfaces.Add(new Surface { Index = 3, Thickness = 0.0 });
        return sys;
    }

    /// <summary>
    /// An r^2 aspheric term is a curvature change, not figuring: the surface is exactly the
    /// sphere of curvature c + 2 A2 carrying whatever figuring is left once that sphere is own
    /// r^4 term is taken out. So the two descriptions must analyse identically.
    ///
    /// <para>This used to be reported as unrepresentable and the term dropped, which analysed
    /// a different surface from the one the file describes.</para>
    /// </summary>
    [Fact]
    public void AnRSquaredAsphericTermIsTheSameSurfaceAsAShiftedSphere()
    {
        const double c = 0.02, a2 = 1e-4;
        double v = c + 2.0 * a2;

        // Same surface, written the other way: sphere at the vertex curvature, and the r^4
        // difference between the two spheres put back as figuring.
        var withR2 = OneAsphere(c, a2, 0.0);
        var shifted = OneAsphere(v, 0.0, c * c * c / 8.0 - v * v * v / 8.0);

        var n = new[] { 1.0, 1.5, 1.0, 1.0 };
        var pa = ParaxialTrace.Trace(withR2, n, 2.0);
        var pb = ParaxialTrace.Trace(shifted, n, 2.0);
        Assert.Equal(pa.Efl, pb.Efl, 10);

        var ba = BuchdahlCoefficients.Compute(withR2, pa);
        var bb = BuchdahlCoefficients.Compute(shifted, pb);
        Assert.Equal(ba.Totals.B, bb.Totals.B, 10);
        Assert.Equal(ba.Totals.F, bb.Totals.F, 10);
        Assert.Equal(ba.Totals.C, bb.Totals.C, 10);
        Assert.Equal(ba.Totals.E, bb.Totals.E, 10);
    }

    /// <summary>
    /// And it is not merely tolerated: the r^2 term moves the surface power, so it must move
    /// the focal length. A version that dropped the term would pass the test above too.
    /// </summary>
    [Fact]
    public void AnRSquaredAsphericTermChangesThePower()
    {
        var n = new[] { 1.0, 1.5, 1.0, 1.0 };
        double plain = ParaxialTrace.Trace(OneAsphere(0.02, 0.0, 0.0), n, 2.0).Efl;
        double bent = ParaxialTrace.Trace(OneAsphere(0.02, 1e-4, 0.0), n, 2.0).Efl;
        Assert.True(Math.Abs(plain - bent) > 1e-6,
            $"an r^2 term must change the power: {plain:F6} against {bent:F6}");
    }
}
