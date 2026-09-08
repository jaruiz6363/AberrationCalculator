using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Report;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AberrationCalculator.Tests")]

namespace AberrationCalculator.Cli;

/// <summary>
/// Command-line entry point.
///
/// Writes the report to stdout so it can be piped, and the same content to files so it can
/// be kept, diffed and re-read. The machine-readable TSVs are written alongside rather than
/// instead: a report you can read and a table you can parse serve different jobs, and
/// deriving both from one pass means they cannot drift apart.
/// </summary>
internal static class Program
{
    private const string Usage = @"
abcalc - read a lens design and report its prescription and first-order data

USAGE
  abcalc <lensfile> [options]

OPTIONS
  -o, --out <dir>     Write report and data files into <dir>.
                      Default: alongside the lens file.
      --no-files      Print to stdout only; write nothing.
      --stdout-only   Same as --no-files.
  -q, --quiet         Write the files but print nothing except errors.
      --glass <dir>   Use this folder of .agf catalogs instead of the bundled ones.
      --distortion-coefficients
                      What the aberration coefficients make of DISTORTION, against
                      the rays. Not a distortion report - tracing one chief ray gives
                      that exactly and no slower. This asks how far the coefficients
                      can be trusted: third, fifth and seventh order over a ladder of
                      field fractions, both mappings, and E, E5 and tau20 read back
                      out of the rays with an error bar. Write nothing else.
                      Abbreviates to --distortion.
      --screen [h]    Report whether this design would test the aspheric seventh-order
                      path, and write nothing else. Optional field fraction,
                      default 1.0 (the corner).
      --forbes [d]    Third, fifth and seventh order per surface, split into what each
                      surface generates on its own and what it generates by acting on
                      the aberration already reaching it. Write nothing else. The
                      seventh order is by the Forbes series trace, which handles conics
                      and even aspheres as well as spheres, at either conjugate.
                      Optional truncation degree 3 to 8, default 3 = seventh order.
                      Raising it must not change the answer; that it does not is a real
                      check that the series has converged for this design.
  -h, --help          This text.

FORMATS
  .zmx  ZEMAX      .seq  CODE V     .otx .opt  OPTALIX
  .len .osl OSLO   .json Optiland   .lhlt      LensHH-LT

OUTPUT
  <name>.report.txt          Everything, formatted to read.
  <name>.prescription.tsv    One row per surface.
  <name>.indices.tsv         One row per material, one column per wavelength.
  <name>.firstorder.tsv      Name/value pairs.
  <name>.paraxial.tsv        Marginal and chief ray at every surface.
  <name>.seidel.tsv          Third-order coefficients per surface, plus totals.
  <name>.buchdahl.tsv        Third, fifth and seventh order, per surface and total.
  <name>.prms.tsv            Predicted RMS spot per field and wavelength, and PRMSA.
  <name>.contributions.tsv   Per-aberration isolated RMS and share of the spot.
  <name>.surfaces.tsv        Per surface: intrinsic, aspheric, induced, and their total.
  <name>.surface-share.tsv   Per surface: share of the spot and induced fraction.

  The .tsv files are tab-separated with full precision, so a script or a
  spreadsheet can use them without reparsing the report.

EXIT CODES
  0  read and analysed
  1  could not read the file, or no glass catalogs found
  2  read, but one or more materials could not be resolved
";

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// The command line proper, separated from <c>Main</c> so the tests can run it. What a
    /// flag actually does is not visible from anywhere else: the report builders are covered
    /// on their own, and a flag wired to the wrong one would pass every one of those tests.
    /// </summary>
    internal static int Run(string[] args)
    {
        string? lensPath = null, outDir = null, glassDir = null;
        bool writeFiles = true, quiet = false, screen = false, forbes = false, distortion = false;
        double screenH = 1.0;
        int forbesDegree = 3;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "-h": case "--help": Console.WriteLine(Usage.Trim()); return 0;
                case "--no-files": case "--stdout-only": writeFiles = false; break;
                case "-q": case "--quiet": quiet = true; break;
                case "-o": case "--out":
                    if (++i >= args.Length) throw new ArgumentException("--out needs a directory");
                    outDir = args[i]; break;
                case "--glass":
                    if (++i >= args.Length) throw new ArgumentException("--glass needs a directory");
                    glassDir = args[i]; break;
                case "--forbes":
                    forbes = true;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer,
                                                            CultureInfo.InvariantCulture, out int fd))
                    {
                        if (fd < 3 || fd > 8)
                            throw new ArgumentException("--forbes takes a degree from 3 to 8");
                        forbesDegree = fd; i++;
                    }
                    break;
                case "--distortion-coefficients": case "--distortion": distortion = true; break;
                case "--screen":
                    screen = true;
                    // Optional field fraction. The corner is the default but is also where
                    // the series is weakest, so being able to ask lower down matters.
                    if (i + 1 < args.Length && double.TryParse(args[i + 1],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double sh))
                    { screenH = sh; i++; }
                    break;
                default:
                    if (a.StartsWith("-")) throw new ArgumentException($"unknown option '{a}'");
                    if (lensPath != null) throw new ArgumentException("give one lens file at a time");
                    lensPath = a; break;
            }
        }

        if (lensPath == null) { Console.WriteLine(Usage.Trim()); return 0; }
        if (!File.Exists(lensPath)) { Console.Error.WriteLine($"error: no such file: {lensPath}"); return 1; }

        // Catalogs come from the bundle unless overridden. Failing loudly matters here:
        // without them every glass silently resolves as air and the whole report is wrong.
        GlassCatalog catalog;
        if (glassDir != null)
        {
            if (!Directory.Exists(glassDir)) { Console.Error.WriteLine($"error: no such folder: {glassDir}"); return 1; }
            catalog = new GlassCatalog();
            catalog.LoadFolder(glassDir);
        }
        else
        {
            catalog = CatalogLocator.LoadBundled();
        }

        var system = LensFile.Read(lensPath, catalog);
        var writer = new ReportWriter(system, catalog, Path.GetFullPath(lensPath));

        // The screen answers one question and writes nothing, so it short-circuits the rest.
        if (screen) { Console.Write(writer.BuildAsphericScreenText(screenH)); return writer.Unresolved.Count > 0 ? 2 : 0; }

        // And so does the distortion check, which is a measurement rather than a report and
        // is the only one of these that traces rays.
        if (distortion) { Console.Write(writer.BuildDistortionText()); return writer.Unresolved.Count > 0 ? 2 : 0; }

        // So does the Forbes report, which is a different question about the same lens.
        if (forbes)
        {
            string? text = writer.BuildForbesText(forbesDegree);
            if (text == null)
            {
                Console.Error.WriteLine("error: the coefficients could not be separated. That happens "
                                      + "when the system has no field, or when the series trace does "
                                      + "not close on this design.");
                return 1;
            }
            Console.Write(text);
            return writer.Unresolved.Count > 0 ? 2 : 0;
        }
        string report = writer.BuildReport();

        if (!quiet) Console.Write(report);

        if (writeFiles)
        {
            string dir = outDir ?? Path.GetDirectoryName(Path.GetFullPath(lensPath)) ?? ".";
            Directory.CreateDirectory(dir);
            string stem = Path.GetFileNameWithoutExtension(lensPath);

            var written = new List<string>
            {
                Write(dir, stem + ".report.txt",       report),
                Write(dir, stem + ".prescription.tsv", writer.BuildPrescriptionTsv()),
                Write(dir, stem + ".indices.tsv",      writer.BuildIndicesTsv()),
                Write(dir, stem + ".firstorder.tsv",   writer.BuildFirstOrderTsv()),
                Write(dir, stem + ".paraxial.tsv",     writer.BuildParaxialRaysTsv()),
                Write(dir, stem + ".seidel.tsv",       writer.BuildSeidelTsv()),
                Write(dir, stem + ".buchdahl.tsv",     writer.BuildBuchdahlTsv()),
                Write(dir, stem + ".prms.tsv",         writer.BuildPrmsTsv()),
                Write(dir, stem + ".contributions.tsv", writer.BuildContributionTsv()),
                Write(dir, stem + ".surfaces.tsv",      writer.BuildSurfaceBreakdownTsv()),
                Write(dir, stem + ".surface-share.tsv", writer.BuildSurfaceShareTsv()),
            };

            if (!quiet)
            {
                Console.WriteLine("Written:");
                foreach (var f in written) Console.WriteLine("  " + f);
            }
        }

        if (writer.Unresolved.Count > 0)
        {
            Console.Error.WriteLine("warning: unresolved materials: " + string.Join(", ", writer.Unresolved));
            return 2;
        }
        return 0;
    }

    private static string Write(string dir, string name, string content)
    {
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }
}
