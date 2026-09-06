using System;
using System.IO;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Opens a lens file without the caller having to know which program wrote it.
    ///
    /// Dispatch is by extension, which is how every one of these formats identifies
    /// itself in practice. A file whose extension does not name a supported format is a
    /// clear error rather than a guess: the formats are similar enough textually that
    /// sniffing one for another produces a plausible but wrong lens.
    /// </summary>
    public static class LensFile
    {
        /// <summary>Extensions this program can open, for a file-picker filter.</summary>
        public static readonly string[] SupportedExtensions =
            { ".zmx", ".seq", ".otx", ".opt", ".len", ".osl", ".json", ".lhlt" };

        /// <summary>
        /// Reads <paramref name="path"/> into a system. <paramref name="glass"/> is used
        /// by the formats that need refractive indices to recover an aperture the file
        /// states only indirectly; the others ignore it.
        /// </summary>
        public static OpticalSystem Read(string path, GlassCatalog? glass = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("No file given.", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Lens file not found.", path);

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".zmx": return ZmxReader.Read(path);
                case ".seq": return CodeVReader.Read(path, glass);
                case ".otx":
                case ".opt": return OptalixReader.Read(path, glass);
                case ".len":
                case ".osl": return OsloReader.Read(path);
                case ".json": return OptilandReader.Read(path);
                case ".lhlt": return LhltReader.Read(path).System;
                default:
                    throw new NotSupportedException(
                        $"'{Path.GetExtension(path)}' is not a lens format this program reads. " +
                        $"Supported: {string.Join(", ", SupportedExtensions)}");
            }
        }
    }
}
