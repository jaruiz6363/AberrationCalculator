using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// TEMPORARY diagnostic. Where the aspheric tertiary stands today, rung by rung, against both
/// oracles at once. Delete when the aspheric arrangement is settled.
/// </summary>
public class AsphericLadderSurvey
{
    private readonly ITestOutputHelper _out;
    public AsphericLadderSurvey(ITestOutputHelper output) => _out = output;

    private static readonly string[] Designs =
    {
        "Ladder1_Sphere", "Ladder1_A4", "Ladder1_Conic", "Ladder1_FiguredSphere",
        "Ladder2_Sphere", "Ladder2_Sphere_FlatRear",
        "Ladder2_A4_First", "Ladder2_A4_Second", "Ladder2_A4_Both",
        "Ladder2_A4_First_FlatRear", "Ladder2_A4_Then_FiguredSphere",
        "Ladder2_FiguredSphere_First", "Ladder2_FiguredSphere_Second",
        "Ladder2_FiguredSphere_Both", "Ladder2_FiguredSphere_Then_A4",
        "Ladder2_FlatFigured", "Ladder2_FiguredFlatRear",
        "Ladder3_Sphere", "Ladder3_A4_First", "Ladder3_A4_Middle",
        "Ladder3_FiguredSphere_Middle",
        "CookeTriplet", "CookeTriplet_PRMSA_START_LO_ASPHERE",
        "CookeTriplet_SPOTM_START_LO_ASPHERE", "CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8",
        "TertiaryTestbed_Triplet24",
    };

    [Fact]
    public void Survey()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tworst_vs_rays_pct\tworst_k_rays\tworst_vs_forbes_pct"
                    + "\tworst_k_forbes\tforbes_vs_rays_pct\tresidual\tfiguring_work_pct");

        foreach (string name in Designs)
        {
            string row;
            try { row = Row(name); }
            catch (Exception ex) { row = name + "\tFAILED: " + ex.Message; }
            sb.AppendLine(row);
            _out.WriteLine(row);
        }

        // Per-coefficient detail on the rungs that matter.
        foreach (string name in new[] { "Ladder2_A4_First", "Ladder2_A4_Second" })
        {
            sb.AppendLine();
            sb.AppendLine(name + "  k\tbuchdahl\tforbes\trays\tB-F share\tF-R share");
            var d = Detail(name);
            foreach (string line in d) sb.AppendLine(line);
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-survey.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine("written: " + path);
    }

    /// <summary>
    /// Every reading of Sec. 85 the new routine offers, measured against the rays on every
    /// rung. This is the instrument the aspheric arrangement is settled with: a reading either
    /// moves the broken rungs toward the oracle or it does not.
    /// </summary>
    [Fact]
    public void Readings()
    {
        var readings = new (string Name, BuchdahlAsphericScheme.Options? O)[]
        {
            ("as-built", null),
            ("intrinsic-chain-on-pass-ratio",
                new BuchdahlAsphericScheme.Options { IntrinsicChainOnPassRatio = true }),
            ("accumulated-figuring-on-height-ratio",
                new BuchdahlAsphericScheme.Options { AccumulatedFiguringOnHeightRatio = true }),
            ("full-figured-barred-secondary-in-dagger",
                new BuchdahlAsphericScheme.Options { FullFiguredBarredSecondaryInDagger = true }),
            ("eq-68.8-bracket",
                new BuchdahlAsphericScheme.Options { Equation688BracketInDagger = true }),
        };

        var sb = new StringBuilder();
        sb.Append("design\tfiguring_work_pct");
        foreach (var r in readings) sb.Append('\t').Append(r.Name);
        sb.AppendLine();

        foreach (string name in Designs)
        {
            var d = Load(name);
            var bare = LoadStripped(name);
            double work = 0.0;
            for (int k = 1; k <= 20; k++)
                work = Math.Max(work, Math.Abs(d.Forbes[k] - bare[k]) / d.Largest);

            sb.Append(name).Append('\t')
              .Append((100 * work).ToString("F1", CultureInfo.InvariantCulture));

            foreach (var r in readings)
            {
                double[] tau = NewRoute(name, r.O);

                // Worst AND rms over the twenty. The worst alone hid the fourth reading
                // entirely: it moved the totals by a large amount while leaving the single
                // worst coefficient untouched, so the column read as "changes nothing".
                double worst = 0.0, sum = 0.0;
                for (int k = 1; k <= 20; k++)
                {
                    double e = (tau[k] - d.Rays[k]) / d.Largest;
                    worst = Math.Max(worst, Math.Abs(e));
                    sum += e * e;
                }
                sb.Append('\t')
                  .Append((100 * worst).ToString("F3", CultureInfo.InvariantCulture))
                  .Append('/')
                  .Append((100 * Math.Sqrt(sum / 20)).ToString("F3",
                                                               CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-readings.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Can ONE number at the accumulated-figuring site bring all twenty tau to the rays at once?
    /// If the twenty agree about where the zero is, the structure is right; if they scatter, no
    /// reading of that site can fix it and the search moves elsewhere.
    /// </summary>
    [Fact]
    public void ShiftScan()
    {
        var sb = new StringBuilder();
        foreach (string name in new[] { "Ladder2_A4_First", "Ladder2_A4_Both",
                                        "Ladder2_A4_First_FlatRear", "Ladder2_FlatFigured",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var d = Load(name);
            sb.AppendLine();
            sb.AppendLine(name + "\tx\tworst_pct\tworst_k");
            foreach (double x in new[] { -2.0, -1.0, -0.5, 0.0, 0.25, 0.5, 0.75, 1.0, 2.0 })
            {
                var tau = NewRoute(name, new BuchdahlAsphericScheme.Options
                {
                    AccumulatedFiguringOnHeightRatio = true,
                    HeightRatioShiftFraction = x,
                });
                double worst = 0.0; int kw = 0;
                for (int k = 1; k <= 20; k++)
                {
                    double e = Math.Abs(tau[k] - d.Rays[k]) / d.Largest;
                    if (e > worst) { worst = e; kw = k; }
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\t{0,5}\t{1:F3}\t{2}", x, 100 * worst, kw));
            }
        }
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-shift-scan.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// What (68.8)'s dropped bracket actually evaluates to, surface by surface. A reading that
    /// changes nothing might be right and inert, or might be reaching a quantity that is zero
    /// for a reason that makes the whole rung unable to test it - and those are not the same
    /// thing to learn.
    /// </summary>
    [Fact]
    public void BracketSizes()
    {
        var sb = new StringBuilder();
        foreach (string name in new[] { "Ladder3_A4_Middle", "Ladder3_A4_First",
                                        "Ladder2_A4_First", "Ladder2_A4_Second" })
        {
            var rows = RowsFor(name, out int count);
            sb.AppendLine();
            sb.AppendLine(name + "  surf\talpha\trho\tq\tA_p\tAbar_p\tA_q\tbracket");
            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i]; var t = r.T;
                double bracket = r.ApFigured
                               * ((t[16] - 2.0 * t[20]) + (2.0 * r.Rho - t[6]) * t[15]);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\t{0}\t{1:E3}\t{2:E3}\t{3:E3}\t{4:E3}\t{5:E3}\t{6:E3}\t{7:E3}",
                    i, r.ApFigured, r.Rho, t[6], t[15], t[16], t[20], bracket));
            }
        }
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-brackets.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>Does the reading reach the totals at all, before asking whether it helps?</summary>
    [Fact]
    public void BracketReachesTheTotals()
    {
        foreach (string name in new[] { "CookeTriplet_SPOTM_START_LO_ASPHERE",
                                        "Ladder2_A4_Second" })
        {
            var rows = RowsFor(name, out int count);
            var plain = BuchdahlAsphericScheme.Totals(
                rows, count, BuchdahlAsphericScheme.Options.AsBuilt);

            var rows2 = RowsFor(name, out int count2);
            var moved = BuchdahlAsphericScheme.Totals(
                rows2, count2,
                new BuchdahlAsphericScheme.Options { Equation688BracketInDagger = true });

            double worst = 0.0;
            for (int k = 1; k <= 10; k++)
            {
                worst = Math.Max(worst, Math.Abs(plain.T[k] - moved.T[k]));
                worst = Math.Max(worst, Math.Abs(plain.Tbar[k] - moved.Tbar[k]));
            }
            _out.WriteLine($"{name}: largest move in the ten totals = {worst:E4}");
        }
    }

    /// <summary>
    /// For the FIRST secondary we now hold two things that claim to be the same quantity: the
    /// bracket M (68.8) prints, and the difference the scheme can form between the figured
    /// barred secondary and its lift half. Reading 3 uses the difference and helps a great deal
    /// where the figuring is in the middle while hurting where it is first; (68.8) helps less
    /// and hurts nowhere. Both cannot be right, and comparing them at m = 0 says what the
    /// difference carries that the printed bracket does not.
    /// </summary>
    [Fact]
    public void BracketAgainstTheDifference()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\teq68.8_bracket\tsecBarFig-Lift\tdifference\tratio");
        foreach (string name in new[] { "Ladder3_A4_Middle", "Ladder3_A4_First",
                                        "Ladder2_A4_First", "Ladder2_A4_Second",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var rows = RowsFor(name, out int count);
            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i]; var t = r.T;
                double printed = r.ApFigured
                               * ((t[16] - 2.0 * t[20]) + (2.0 * r.Rho - t[6]) * t[15]);
                double formed = r.SecBarFig[0] - r.SecBarFigLift[0];
                if (Math.Abs(printed) < 1e-14 && Math.Abs(formed) < 1e-14) continue;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E4}\t{3:E4}\t{4:E4}\t{5:F4}",
                    name, i, printed, formed, formed - printed,
                    Math.Abs(printed) > 1e-20 ? formed / printed : double.NaN));
            }
        }
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-bracket-vs-diff.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    private static BuchdahlTableIRow[] RowsFor(string name, out int count)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        var coefficients = BuchdahlCoefficients.Compute(sys, p);
        double objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectDistance);
        double iota = infinite ? 0.0 : -p.Efl / objectDistance;
        int stop = sys.StopSurfaceIndex;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[stop].SemiDiameter, iota);
        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, iota: iota);
        var increments = AsphericSchemeIncrements.Build(coefficients, spherical,
                                                        sys.LastOpticalSurface());
        double stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;
        count = sys.Surfaces.Count;
        return BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter, increments,
                                      iota: iota);
    }

    /// <summary>The new routine's tau, in the transverse convention the oracles use.</summary>
    private static double[] NewRoute(string name, BuchdahlAsphericScheme.Options? options)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var coefficients = BuchdahlCoefficients.Compute(sys, p);

        double objectDistance = sys.Surfaces[0].Thickness;
        bool infinite = double.IsInfinity(objectDistance);
        double iota = infinite ? 0.0 : -p.Efl / objectDistance;

        int stop = sys.StopSurfaceIndex;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, p.Efl,
                                            sys.Surfaces[stop].SemiDiameter, iota);
        var spherical = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, scheme.P, iota: iota);
        var increments = AsphericSchemeIncrements.Build(coefficients, spherical,
                                                        sys.LastOpticalSurface());

        double stopParameter = infinite ? scheme.P : p.EntrancePupilPosition / p.Efl;
        double g = 1.0 - stopParameter * iota;
        double lengthFactor = p.Efl / (p.N[sys.LastOpticalSurface()] * scheme.PRayFinalAngle);
        double u = -(0.5 * p.Epd / p.Efl) / g;
        double hmax = infinite
            ? Math.Tan(field * Math.PI / 180.0)
            : -(p.ParaxialImageHeight / p.Magnification) / objectDistance;

        var raw = BuchdahlAsphericScheme.Tau(sys.Surfaces, n, p.Efl, stopParameter,
                                             increments, iota, options);
        return TertiaryCoefficients.ToTransverse(raw, lengthFactor, u, hmax,
                                                 coefficients.Totals.B7);
    }

    private sealed record Loaded(double[] Scheme, double[] Forbes, double[] Rays,
                                 double Largest, double Residual);

    private static Loaded Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var t = b.Totals;

        var scheme = new double[21];
        for (int k = 1; k <= 20; k++)
            scheme[k] = k == 1 ? t.B7
                : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;

        var inv = CoefficientInversion.Invert(sys, n, p, field);
        var forbes = ForbesCoefficients.Invert(sys, n, p, field);

        double big = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(scheme[k]));

        return new Loaded(scheme, forbes?.Tau ?? new double[21], inv?.Tau ?? new double[21],
                          big, inv?.Residual ?? double.NaN);
    }

    /// <summary>
    /// The same lens with every figuring removed, by Forbes - so that "the figuring does real
    /// work on this rung" is a measurement rather than an assumption. Without it, a rung that
    /// agrees might only be one where the figuring does nothing.
    /// </summary>
    private static double[] LoadStripped(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        foreach (var s in sys.Surfaces)
        {
            s.Conic = 0.0;
            for (int k = 0; k < s.AsphericCoefficients.Length; k++)
                s.AsphericCoefficients[k] = 0.0;
        }
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;
        var p = ParaxialTrace.Trace(sys, n, field);
        return ForbesCoefficients.Invert(sys, n, p, field)?.Tau ?? new double[21];
    }

    private static string Row(string name)
    {
        var d = Load(name);
        var bare = LoadStripped(name);
        double work = 0.0;
        for (int k = 1; k <= 20; k++)
            work = Math.Max(work, Math.Abs(d.Forbes[k] - bare[k]) / d.Largest);

        double wr = 0, wf = 0, fr = 0;
        int kr = 0, kf = 0;
        for (int k = 1; k <= 20; k++)
        {
            double a = Math.Abs(d.Scheme[k] - d.Rays[k]) / d.Largest;
            double b = Math.Abs(d.Scheme[k] - d.Forbes[k]) / d.Largest;
            double c = Math.Abs(d.Forbes[k] - d.Rays[k]) / d.Largest;
            if (a > wr) { wr = a; kr = k; }
            if (b > wf) { wf = b; kf = k; }
            if (c > fr) fr = c;
        }
        return string.Format(CultureInfo.InvariantCulture,
            "{0}\t{1:F3}\t{2}\t{3:F3}\t{4}\t{5:F3}\t{6:E1}\t{7:F3}",
            name, 100 * wr, kr, 100 * wf, kf, 100 * fr, d.Residual, 100 * work);
    }

    private static List<string> Detail(string name)
    {
        var d = Load(name);
        var lines = new List<string>();
        for (int k = 1; k <= 20; k++)
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "  tau{0}\t{1:E6}\t{2:E6}\t{3:E6}\t{4:F3}\t{5:F3}",
                k, d.Scheme[k], d.Forbes[k], d.Rays[k],
                100 * Math.Abs(d.Scheme[k] - d.Forbes[k]) / d.Largest,
                100 * Math.Abs(d.Forbes[k] - d.Rays[k]) / d.Largest));
        return lines;
    }
}
