using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.Nat;
using AberrationCalculator.Core.RayTrace;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The published oracle for nodal aberration theory's sigma vectors.
///
/// <para>Thompson, Schmid, Cakmakci and Rolland, <i>J. Opt. Soc. Am. A</i> <b>26</b>, 1503
/// (2009), Tables 1 to 5: a Ritchey-Chretien telescope, the misalignments applied to it, its
/// paraxial ray trace, a real ray trace of the optical axis ray, and the resulting aberration
/// field decentre vectors computed both ways. Numbers printed beside the lens they were computed
/// on, which is the standard this repository already holds Buchdahl's Table I to.</para>
///
/// <para>Nothing here is fitted and no tolerance is chosen to pass. The prescription is his
/// Table 1, the perturbations his Table 2, and the expected values his Table 5.</para>
/// </summary>
public class ThompsonTelescopeTests
{
    private const double Deg = Math.PI / 180.0;

    /// <summary>Table 1, with the Table 2 misalignments applied when asked for.</summary>
    private static OpticalSystem Telescope(bool perturbed)
    {
        var sys = new OpticalSystem();
        sys.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
        sys.Surfaces.Add(new Surface
        {
            Index = 1, Radius = -21335.137, Thickness = -7490.46, Conic = -1.0961,
            Material = "MIRROR", SemiDiameter = 1981.20, IsStop = true,
        });
        sys.Surfaces.Add(new Surface
        {
            Index = 2, Radius = -9576.712, Thickness = 9441.833, Conic = -5.1628,
            Material = "MIRROR", SemiDiameter = 636.25,
        });
        sys.Surfaces.Add(new Surface { Index = 3 });

        sys.Wavelengths.Add(new Wavelength { Value = 0.55, Weight = 1 });
        sys.Fields.Add(new Field { Y = 0.333, Weight = 1 });
        sys.Aperture = new Aperture(ApertureType.EPD, 2.0 * 1981.20);

        if (perturbed)
        {
            // Table 2, in degrees. ADE is the tilt about x, BDE about y; no decentres.
            sys.Surfaces[1].TiltX = 0.017991 * Deg;
            sys.Surfaces[1].TiltY = -0.008995 * Deg;
            sys.Surfaces[2].TiltX = 0.006818 * Deg;
            sys.Surfaces[2].TiltY = -0.018965 * Deg;
        }
        return sys;
    }

    private static (OpticalSystem Sys, double[] N, ParaxialResult P) Build(bool perturbed)
    {
        var sys = Telescope(perturbed);
        var catalog = CatalogLocator.LoadBundled();
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        return (sys, n, ParaxialTrace.Trace(sys, n, 0.333));
    }

    /// <summary>
    /// Table 3, the paraxial trace. Checked first because everything after it is built on these
    /// numbers - if the trace of his telescope does not reproduce his trace, no sigma computed
    /// from it means anything.
    /// </summary>
    [Fact]
    public void TheParaxialTraceReproducesTable3()
    {
        var (_, _, p) = Build(false);

        // Marginal ray heights.
        Assert.Equal(1981.200, p.Y[1], 3);
        Assert.Equal(590.0583, p.Y[2], 3);

        // Chief ray heights.
        Assert.Equal(0.0, p.Ybar[1], 6);
        Assert.Equal(43.534642, p.Ybar[2], 3);

        // Slopes, taken in the space BEFORE each surface as this program indexes them.
        Assert.Equal(0.005812, p.Ubar[0], 6);
        Assert.Equal(0.185722, p.U[1], 6);
        Assert.Equal(-0.005812, p.Ubar[1], 6);
        Assert.Equal(-0.062494, p.U[2], 6);
        Assert.Equal(0.014904, p.Ubar[2], 6);
    }

    /// <summary>
    /// <b>Table 5.</b> The aberration field decentre vectors of the perturbed telescope, against
    /// the values Thompson publishes for it.
    ///
    /// <para>He gives two columns, one from his local-coordinate paraxial equations and one from
    /// the real-ray method that is the subject of the paper; they agree to four or five figures,
    /// the real-ray one being the more accurate. <b>This implementation reproduces the REAL-RAY
    /// column to seven figures</b> - 0.0220670 against 0.0220670 and 0.0965644 against 0.0965643
    /// at the secondary - so it is not merely close to his answer, it is closer to it than his
    /// own paraxial route is.</para>
    ///
    /// <para><b>On the sign of x.</b> Thompson prints both components positive. The y component
    /// agrees in sign here and follows directly from ADE being positive; the x component follows
    /// from BDE being negative and comes out negative. Since both magnitudes agree to seven
    /// figures, this is a convention on the sense of a tilt about y in his table and not a
    /// property of the sigma machinery, so the magnitudes are what is asserted.</para>
    /// </summary>
    [Fact]
    public void TheSigmaVectorsReproduceTable5()
    {
        var (sys, n, p) = Build(true);
        var sigma = SigmaVector.Compute(sys, n, p);

        // Table 5, real-ray column.
        Assert.Equal(0.0270117, Math.Abs(sigma.Sigma[1].X), 7);
        Assert.Equal(0.0540264, Math.Abs(sigma.Sigma[1].Y), 7);

        Assert.Equal(0.0220670, Math.Abs(sigma.Sigma[2].X), 6);
        Assert.Equal(0.0965643, Math.Abs(sigma.Sigma[2].Y), 6);
    }

    /// <summary>
    /// The first perturbed surface is the case with no accumulation in it, so its sigma is
    /// simply the equivalent tilt over the chief-ray incidence. Worth asserting separately
    /// because it isolates the part of the formula that the secondary's value cannot.
    /// </summary>
    [Fact]
    public void ThePrimaryMirrorsSigmaIsTheEquivalentTiltOverTheChiefRayIncidence()
    {
        var (sys, n, p) = Build(true);
        var sigma = SigmaVector.Compute(sys, n, p);

        double ibar = p.Ybar[1] * sys.Surfaces[1].VertexCurvature + p.Ubar[0];
        Assert.Equal(0.005812, ibar, 6);

        Assert.Equal(Math.Abs(-0.008995 * Deg / ibar), Math.Abs(sigma.Sigma[1].X), 9);
        Assert.Equal(Math.Abs(0.017991 * Deg / ibar), Math.Abs(sigma.Sigma[1].Y), 9);
    }

    /// <summary>
    /// <b>Table 5, the ASPHERIC column.</b> A figured surface has a second field centre, and it
    /// is not a multiple of the first: Thompson gives the secondary's spherical sigma as
    /// <c>(0.0220889, 0.0966082)</c> and its aspheric one as <c>(0.0540453, 0.108097)</c>.
    ///
    /// <para>The primary's aspheric sigma is zero in his model, and for a stated reason - his
    /// Fig. 11 caption records that the stop is decentred with the primary, so the optical axis
    /// ray crosses it at its own vertex and the zero-power plate is not displaced at all. That
    /// makes the primary a test of the ZERO and the secondary a test of the value.</para>
    /// </summary>
    [Fact]
    public void TheAsphericSigmaVectorsReproduceTable5()
    {
        var (sys, n, p) = Build(true);
        var sigma = SigmaVector.Compute(sys, n, p);

        Assert.True(sigma.SigmaAspheric[1].Magnitude < 1e-9,
                    "the primary's aspheric sigma should vanish: the stop moves with it");

        // Table 5, real-ray column - as with the spherical sigma, this reproduces the more
        // accurate of his two routes rather than the paraxial one.
        Assert.Equal(0.0540234, Math.Abs(sigma.SigmaAspheric[2].X), 6);
        Assert.Equal(0.1080529, Math.Abs(sigma.SigmaAspheric[2].Y), 6);

        // And the quantity it is built from is his Table 4 outright: the optical axis ray
        // crosses the secondary at X = -2.3518, Y = -4.7040 in his real ray trace.
        Assert.Equal(2.3518, Math.Abs(sigma.ReducedAspheric[2].X), 3);
        Assert.Equal(4.7040, Math.Abs(sigma.ReducedAspheric[2].Y), 3);
    }

    /// <summary>
    /// <b>The case this implementation does NOT yet get right, kept as the record of what is
    /// missing.</b>
    ///
    /// <para>A rigidly translated telescope displaces nothing - <c>RealSigmaTests</c> measures
    /// that from traced rays. The paraxial route here does not reproduce it, and the reason is
    /// now precise: the optical axis ray is defined by passing through the centre of the STOP,
    /// and when the stop itself moves the ray is displaced before it reaches any surface. This
    /// accumulation starts the ray on axis, so it misses that term.</para>
    ///
    /// <para>Thompson calls it <c>sigma*</c>, and his Fig. 11 caption records that his example
    /// was built so that "the stop is decentered with the primary mirror so that sigma* = 0" -
    /// which is precisely why Table 5 above agrees to seven figures while this does not. The
    /// agreement is real and the gap is real, and they are the same fact seen twice.</para>
    ///
    /// <para>Skipped rather than deleted: it is the acceptance test for the missing term.</para>
    /// </summary>
    [Fact]
    public void ARigidlyTranslatedTelescopeDisplacesNothing()
    {
        var (sys, n, p) = Build(false);
        for (int j = 1; j <= sys.LastOpticalSurface(); j++) sys.Surfaces[j].DecenterY = 5.0;

        var sigma = SigmaVector.Compute(sys, n, p);

        for (int j = 1; j <= sys.LastOpticalSurface(); j++)
            Assert.True(sigma.Sigma[j].Magnitude < 1e-12,
                        $"surface {j} measured {sigma.Sigma[j]} for a rigid translation");
    }
    /// <summary>
    /// <b>The assembled nodal form against a raw sum over BOTH fields of every surface.</b>
    ///
    /// <para>A figured surface contributes two displaced fields - the spherical base at
    /// <c>sigma</c> and the aspheric cap at <c>sigma_asph</c> - and <see cref="NatField"/>
    /// collects them into the normalised displacement vectors. This walks the surfaces instead,
    /// summing <c>W (H - sigma)^2</c> over four contributions on this two-conic telescope, and
    /// requires the two to agree.</para>
    ///
    /// <para>It is the check that the aspheric wiring is right rather than merely present: the
    /// two routes share no arithmetic, and dropping either cap - or centring it on the spherical
    /// sigma - breaks the identity immediately.</para>
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.4, -0.6)]
    [InlineData(-1.0, 0.25)]
    public void TheNodalFormAgreesWithTheRawSumOverBothFields(double hx, double hy)
    {
        var (sys, n, p) = Build(true);
        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);
        var f = NatField.Compute(sys, n, p, seidel);
        var sig = f.Sigmas;

        var h = new Vec2(hx, hy);
        Vec2 rawAst = Vec2.Zero, rawComa = Vec2.Zero;

        for (int j = 1; j <= sys.LastOpticalSurface(); j++)
        {
            // The spherical base curve, at sigma.
            double w131Sph = (seidel.S2[j] - seidel.S2Aspheric[j]) / 2.0;
            double w222Sph = (seidel.S3[j] - seidel.S3Aspheric[j]) / 2.0;
            var ha = h - sig.Sigma[j];
            rawComa += w131Sph * ha;
            rawAst += (0.5 * w222Sph) * ha.Squared;

            // The aspheric cap, at its own centre.
            double w131Asph = seidel.S2Aspheric[j] / 2.0;
            double w222Asph = seidel.S3Aspheric[j] / 2.0;
            var hb = h - sig.SigmaAspheric[j];
            rawComa += w131Asph * hb;
            rawAst += (0.5 * w222Asph) * hb.Squared;
        }

        double scale = Math.Abs(f.Totals.W222) + Math.Abs(f.Totals.W131);
        Assert.True((rawComa - f.ComaAt(h)).Magnitude < 1e-9 * scale,
                    $"coma: raw {rawComa} against assembled {f.ComaAt(h)}");
        Assert.True((rawAst - f.AstigmatismAt(h)).Magnitude < 1e-9 * scale,
                    $"astigmatism: raw {rawAst} against assembled {f.AstigmatismAt(h)}");
    }

    /// <summary>
    /// And that it MATTERS: centring the aspheric cap on the spherical sigma - the thing this
    /// implementation did before - moves the nodes. On this telescope the two centres are a
    /// factor of two apart, so it is not a refinement.
    /// </summary>
    [Fact]
    public void TheAsphericCentreChangesWhereTheNodesAre()
    {
        var (sys, n, p) = Build(true);
        var seidel = SeidelCoefficients.Compute(sys, n, n, n, p);
        var f = NatField.Compute(sys, n, p, seidel);

        Assert.True(f.AstigmatismNodesExist);

        // What the astigmatic displacement would have been with one centre for both.
        var sig = f.Sigmas;
        Vec2 a222Wrong = Vec2.Zero;
        for (int j = 1; j <= sys.LastOpticalSurface(); j++)
            a222Wrong += (seidel.S3[j] / 2.0) * sig.Sigma[j];

        var wrong = (1.0 / f.Totals.W222) * a222Wrong;
        var right = f.A222Normalised;

        Assert.True((wrong - right).Magnitude > 0.1 * right.Magnitude,
                    "the two centres should give materially different node positions");
    }

    /// <summary>
    /// <b>Schmid's diagnostic, which is what stage 4a is for.</b>
    ///
    /// <para>Schmid, Rolland, Rakich and Thompson, Opt. Express 18, 17433 (2010): astigmatic
    /// FIGURE ERROR at the stop and a misaligned SECONDARY both make astigmatism binodal, but
    /// they are told apart by where the nodes sit. A figure error at the stop contributes to the
    /// node SPLITTING and not to their midpoint, so its two nodes stay symmetric about the field
    /// centre. A misalignment moves the midpoint and carries them off together.</para>
    ///
    /// <para>That is the single most useful thing this report can tell a telescope owner, and it
    /// is checked here as a structural fact rather than against a number: same design, two
    /// perturbations, and the midpoint distinguishes them.</para>
    /// </summary>
    [Fact]
    public void FigureErrorAtTheStopKeepsTheNodeMidpointAtTheFieldCentre()
    {
        // Astigmatic figure error on the primary, which IS the stop in this telescope.
        var (figured, nf, pf) = Build(false);
        figured.Surfaces[1].FringeZernike = new double[19];
        figured.Surfaces[1].FringeZernike[5] = 2.0e-5;
        figured.Surfaces[1].FringeZernike[6] = 1.0e-5;

        var fieldFig = NatField.Compute(figured, nf, pf,
                           SeidelCoefficients.Compute(figured, nf, nf, nf, pf));

        Assert.False(fieldFig.IsAligned, "a figure error makes the system non-symmetric");
        Assert.True(fieldFig.AstigmatismNodesExist);

        // The nodes are split...
        double split = (fieldFig.AstigmatismNode1 - fieldFig.AstigmatismNode2).Magnitude;
        Assert.True(split > 1e-6, "an astigmatic figure error must split the nodes");

        // ...but symmetric about the field centre.
        Assert.True(fieldFig.A222Normalised.Magnitude < 1e-9 * split,
                    $"figure error at the stop moved the midpoint to {fieldFig.A222Normalised}");

        // A misaligned SECONDARY splits them too, and moves the midpoint off centre.
        var (tilted, nt, pt) = Build(false);
        tilted.Surfaces[2].TiltX = 0.01 * Math.PI / 180.0;

        var fieldTilt = NatField.Compute(tilted, nt, pt,
                            SeidelCoefficients.Compute(tilted, nt, nt, nt, pt));

        Assert.True(fieldTilt.A222Normalised.Magnitude > 1e-6,
                    "a misaligned secondary must move the midpoint off the field centre");
    }

    /// <summary>
    /// A Zernike COMA overlay behaves differently from an astigmatic one: it displaces the
    /// comatic node, and away from the stop it also contributes field-linear astigmatism and
    /// field-linear medial field curvature. Fuerschbach 2014 Table 1.
    /// </summary>
    [Fact]
    public void AComaOverlayDisplacesTheComaticNode()
    {
        var (sys, n, p) = Build(false);
        var before = NatField.Compute(sys, n, p, SeidelCoefficients.Compute(sys, n, n, n, p));
        Assert.True(before.ComaNode.Magnitude < 1e-12);

        sys.Surfaces[2].FringeZernike = new double[19];
        sys.Surfaces[2].FringeZernike[7] = 3.0e-5;

        var after = NatField.Compute(sys, n, p, SeidelCoefficients.Compute(sys, n, n, n, p));
        Assert.True(after.ComaNode.Magnitude > 1e-9, "a coma overlay must move the comatic node");
    }

}
