using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// A ladder of minimal designs that switches Buchdahl's INDUCED terms on and off, to separate
/// them from the intrinsic ones on a figured surface.
///
/// <para><b>The device.</b> An induced contribution is a product of what a surface has of its
/// own with what the surfaces ahead of it have accumulated. Give a system exactly ONE powered
/// surface and there is nothing ahead to accumulate, so every induced term vanishes identically
/// and what remains is intrinsic. Add a second powered surface and they switch on. The plano
/// stop ahead of the lens gives a real stop shift q without contributing anything itself, a
/// plane having no power.</para>
///
/// <para><b>Why it was needed.</b> The aspheric tertiary is wrong and had resisted localisation
/// for a long time, because every design it had been measured on was a six-surface triplet
/// where intrinsic and induced content are inseparable. On the ladder they separate completely.
/// </para>
///
/// <para><b>What it found</b>, measured at the commit that added this file, as the worst
/// disagreement with the ray-inversion oracle expressed as a share of the LARGEST coefficient
/// in the set - relative error is meaningless on a coefficient three orders below the rest,
/// which is where the inversion noise floor sits:</para>
///
/// <code>
///   one powered surface, spherical        no induced      0.032%
///   one powered surface, r^4 figured      no induced      0.026%
///   one powered surface, conic figured    no induced      0.112%
///   two powered surfaces, spherical       induced         0.005%
///   two powered surfaces, r^4 on first    induced          1.716%
///   two powered surfaces, r^4 on second   induced         12.282%
/// </code>
///
/// <para>So the INTRINSIC aspheric tertiary is right - a figured surface with no induced terms
/// is as accurate as a spherical one - and the whole error is in the induced stage. The two
/// failing cases decompose without ambiguity, because the spherical ladder is clean and the
/// one-surface figured cases are clean: in the first, a SPHERICAL surface's induced terms are
/// built from accumulated FIGURED content; in the second, a FIGURED surface's induced terms are
/// built from accumulated SPHERICAL content. Both are wrong.</para>
///
/// <para>The second figure was 38.896 per cent when this file was written. It came down to
/// 16.542 when the check pass was given the (Y) family at secondary order, and to 12.282 when
/// it was given the (Y) family at tertiary order too. The FIRST is untouched by both, its
/// figured surface being the first powered one, with nothing accumulated ahead of it for
/// either family to carry - so that rung is now the whole of what is left.</para>
///
/// <para>The two failing cases are deliberately NOT pinned to a number here. Freezing today's
/// wrong values would bless them, and whoever fixes the induced stage would have to rewrite the
/// assertion. What is asserted of them is that the fit closes, which is what makes the oracle
/// worth believing at all.</para>
/// </summary>
public class AsphericInducedLadderTests
{
    /// <summary>The scheme coefficients alone, tau1..tau20, without tracing anything.</summary>
    private static Func<int, double> Coefficients(string fixtureName)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);
        var t = b.Totals;
        return k => k == 1 ? t.B7
                  : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;
    }

    private static (double WorstShare, double Residual) Score(string fixtureName)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(fixtureName), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var p = ParaxialTrace.Trace(sys, n, field);
        var b = BuchdahlCoefficients.Compute(sys, p);
        TertiaryCoefficients.Attach(sys, n, p, b, field);

        var inv = CoefficientInversion.Invert(sys, n, p, field);
        Assert.NotNull(inv);

        var t = b.Totals;
        double Scheme(int k) => k == 1 ? t.B7
            : (double)typeof(BuchdahlTerms).GetField("Tau" + k)!.GetValue(t)!;

        double big = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(Scheme(k)));
        Assert.True(big > 0.0, $"{fixtureName}: every coefficient is zero");

        double worst = 0.0;
        for (int k = 1; k <= 20; k++)
            worst = Math.Max(worst, Math.Abs(Scheme(k) - inv!.Tau[k]) / big);

        return (worst, inv!.Residual);
    }

    /// <summary>
    /// The ladder itself has to be sound before anything read off it means something: the fit
    /// must close on every rung, and the spherical rungs must come out right, with and without
    /// induced terms.
    /// </summary>
    [Theory]
    [InlineData("Ladder1_Sphere")]
    [InlineData("Ladder2_Sphere")]
    public void TheSphericalRungsAreCorrect(string fixtureName)
    {
        var (worst, residual) = Score(fixtureName);

        Assert.True(residual < 1e-3,
            $"{fixtureName}: the least-squares fit did not close, residual {residual:E2}. " +
            "Nothing read off this rung means anything.");
        Assert.True(worst < 0.005,
            $"{fixtureName}: worst disagreement with the rays is {100 * worst:F3} per cent of " +
            "the largest coefficient, on a system of SPHERES where the scheme is known right. " +
            "The ladder or the oracle has broken, not the aspheric tertiary.");
    }

    /// <summary>
    /// The finding: with the induced terms switched off, a FIGURED surface is as accurate as a
    /// spherical one. Both kinds of figuring - the conic, which reaches the coefficients by one
    /// path, and the r^4 polynomial term, which reaches them by another.
    ///
    /// <para>This is what says the intrinsic side is finished and the remaining work is all in
    /// the induced stage. If it ever fails, that conclusion is void.</para>
    /// </summary>
    [Theory]
    [InlineData("Ladder1_A4")]
    [InlineData("Ladder1_Conic")]
    public void TheIntrinsicAsphericTertiaryIsCorrectWhereNoInducedTermsExist(string fixtureName)
    {
        var (worst, residual) = Score(fixtureName);

        Assert.True(residual < 1e-3,
            $"{fixtureName}: the fit did not close, residual {residual:E2}");
        Assert.True(worst < 0.005,
            $"{fixtureName}: worst disagreement with the rays is {100 * worst:F3} per cent of " +
            "the largest coefficient. On a single powered surface there are NO induced terms, " +
            "so this is the intrinsic aspheric tertiary alone, and it was 0.026 per cent (r^4) " +
            "and 0.112 per cent (conic) when this test was written.");
    }

    /// <summary>
    /// Figuring that is present must be read whether or not the surface carries the label for
    /// it. The two fixtures here are the same lens, differing only in <c>Type</c>: one says
    /// <c>EvenAsphere</c>, the other <c>Standard</c>, and both have the same r^4 coefficient.
    ///
    /// <para>They must give the same coefficients, because the ray trace gives the same rays.
    /// Before this test the scheme skipped the figuring on the unlabelled one and returned the
    /// SPHERICAL answer, which reads as a 151 per cent error in tau1 - a coefficient that is
    /// in fact computed correctly. Every reader in the repository sets the label, so no file
    /// on disk was ever affected; a hand-edited file or a system built through the API is.
    /// </para>
    /// </summary>
    [Fact]
    public void FiguringIsReadWhetherOrNotTheSurfaceIsLabelledAnAsphere()
    {
        var labelled = Coefficients("Ladder1_A4");
        var unlabelled = Coefficients("Ladder1_A4_Unlabelled");

        double big = 0.0;
        for (int k = 1; k <= 20; k++) big = Math.Max(big, Math.Abs(labelled(k)));

        for (int k = 1; k <= 20; k++)
            Assert.True(Math.Abs(labelled(k) - unlabelled(k)) / big < 1e-12,
                $"tau{k}: labelled EvenAsphere gives {labelled(k):E6}, the same lens left " +
                $"Standard gives {unlabelled(k):E6}. The figuring is present on both.");
    }

    /// <summary>
    /// The two rungs where the scheme is known to be wrong. Only the soundness of the oracle is
    /// asserted - see the class remarks for why the disagreement itself is left unpinned.
    /// </summary>
    [Theory]
    [InlineData("Ladder2_A4_First")]
    [InlineData("Ladder2_A4_Second")]
    public void TheOracleIsSoundOnTheRungsWhereTheInducedStageFails(string fixtureName)
    {
        var (_, residual) = Score(fixtureName);

        Assert.True(residual < 1e-3,
            $"{fixtureName}: the fit did not close, residual {residual:E2}. The traced " +
            "degree-seven data is not representable in the tau basis, so the disagreement on " +
            "this rung could not be blamed on the scheme.");
    }
}
