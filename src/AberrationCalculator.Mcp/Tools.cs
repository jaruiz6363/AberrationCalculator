using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Report;

[assembly: InternalsVisibleTo("AberrationCalculator.Tests")]

namespace AberrationCalculator.Mcp;

/// <summary>
/// What the server offers, and what each one does.
///
/// <para>Every tool takes a lens file and returns text, because that is what this program
/// produces: a report meant to be read, and tab-separated tables meant to be parsed. The
/// division is deliberate and is kept here rather than collapsed - a caller that wants to
/// reason about one number should ask for the table, not scrape the report.</para>
///
/// <para>The tools are a thin layer over <see cref="ReportWriter"/>, which the command line
/// also uses, so the two cannot drift apart.</para>
/// </summary>
internal sealed record Tool(
    string Name,
    string Description,
    Func<ReportWriter, string> Run);

internal static class Tools
{
    /// <summary>
    /// Reads the lens and prepares the report. The catalogs are loaded from the bundle unless
    /// the caller names a folder; failing to find them at all is worth an exception rather
    /// than a silent fallback, because without a catalog every glass resolves as air and the
    /// whole answer is quietly wrong.
    /// </summary>
    public static ReportWriter Open(string lensPath, string? glassDir)
    {
        if (string.IsNullOrWhiteSpace(lensPath))
            throw new ArgumentException("a lens file path is required");

        // Bare names are taken against the base folder. This matters more here than on the
        // command line: an MCP server's working directory is whatever the client started it in,
        // not anything the user chose, so without a base every path has to be absolute.
        lensPath = BasePath.Resolve(lensPath);
        glassDir = BasePath.ResolveIfGiven(glassDir);

        if (!File.Exists(lensPath))
            throw new FileNotFoundException($"no such file: {lensPath}", lensPath);

        GlassCatalog catalog;
        if (!string.IsNullOrWhiteSpace(glassDir))
        {
            if (!Directory.Exists(glassDir))
                throw new DirectoryNotFoundException($"no such folder: {glassDir}");
            catalog = new GlassCatalog();
            catalog.LoadFolder(glassDir);
        }
        else
        {
            catalog = CatalogLocator.LoadBundled();
        }

        var system = LensFile.Read(lensPath, catalog);
        return new ReportWriter(system, catalog, Path.GetFullPath(lensPath));
    }

    public static readonly IReadOnlyList<Tool> All = new[]
    {
        new Tool("analyse_lens",
            "The whole analysis of one lens as formatted text: prescription, first-order "
          + "data, paraxial rays, Seidel, Buchdahl third/fifth/seventh order, predicted RMS "
          + "spot and the per-aberration and per-surface breakdowns. Start here; ask for one "
          + "of the table tools when a specific number is wanted.",
            w => w.BuildReport()),

        new Tool("prescription",
            "One row per surface: radius, thickness, material, semi-diameter, conic and "
          + "aspheric terms. Tab-separated.",
            w => w.BuildPrescriptionTsv()),

        new Tool("first_order",
            "Focal length, back and front focal length, F-number, numerical aperture, "
          + "entrance and exit pupils, image height, magnification and total track. "
          + "Name/value pairs, tab-separated.",
            w => w.BuildFirstOrderTsv()),

        new Tool("paraxial_rays",
            "The marginal and chief ray height and angle at every surface. Tab-separated.",
            w => w.BuildParaxialRaysTsv()),

        new Tool("indices",
            "Refractive index of each material at each wavelength. Tab-separated.",
            w => w.BuildIndicesTsv()),

        new Tool("seidel",
            "The third-order coefficients - spherical, coma, astigmatism, Petzval, "
          + "distortion and the two chromatic ones - per surface and totalled. "
          + "Tab-separated.",
            w => w.BuildSeidelTsv()),

        new Tool("buchdahl",
            "Third, fifth and SEVENTH order aberration coefficients, per surface and "
          + "totalled. The seventh-order set is the part this program exists for; see "
          + "`docs/verification.md` for what is and is not verified about it. Tab-separated.",
            w => w.BuildBuchdahlTsv()),

        new Tool("rms_spot",
            "Predicted RMS spot radius at each field and wavelength, and the composite "
          + "PRMSA, from the coefficients rather than from traced rays. Tab-separated.",
            w => w.BuildPrmsTsv()),

        new Tool("contributions",
            "Per aberration: the RMS spot it would produce on its own, and its share of the "
          + "whole. This is what says WHICH aberration is costing the design its performance. "
          + "Tab-separated.",
            w => w.BuildContributionTsv()),

        new Tool("surface_breakdown",
            "Per surface: intrinsic, aspheric and induced contributions and their total. The "
          + "induced column is the part a designer cannot see any other way - a surface can "
          + "be blameless on its own and still spoil the system through what it induces "
          + "downstream. Tab-separated.",
            w => w.BuildSurfaceBreakdownTsv()),

        new Tool("aspheric_screen",
            "Whether this design would exercise the aspheric SEVENTH-order path hard enough "
          + "to test it: how much of the predicted spot the three suspect coefficients carry, "
          + "how much of those comes from the figuring rather than from the underlying "
          + "spheres, whether the series still converges at this aperture and field, and "
          + "whether the seventh order is visible at all. Use it to sort candidate test "
          + "designs before tracing any of them. Readable text.",
            w => w.BuildAsphericScreenText()),

        new Tool("seventh_order",
            "Third, FIFTH and SEVENTH order aberration coefficients per surface, each split "
          + "into what the surface generates on its own, what its figuring adds, and what it "
          + "generates by acting on the aberration already reaching it. The seventh order is "
          + "the twenty tau, by the Forbes series trace (J. Opt. Soc. Am. 73, 782), which "
          + "handles spheres, conics and even aspheres alike and needs no ray tracer and no "
          + "other program. Either conjugate. Ends with a cross-check: seventh-order spherical "
          + "aberration reached by two routes sharing no code, which must agree. Use this when "
          + "asked why a design will not correct, or which surface to change - a table of "
          + "totals cannot say, and the induced column can. Readable text.",
            w => w.BuildForbesText() ?? "The coefficients could not be separated. That happens "
               + "when the system has no field, or when the series trace does not close on "
               + "this design."),

        new Tool("distortion_from_coefficients",
            "How far the ABERRATION COEFFICIENTS can be trusted for distortion, measured "
          + "against rays. NOT the way to obtain a distortion figure - tracing one chief ray "
          + "gives that exactly, at the same speed, with no error at the corner - so do not "
          + "quote the predicted columns when asked what a lens's distortion is; the traced "
          + "column beside them is the answer. What this gives that a trace cannot is WHICH "
          + "ORDER the distortion is: third order is stop position and symmetry, the higher "
          + "orders are not, and they respond to different changes. Third, fifth and seventh "
          + "order across the field in both mappings, F-tan(theta) and F-theta; the paraxial "
          + "image plane the coefficients live at reconciled with the image surface the file "
          + "defines, where a design program quotes; and E, E5 and tau20 read back out of the "
          + "rays with an error bar, which is a check the predicted RMS spot cannot make. On a "
          + "FIGURED design the seventh-order term comes from the Forbes series trace, since "
          + "the scheme's aspheric arrangement is a reconstruction the rays reject. Readable "
          + "text.",
            w => w.BuildDistortionText()),

        new Tool("surface_share",
            "Per surface: its share of the spot and the fraction of that which is induced "
          + "rather than its own. Tab-separated.",
            w => w.BuildSurfaceShareTsv()),
    };
}
