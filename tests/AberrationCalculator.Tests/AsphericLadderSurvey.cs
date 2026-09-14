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
            ("dagger-increment-figured-half",
                new BuchdahlAsphericScheme.Options
                    { DaggerIncrementFiguredHalfOnHeightRatio = true }),
            ("no-figured-correction-in-dagger",
                new BuchdahlAsphericScheme.Options { NoFiguredCorrectionInDagger = true }),
            ("q-side-products-spherical",
                new BuchdahlAsphericScheme.Options { QSideProductsOnSphericalHalves = true }),
            ("split-primary-only",
                new BuchdahlAsphericScheme.Options { SharedQBarWithSplitPrimary = true }),
            ("spherical-accumulations-in-direct-uses",
                new BuchdahlAsphericScheme.Options { SphericalAccumulationsInDirectUses = true }),
            ("dagger-on-increment-alone",
                new BuchdahlAsphericScheme.Options { DaggerCorrectionOnIncrementAlone = true }),
            ("increment-alone + D-half",
                new BuchdahlAsphericScheme.Options
                {
                    DaggerCorrectionOnIncrementAlone = true,
                    FiguredSecondarySplitByDandL = true,
                }),
            ("lift-split-by-D-and-L",
                new BuchdahlAsphericScheme.Options { LiftSplitByDandL = true }),
            ("D-half-into-the-hat",
                new BuchdahlAsphericScheme.Options { FiguredSecondarySplitByDandL = true }),
            ("D-half-plus-85.1",
                new BuchdahlAsphericScheme.Options
                {
                    FiguredSecondarySplitByDandL = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
            ("Y-barred-shared-accumulations",
                new BuchdahlAsphericScheme.Options { YBarredFromSharedAccumulations = true }),
            ("barred-q-from-identities",
                new BuchdahlAsphericScheme.Options { BarredQAccumulationFromIdentities = true }),
            ("identities + D-half",
                new BuchdahlAsphericScheme.Options
                {
                    BarredQAccumulationFromIdentities = true,
                    FiguredSecondarySplitByDandL = true,
                }),
            ("both-halves-together",
                new BuchdahlAsphericScheme.Options
                {
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
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

                // EACH COEFFICIENT AGAINST ITSELF, and against Forbes rather than the ray
                // inversion. Both of those were wrong before and both mattered.
                //
                // Dividing by the largest coefficient in the set hid the failures this
                // arrangement actually has. verification.md records tau15 wrong by a factor of
                // nearly five including its sign and tau20 by half, while the large ones agree
                // to one per cent; a small coefficient wrong by five times is nothing when it is
                // divided by the largest, so the instrument reported the design that does that
                // as 1.7 per cent and the readings were being ranked on coefficients that were
                // already right.
                //
                // The old normalisation existed because relative error is meaningless at the ray
                // INVERSION's noise floor. Against Forbes that objection lapses: its series is
                // an expansion rather than a least-squares recovery, it tracks the rays to
                // between 0.001 and 0.13 per cent on every design here, and where the two
                // disagree beyond the recovery's error bar the rays land on Forbes.
                double worst = 0.0;
                int bad = 0, worstK = 0;
                for (int k = 1; k <= 20; k++)
                {
                    double f = d.Forbes[k];
                    if (Math.Abs(f) < 1e-9 * d.Largest) continue;
                    double rel = Math.Abs(tau[k] - f) / Math.Abs(f);
                    if (rel > worst) { worst = rel; worstK = k; }
                    if (rel > 0.01) bad++;
                }
                sb.Append('\t')
                  .Append((100 * worst).ToString("F1", CultureInfo.InvariantCulture))
                  .Append("(t").Append(worstK).Append(")/").Append(bad);
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

    /// <summary>
    /// Every coefficient, against Forbes, as its OWN relative error.
    ///
    /// <para><b>Why not share-of-the-largest.</b> That normalisation was chosen because relative
    /// error is meaningless on a coefficient three orders below the rest, which is where the ray
    /// inversion's noise floor sits. But it hides the failures this arrangement actually has:
    /// <c>docs/verification.md</c> records tau15 wrong "by a factor of nearly five including its
    /// sign" and tau20 by half, while the large ones agree to under one per cent. Divided by the
    /// largest coefficient in the set, a small coefficient wrong by five times reports as
    /// nothing, and a reading can be ranked on the coefficients that were already right.</para>
    ///
    /// <para>Against FORBES the objection to relative error does not apply. Forbes' series is an
    /// expansion, not a least-squares recovery from traced landings, so it has no noise floor of
    /// its own - it tracks the rays to between 0.001 and 0.13 per cent on every design here, and
    /// where the two routes disagree beyond the recovery's error bar the rays land on Forbes.
    /// So each coefficient can be asked about on its own terms.</para>
    /// </summary>
    [Fact]
    public void PerCoefficient()
    {
        var readings = new (string Name, BuchdahlAsphericScheme.Options? O)[]
        {
            ("as-built", null),
            ("eq-68.8", new BuchdahlAsphericScheme.Options { Equation688BracketInDagger = true }),
            ("full-fig-dagger",
                new BuchdahlAsphericScheme.Options { FullFiguredBarredSecondaryInDagger = true }),
        };

        var sb = new StringBuilder();
        foreach (string name in new[] { "Ladder3_Sphere", "Ladder3_A4_Middle",
                                        "Ladder3_A4_First", "Ladder2_A4_First",
                                        "Ladder2_A4_Second",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var d = Load(name);
            var tau = new double[readings.Length][];
            for (int r = 0; r < readings.Length; r++) tau[r] = NewRoute(name, readings[r].O);

            sb.AppendLine();
            sb.Append(name).Append("\tk\tforbes\trays");
            foreach (var r in readings) sb.Append('\t').Append(r.Name).Append("_rel%");
            sb.AppendLine("\tsize_vs_largest");

            for (int k = 1; k <= 20; k++)
            {
                double f = d.Forbes[k];
                sb.Append('\t').Append(k)
                  .Append('\t').Append(f.ToString("E4", CultureInfo.InvariantCulture))
                  .Append('\t').Append(d.Rays[k].ToString("E4", CultureInfo.InvariantCulture));

                foreach (var t in tau)
                    sb.Append('\t').Append(Relative(t[k], f));

                sb.Append('\t')
                  .Append((100 * Math.Abs(f) / d.Largest)
                              .ToString("F2", CultureInfo.InvariantCulture))
                  .AppendLine();
            }

            // The headline: worst relative error over the twenty, and how many are off by more
            // than one per cent - which is the figure verification.md quotes.
            sb.Append("\tWORST/over1pct");
            foreach (var t in tau)
            {
                double worst = 0.0; int bad = 0, k0 = 0;
                for (int k = 1; k <= 20; k++)
                {
                    double f = d.Forbes[k];
                    if (Math.Abs(f) < 1e-9 * d.Largest) continue;   // no signal to divide by
                    double rel = Math.Abs(t[k] - f) / Math.Abs(f);
                    if (rel > worst) { worst = rel; k0 = k; }
                    if (rel > 0.01) bad++;
                }
                sb.Append('\t')
                  .Append((100 * worst).ToString("F1", CultureInfo.InvariantCulture))
                  .Append(" (tau").Append(k0).Append(") / ").Append(bad);
            }
            sb.AppendLine();
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-per-coefficient.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// The ten tertiary coefficients and their barred partners, which is the level Sec. 85
    /// actually computes at, against what Forbes implies for them.
    ///
    /// <para>Paper III's Table II is a linear map from (T1..T10, T-1..T-10) to tau1..tau20 and it
    /// INVERTS, so Forbes' twenty tau give Forbes' twenty totals. Comparing there rather than at
    /// tau is sharper for localising: tau2 and tau3 both draw on T2, so a fault in T2 shows up
    /// twice at tau and once here, and a coefficient that the arrangement builds in one place can
    /// be named.</para>
    /// </summary>
    private static (double[] T, double[] Tbar) TotalsFromTau(double[] t)
    {
        var T = new double[11];
        var B = new double[11];

        T[1] = t[1];
        T[2] = 2.0 * t[3];            B[1] = t[2] - t[3];
        T[3] = t[5];                  B[2] = t[4] - t[5];
        T[4] = t[6];
        T[7] = 8.0 * t[10];
        T[5] = 2.0 * t[9] - 4.0 * t[10];
        B[4] = 2.0 * t[8] - 2.0 * t[9] - 4.0 * t[10];
        B[3] = t[7] - t[8] + t[10];
        T[6] = t[13];                 B[5] = t[11] - t[13];
        T[8] = t[14];                 B[7] = t[12] - t[14];
        T[9] = 2.0 * t[17];
        B[8] = 2.0 * t[16] - 2.0 * t[17];
        B[6] = t[15] - t[16];
        T[10] = t[19];                B[9] = t[18] - t[19];
        B[10] = t[20];

        return (T, B);
    }

    /// <summary>
    /// Which of the twenty totals the confirmed (85.1) reading breaks on the two designs it
    /// makes worse - both of which figure the LAST powered surface.
    ///
    /// <para>The reading is correct by the source: (85.1) gives the barred (Y) member as
    /// <c>q~ 'G-_p - 'G-_q</c> on the same accumulations as its (I) partner, and rebuilding
    /// <c>'S-_1q</c> as an explicit accumulation is algebraically identical to applying the
    /// reading as a delta, so no reformulation can change these numbers. Something else is
    /// wrong, and the totals say where: each T-bar entry is built from named daggers, so the
    /// one that moves names the entry.</para>
    /// </summary>
    [Fact]
    public void WhichTotalsTheConfirmedReadingBreaks()
    {
        var reading = new BuchdahlAsphericScheme.Options
        {
            SharedQBarWithSplitPrimary = true,
            YBarredFromSharedAccumulations = true,
        };

        var sb = new StringBuilder();
        sb.AppendLine("design\tentry\tforbes\tas-built rel%\treading rel%\tverdict");

        foreach (string name in new[] { "Ladder2_A4_Second", "Ladder2_FiguredSphere_Then_A4",
                                        "Ladder3_A4_Middle", "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var d = Load(name);
            var (fT, fB) = TotalsFromTau(d.Forbes);
            var (aT, aB) = TotalsFromTau(NewRoute(name, null));
            var (rT, rB) = TotalsFromTau(NewRoute(name, reading));

            double big = 0.0;
            for (int k = 1; k <= 10; k++)
            {
                big = Math.Max(big, Math.Abs(fT[k]));
                big = Math.Max(big, Math.Abs(fB[k]));
            }

            for (int pass = 0; pass < 2; pass++)
            {
                var f = pass == 0 ? fT : fB;
                var a = pass == 0 ? aT : aB;
                var r = pass == 0 ? rT : rB;
                string tag = pass == 0 ? "T" : "Tbar";

                for (int k = 1; k <= 10; k++)
                {
                    if (Math.Abs(f[k]) < 1e-12 * big) continue;
                    double before = 100 * Math.Abs(a[k] - f[k]) / Math.Abs(f[k]);
                    double after = 100 * Math.Abs(r[k] - f[k]) / Math.Abs(f[k]);
                    if (before < 0.5 && after < 0.5) continue;

                    string verdict = after < before * 0.9 ? "better"
                                   : after > before * 1.1 ? "WORSE" : "same";
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}{2}\t{3:E3}\t{4:F2}\t{5:F2}\t{6}",
                        name, tag, k, f[k], before, after, verdict));
                }
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-reading-totals.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Which of the two families of term in the barred rule carries defect 2.
    ///
    /// <para>On <c>Ladder2_A4_Second</c> the figured surface is the last powered one and the one
    /// before it is a sphere, so every contribution to T-bar except that surface's CHECK half is
    /// spherical and known right - the whole 79 per cent error sits in one place. The barred rule
    /// there is <c>q~ T_1 + t115 alpha + Y31 t40</c>, two families of term, and suppressing each
    /// in turn says which carries it. Suppression is a probe, not a candidate arrangement.</para>
    /// </summary>
    [Fact]
    public void WhichTermOfTheBarredRuleCarriesDefectTwo()
    {
        var probes = new (string Name, BuchdahlAsphericScheme.Options? O)[]
        {
            ("as-built", null),
            ("drop own-primary x tertiary-family",
                new BuchdahlAsphericScheme.Options
                    { DropOwnPrimaryTimesTertiaryFamilyInCheckBarred = true }),
            ("drop dagger x own-secondary",
                new BuchdahlAsphericScheme.Options
                    { DropDaggerTimesOwnSecondaryInCheckBarred = true }),
            ("drop both",
                new BuchdahlAsphericScheme.Options
                {
                    DropOwnPrimaryTimesTertiaryFamilyInCheckBarred = true,
                    DropDaggerTimesOwnSecondaryInCheckBarred = true,
                }),
            ("t38 for t40 in check barred",
                new BuchdahlAsphericScheme.Options { IntrinsicSecondaryInCheckBarred = true }),
            ("D half into the hat",
                new BuchdahlAsphericScheme.Options { FiguredSecondarySplitByDandL = true }),
            ("D half + the (85.1) reading",
                new BuchdahlAsphericScheme.Options
                {
                    FiguredSecondarySplitByDandL = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
            ("t38 + the (85.1) reading",
                new BuchdahlAsphericScheme.Options
                {
                    IntrinsicSecondaryInCheckBarred = true,
                    SharedQBarWithSplitPrimary = true,
                    YBarredFromSharedAccumulations = true,
                }),
        };

        var sb = new StringBuilder();

        foreach (string name in new[] { "Ladder2_A4_Second", "Ladder2_FiguredSphere_Then_A4",
                                        "Ladder3_A4_Middle" })
        {
            var d = Load(name);
            var (fT, fB) = TotalsFromTau(d.Forbes);

            sb.AppendLine();
            sb.Append(name).Append("\tentry\tforbes");
            foreach (var p in probes) sb.Append('\t').Append(p.Name);
            sb.AppendLine();

            var got = new (double[] T, double[] B)[probes.Length];
            for (int i = 0; i < probes.Length; i++) got[i] = TotalsFromTau(NewRoute(name, probes[i].O));

            double big = 0.0;
            for (int k = 1; k <= 10; k++)
            {
                big = Math.Max(big, Math.Abs(fT[k]));
                big = Math.Max(big, Math.Abs(fB[k]));
            }

            for (int k = 1; k <= 10; k++)
            {
                if (Math.Abs(fB[k]) < 1e-12 * big) continue;
                sb.Append("\tTbar").Append(k).Append('\t')
                  .Append(fB[k].ToString("E3", CultureInfo.InvariantCulture));
                foreach (var g in got)
                    sb.Append('\t')
                      .Append((100 * Math.Abs(g.B[k] - fB[k]) / Math.Abs(fB[k]))
                                  .ToString("F2", CultureInfo.InvariantCulture));
                sb.AppendLine();
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-defect2.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Where the arrangement goes wrong at the level it computes: which of the twenty totals,
    /// and by how much of itself.
    /// </summary>
    [Fact]
    public void PerTotal()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tentry\tforbes\tbuchdahl\trel%\tsize_vs_largest%");

        foreach (string name in new[] { "Ladder3_Sphere", "Ladder3_A4_Middle",
                                        "Ladder3_A4_First", "Ladder2_A4_First",
                                        "Ladder2_A4_Second",
                                        "CookeTriplet_SPOTM_START_LO_ASPHERE" })
        {
            var d = Load(name);
            var (fT, fB) = TotalsFromTau(d.Forbes);
            var (bT, bB) = TotalsFromTau(NewRoute(name, null));

            double big = 0.0;
            for (int k = 1; k <= 10; k++)
            {
                big = Math.Max(big, Math.Abs(fT[k]));
                big = Math.Max(big, Math.Abs(fB[k]));
            }

            for (int pass = 0; pass < 2; pass++)
            {
                var f = pass == 0 ? fT : fB;
                var b = pass == 0 ? bT : bB;
                string tag = pass == 0 ? "T" : "Tbar";
                for (int k = 1; k <= 10; k++)
                {
                    if (Math.Abs(f[k]) < 1e-12 * big) continue;
                    double rel = 100 * Math.Abs(b[k] - f[k]) / Math.Abs(f[k]);
                    if (rel < 0.5) continue;              // only what is actually wrong
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}{2}\t{3:E3}\t{4:E3}\t{5:F2}\t{6:F2}",
                        name, tag, k, f[k], b[k], rel, 100 * Math.Abs(f[k]) / big));
                }
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-per-total.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>One coefficient's relative error, or a dash where there is nothing to divide by.</summary>
    private static string Relative(double mine, double reference)
    {
        if (Math.Abs(reference) < 1e-300) return "-";
        return (100 * Math.Abs(mine - reference) / Math.Abs(reference))
            .ToString("F3", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Pins how M (68.8)'s accumulations map onto the scheme's entries, using the SPHERICAL case
    /// as the identity - it is verified against Buchdahl's own printed numbers, so whatever the
    /// symbols mean, the two must agree there.
    ///
    /// <para>(68.8) has <c>s-_1p = q s_1p - ['A_q - q('A-_p + 'A_q) + q^2 'A_p] a_p + ...</c>,
    /// and the scheme forms the same barred entry as <c>a_p t31 + q s_1p</c>. So <c>t31</c> must
    /// BE the negative of that bracket. If it is, reading the bracket off t15, t16 and t20 is
    /// right and the (68.8) reading was transcribed correctly; if it is not, that reading was
    /// measuring the wrong quantity and its result means nothing.</para>
    /// </summary>
    [Fact]
    public void WhatTheAccumulationsInEq688Are()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\tt31\t-[A_q-q(Abar_p+A_q)+q^2 A_p]\tdiff\tusing t21 instead");

        foreach (string name in new[] { "Ladder3_Sphere", "CookeTriplet",
                                        "Ladder3_A4_Middle", "KingslakeDG" })
        {
            var rows = RowsFor(name, out int count);
            for (int i = 1; i < count - 1; i++)
            {
                var t = rows[i].T;
                double q = t[6];
                double withT20 = -(t[20] - q * (t[16] + t[20]) + q * q * t[15]);
                double withT21 = -(t[21] - q * (t[16] + t[21]) + q * q * t[15]);
                if (Math.Abs(t[31]) < 1e-14 && Math.Abs(withT20) < 1e-14) continue;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E5}\t{3:E5}\t{4:E2}\t{5:E5}",
                    name, i, t[31], withT20, t[31] - withT20, withT21));
            }
        }
        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-t31.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// How much of the aspheric machinery a FIGURED SPHERE actually exercises.
    ///
    /// <para>The control rests on it: those rungs come out exact, the r^4 rungs do not, and the
    /// difference between them is what says the defect lives in the figuring's PRIMARY content.
    /// That inference is only worth as much as the control is - if a figured sphere barely
    /// perturbs the scheme at all, "exact" there is not evidence of anything.</para>
    ///
    /// <para>So this prints, per figured surface, the three things the scheme carries: the
    /// figured primary alpha, the figured secondary, and the figured tertiary. A figured sphere
    /// should show alpha at zero and the other two alive; if all three are near zero it is a null
    /// test wearing a control's clothes.</para>
    /// </summary>
    [Fact]
    public void WhatAFiguredSphereExercises()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\talpha\t|sec_fig|\t|z_check|\tfiguring_work_pct");

        foreach (string name in new[] { "Ladder1_FiguredSphere",
                                        "Ladder2_FiguredSphere_First",
                                        "Ladder2_FiguredSphere_Both",
                                        "Ladder3_FiguredSphere_Middle",
                                        "Ladder1_A4", "Ladder2_A4_First",
                                        "Ladder3_A4_Middle" })
        {
            var d = Load(name);
            var bare = LoadStripped(name);
            double work = 0.0;
            for (int k = 1; k <= 20; k++)
                work = Math.Max(work, Math.Abs(d.Forbes[k] - bare[k]) / d.Largest);

            var rows = RowsFor(name, out int count);
            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i];

                double sec = 0.0, z = 0.0;
                for (int m = 1; m <= 6; m++) sec = Math.Max(sec, Math.Abs(r.SecFig[m]));
                for (int m = 1; m <= 10; m++) z = Math.Max(z, Math.Abs(r.ZCheck[m]));
                if (Math.Abs(r.ApFigured) < 1e-15 && sec < 1e-15 && z < 1e-15) continue;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E3}\t{3:E3}\t{4:E3}\t{5:F2}",
                    name, i, r.ApFigured, sec, z, 100 * work));
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-control.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>Is t86 the accumulated first q-side secondary on a FIGURED system?</b>
    ///
    /// <para>Six dagger corrections have failed, and every one assumed that <c>t86|i -
    /// t86|i-1</c> is surface i-1's q-side secondary. Asking whether the increment is "a
    /// per-surface quantity" settles nothing - any closed form of accumulations has increments
    /// that depend only on what lies at and before the surface. The question with content is
    /// whether the closed form is <c>'S1_q</c> at all once the system is figured.</para>
    ///
    /// <para><b>An independent value.</b> M (21.6) gives <c>omega1 = 'S-1_p - 'S1_q</c> and
    /// (22.41) <c>omega7 = 4 'S-1_p - 'S2_p</c>; together they give
    /// <c>omega7 - 2 omega1 = 3[AB] + paraxial terms</c>, and (22.12) accumulates [AB] surface
    /// by surface from the primaries. So</para>
    /// <code>
    ///   'S1_q = t70 - (omega7 - 3[AB] + N1 vp^2 dvp - (1/4) N1 dvp (vp^2 + 3 vp1^2)) / 2
    /// </code>
    /// <para>from nothing but the p-side secondary sums and the primaries - all verified on
    /// figured systems, by the primary and secondary identities and against Forbes.
    /// <c>TheBracketReproducesTheDeterminedAB</c> closes this at 1E-9 on Buchdahl's spherical
    /// triplet. The identities come from the characteristic function, not from the surfaces
    /// being spheres, so they hold for figuring as well.</para>
    ///
    /// <para>Each residual is printed against the figured content of t86 itself - the
    /// difference from a spherical twin with the same stop - so it reads as a share of what
    /// the figuring put there.</para>
    /// </summary>
    [Fact]
    public void IsT86TheAccumulatedFirstQSecondary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\tt86\tidentity\trel\tt86_figured\tresid/figured"
                    + "\td_t86\td_identity\td_resid/d_figured");

        foreach (string name in Designs)
        {
            var rows = RowsFor(name, out int count, out var twin, out double n1);
            double vp1 = rows[1][2];
            double ab = 0.0;
            double prevT86 = 0.0, prevId = 0.0, prevFig = 0.0;

            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i];
                double vp = r[2];
                double omega7 = 4.0 * r[70] - r[71];
                double dvp = vp * vp - vp1 * vp1;
                double omega1 = 0.5 * (omega7 - 3.0 * ab + n1 * vp * vp * dvp
                                       - 0.25 * n1 * dvp * (vp * vp + 3.0 * vp1 * vp1));
                double identity = r[70] - omega1;
                double t86 = r[86];
                double figured = t86 - twin[i][86];

                double scale = Math.Max(Math.Abs(t86), Math.Abs(identity));
                double rel = scale < 1e-14 ? 0.0 : Math.Abs(t86 - identity) / scale;
                double ofFig = Math.Abs(figured) < 1e-14 ? double.NaN
                             : Math.Abs(t86 - identity) / Math.Abs(figured);

                double dT = t86 - prevT86, dI = identity - prevId, dF = figured - prevFig;
                double dOfFig = Math.Abs(dF) < 1e-14 ? double.NaN : Math.Abs(dT - dI) / Math.Abs(dF);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:E6}\t{3:E6}\t{4:E2}\t{5:E3}\t{6:E2}\t{7:E4}\t{8:E4}\t{9:E2}",
                    name, i, t86, identity, rel, figured, ofFig, dT, dI, dOfFig));

                prevT86 = t86; prevId = identity; prevFig = figured;

                // (22.12): Delta[AB] = (1/N1){(A|b) - (B|a) + (a|b)}, the pairing skew over p, q.
                double Ap = r[15], Aq = r[20], Bp = 2.0 * r[16], Bq = r[22];
                double ap = r[10], aq = r[99], bp = 2.0 * r[11], bq = r[100];
                ab += (Ap * bq - Aq * bp - (Bp * aq - Bq * ap) + (ap * bq - aq * bp)) / n1;
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-t86-identity.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// <b>What the barred q-side accumulation the dagger family carries SHOULD be</b>, from the
    /// identities, against what the scheme builds.
    ///
    /// <para>The (I) barred members are <c>t102 = q t70 - 'S-1_q</c> and partners, so the
    /// scheme's <c>'S-_q</c> is read back as <c>q 'S-_p - t102</c>. <see cref="BuchdahlSecondaryQ"/>
    /// recovers the same accumulations from M Sec. 22 without the dagger recursion at all -
    /// <c>Sbar_1q</c> from omega9 and t73, <c>Sbar_3q</c> from omega15 and t79, neither touching
    /// a q-side closed form. So the value the recursion must reach is known per surface, and
    /// every reading of the correction can be judged against it rather than against the totals
    /// several stages downstream.</para>
    ///
    /// <para>Printed four ways: the (I) member as built, the same with the lift correction taken
    /// back out, and the (Y) member likewise on the height ratio - so it says whether the
    /// correction should exist, and on which family.</para>
    /// </summary>
    [Fact]
    public void WhatTheBarredQAccumulationShouldBe()
    {
        var sb = new StringBuilder();
        sb.AppendLine("design\tsurf\tm\tidentity\tI_built\tI_nocorr\tY_built"
                    + "\terr_I_built\terr_I_nocorr\terr_Y_built\tlift_corr");

        // (m, barred p sum, (I) barred member): Sbar_1q .. Sbar_5q.
        var sites = new[] { (1, 70, 102), (2, 72, 104), (3, 74, 107), (4, 76, 109), (5, 78, 112) };

        foreach (string name in Designs)
        {
            var rows = RowsFor(name, out int count, out _, out double n1);
            var corr = new double[6];

            for (int i = 1; i < count - 1; i++)
            {
                var r = rows[i];
                var t = r.T;

                if (i > 1)
                {
                    var prv = rows[i - 1];
                    double dr = prv.Rho - prv.T[6];
                    for (int m = 0; m < 5; m++) corr[m] += dr * prv.SecBarFigLift[m];
                }

                if (r.FlatInCollimatedSpace || Math.Abs(t[6]) > 1e6) continue;
                var id = BuchdahlSecondaryQ.At(rows, i, n1);

                // The recovery's one internal check, (22.42) against (22.53) for Sbar_2q, on the
                // figured rows as well - it passes through t92 and so vouches for the closed forms
                // Sbar_2q, Sbar_4q and Sbar_5q lean on.
                double alt = BuchdahlSecondaryQ.SecondBarredQAlternative(rows, i, n1);
                double altScale = Math.Max(Math.Abs(alt), 1e-14);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "#alt\t{0}\t{1}\t(22.42) {2:E6}\t(22.53) {3:E6}\trel {4:E2}",
                    name, i, id[2], alt, Math.Abs(id[2] - alt) / altScale));

                foreach (var (m, sp, member) in sites)
                {
                    double target = id[m];
                    double iBuilt = t[6] * t[sp] - t[member];
                    double iNo = t[6] * t[sp] - (t[member] + corr[m - 1]);
                    // The (Y) recursion carries no lift correction, so it has no second column.
                    double yBuilt = r.Rho * t[sp] - r.Y[member];

                    double scale = Math.Max(Math.Abs(target), 1e-14);
                    string E(double v) =>
                        (Math.Abs(v - target) / scale).ToString("E2", CultureInfo.InvariantCulture);

                    if (Math.Abs(target) < 1e-14 && Math.Abs(iBuilt) < 1e-14) continue;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3:E5}\t{4:E5}\t{5:E5}\t{6:E5}\t{7}\t{8}\t{9}\t{10:E3}",
                        name, i, m, target, iBuilt, iNo, yBuilt,
                        E(iBuilt), E(iNo), E(yBuilt), corr[m - 1]));
                }
            }
        }

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "aspheric-qbar-identity.tsv");
        File.WriteAllText(path, sb.ToString());
        _out.WriteLine(sb.ToString());
    }

    private static BuchdahlTableIRow[] RowsFor(string name, out int count,
                                               out BuchdahlTableIRow[] twin, out double n1)
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
        n1 = n[0];
        twin = BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter, iota: iota);
        return BuchdahlTableI.Compute(sys.Surfaces, n, p.Efl, stopParameter, increments,
                                      iota: iota);
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
