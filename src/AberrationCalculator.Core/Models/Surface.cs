using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Enums;

namespace AberrationCalculator.Core.Models;

/// <summary>
/// One surface of the prescription.
///
/// Curvature rather than radius is the stored quantity, because a plane is c = 0 and needs
/// no special case, whereas r = infinity does. <see cref="Radius"/> is a view onto it for
/// reading and printing, where radius is what an optical prescription conventionally shows.
/// </summary>
public class Surface
{
    /// <summary>Position in the system, 0 = object.</summary>
    public int Index { get; set; }

    public SurfaceType Type { get; set; } = SurfaceType.Standard;

    /// <summary>1/radius, in reciprocal lens units. Zero is a plane.</summary>
    public double Curvature { get; set; }

    /// <summary>
    /// Radius of curvature. Infinite for a plane, in both directions: reading infinity back
    /// gives c = 0, so a plane round-trips instead of producing a division by zero.
    /// </summary>
    public double Radius
    {
        get => Math.Abs(Curvature) < 1e-15 ? double.PositiveInfinity : 1.0 / Curvature;
        set => Curvature = double.IsInfinity(value) || value == 0.0 ? 0.0 : 1.0 / value;
    }

    /// <summary>Axial distance to the next surface.</summary>
    public double Thickness { get; set; }

    /// <summary>Conic constant. 0 = sphere, −1 = paraboloid, &lt; −1 = hyperboloid.</summary>
    public double Conic { get; set; }

    /// <summary>
    /// Even-asphere coefficients. Index k multiplies r^(2k+2), so [0] is the r² term, [1] is
    /// r⁴, and so on. The r² term is separate from curvature and some formats do not write it.
    /// </summary>
    public double[] AsphericCoefficients { get; set; } = new double[8];

    /// <summary>Catalog glass name, or null/empty for air. "MIRROR" reflects.</summary>
    public string? Material { get; set; }

    /// <summary>Catalog the material was resolved from, when a file names one.</summary>
    public string? CatalogName { get; set; }

    /// <summary>True when this surface is the aperture stop.</summary>
    public bool IsStop { get; set; }

    public bool IsMirror => !string.IsNullOrEmpty(Material)
                            && Material!.Equals("MIRROR", StringComparison.OrdinalIgnoreCase);

    /// <summary>Clear semi-diameter.</summary>
    public double SemiDiameter { get; set; }

    public SemiDiameterMode SemiDiameterMode { get; set; } = SemiDiameterMode.Auto;

    /// <summary>Clear aperture as a percentage of the solved semi-diameter; 100 = full.</summary>
    public double ClearAperturePercent { get; set; } = 100.0;

    /// <summary>Central obstruction radius, 0 for none.</summary>
    public double ObscurationRadius { get; set; }

    /// <summary>Free-text note carried through from the file.</summary>
    public string? Comment { get; set; }

    // ── Model ("fictitious") glass ────────────────────────────────────────────────
    // Some files give dispersion directly instead of naming a catalog glass.

    public bool ModelIndexEnabled { get; set; }
    public double ModelNd { get; set; }
    public double ModelVd { get; set; }
    public double ModelDPgF { get; set; }

    /// <summary>Focal length of an ideal thin lens, for <see cref="SurfaceType.Paraxial"/>.</summary>
    public double FocalLength { get; set; }

    // ── Format-specific extras ───────────────────────────────────────────────────
    // Readers set these; the analysis does not use them, but dropping them would lose
    // information when a file is opened and its prescription printed.

    public double FloatingApertureRadius { get; set; }
    public double ClapOuterRadius { get; set; }
    public double InnerRadius { get; set; }

    /// <summary>
    /// Numbered surface parameters as a format wrote them (coordinate-break tilts, ABCD
    /// terms, and so on). Kept so an opened file prints back what it said, even for a
    /// surface type this program does not analyse.
    /// </summary>
    public double[] Parameters { get; } = new double[8];

    /// <summary>Integer surface settings, same purpose as <see cref="Parameters"/>.</summary>
    public int[] Settings { get; } = new int[8];

    /// <summary>
    /// The thickness after this surface is solved to put the paraxial marginal ray on
    /// axis. Recorded because the stored thickness alone does not say it was solved.
    /// </summary>
    public bool HasMarginalRaySolve { get; set; }

    public void SetParameter(int index, double value)
    {
        if (index >= 0 && index < Parameters.Length) Parameters[index] = value;
    }

    public void SetSetting(int index, int value)
    {
        if (index >= 0 && index < Settings.Length) Settings[index] = value;
    }

    /// <summary>
    /// The curvature the surface actually has at its vertex, which is what sets its paraxial
    /// power.
    ///
    /// <para>The even-asphere polynomial starts at r-squared, not r-to-the-fourth, and that
    /// first term is NOT figuring: expanding the sag gives z = (c/2 + A2) r^2 + ..., so a
    /// nonzero A2 shifts the vertex curvature to c + 2 A2 and changes the surface power. Every
    /// paraxial and aberration path must use this rather than <see cref="Curvature"/>, or it
    /// silently analyses a different surface from the one <see cref="Sag"/> describes.</para>
    ///
    /// <para>Identical to <see cref="Curvature"/> whenever the r-squared coefficient is zero,
    /// which it is in every design shipped with this program.</para>
    /// </summary>
    public double VertexCurvature =>
        Curvature + 2.0 * (AsphericCoefficients.Length > 0 ? AsphericCoefficients[0] : 0.0);

    /// <summary>
    /// Whether the surface is figured: a conic, or any aspheric term.
    ///
    /// <para>This is the test that decides which route may be trusted for the seventh order.
    /// Buchdahl's scheme and Forbes' series trace agree to roundoff on all twenty tertiary
    /// coefficients wherever every surface is unfigured; where one is not, the scheme needs
    /// an aspheric arrangement Buchdahl never published and which is reconstructed here, and
    /// real rays say the reconstruction is wrong. See <c>docs/distortion-prediction.md</c>.</para>
    /// </summary>
    public bool IsFigured
    {
        get
        {
            if (Math.Abs(Conic) > 1e-12) return true;
            foreach (double a in AsphericCoefficients) if (Math.Abs(a) > 1e-30) return true;
            return false;
        }
    }

    /// <summary>
    /// The surface rewritten as a sphere at its own vertex curvature plus polynomial figuring,
    /// which is the form every aberration treatment here wants.
    ///
    /// <para>An r-squared coefficient is not a deformation - it is a curvature change wearing
    /// a polynomial coat - so it cannot be handled as figuring and does not need to be.
    /// Matching the sag series term by term to r^8,</para>
    ///
    /// <code>
    ///   th1 = c/2 + A2                      th2 = (1+k) c^3/8        + A4
    ///   th3 = (1+k)^2 c^5/16 + A6           th4 = 5 (1+k)^3 c^7/128  + A8
    /// </code>
    ///
    /// <para>the equivalent surface is the sphere of curvature 2*th1 = c + 2 A2, conic zero,
    /// carrying whatever is left once that sphere is own conic series is subtracted. The two
    /// descriptions are the same surface to r^8, which is every order this program computes.
    /// </para>
    ///
    /// <para>With no r-squared term the surface is returned untouched, so the ordinary path is
    /// bit-for-bit what it was.</para>
    /// </summary>
    public (double Curvature, double Conic, double A4, double A6, double A8) VertexForm()
    {
        var a = AsphericCoefficients;
        double a2 = a.Length > 0 ? a[0] : 0.0;
        double a4 = a.Length > 1 ? a[1] : 0.0;
        double a6 = a.Length > 2 ? a[2] : 0.0;
        double a8 = a.Length > 3 ? a[3] : 0.0;
        if (a2 == 0.0) return (Curvature, Conic, a4, a6, a8);

        double c = Curvature, c2 = c * c, c3 = c2 * c, c5 = c3 * c2, c7 = c5 * c2;
        double k1 = 1.0 + Conic;
        double th2 = k1 * c3 / 8.0 + a4;
        double th3 = k1 * k1 * c5 / 16.0 + a6;
        double th4 = 5.0 * k1 * k1 * k1 * c7 / 128.0 + a8;

        double v = c + 2.0 * a2;
        double v2 = v * v, v3 = v2 * v, v5 = v3 * v2, v7 = v5 * v2;
        return (v, 0.0, th2 - v3 / 8.0, th3 - v5 / 16.0, th4 - 5.0 * v7 / 128.0);
    }

    /// <summary>Sag z(r) along the axis, positive toward the image.</summary>
    public double Sag(double r)
    {
        double r2 = r * r;
        double sag = 0.0;

        if (Math.Abs(Curvature) > 1e-15)
        {
            // Standard conic sag. The radicand goes negative outside the surface, which is a
            // real question about the geometry rather than a rounding artefact, so it is
            // reported as NaN instead of being clamped to something plausible.
            double disc = 1.0 - (1.0 + Conic) * Curvature * Curvature * r2;
            if (disc < 0.0) return double.NaN;
            sag = Curvature * r2 / (1.0 + Math.Sqrt(disc));
        }

        double rp = r2;                                   // r², then r⁴, r⁶ …
        for (int k = 0; k < AsphericCoefficients.Length; k++)
        {
            sag += AsphericCoefficients[k] * rp;
            rp *= r2;
        }
        return sag;
    }
}
