using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes an optimised design back into the file it came from, by EDITING that file rather
    /// than regenerating it.
    ///
    /// <para><b>Why editing.</b> This program models a slice of every format it reads - the .zmx
    /// reader recognises twenty-three directives, and a real .zmx has many times that in solves,
    /// coatings, apertures, tolerances and multi-configuration data. Writing a fresh file from
    /// this program's <see cref="OpticalSystem"/> would silently drop all of it, and the user
    /// would find out when they next opened their design. So the original file is read, the
    /// values the optimiser actually moved are changed, and every other byte is left alone -
    /// including the encoding and the line endings, which for a .zmx are UTF-16 and CRLF.</para>
    ///
    /// <para><b>Only three things ever change:</b> curvatures, thicknesses and glass names. Those
    /// are what this optimiser can move. Everything else in the file is somebody else's.</para>
    ///
    /// <para><b>Units go back the way they came.</b> Everything inside this program is in
    /// millimetres; a file written in inches gets inches back, through
    /// <see cref="OpticalSystem.FileUnitScale"/>. A design returned in the wrong units would be
    /// wrong by a factor of twenty-five and look perfectly reasonable.</para>
    /// </summary>
    public static class LensPatcher
    {
        /// <summary>Formats this can currently write back.</summary>
        public static readonly string[] SupportedExtensions =
            { ".lhlt", ".zmx", ".json", ".seq", ".len", ".osl", ".otx", ".opt" };

        /// <summary>True when an optimised design can be written back in this file's format.</summary>
        public static bool CanSave(string path) =>
            Array.IndexOf(SupportedExtensions,
                          Path.GetExtension(path).ToLowerInvariant()) >= 0;

        /// <summary>
        /// Applies <paramref name="system"/> to the file at <paramref name="originalPath"/> and
        /// writes the result to <paramref name="outputPath"/>.
        ///
        /// <para><paramref name="catalog"/> is wanted by one format only. A .seq writes a glass
        /// without its punctuation and may qualify it with its catalog, and deciding which
        /// catalog owns a glass means asking the loaded ones. Without it the names still go back
        /// correctly for glasses that were never punctuated, which is most of them, and the
        /// qualifier is omitted rather than guessed at.</para>
        /// </summary>
        public static void Save(OpticalSystem system, string originalPath, string outputPath,
                                GlassCatalog? catalog = null)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (originalPath == null) throw new ArgumentNullException(nameof(originalPath));
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));
            if (!File.Exists(originalPath))
                throw new FileNotFoundException("the original lens file is needed in order to "
                                              + "edit it", originalPath);

            switch (Path.GetExtension(originalPath).ToLowerInvariant())
            {
                case ".lhlt": LhltPatcher.Patch(system, originalPath, outputPath); return;
                case ".zmx": PatchZmx(system, originalPath, outputPath); return;
                case ".json": PatchOptiland(system, originalPath, outputPath); return;
                case ".seq": SeqPatcher.Patch(system, originalPath, outputPath, catalog); return;

                // Two extensions apiece, one format apiece: the reader treats them the same and
                // so does this.
                case ".len":
                case ".osl": LenPatcher.Patch(system, originalPath, outputPath); return;
                case ".otx":
                case ".opt": OtxPatcher.Patch(system, originalPath, outputPath); return;

                default:
                    throw new NotSupportedException(
                        $"Writing an optimised design back to '{Path.GetExtension(originalPath)}' "
                      + "is not implemented yet. Editing a file in place has to be done format by "
                      + "format and checked against real examples of it, and there are none of "
                      + "this one in the repository to check against. The optimised prescription "
                      + "is in the report, and the settings are in the sidecar; save as .lhlt to "
                      + "keep the design itself.");
            }
        }

        // ── ZEMAX ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A .zmx is a flat list of keyword lines, with <c>SURF n</c> opening each surface and
        /// its properties indented beneath. Only the first argument of CURV, DISZ and GLAS is
        /// ever touched; the trailing fields on those lines - solve codes, index and Abbe hints -
        /// belong to the format and are left exactly as found.
        /// </summary>
        private static void PatchZmx(OpticalSystem system, string originalPath, string outputPath)
        {
            var file = PatchText.Read(originalPath);
            double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;
            int surface = -1;

            for (int i = 0; i < file.Lines.Count; i++)
            {
                string? line = file.Lines[i];
                if (line == null) continue;              // already deleted
                string keyword = LineEdit.Keyword(line);

                if (keyword == "SURF")
                {
                    surface = LineEdit.ArgumentAsInt(line, -1);
                    continue;
                }
                if (surface < 0 || surface >= system.Surfaces.Count) continue;
                var s = system.Surfaces[surface];

                switch (keyword)
                {
                    case "CURV":
                        // A curvature is a reciprocal length, so it scales the other way from a
                        // distance: per millimetre becomes per file unit by MULTIPLYING.
                        file.Lines[i] = LineEdit.ReplaceNumberIfChanged(line, s.Curvature * scale);
                        break;

                    case "DISZ":
                        file.Lines[i] = double.IsInfinity(s.Thickness)
                            ? (LineEdit.Argument(line).Equals("INFINITY", StringComparison.OrdinalIgnoreCase)
                                 ? line : LineEdit.ReplaceArgument(line, "INFINITY"))
                            : LineEdit.ReplaceNumberIfChanged(line, s.Thickness / scale);
                        break;

                    case "GLAS":
                        string material = s.Material ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(material))
                        {
                            // The glass is gone. Removing the line is the only honest edit; a
                            // GLAS line naming nothing is not a thing a well-formed file contains.
                            file.Lines[i] = null;
                        }
                        else if (!LineEdit.Argument(line).Equals(material, StringComparison.Ordinal))
                        {
                            file.Lines[i] = LineEdit.ReplaceArgument(line, material);
                        }
                        break;
                }
            }

            file.Write(outputPath);
        }

        // ── Optiland ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Optiland's JSON records each surface's ABSOLUTE position along the axis rather than
        /// its thickness, so changing one thickness moves every surface after it. The z of the
        /// first surface is left where it was and the rest are re-accumulated from the
        /// thicknesses - which is what the reader does in reverse.
        /// </summary>
        private static void PatchOptiland(OpticalSystem system, string originalPath,
                                          string outputPath)
        {
            string json = HideInfinities(File.ReadAllText(originalPath));
            var root = JsonNode.Parse(json, null, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }) ?? throw new InvalidOperationException($"'{originalPath}' is not JSON.");

            if (root["surface_group"]?["surfaces"] is not JsonArray surfaces)
                throw new InvalidOperationException(
                    $"'{originalPath}' has no surface_group.surfaces to edit.");

            double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;
            int count = Math.Min(surfaces.Count, system.Surfaces.Count);

            double z = 0.0;
            bool haveZ = false;

            for (int i = 0; i < count; i++)
            {
                if (surfaces[i] is not JsonObject node) continue;
                if (node["geometry"] is not JsonObject geometry) continue;
                var s = system.Surfaces[i];

                // Radius. The format writes a plane as a very large number rather than an
                // infinity, and the reader treats anything past 1e10 as one, so a plane that was
                // already a plane is left spelled however the file spelled it.
                if (geometry["radius"] != null)
                {
                    double current = AsDouble(geometry["radius"]);
                    bool currentIsPlane = double.IsNaN(current) || Math.Abs(current) >= 1e10;
                    if (double.IsInfinity(s.Radius))
                    {
                        if (!currentIsPlane) geometry["radius"] = 1e30;
                    }
                    else
                    {
                        double wanted = s.Radius / scale;
                        if (currentIsPlane || Math.Abs(wanted - current) > 1e-12 * Math.Max(1.0, Math.Abs(current)))
                            geometry["radius"] = wanted;
                    }
                }

                // The format records a surface's position BOTH ways: an explicit thickness, and
                // an absolute z. The reader prefers the explicit one, so a patch that updated
                // only the positions would appear to do nothing - and one that updated only the
                // thicknesses would leave the two disagreeing, which is a trap for anything else
                // that reads the file. Both are written, and kept consistent.
                double t = s.Thickness;
                bool finite = !double.IsInfinity(t) && !double.IsNaN(t);

                if (node["thickness"] != null && finite)
                {
                    double current = AsDouble(node["thickness"]);
                    double wanted = t / scale;
                    if (double.IsNaN(current) || double.IsInfinity(current)
                        || Math.Abs(wanted - current) > 1e-12 * Math.Max(1.0, Math.Abs(current)))
                        node["thickness"] = wanted;
                }

                if (geometry["cs"] is JsonObject cs)
                {
                    // THE RUN STARTS AT THE FIRST SURFACE THAT HAS A POSITION. An object at
                    // infinity is written at z = -Infinity, and accumulating from there would
                    // put every surface in the lens at minus infinity - and then fail on the
                    // way out, because an infinity is not a number JSON can carry.
                    double currentZ = AsDouble(cs["z"]);
                    if (!haveZ)
                    {
                        if (!double.IsNaN(currentZ) && !double.IsInfinity(currentZ))
                        {
                            z = currentZ;
                            haveZ = true;
                        }
                    }
                    else
                    {
                        cs["z"] = z;
                    }
                }

                // An object at infinity has no distance to accumulate; the surface after it stays
                // where the file put it.
                if (finite) z += t / scale;

                SetMaterial(node, s.Material);
            }

            string? dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, ShowInfinities(
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })));
        }

        // ── Optiland's infinities ────────────────────────────────────────────────────────
        //
        // The format writes an object at infinity as the bare word `Infinity`, which Python's
        // json module accepts and the JSON specification does not - so a parser that follows the
        // specification stops on it. The reader deals with this by substituting a huge number;
        // that is fine for reading and wrong for writing back, because it would hand the user a
        // file in which their infinity had quietly become 1e308.
        //
        // So the words are turned into STRINGS on the way in, which survive a parse and a
        // re-serialisation byte for byte, and turned back into words on the way out.

        private const string PosInf = "\"__abcalc_inf__\"";
        private const string NegInf = "\"__abcalc_neg_inf__\"";

        private static string HideInfinities(string json) =>
            json.Replace("-Infinity", NegInf).Replace("Infinity", PosInf);

        private static string ShowInfinities(string json) =>
            json.Replace(NegInf, "-Infinity").Replace(PosInf, "Infinity");

        /// <summary>
        /// A number that may be one of the hidden infinities, or NaN when the node is neither a
        /// number nor one of them.
        /// </summary>
        private static double AsDouble(JsonNode? node)
        {
            if (node is not JsonValue value) return double.NaN;
            if (value.TryGetValue(out double d)) return d;
            if (value.TryGetValue(out string? s) && s != null)
            {
                if (s == "__abcalc_inf__") return double.PositiveInfinity;
                if (s == "__abcalc_neg_inf__") return double.NegativeInfinity;
            }
            return double.NaN;
        }

        /// <summary>
        /// Renames the glass. Optiland puts it in <c>material_post</c> - the medium AFTER the
        /// surface - as an object carrying a name.
        ///
        /// <para>A material described by its own dispersion coefficients rather than by a name is
        /// left alone. Renaming one would leave the name saying one thing and the coefficients
        /// another, and a glass substitution that cannot be written faithfully is better not
        /// written at all.</para>
        /// </summary>
        private static void SetMaterial(JsonObject node, string? material)
        {
            if (string.IsNullOrWhiteSpace(material)) return;
            if (node["material_post"] is not JsonObject post) return;
            if (post["name"] is not JsonValue value) return;
            if (!value.TryGetValue(out string? existing) || existing == null) return;
            if (string.Equals(existing, material, StringComparison.Ordinal)) return;
            post["name"] = material;
        }

    }
}
