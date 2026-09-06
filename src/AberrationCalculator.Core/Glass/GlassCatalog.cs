using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AberrationCalculator.Core.Glass;

/// <summary>
/// Reads .AGF glass catalogs and resolves names to <see cref="GlassData"/>.
///
/// AGF is a line-oriented text format. Only the records needed to evaluate and print an
/// index are read:
///
///   NM  name  formula  MIL  nd  vd  exclude  status  melt
///   CD  c1 … c10                       dispersion coefficients
///   LD  lambda_min lambda_max          validity range, micrometres
///   GC  free text                      comment
///
/// Anything else (TD thermal, OD cost, IT transmission) is skipped. A malformed line is
/// skipped rather than aborting the catalog: one bad entry in a vendor file should not cost
/// you the other six hundred glasses in it.
/// </summary>
public class GlassCatalog
{
    // Keyed "CATALOG:NAME" so two vendors can ship the same name without collision.
    private readonly Dictionary<string, GlassData> _byQualifiedName =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _catalogs = new();

    /// <summary>Catalog names loaded, in load order.</summary>
    public IReadOnlyList<string> Catalogs => _catalogs;

    public int Count => _byQualifiedName.Count;

    /// <summary>Loads every .agf in a folder. Missing folder is not an error.</summary>
    public void LoadFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;

        // Enumerate and filter by extension rather than globbing "*.agf": Linux filesystems
        // are case-sensitive and vendor files ship as .AGF as often as .agf.
        foreach (var file in Directory.GetFiles(folder))
            if (Path.GetExtension(file).Equals(".agf", StringComparison.OrdinalIgnoreCase))
                LoadFile(file);
    }

    /// <summary>Loads one .agf file. The catalog name is the file name without extension.</summary>
    public void LoadFile(string path)
    {
        string catalog = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        GlassData? current = null;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length < 2) continue;

            string tag = line.Substring(0, 2).ToUpperInvariant();
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            switch (tag)
            {
                case "NM":
                {
                    current = null;
                    if (parts.Length < 3) break;

                    var g = new GlassData { Name = parts[1], Catalog = catalog };
                    if (TryNum(parts, 2, out double formula))
                        g.Formula = (DispersionFormula)(int)formula;
                    if (TryNum(parts, 4, out double nd)) g.Nd = nd;
                    if (TryNum(parts, 5, out double vd)) g.Vd = vd;

                    _byQualifiedName[catalog + ":" + g.Name] = g;
                    current = g;
                    break;
                }

                case "CD":
                {
                    if (current == null) break;
                    var c = new List<double>();
                    for (int i = 1; i < parts.Length; i++)
                        c.Add(TryNum(parts, i, out double v) ? v : 0.0);
                    current.Coefficients = c.ToArray();
                    break;
                }

                case "LD":
                {
                    if (current == null) break;
                    if (TryNum(parts, 1, out double lo)) current.LambdaMin = lo;
                    if (TryNum(parts, 2, out double hi)) current.LambdaMax = hi;
                    break;
                }

                case "GC":
                {
                    if (current == null) break;
                    current.Comment = line.Length > 2 ? line.Substring(2).Trim() : null;
                    break;
                }
            }
        }

        if (!_catalogs.Contains(catalog)) _catalogs.Add(catalog);
    }

    /// <summary>
    /// Finds a glass by name. Accepts "CATALOG:NAME" for an exact hit; a bare name is looked
    /// up in <paramref name="preferred"/> order first, then across every loaded catalog.
    /// Returns null when nothing matches.
    /// </summary>
    public GlassData? Find(string? name, IReadOnlyList<string>? preferred = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (_byQualifiedName.TryGetValue(name!, out var exact)) return exact;

        if (preferred != null)
            foreach (var cat in preferred)
                if (_byQualifiedName.TryGetValue(cat.ToUpperInvariant() + ":" + name, out var p))
                    return p;

        foreach (var cat in _catalogs)
            if (_byQualifiedName.TryGetValue(cat + ":" + name, out var any))
                return any;

        return null;
    }

    /// <summary>Every glass in one catalog.</summary>
    public IEnumerable<GlassData> InCatalog(string catalog)
    {
        string prefix = catalog.ToUpperInvariant() + ":";
        foreach (var kv in _byQualifiedName)
            if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return kv.Value;
    }

    // ── Names the file readers use ───────────────────────────────────────────────

    /// <summary>Catalogs currently loaded, in load order.</summary>
    public IReadOnlyList<string> LoadedCatalogs => _catalogs;

    /// <summary>
    /// Looks a glass up by name, optionally qualified "CATALOG:NAME". Returns null when
    /// no loaded catalog has it — the caller decides whether that is fatal.
    /// </summary>
    public GlassData? GetGlass(string? name) => Find(name);

    /// <summary>Every glass in one catalog.</summary>
    public IEnumerable<GlassData> GetGlassesInCatalog(string catalog) => InCatalog(catalog);

    private static bool TryNum(string[] parts, int i, out double value)
    {
        value = 0.0;
        return i < parts.Length
            && double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
