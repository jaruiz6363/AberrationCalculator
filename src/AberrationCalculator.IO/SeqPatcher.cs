using System;
using System.Globalization;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Writes an optimised design back into the .seq it came from.
    ///
    /// <para>The surface lines are POSITIONAL - <c>S radius thickness material</c> - which is the
    /// one thing that makes this format different from the other three patched here. Everywhere
    /// else a value lives on a line of its own with a keyword in front of it, so editing one
    /// value cannot disturb another. Here all three share a line, so the line is taken apart and
    /// reassembled with the fields that did not move copied through as the file spelled
    /// them.</para>
    ///
    /// <para>Everything that is not a surface line - the aperture, the wavelengths, the field
    /// angles, CIR, STO, ASP, CON, and any of the several hundred directives this program has
    /// never heard of - is left exactly where it was.</para>
    /// </summary>
    internal static class SeqPatcher
    {
        /// <summary>The format writes an infinite object distance as a very large number.</summary>
        private const string Infinite = "1E+20";

        public static void Patch(OpticalSystem system, string originalPath, string outputPath,
                                 GlassCatalog? catalog)
        {
            var file = PatchText.Read(originalPath);
            double scale = system.FileUnitScale > 0.0 ? system.FileUnitScale : 1.0;
            var qualifier = new CodeVGlassQualifier(catalog, system.GlassCatalogs);

            // Both directions are needed. The qualifier says how a glass is SPELLED here; the
            // resolver says what a spelling MEANS, and without it a bare SK4 that never moved
            // would be rewritten as SK4_SCHOTT - a line in the diff reporting no change, and
            // worse than that, this program's guess at the catalog written into the user's file
            // as though the file had said it.
            var resolver = new CodeVGlassResolver(catalog);

            // The file numbers its surfaces by the ORDER of the SO/S/SI lines, exactly as the
            // reader counts them, so the count here is the same count and the two agree without
            // either having to name an index.
            int surface = 0;

            for (int i = 0; i < file.Lines.Count; i++)
            {
                string? line = file.Lines[i];
                if (line == null) continue;

                // A comment line can begin with anything, including the letter S.
                if (line.TrimStart().StartsWith("!", StringComparison.Ordinal)) continue;

                string keyword = LineEdit.Keyword(line);
                if (keyword != "S" && keyword != "SO" && keyword != "SI") continue;

                if (surface < system.Surfaces.Count)
                    file.Lines[i] = PatchSurfaceLine(line, system.Surfaces[surface], scale,
                                                     qualifier, resolver);
                surface++;
            }

            file.Write(outputPath);
        }

        /// <summary>
        /// Rewrites the radius, thickness and material of one surface line, and only those of
        /// them that actually changed.
        /// </summary>
        private static string PatchSurfaceLine(string line, Surface s, double scale,
                                               CodeVGlassQualifier qualifier,
                                               CodeVGlassResolver resolver)
        {
            // A PLANE IS A ZERO HERE. The format has no infinity for a radius; a flat surface is
            // written as radius 0, and the reader reads any radius below its own epsilon back as
            // infinite. Writing this program's infinity into the field would produce a file
            // neither program can read.
            string radius = double.IsInfinity(s.Radius) || double.IsNaN(s.Radius)
                ? "0"
                : LineEdit.Number(s.Radius / scale);

            string thickness = double.IsInfinity(s.Thickness)
                ? Infinite
                : LineEdit.Number(s.Thickness / scale);

            string patched = line;
            patched = KeepOrReplaceNumber(patched, 0, radius);
            patched = KeepOrReplaceNumber(patched, 1, thickness);

            // The material is the third field and may be absent - a line that names no material
            // is air, and a surface that has GAINED a glass needs the word written in.
            if (!MeansTheSameGlass(LineEdit.Argument(patched, 2), s, resolver))
                patched = LineEdit.ReplaceArgument(patched, Material(s, qualifier), 2);

            return patched;
        }

        /// <summary>
        /// Whether the material token already in the file names the glass the surface is now
        /// carrying - which is asked of the RESOLVER, not of the spelling, because the file may
        /// legitimately spell it several ways. <c>NBK7</c>, <c>NBK7_SCHOTT</c> and <c>N-BK7</c> are
        /// all the same glass, and rewriting one as another would change a line that did not move.
        /// </summary>
        private static bool MeansTheSameGlass(string token, Surface s, CodeVGlassResolver resolver)
        {
            bool isAir = !s.IsMirror && string.IsNullOrWhiteSpace(s.Material);
            if (string.IsNullOrWhiteSpace(token))
                return isAir;                       // an absent token is air

            if (token.Equals("AIR", StringComparison.OrdinalIgnoreCase)) return isAir;
            if (token.Equals("REFL", StringComparison.OrdinalIgnoreCase))
                return s.IsMirror
                    || "MIRROR".Equals(s.Material, StringComparison.OrdinalIgnoreCase);

            if (isAir || s.IsMirror) return false;
            return resolver.Resolve(token)
                           .Equals(s.Material, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The material token: <c>AIR</c> for no glass, <c>REFL</c> for a mirror, and otherwise
        /// the glass in this format's own punctuation-free spelling, qualified with its catalog
        /// when that can be said - which is what keeps SCHOTT's SK16 apart from SUMITA's on the
        /// way back in.
        /// </summary>
        private static string Material(Surface s, CodeVGlassQualifier qualifier)
        {
            if (s.IsMirror) return "REFL";
            string name = s.Material ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) return "AIR";
            if (name.Equals("MIRROR", StringComparison.OrdinalIgnoreCase)) return "REFL";
            return qualifier.ToCodeVMaterial(name);
        }

        /// <summary>
        /// Replaces a numeric field only when it says a different number. <c>1E+20</c> and
        /// <c>1.0000E20</c> are the same object distance, and rewriting one as the other would put
        /// a line in the diff that reports no change.
        /// </summary>
        private static string KeepOrReplaceNumber(string line, int field, string value)
        {
            string current = LineEdit.Argument(line, field);
            if (current.Equals(value, StringComparison.OrdinalIgnoreCase)) return line;

            if (double.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture,
                                out double have)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                                   out double want)
                && Math.Abs(have - want) <= 1e-12 * Math.Max(1.0, Math.Abs(have)))
                return line;

            return LineEdit.ReplaceArgument(line, value, field);
        }
    }
}
