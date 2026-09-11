using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes an optimised design back into the .otx it came from.
    ///
    /// <para><c>SUR n</c> opens a surface and the indented keywords beneath it - <c>CUY</c>,
    /// <c>THI</c>, <c>GLA</c> - set its properties. The surface NUMBERS itself, so the blocks are
    /// matched by the number on the SUR line rather than by counting them.</para>
    ///
    /// <para>The surface shape is given as a CURVATURE, not a radius, which is the same quantity
    /// this program optimises - so a plane is an honest zero here and needs none of the
    /// zero-means-infinity translation the other two formats do.</para>
    ///
    /// <para><c>VAR</c> lines say which parameters that program was allowed to vary. They are left
    /// alone: what this optimiser was allowed to move is recorded in the sidecar, and rewriting
    /// somebody else's variable list from it would be answering a question nobody asked.</para>
    /// </summary>
    internal static class OtxPatcher
    {
        /// <summary>How the format spells an infinite object distance.</summary>
        private const string Infinite = "1.0000000000000000E+020";

        private const string DefaultIndent = "  ";

        public static void Patch(OpticalSystem system, string originalPath, string outputPath)
        {
            var file = PatchText.Read(originalPath);
            double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;
            var insertions = new List<(int Index, string Text)>();

            int surface = -1;
            var block = new Block();

            for (int i = 0; i < file.Lines.Count; i++)
            {
                string? line = file.Lines[i];
                if (line == null) continue;
                if (line.TrimStart().StartsWith("!", StringComparison.Ordinal)) continue;

                string keyword = LineEdit.Keyword(line);

                if (keyword == "SUR")
                {
                    Flush(system, surface, block, i, scale, insertions);
                    surface = LineEdit.ArgumentAsInt(line, -1);
                    block = new Block();
                    continue;
                }

                if (surface < 0 || surface >= system.Surfaces.Count) continue;
                var s = system.Surfaces[surface];

                switch (keyword)
                {
                    case "CUY":
                        block.Curvature = i;
                        block.Indent ??= LineEdit.Indent(line);
                        file.Lines[i] = LineEdit.ReplaceNumberIfChanged(line, s.Curvature * scale);
                        break;

                    case "THI":
                        block.Thickness = i;
                        block.Indent ??= LineEdit.Indent(line);
                        file.Lines[i] = PatchThickness(line, s, scale);
                        break;

                    case "GLA":
                        block.Glass = i;
                        block.Indent ??= LineEdit.Indent(line);
                        string material = s.IsMirror ? string.Empty : s.Material ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(material))
                            file.Lines[i] = null;
                        else if (!LineEdit.Argument(line).Equals(material, StringComparison.Ordinal))
                            file.Lines[i] = LineEdit.ReplaceArgument(line, material);
                        break;

                    case "SUT":
                    case "STO":
                    case "APE":
                    case "ASP":
                    case "COM":
                    case "VAR":
                        // Not ours to move, but they mark how far this surface's block reaches.
                        block.Last = i;
                        block.Indent ??= LineEdit.Indent(line);
                        break;
                }
            }

            Flush(system, surface, block, file.Lines.Count, scale, insertions);

            file.InsertAll(insertions);
            file.Write(outputPath);
        }

        /// <summary>
        /// The thickness, with the format's two sentinels left standing.
        ///
        /// <para>An object at infinity is a very large number, and the image surface is written as
        /// <c>-999</c> - a flag, not a distance, which the reader turns into a zero. Replacing that
        /// flag with the zero it was read as would throw away what the file was saying.</para>
        /// </summary>
        private static string PatchThickness(string line, Surface s, double scale)
        {
            string current = LineEdit.Argument(line);

            if (double.IsInfinity(s.Thickness))
                return IsInfinite(current) ? line : LineEdit.ReplaceArgument(line, Infinite);

            if (LineEdit.ArgumentAsDouble(line, out double have) && have <= -900.0
                && Math.Abs(s.Thickness) <= 0.0)
                return line;

            return LineEdit.ReplaceNumberIfChanged(line, s.Thickness / scale);
        }

        /// <summary>
        /// Adds the lines this surface needs and does not have - a glass where the substitution
        /// put one into an air space, a thickness or a curvature the file never wrote out.
        /// </summary>
        private static void Flush(OpticalSystem system, int surface, Block block, int terminator,
                                  double scale, List<(int, string)> insertions)
        {
            if (block.Done) return;
            block.Done = true;
            if (surface < 0 || surface >= system.Surfaces.Count) return;

            var s = system.Surfaces[surface];
            string indent = block.Indent ?? DefaultIndent;
            int at = Math.Max(block.Last,
                              Math.Max(block.Curvature, Math.Max(block.Thickness, block.Glass)));
            at = at < 0 ? terminator : at + 1;

            if (block.Curvature < 0 && Math.Abs(s.Curvature) > 0.0)
                insertions.Add((at, indent + "CUY " + LineEdit.Number(s.Curvature * scale)));

            if (block.Thickness < 0)
            {
                if (double.IsInfinity(s.Thickness))
                    insertions.Add((at, indent + "THI " + Infinite));
                else if (Math.Abs(s.Thickness) > 0.0)
                    insertions.Add((at, indent + "THI " + LineEdit.Number(s.Thickness / scale)));
            }

            if (block.Glass < 0 && !s.IsMirror && !string.IsNullOrWhiteSpace(s.Material))
                insertions.Add((at, indent + "GLA " + s.Material));
        }

        private static bool IsInfinite(string argument) =>
            double.TryParse(argument, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double v)
            && Math.Abs(v) > 1e18;

        /// <summary>Where one surface's lines are, as the file is walked.</summary>
        private sealed class Block
        {
            public int Curvature = -1;
            public int Thickness = -1;
            public int Glass = -1;
            public int Last = -1;
            public string? Indent;
            public bool Done;
        }
    }
}
