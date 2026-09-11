using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize;
using AberrationCalculator.Optimize.Algorithms;
using AberrationCalculator.Optimize.Io;
using AberrationCalculator.Optimize.Operands;

namespace AberrationCalculator.Mcp;

/// <summary>One argument of an action tool, and what to say about it in the schema.</summary>
internal sealed record ArgumentSpec(string Name, string Type, string Description,
                                    bool Required = false);

/// <summary>
/// A tool that DOES something rather than reporting on something.
///
/// <para>The fourteen tools in <see cref="Tools"/> all have the same shape: hand them a lens and
/// they hand back a reading of it, so one schema serves all of them. An optimisation is not that
/// shape - it needs to be told what may change and what the design is being asked to be, and it
/// produces a new design rather than a description of the old one. It gets its own schema and
/// its own handler instead of being forced into the reporting one.</para>
/// </summary>
internal sealed record ActionTool(
    string Name,
    string Description,
    IReadOnlyList<ArgumentSpec> Arguments,
    Func<JsonNode?, string> Run);

internal static class ActionTools
{
    /// <summary>
    /// What every operand takes, grouped, for the tool description.
    ///
    /// <para>Generated from the same table the parser reads. Written out by hand it drifted the
    /// moment an operand's signature changed, and a tool description that lies about its own
    /// arguments is worse than one that says nothing - the caller has no way to check it.</para>
    /// </summary>
    private static string InputsByType()
    {
        // Grouped by signature rather than listed one per line: fifteen ray operands that all
        // take the same five inputs is one fact, not fifteen.
        var groups = new List<(string Inputs, List<string> Types)>();
        foreach (var type in OperandHelp.All)
        {
            string inputs = OperandInputs.Describe(type);
            var group = groups.Find(g => g.Inputs == inputs);
            if (group.Types == null) groups.Add((inputs, new List<string> { type.ToString() }));
            else group.Types.Add(type.ToString());
        }

        var sb = new StringBuilder("INPUTS BY TYPE: ");
        for (int i = 0; i < groups.Count; i++)
        {
            if (i > 0) sb.Append("; ");
            sb.Append(string.Join(" ", groups[i].Types))
              .Append(groups[i].Types.Count == 1 ? " takes " : " take ")
              .Append(groups[i].Inputs);
        }
        return sb.Append(".\n").ToString();
    }

    public static readonly IReadOnlyList<ActionTool> All = new[]
    {
        new ActionTool("optimize",
            "Optimise a lens against a merit function, and report what changed.\n\n"
          + "THE DERIVATIVES ARE ANALYTIC. Every operand - including PRMSA, the predicted spot, "
          + "which is a quadratic form in thirty-seven aberration coefficients - is "
          + "differentiated exactly, by carrying dual numbers through the very same aberration "
          + "chain that computes the value. There is no finite difference anywhere, so no step "
          + "size to choose and no subtractive cancellation.\n\n"
          + "SPHERICAL SURFACES ONLY. The coefficients come from Buchdahl's closed-form scheme, "
          + "whose aspheric SEVENTH order is a reconstruction real rays reject - out by up to a "
          + "factor of four. A figured design, or a conic asked to be a variable, is refused "
          + "before the run rather than optimised against a number known to be wrong. The "
          + "ANALYSIS tools are unaffected and handle conics and even aspheres at every order "
          + "they report.\n\n"
          + "The lens file on disk is NEVER modified. Pass save_to to write the result "
          + "somewhere.\n\n"
          + "WHERE THE SETTINGS COME FROM. Beside the lens: <lens>.mf holds the merit function "
          + "and <lens>.var the variables and pickups. A .lhlt keeps its variables, bounds and "
          + "pickups in the lens itself and has no .var file; its own merit function is NOT read, "
          + "because this tool optimises a different one, and is left untouched. Pass `merit` or "
          + "`variables` as text to override either without writing a file.\n\n"
          + "MERIT FUNCTION - one operand per line, TYPE, WEIGHT, then TAR / MIN / MAX, then the "
          + "inputs, all comma separated:\n"
          + "  PRMSA,   1, TAR 0                     # the predicted spot: no inputs\n"
          + "  EFL,   100, TAR 50,          2        # focal length in wavelength 2\n"
          + "  TTL,     5, MAX 60                    # total track: no inputs\n"
          + "  EGT,    10, MIN 1,           2, 4     # glass edges over surfaces 2 to 4\n"
          + "  DTRGT,  10, MIN 1.5, MAX 12, 2, 4     # diameter-to-thickness ratio\n"
          + "  LCF,     5, TAR 0,           1.0      # lateral colour at the full field\n"
          + "  DISTF,  10, MIN -2, MAX 2,   0.7      # distortion at seven tenths of the field\n"
          + "  RY,      1, TAR 0,           7, 1, 1, 0, 1   # surface, wave, hy, px, py\n"
          + InputsByType()
          + "Trailing inputs may be left off and take their "
          + "defaults. hy is a fraction of the maximum field, px and py fractions of the pupil "
          + "radius, and wavelengths are numbered from 1 - there is no zero, so leave the "
          + "wavelength off to use the reference colour.\n\n"
          + "VARIABLES - one per line, merging into whatever was said before:\n"
          + "  VAR CV 1                                 # curvature of surface 1\n"
          + "  VAR TH 2 MIN 1.0 MAX 12.0                # a thickness, bounded\n"
          + "  PICKUP TH 2 INDEX 1 SCALE 1 OFFSET -0.1  # surface 2 follows surface 1\n"
          + "A TAR operand is driven to a value; a MIN/MAX operand costs nothing while it is "
          + "satisfied.",
            new[]
            {
                new ArgumentSpec("lens_file", "string",
                    "Path to the lens to optimise. It is read, not written.", true),
                new ArgumentSpec("merit", "string",
                    "The merit function as text. Either this or merit_file is required."),
                new ArgumentSpec("variables", "string",
                    "Variables and pickups as text, in the .var format: VAR CV 1, "
                  + "VAR TH 2 MIN 1 MAX 12, PICKUP TH 2 INDEX 1 SCALE 1 OFFSET -0.1. For a "
                  + ".lhlt these come from the lens file itself and this is not needed."),
                new ArgumentSpec("merit_file", "string",
                    "Path to a merit function file, instead of passing the text."),
                new ArgumentSpec("method", "string",
                    "lm, psd2, psd3 or hj. Default psd3: Dilworth's pseudo-second-derivative, "
                  + "which recovers the curvature Gauss-Newton discards from successive exact "
                  + "Jacobians and goes deeper than Marquardt on most designs. hj is "
                  + "Hooke-Jeeves pattern search, which uses no derivatives at all and is "
                  + "useful where a boundary operand makes the merit function kinked."),
                new ArgumentSpec("iterations", "integer",
                    "Local iterations, or iterations per hop. Default 200."),
                new ArgumentSpec("hops", "integer",
                    "Basin-hop this many times per chain instead of optimising once. Each hop "
                  + "kicks the design out of its basin, re-minimises, and keeps or rejects the "
                  + "result by a Metropolis rule. Default 0, meaning a single local run."),
                new ArgumentSpec("chains", "integer",
                    "Independent hopping chains. Default 0 = one per processor."),
                new ArgumentSpec("seed", "integer", "Random seed for the hopping. Default 1234."),
                new ArgumentSpec("glass_substitution", "string",
                    "Name of a substitution catalogue the hopping may take glasses from, e.g. "
                  + "CoreSet28. Glass is discrete - there is no gradient from one glass to the "
                  + "next - so it can only be proposed and judged, never followed downhill. "
                  + "Needs hops > 0. Substitution catalogues are deliberately NOT the "
                  + "catalogues a design is read through: a search free to pick from every "
                  + "vendor at once settles on glasses nobody stocks."),
                new ArgumentSpec("save_to", "string",
                    "Where to write the optimised lens, in the format it was read from. Under "
                  + "basin hopping this is a FOLDER, and one design per chain is written into "
                  + "it. Nothing is written without this."),
                new ArgumentSpec("glass_dir", "string",
                    "Optional folder of .agf catalogs instead of the bundled ones."),
            },
            Optimize),

        new ActionTool("base_path",
            "Set or show the FOLDER that bare file names are taken to mean, so that lens_file "
          + "and the rest can be given as 'L.zmx' rather than as a full path.\n\n"
          + "WHY THIS MATTERS HERE. This server's working directory is whatever started it, not "
          + "anything the user chose, so without a base every path has to be absolute. Set it "
          + "once at the start of a session and every tool below resolves against it.\n\n"
          + "It is the same setting as the command line's BASE command and is kept between "
          + "runs. Call with no path to see what is in force and where it came from; call with "
          + "clear=true to forget it. An ABSOLUTE path is never re-rooted - it always means what "
          + "it says.",
            new[]
            {
                new ArgumentSpec("path", "string",
                    "The folder to take bare file names against. It has to exist. Omit to "
                  + "report the base in force without changing it."),
                new ArgumentSpec("clear", "boolean",
                    "Forget the stored base, leaving the working directory in charge again."),
            },
            SetBasePath),
    };

    /// <summary>The command line's BASE, BASELIST and BASEREMOVE, as one tool.</summary>
    private static string SetBasePath(JsonNode? a)
    {
        string? path = Text(a, "path");
        bool clear = a?["clear"] != null && a["clear"]!.GetValue<bool>();

        if (clear && !string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "give a path or clear, not both - they say opposite things");

        var command = clear ? "BASEREMOVE"
                    : string.IsNullOrWhiteSpace(path) ? "BASELIST"
                    : "BASE";

        return SettingsCommands.ExecuteGlobal(command, path).Output;
    }

    private static string Optimize(JsonNode? a)
    {
        // Every path here is taken against the base folder, which is the same setting the
        // command line's BASE names. An MCP server's working directory is whatever the client
        // started it in rather than anything the user chose, so this is the only way a bare
        // file name can mean anything at all.
        string lensPath = BasePath.Resolve(
            Text(a, "lens_file") ?? throw new ArgumentException("lens_file is required"));
        if (!File.Exists(lensPath))
            throw new FileNotFoundException($"no such file: {lensPath}", lensPath);

        string? meritText = Text(a, "merit");
        string? meritPath = BasePath.ResolveIfGiven(Text(a, "merit_file"));
        string? varText = Text(a, "variables");

        GlassCatalog catalog;
        string? glassDir = BasePath.ResolveIfGiven(Text(a, "glass_dir"));
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

        // Checked here rather than being left to fail inside the hopping, which runs on several
        // threads and would surface a mistyped name as an aggregate exception minutes in.
        string? substitution = Text(a, "glass_substitution");
        if (!string.IsNullOrWhiteSpace(substitution)) SubstitutionCatalog.Load(substitution!);

        var settings = new RunSettings
        {
            Iterations = Integer(a, "iterations") ?? 200,
            Hops = Integer(a, "hops") ?? 0,
            Chains = Integer(a, "chains") ?? 0,
            Seed = Integer(a, "seed") ?? 1234,
            GlassSubstitution = !string.IsNullOrWhiteSpace(substitution),
            SubstitutionCatalog = string.IsNullOrWhiteSpace(substitution) ? null : substitution,
        };

        switch ((Text(a, "method") ?? "psd3").ToLowerInvariant())
        {
            case "lm": settings.Method = StepMethod.Lm; break;
            case "psd2": settings.Method = StepMethod.Psd2; break;
            case "psd3": settings.Method = StepMethod.Psd3; break;
            case "hj": settings.HookeJeeves = true; break;
            default: throw new ArgumentException("method must be lm, psd2, psd3 or hj");
        }

        var system = LensFile.Read(lensPath, catalog);

        // The same three sources the command line uses, in the same order: what is passed in
        // here wins, then the sidecar files beside the lens, then - for a .lhlt - the lens
        // itself. Passing text in is the one thing the MCP adds, so an assistant can compose a
        // merit function without writing a file first.
        var setup = Sidecar.Load(system, lensPath, meritPath);

        if (!string.IsNullOrWhiteSpace(meritText))
        {
            setup.Operands.Clear();
            setup.Operands.AddRange(
                MeritFile.Parse(meritText!.Replace("\r\n", "\n").Split('\n'), "merit"));
            setup.MeritSource = "Merit function given inline.";
        }

        if (!string.IsNullOrWhiteSpace(varText))
        {
            var vars = VarFile.Parse(varText!.Replace("\r\n", "\n").Split('\n'), "variables");
            setup.Variables.Clear();
            setup.Variables.AddRange(vars.Variables.Items);
            setup.Pickups.Clear();
            setup.Pickups.AddRange(vars.Pickups);
            setup.VariableSource = "Variables given inline.";
        }

        if (setup.Variables.Count == 0)
            throw new ArgumentException(
                "nothing is declared variable, so there is nothing the optimiser is allowed to "
              + "change. " + setup.VariableSource);
        if (setup.Operands.Count == 0)
            throw new ArgumentException(
                "no operands are declared, so there is nothing to optimise towards. "
              + setup.MeritSource);

        if (setup.Pickups.Count > 0 && !Sidecar.KeepsVariablesInTheLensFile(lensPath))
        {
            system.Pickups.Clear();
            system.Pickups.AddRange(setup.Pickups);
        }

        var outcome = OptimizationRun.Execute(system, catalog, setup, settings);
        string report = OptimizationReport.Build(outcome, Path.GetFileName(lensPath));

        string? saveTo = BasePath.ResolveIfGiven(Text(a, "save_to"));
        if (!outcome.Ok) return report;

        if (string.IsNullOrWhiteSpace(saveTo))
            return report + "Nothing was written. Pass save_to to keep this design.\n";

        // Saved the same way the command line saves: the original file is edited rather than
        // regenerated, and the settings go beside it. Basin hopping produces one design PER
        // CHAIN - chains land in different valleys, and which of them is interesting is a
        // judgement only a designer can make - so save_to is a folder there and every chain is
        // kept, rather than the lowest merit being kept and the rest thrown away.
        var designs = outcome.ChainBest.Count > 0
            ? outcome.ChainBest
            : new List<OpticalSystem> { outcome.Best };

        if (designs.Count == 1)
        {
            report += "Written: " + Save(designs[0], setup, lensPath, saveTo!, catalog) + "\n";
            foreach (string path in Sidecar.Save(saveTo!, setup))
                report += "         " + Path.GetFullPath(path) + "\n";
            return report;
        }

        Directory.CreateDirectory(saveTo!);
        string stem = Path.GetFileNameWithoutExtension(lensPath);
        string extension = Path.GetExtension(lensPath);
        report += "Written:\n";

        for (int i = 0; i < designs.Count; i++)
        {
            string chain = Path.Combine(
                saveTo!,
                stem + ".chain" + (i + 1).ToString("00", CultureInfo.InvariantCulture) + extension);

            report += "  " + Save(designs[i], setup, lensPath, chain, catalog) + "\n";
            foreach (string path in Sidecar.Save(chain, setup))
                report += "  " + Path.GetFullPath(path) + "\n";
        }
        return report;
    }

    /// <summary>
    /// Writes one design back into the format it came from.
    ///
    /// <para>A <c>.lhlt</c> carries its own statement of what may move, so the variables, bounds
    /// and pickups travel back into the lens file with the new curvatures. Every other format has
    /// nowhere to put them and gets a sidecar instead.</para>
    /// </summary>
    private static string Save(OpticalSystem design, OptimizationSetup setup,
                               string lensPath, string outPath, GlassCatalog? catalog)
    {
        if (Sidecar.KeepsVariablesInTheLensFile(outPath))
        {
            SurfaceVariables.Write(setup.Variables, design);
            design.Pickups.Clear();
            design.Pickups.AddRange(setup.Pickups);
        }

        LensPatcher.Save(design, lensPath, outPath, catalog);
        return Path.GetFullPath(outPath);
    }

    private static string? Text(JsonNode? a, string name)
    {
        var v = a?[name];
        return v == null ? null : v.GetValue<string>();
    }

    private static int? Integer(JsonNode? a, string name)
    {
        var v = a?[name];
        if (v == null) return null;
        // A client may send a number as a JSON number or as a string; both mean the same thing
        // and refusing one of them is a needless way to fail.
        try { return v.GetValue<int>(); }
        catch (Exception)
        {
            return int.TryParse(v.ToString(), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int parsed)
                 ? parsed
                 : throw new ArgumentException($"{name} must be a whole number");
        }
    }
}
