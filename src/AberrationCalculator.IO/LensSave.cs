using System;
using System.Collections.Generic;
using System.IO;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Saves a design: back into the format it was read from, by editing that file, or into another
    /// format, by writing a whole new lens.
    ///
    /// <para><b>The same format is edited, never regenerated.</b> A real lens file carries far more
    /// than this program models - solves, coatings, tolerances, configurations - and
    /// <see cref="LensPatcher"/> changes only what the optimiser moved, keeping every other byte.</para>
    ///
    /// <para><b>Another format is written whole</b>, by <see cref="LensFile.Write"/>: what this
    /// program models goes across - surfaces, glasses, conics and aspheric terms, aperture, fields,
    /// wavelengths - and nothing else can, since the target format has never seen the rest. The
    /// returned note says so. A design the target format cannot carry - an r^2 aspheric term in
    /// CODE V, OSLO or OPTALIX - is refused with the reason.</para>
    ///
    /// <para>This used to go through <see cref="LensPatcher"/> whatever the output was called, so
    /// saving a ZEMAX design "as" <c>out.len</c> wrote ZEMAX text into a file named for OSLO.</para>
    /// </summary>
    public static class LensSave
    {
        /// <summary>
        /// Saves <paramref name="system"/>, read from <paramref name="originalPath"/>, to
        /// <paramref name="outputPath"/>.
        /// </summary>
        /// <returns>Notes for the user; empty for a save into the same format.</returns>
        /// <exception cref="NotSupportedException">The output format cannot carry the design, or
        /// is not one this program writes. Nothing is written.</exception>
        public static IReadOnlyList<string> Save(OpticalSystem system, string originalPath, string outputPath,
                                                 GlassCatalog? catalog = null, bool installOptilandGlasses = true)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (originalPath == null) throw new ArgumentNullException(nameof(originalPath));
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));

            string from = Format(originalPath), to = Format(outputPath);
            if (from == to)
            {
                LensPatcher.Save(system, originalPath, outputPath, catalog);
                return Array.Empty<string>();
            }

            if (Array.IndexOf(LensFile.WritableExtensions, to) < 0)
                throw new NotSupportedException(
                    $"'{Path.GetExtension(outputPath)}' is not a lens format this program writes. "
                    + $"Supported: {string.Join(", ", LensFile.WritableExtensions)}.");

            var notes = new List<string>();
            try
            {
                notes.AddRange(LensFile.Write(system, outputPath, catalog, installOptilandGlasses));
            }
            catch (InvalidOperationException ex)
            {
                throw new NotSupportedException($"The design cannot be written as {Name(to)}: {ex.Message}", ex);
            }
            notes.Insert(0,
                $"Written as {Name(to)}, a new lens made from the design, not an edit of the {Name(from)} file: "
                + "its surfaces, glasses, conics and aspheric terms, aperture, fields and wavelengths go across, "
                + $"and anything else the {Name(from)} file held (solves, coatings, tolerances, configurations) does not.");
            return notes;
        }

        /// <summary>A format by its extension, two extensions that name one format being one.</summary>
        private static string Format(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".osl" ? ".len" : ext == ".opt" ? ".otx" : ext;
        }

        private static string Name(string ext) => ext switch
        {
            ".zmx" => "ZEMAX",
            ".seq" => "CODE V",
            ".len" => "OSLO",
            ".otx" => "OPTALIX",
            ".json" => "Optiland",
            ".lhlt" => "LensHH-LT",
            _ => ext,
        };
    }
}
