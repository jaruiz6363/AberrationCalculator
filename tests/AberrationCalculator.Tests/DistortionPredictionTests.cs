using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Forbes;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Distortion predicted from the coefficients, against distortion traced.
///
/// <para><b>What this reaches that nothing else here does.</b> Every other acceptance test in
/// this repository compares a PREDICTED RMS SPOT with a traced one, and a spot is one number
/// standing in for eighteen coefficients: errors that cancel pass unnoticed, and
/// <c>docs/spot-prediction.md</c> records that happening. Distortion is the opposite case.
/// At zero pupil radius Robb's polynomial has three terms and they are separated by their
/// power of the field alone:</para>
///
/// <code>
///   eps_y(0, theta, h) = E h^3 + E5 h^5 + tau20 h^7
/// </code>
///
/// <para>So each of the three can be measured on its own against exact rays. It is also not
/// B7: B7 is pure aperture and contributes nothing to distortion, so nothing here could be
/// passed by the one seventh-order quantity that was available before the tertiary work.</para>
///
/// <para><b>What this adds over <see cref="CoefficientInversion"/>, which came first.</b> That
/// recovers all twenty from traced rays by scaling ray shapes and fitting an odd polynomial in
/// the scale, and <c>ForbesCoefficientsTests</c> already uses it to establish, on nine
/// fixtures, that Forbes agrees with rays where this program's aspheric arrangement does not.
/// This route is narrower: three coefficients, no basis, no least-squares solve, no model of
/// the other seventeen, and an error bar of its own. What follows is therefore a corroboration
/// by a second and much simpler instrument rather than a first finding - and the two agree on
/// tau20 to between 0.03 and 1.2 per cent across the figured fixtures.</para>
///
/// <para><b>And it is a two-sided check.</b> A truncation's error must fall at the right RATE
/// as the field shrinks - h^2 for third order, h^4 for fifth, h^6 for seventh - which is what
/// the coefficient being right MEANS. A wrong coefficient still fits at one field.</para>
/// </summary>
public class DistortionPredictionTests
{
    private sealed record Setup(OpticalSystem System, double[] Indices, ParaxialResult Paraxial,
                                BuchdahlTerms Totals, double Field);

    private static Setup Load(string name)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        int primary = sys.PrimaryWavelengthIndex < 0 ? 0 : sys.PrimaryWavelengthIndex;
        var n = IndexResolver.Build(sys, catalog, sys.Wavelengths[primary].Value, new List<string>());

        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        return new Setup(sys, n, p, b.Totals, field);
    }

    private static IReadOnlyList<DistortionPrediction.Row> Rows(Setup s, params double[] hs)
        => DistortionPrediction.Compare(s.System, s.Indices, s.Paraxial, s.Totals, s.Field, hs);

    // ── The three coefficients, one at a time ───────────────────────────────────────────

    /// <summary>
    /// The error of each truncation must fall at the order of the first term it omits: halving
    /// the field divides the third-order error by four, the fifth-order error by sixteen and
    /// the seventh-order error by sixty-four.
    ///
    /// <para>This is the whole test in one. A wrong E would leave the third-order error
    /// constant instead of falling; a wrong E5 would leave the fifth-order error falling like
    /// four rather than sixteen; a wrong tau20 leaves the seventh-order error falling like
    /// sixteen rather than sixty-four - which is exactly what the aspheric designs below do.
    /// No tolerance on a single field could distinguish any of those from being right.</para>
    /// </summary>
    [Fact]
    public void EachTruncationConvergesAtItsOwnOrder()
    {
        var s = Load("CookeTriplet");
        var rows = Rows(s, 0.4, 0.2, 0.1, 0.05);

        var expected = new Dictionary<int, double> { [3] = 4.0, [5] = 16.0, [7] = 64.0 };
        foreach (var (order, rate) in expected)
        {
            // The coarsest step still carries the next order strongly, so the rate is read
            // from the three finest, where the leading term dominates.
            for (int i = 1; i + 1 < rows.Count; i++)
            {
                double coarse = Math.Abs(rows[i].RelativeError(order));
                double fine = Math.Abs(rows[i + 1].RelativeError(order));
                double ratio = coarse / fine;

                Assert.True(ratio > 0.6 * rate && ratio < 1.7 * rate,
                    $"order {order}: halving the field from H = {rows[i].H} divided the error "
                  + $"by {ratio:F1}, not by about {rate}. The h^{order} coefficient does not "
                  + "match the rays.");
            }
        }
    }

    /// <summary>
    /// The recovered coefficients are the same numbers the program reports, on an
    /// all-spherical design. Recorded rather than merely asserted: these are E, E5 and tau20
    /// measured from rays, and the seventh-order one is measured nowhere else.
    /// </summary>
    [Fact]
    public void TheRaysReturnTheCoefficientsOnASphericalTriplet()
    {
        var s = Load("CookeTriplet");
        var found = DistortionPrediction.Recover(s.System, s.Indices, s.Paraxial, s.Totals, s.Field);

        Assert.Equal(3, found.Count);
        foreach (var r in found)
        {
            Assert.True(r.Reliable,
                $"{r.Name}: the two field fractions the recovery used disagree by {r.Spread:P1}, "
              + "so nothing was measured");
            Assert.True(Math.Abs(r.Ratio - 1.0) < 0.02,
                $"{r.Name}: reported {r.Reported:E6}, rays say {r.FromRays:E6}, ratio {r.Ratio:F4}");
        }
    }

    /// <summary>
    /// <b>Figuring by itself does not break it.</b> On these three the surface is figured - a
    /// conic, an r^4 asphere, a figured sphere - Buchdahl's scheme and Forbes' trace already
    /// agree, and the rays confirm both. So the disagreement recorded below is not "aspherics",
    /// and any account of it has to survive these.
    ///
    /// <para>Their tau20 is 5E-08, three orders below the triplets', and it still comes back:
    /// which is what says the recovery measures a coefficient rather than reproducing a large
    /// number.</para>
    /// </summary>
    [Theory]
    [InlineData("Ladder1_Conic")]
    [InlineData("Ladder1_A4")]
    [InlineData("Ladder2_FiguredSphere_Both")]
    public void TheRaysReturnTheCoefficientsOnFiguredDesignsTheTwoRoutesAgreeOn(string fixtureName)
    {
        var s = Load(fixtureName);
        var found = DistortionPrediction.Recover(s.System, s.Indices, s.Paraxial, s.Totals, s.Field);

        var forbes = ForbesCoefficients.Invert(s.System, s.Indices, s.Paraxial, s.Field);
        Assert.NotNull(forbes);
        Assert.True(Math.Abs(forbes!.Tau[20] / s.Totals.Tau20 - 1.0) < 0.001,
            $"{fixtureName} was chosen because the two routes agree on tau20, and they no "
          + $"longer do: Buchdahl {s.Totals.Tau20:E6}, Forbes {forbes.Tau[20]:E6}");

        var tau20 = found.Single(r => r.Name == "Tau20");
        Assert.True(tau20.Reliable, $"tau20 could not be recovered at all on {fixtureName}");
        Assert.True(Math.Abs(tau20.Ratio - 1.0) < 0.02,
            $"tau20: reported {tau20.Reported:E6}, rays say {tau20.FromRays:E6}");
    }

    /// <summary>
    /// <b>THIS PROGRAM'S aspheric tertiary arrangement is wrong, and this measures it.</b> On
    /// the aspheric testbed the rays put tau20 at about twice what it reports, while E and E5
    /// come back exactly - so it is the SEVENTH-order aspheric term that is at fault and not
    /// the conversion, the field variable or the trace, all of which the other two coefficients
    /// exercise identically.
    ///
    /// <para><b>It is not a finding about Buchdahl.</b> He never published the tertiary
    /// aspheric arrangement, so the figured coefficients here come from a reconstruction made
    /// in this repository, and that is what the rays disagree with. On spherical systems his
    /// scheme and Forbes' trace agree on all twenty tau to 2E-13 at both conjugates, and
    /// <see cref="TheRaysReturnTheCoefficientsOnASphericalTriplet"/> confirms tau20 there
    /// against rays as well - so there is no spherical case in which the two part company.</para>
    ///
    /// <para>That the aspheric tau are wrong is already recorded - see
    /// <c>docs/verification.md</c> and the Forbes work - but it was established by comparison
    /// with another series. This is rays, and it is per coefficient.</para>
    ///
    /// <para>The bound is deliberately loose. The point is not the value of the discrepancy,
    /// which will move when the arrangement is fixed; it is that there IS one, and that a test
    /// exists which will notice when it goes away.</para>
    ///
    /// <para><c>Ladder2_A4_Both</c> is the sharp case rather than the loud one. The two routes
    /// differ there by 12.65 per cent, not by a factor, and the rays land on Forbes' value to
    /// two parts in ten thousand. A factor of two could be almost any mistake; a twelve per
    /// cent gap hit to that precision could not be a coincidence.</para>
    /// </summary>
    [Theory]
    [InlineData("TertiaryTestbed_Triplet24")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("Ladder2_A4_Both")]
    public void TheAsphericSeventhOrderDistortionDisagreesWithTheRays(string fixtureName)
    {
        var s = Load(fixtureName);
        var found = DistortionPrediction.Recover(s.System, s.Indices, s.Paraxial, s.Totals, s.Field);

        var third = found.Single(r => r.Name == "E");
        var fifth = found.Single(r => r.Name == "E5");
        Assert.True(Math.Abs(third.Ratio - 1.0) < 0.01, $"E is off by {third.Ratio - 1.0:P2}");
        Assert.True(Math.Abs(fifth.Ratio - 1.0) < 0.01, $"E5 is off by {fifth.Ratio - 1.0:P2}");

        var tau20 = found.Single(r => r.Name == "Tau20");
        Assert.True(tau20.Reliable, "tau20 could not be recovered, so nothing is being claimed");
        Assert.True(Math.Abs(tau20.Ratio - 1.0) > 0.05,
            $"{fixtureName}: the aspheric tau20 now agrees with the rays to {tau20.Ratio:F4}. "
          + "If the aspheric tertiary arrangement has been fixed, this test has done its job "
          + "and should be turned around into the agreement it now records.");
    }

    /// <summary>
    /// And the Forbes series trace gets tau20 right where Buchdahl's scheme does not - on every
    /// one of these designs, to a fraction of a per cent. Two routes, rays adjudicating.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("TertiaryTestbed_Triplet24")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_PRMSA_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    [InlineData("Ladder2_A4_Both")]
    [InlineData("Ladder1_Conic")]
    public void TheForbesSeventhOrderDistortionAgreesWithTheRays(string fixtureName)
    {
        var s = Load(fixtureName);
        var tau20 = DistortionPrediction
            .Recover(s.System, s.Indices, s.Paraxial, s.Totals, s.Field)
            .Single(r => r.Name == "Tau20");
        Assert.True(tau20.Reliable, "tau20 could not be recovered from the rays");

        var forbes = ForbesCoefficients.Invert(s.System, s.Indices, s.Paraxial, s.Field);
        Assert.NotNull(forbes);

        double ratio = forbes!.Tau[20] / tau20.FromRays;
        Assert.True(Math.Abs(ratio - 1.0) < 0.02,
            $"{fixtureName}: the Forbes trace gives tau20 = {forbes.Tau[20]:E6} and the rays "
          + $"{tau20.FromRays:E6}, a ratio of {ratio:F4}");
    }

    /// <summary>
    /// <b>On a purely spherical system the two schemes never part company, and the rays back
    /// both.</b> This is the boundary of everything above: the disagreement is confined to the
    /// aspheric arrangement, which is the part Buchdahl never published and which had to be
    /// reconstructed here. Where he did publish, there is nothing at issue.
    ///
    /// <para>All twenty tau are compared, not only tau20, and against the largest of them so
    /// that a coefficient which is near zero cannot pass by being near zero. The measured
    /// departures are at roundoff - 3E-15 - except on the near-degenerate flat fixture, where
    /// they reach 2E-08 for reasons that belong to that fixture rather than to either scheme.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("Ladder1_Sphere")]
    [InlineData("Ladder2_Sphere")]
    [InlineData("Ladder2_Sphere_FlatRear")]
    [InlineData("Ladder2_Sphere_NearFlatRear")]
    [InlineData("Ladder2_FlatPlain")]
    [InlineData("Ladder2_FlatPlain_NearLimit")]
    public void OnSphericalSystemsTheTwoSchemesAgreeOnAllTwentyAndTheRaysBackBoth(string fixtureName)
    {
        var s = Load(fixtureName);

        foreach (var surface in s.System.Surfaces)
            Assert.True(surface.Conic == 0.0 && surface.AsphericCoefficients.All(c => c == 0.0),
                $"{fixtureName} is figured, so it does not belong in this test");

        var forbes = ForbesCoefficients.Invert(s.System, s.Indices, s.Paraxial, s.Field);
        Assert.NotNull(forbes);

        double largest = 0.0;
        for (int k = 1; k <= 20; k++) largest = Math.Max(largest, Math.Abs(forbes!.Tau[k]));

        for (int k = 1; k <= 20; k++)
        {
            double scheme = k == 1 ? s.Totals.B7
                : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(s.Totals)!;
            double departure = Math.Abs(scheme - forbes!.Tau[k]) / largest;
            Assert.True(departure < 1e-6,
                $"{fixtureName}: tau{k} differs between the two schemes by {departure:E2} of the "
              + "largest coefficient, on a system with no figuring, where nothing separates them.");
        }

        var tau20 = DistortionPrediction
            .Recover(s.System, s.Indices, s.Paraxial, s.Totals, s.Field)
            .Single(r => r.Name == "Tau20");
        Assert.True(tau20.Reliable, "tau20 could not be recovered from the rays");
        Assert.True(Math.Abs(tau20.Ratio - 1.0) < 0.02,
            $"{fixtureName}: both schemes give tau20 = {tau20.Reported:E6} and the rays "
          + $"{tau20.FromRays:E6}");
    }

    /// <summary>
    /// The two ray routes to tau20 agree. <see cref="CoefficientInversion"/> solves for all
    /// twenty at once from scaled ray shapes; this one differences three terms at zero pupil
    /// radius. They share the tracer and nothing else - no basis, no fit, no model of the other
    /// seventeen coefficients - so where they agree, very little is being taken on trust.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet")]
    [InlineData("TertiaryTestbed_Triplet24")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8")]
    [InlineData("Ladder2_A4_Both")]
    public void TheTwoRayRoutesToTau20Agree(string fixtureName)
    {
        var s = Load(fixtureName);

        var inversion = CoefficientInversion.Invert(s.System, s.Indices, s.Paraxial, s.Field);
        Assert.NotNull(inversion);

        var tau20 = DistortionPrediction
            .Recover(s.System, s.Indices, s.Paraxial, s.Totals, s.Field)
            .Single(r => r.Name == "Tau20");
        Assert.True(tau20.Reliable, "tau20 could not be recovered from the chief rays");

        double ratio = inversion!.Tau[20] / tau20.FromRays;
        Assert.True(Math.Abs(ratio - 1.0) < 0.02,
            $"{fixtureName}: the full inversion gives tau20 = {inversion.Tau[20]:E6} and the "
          + $"chief-ray recovery {tau20.FromRays:E6}, a ratio of {ratio:F4}");
    }

    // ── The conventions the answer depends on ───────────────────────────────────────────

    /// <summary>
    /// The field variable is a TANGENT. Reading h as a fraction of the field ANGLE instead
    /// puts the traced ray at the wrong place and moves its Gaussian reference with it, and
    /// the agreement above cannot survive it: at nine tenths of a 20 degree field the two
    /// readings differ by 0.8 per cent, which is 2.4 per cent in a term of degree three.
    /// </summary>
    [Fact]
    public void ReadingTheFieldAsAnAngleFractionRuinsIt()
    {
        var s = Load("CookeTriplet");
        const double h = 0.2;

        double tangent = Math.Abs(Rows(s, h)[0].RelativeError(7));

        // The same fraction taken of the angle rather than of its tangent.
        double angle = h * s.Field;
        var p = ParaxialTrace.Trace(s.System, s.Indices, angle);
        var land = RealRayTrace.TraceFrom(s.System, s.Indices, s.Paraxial,
                                          0.0, p.Ybar[1], 0.0, p.Ubar[0], 1.0);
        double displacement = land.Y - p.ParaxialImageHeight;
        double wrong = Math.Abs((DistortionPrediction.Displacement(s.Totals, h) - displacement)
                                / displacement);

        Assert.True(wrong > 100.0 * tangent,
            $"reading the field as an angle fraction is off by {wrong:P2} and reading it as a "
          + $"tangent fraction by {tangent:P2}. The two should not be close.");
    }

    /// <summary>
    /// B7 is the seventh-order coefficient every program has had for decades, and it says
    /// nothing about distortion: it carries the seventh power of the APERTURE and the zeroth
    /// of the field. tau20 is the reverse, and is the whole of what the seventh order
    /// contributes here.
    /// </summary>
    [Fact]
    public void TheSeventhOrderEntersThroughTau20AndNotB7()
    {
        var t = new BuchdahlTerms { E = 1e-2, E5 = 1e-3, B7 = 1.0, Tau20 = 1e-4 };
        double before = DistortionPrediction.Displacement(t, 0.7);

        t.B7 = -50.0;
        Assert.Equal(before, DistortionPrediction.Displacement(t, 0.7), 15);

        t.Tau20 = 0.0;
        Assert.NotEqual(before, DistortionPrediction.Displacement(t, 0.7), 15);
    }

    /// <summary>Truncation keeps what it says it keeps, and nothing else.</summary>
    [Fact]
    public void TruncationKeepsWhatItSaysItKeeps()
    {
        var t = new BuchdahlTerms { E = 2.0, E5 = 3.0, Tau20 = 5.0 };
        const double h = 0.5;

        Assert.Equal(2.0 * Math.Pow(h, 3), DistortionPrediction.Displacement(t, h, 3), 15);
        Assert.Equal(2.0 * Math.Pow(h, 3) + 3.0 * Math.Pow(h, 5),
                     DistortionPrediction.Displacement(t, h, 5), 15);
        Assert.Equal(2.0 * Math.Pow(h, 3) + 3.0 * Math.Pow(h, 5) + 5.0 * Math.Pow(h, 7),
                     DistortionPrediction.Displacement(t, h, 7), 15);
    }

    /// <summary>An on-axis-only design has no distortion, and says so instead of dividing by zero.</summary>
    [Fact]
    public void ADesignWithNoFieldYieldsNoRows()
    {
        var s = Load("CookeTriplet");
        Assert.Empty(DistortionPrediction.Compare(s.System, s.Indices, s.Paraxial, s.Totals, 0.0));
    }

    // ── How far into the field it is worth quoting ──────────────────────────────────────

    /// <summary>
    /// The measurement this exists to make. Through the middle of the field the seventh order
    /// is worth having and the numbers say by how much; at the corner of a 20 degree triplet it
    /// is not, and OVERSHOOTS by a third while third order alone happens to land within five
    /// per cent. Recorded so that a change to the coefficients has to account for it.
    ///
    /// <para>The overshoot is real and is not this program's: Forbes' series trace, run at the
    /// same truncation and sharing no code with the Buchdahl route, overshoots the same way,
    /// and taking it one degree further walks the prediction back towards the traced ray. It is
    /// the ninth order arriving, in a quantity small enough that its arrival is the whole
    /// answer. See <c>docs/distortion-prediction.md</c>.</para>
    /// </summary>
    [Theory]
    [InlineData(0.2, 7, 0.001)]    // seventh order, essentially exact in the inner field
    [InlineData(0.4, 7, 0.005)]
    [InlineData(0.6, 5, 0.010)]    // and fifth order is already good to a per cent there
    public void TheInnerFieldIsPredictedToAFractionOfAPerCent(double h, int order, double bound)
    {
        var s = Load("CookeTriplet");
        double error = Math.Abs(Rows(s, h)[0].RelativeError(order));
        Assert.True(error < bound,
            $"H = {h}, order {order}: error {error:P3}, bound {bound:P3}");
    }

    /// <summary>The corner of the same lens, where the truncation runs out.</summary>
    [Fact]
    public void TheSeventhOrderOvershootsAtTheCornerOfAWideFieldTriplet()
    {
        var r = Rows(Load("CookeTriplet"), 1.0)[0];

        // Traced 8.836E-03; the series says 1.186E-02.
        Assert.InRange(r.Displacement, 8.5e-3, 9.2e-3);
        Assert.InRange(r.RelativeError(7), 0.28, 0.40);

        // Third order alone is closer here, by cancellation rather than by being right: it is
        // seven per cent LOW at H = 0.8 and five per cent high at the corner.
        Assert.True(Math.Abs(r.RelativeError(3)) < Math.Abs(r.RelativeError(7)));
    }

    /// <summary>
    /// A slow, narrow-field design is described by third order alone, and the higher terms
    /// neither help nor hurt. The counterpart of the same finding for the spot: the order a
    /// design needs is a property of the design.
    /// </summary>
    [Fact]
    public void ASlowNarrowFieldDesignNeedsNoMoreThanThirdOrder()
    {
        var s = Load("Ladder1_Conic");
        foreach (var r in Rows(s, 0.2, 0.5, 0.8, 1.0))
        {
            Assert.True(Math.Abs(r.RelativeError(3)) < 0.005,
                $"H = {r.H}: third order is off by {r.RelativeError(3):P3}");
            Assert.True(Math.Abs(r.RelativeError(7)) < 0.005,
                $"H = {r.H}: the full seventh order is off by {r.RelativeError(7):P3}");
        }
    }

    // ── The two ways a caller reaches this ──────────────────────────────────────────────

    /// <summary>
    /// The MCP server offers it, and offers the same text the report builder produces. A tool
    /// wired to the wrong builder would pass every other test in the suite.
    /// </summary>
    [Fact]
    public void TheMcpServerOffersIt()
    {
        var writer = Mcp.Tools.Open(Fixtures.Lens("CookeTriplet"), null);
        var tool = Mcp.Tools.All.Single(t => t.Name == "distortion_from_coefficients");

        string text = tool.Run(writer);
        Assert.Equal(writer.BuildDistortionText(), text);
        Assert.Contains("DISTORTION FROM THE ABERRATION COEFFICIENTS", text);
        Assert.Contains("Tau20", text);
    }

    /// <summary>
    /// <b>A figured design is predicted from FORBES' tau20, without being asked.</b> The
    /// scheme's aspheric tertiary arrangement is a reconstruction the rays disagree with, so
    /// there is no reason to put a number known to be wrong in front of a designer, and no
    /// reason to make them choose between two. The report says which route it used.
    /// </summary>
    [Theory]
    [InlineData("CookeTriplet_SPOTM_START_LO_ASPHERE")]
    [InlineData("TertiaryTestbed_Triplet24")]
    [InlineData("Ladder2_A4_Both")]
    public void AFiguredDesignIsPredictedFromForbes(string fixtureName)
    {
        var s = Load(fixtureName);
        var forbes = ForbesCoefficients.Invert(s.System, s.Indices, s.Paraxial, s.Field);
        Assert.NotNull(forbes);

        // The fixture has to be one where the choice is visible, or the test proves nothing.
        Assert.True(Math.Abs(forbes!.Tau[20] / s.Totals.Tau20 - 1.0) > 0.01,
            $"{fixtureName}: the two routes now agree on tau20, so this fixture no longer "
          + "shows which one was used");

        string text = Mcp.Tools.Open(Fixtures.Lens(fixtureName), null).BuildDistortionText();

        Assert.Contains("FORBES series trace", text);
        Assert.Contains(forbes.Tau[20].ToString("0.0000E+00", CultureInfo.InvariantCulture), text);
        Assert.DoesNotContain(s.Totals.Tau20.ToString("0.0000E+00", CultureInfo.InvariantCulture),
                              text);
    }

    /// <summary>
    /// With nothing figured the two routes agree to roundoff, so the choice is empty and the
    /// scheme's own value is kept - which leaves every number validated at infinite conjugate
    /// bit-identical. The report says that too, rather than leaving the reader to wonder which
    /// of two identical numbers they are looking at.
    /// </summary>
    [Fact]
    public void ASphericalDesignKeepsTheSchemesOwnValueAndSaysSo()
    {
        var s = Load("CookeTriplet");
        string text = Mcp.Tools.Open(Fixtures.Lens("CookeTriplet"), null).BuildDistortionText();

        Assert.Contains("Every surface is spherical", text);
        Assert.Contains(s.Totals.Tau20.ToString("0.0000E+00", CultureInfo.InvariantCulture), text);
    }

    /// <summary>
    /// F-theta distortion is reported beside F-tan(theta), and is a different number: the two
    /// ideals differ by theta/tan(theta), which is 4.3 per cent at 20 degrees and swamps the
    /// distortion of any reasonable imaging lens. F-tan(theta) stays the default.
    /// </summary>
    [Fact]
    public void FThetaIsReportedBesideFTanTheta()
    {
        var s = Load("CookeTriplet");
        var row = Rows(s, 1.0)[0];

        // The F-theta ideal is f*theta against the F-tan(theta) ideal's f*tan(theta).
        double theta = row.Field * Math.PI / 180.0;
        Assert.Equal(row.Gaussian * theta / Math.Tan(theta), row.IdealFTheta, 9);

        // At 20 degrees that is 4.3 per cent, and it dwarfs this lens's 0.05 per cent of
        // real distortion - which is why the two are reported separately and not merged.
        Assert.InRange(row.TracedPercentFTheta, 4.0, 4.5);
        Assert.InRange(row.TracedPercent, 0.04, 0.06);

        // The two are related exactly by the mapping factor, and the report prints that
        // identity because the two tables otherwise look like a contradiction: -0.09 per cent
        // against +4.18 on the same lens, which is the mapping and not the design.
        double mapping = row.Gaussian / row.IdealFTheta;
        Assert.Equal(row.TracedPercentFTheta,
                     100.0 * ((1.0 + row.TracedPercent / 100.0) * mapping - 1.0), 9);

        string text = Mcp.Tools.Open(Fixtures.Lens("CookeTriplet"), null).BuildDistortionText();
        Assert.Contains("F-tan(th)", text);
        Assert.Contains("F-theta", text);
    }

    /// <summary>
    /// F-theta needs a field ANGLE to be proportional to. An object height is not one, and
    /// neither is an angle subtended from a finite object distance, so it is withheld rather
    /// than computed from something that does not mean it.
    /// </summary>
    [Fact]
    public void FThetaIsWithheldAtAFiniteConjugate()
    {
        var s = Load("CookeTriplet");
        s.System.Surfaces[0].Thickness = 250.0;

        var indices = s.Indices;
        var p = ParaxialTrace.Trace(s.System, indices, s.Field);
        var b = BuchdahlCoefficients.Compute(s.System, p);
        TertiaryCoefficients.Attach(s.System, indices, p, b, s.Field);

        var rows = DistortionPrediction.Compare(s.System, indices, p, b.Totals, s.Field,
                                                new[] { 0.5, 1.0 });
        foreach (var r in rows)
        {
            Assert.True(double.IsNaN(r.IdealFTheta), "F-theta was computed at a finite conjugate");
            Assert.True(double.IsNaN(r.TracedPercentFTheta));

            // F-tan(theta) still works there, and is what the finite conjugate is quoted in.
            Assert.False(double.IsNaN(r.TracedPercent));
        }
    }

    /// <summary>
    /// Where the file's image surface is not at paraxial focus the report says what the
    /// distortion reads there as well, because that is the figure a design program quotes and
    /// a reader comparing the two would otherwise think one of them was wrong. On the Cooke
    /// triplet the file sits 0.2073 inside paraxial focus and the corner reads 0.0620 per cent
    /// there against 0.0486 - LensHH-LT reports 0.062021 for this lens.
    ///
    /// <para>The table itself cannot move there: the coefficients are referred to the paraxial
    /// plane and the polynomial has no defocus term.</para>
    /// </summary>
    [Fact]
    public void TheFilesOwnImagePlaneIsReconciled()
    {
        var s = Load("CookeTriplet");
        var atFile = DistortionPrediction.Compare(s.System, s.Indices, s.Paraxial, s.Totals,
                                                  s.Field, new[] { 1.0 }, atParaxialFocus: false);

        Assert.Equal(0.062031, atFile[0].TracedPercent, 5);
        Assert.Equal(18.136026, atFile[0].Traced, 5);       // LensHH-LT: 18.136026
        Assert.Equal(18.124783, atFile[0].Gaussian, 5);     // LensHH-LT: 18.124785

        string text = Mcp.Tools.Open(Fixtures.Lens("CookeTriplet"), null).BuildDistortionText();
        Assert.Contains("The file's image surface is", text);

        // And it stays quiet when the planes coincide, rather than printing a paragraph about
        // a rounding-sized offset.
        Assert.DoesNotContain("The file's image surface is", Mcp.Tools
            .Open(Fixtures.Lens("CookeTriplet_PRMSA_START_LO_ASPHERE"), null)
            .BuildDistortionText());
    }

    /// <summary>And so does the command line, under --distortion.</summary>
    [Fact]
    public void TheCommandLineOffersIt()
    {
        var captured = new StringWriter();
        var previous = Console.Out;
        int code;
        try
        {
            Console.SetOut(captured);
            code = Cli.Program.Run(new[] { Fixtures.Lens("CookeTriplet"), "--distortion" });
        }
        finally
        {
            Console.SetOut(previous);
        }

        Assert.Equal(0, code);
        Assert.Contains("DISTORTION FROM THE ABERRATION COEFFICIENTS", captured.ToString());

        // And it writes nothing: this mode answers one question and leaves the folder alone.
        Assert.False(File.Exists(Path.Combine(Fixtures.LensDir, "CookeTriplet.report.txt")),
            "--distortion wrote the report files, which it is not supposed to");
    }
}
