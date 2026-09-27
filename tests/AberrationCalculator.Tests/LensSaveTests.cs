using System;
using System.IO;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Saving a design: into the format it was read, by editing that file; into another, by writing a
/// whole lens. The second went through the editor too, whatever the output was called, so a ZEMAX
/// design saved "as" a .len became ZEMAX text in a file named for OSLO.
/// </summary>
public class LensSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "abcalc_save_" + Guid.NewGuid().ToString("N"));
    private static readonly GlassCatalog Catalog = CatalogLocator.LoadBundled();

    public LensSaveTests()
    {
        Directory.CreateDirectory(_dir);
        OptilandGlass.UserCatalogsFolderOverride = Path.Combine(_dir, "optiland-home");
    }

    public void Dispose()
    {
        OptilandGlass.UserCatalogsFolderOverride = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static string Fixture(string name, string folder) => Designs.PathOf(name, folder);

    private static double Efl(OpticalSystem s)
    {
        int pw = Math.Max(0, s.PrimaryWavelengthIndex);
        return ParaxialTrace.Trace(s, IndexResolver.Build(s, Catalog, s.Wavelengths[pw].Value), 0.0).Efl;
    }

    [Fact]
    public void AZemaxDesignSavedAsOsloIsAnOsloLens()
    {
        string original = Fixture("KingslakeDG.zmx", "lenses");
        var design = LensFile.Read(original, Catalog);
        design.Surfaces[2].Curvature *= 1.01;           // as an optimiser would move it
        string output = Path.Combine(_dir, "better.len");

        var notes = LensSave.Save(design, original, output, Catalog);

        string text = File.ReadAllText(output);
        Assert.StartsWith("// OSLO", text);
        Assert.Contains("LEN NEW", text);
        Assert.DoesNotContain("SURF ", text);           // no ZEMAX in it
        Assert.Contains(notes, n => n.Contains("Written as OSLO") && n.Contains("does not"));

        var back = LensFile.Read(output, Catalog);
        Assert.Equal(Efl(design), Efl(back), 9);
        Assert.Equal(design.Surfaces[2].Curvature, back.Surfaces[2].Curvature, 12);
    }

    [Theory]
    [InlineData(".seq")]
    [InlineData(".otx")]
    [InlineData(".json")]
    [InlineData(".lhlt")]
    [InlineData(".zmx")]
    public void AnOsloDesignSavedAsAnotherFormatReadsBackTheSame(string ext)
    {
        string original = Fixture("KingslakeDG.len", "lenses");
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "out" + ext);

        LensSave.Save(design, original, output, Catalog);

        var back = LensFile.Read(output, Catalog);
        Assert.Equal(Efl(design), Efl(back), 9);
        Assert.Equal(design.Surfaces.Count, back.Surfaces.Count);
    }

    /// <summary>The same format is still edited: nothing is said, and what the program does not model survives.</summary>
    [Fact]
    public void TheSameFormatIsEditedNotRewritten()
    {
        string original = Path.Combine(_dir, "lens.zmx");
        File.Copy(Fixture("KingslakeDG.zmx", "lenses"), original);
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "lens.optimised.zmx");

        var notes = LensSave.Save(design, original, output, Catalog);

        Assert.Empty(notes);
        Assert.Equal(File.ReadAllText(original), File.ReadAllText(output));
    }

    /// <summary>Two extensions that name one format are one format: .osl is OSLO as .len is.</summary>
    [Fact]
    public void AnOsloFileSavedAsOslIsEdited()
    {
        string original = Fixture("KingslakeDG.len", "lenses");
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "lens.osl");

        var notes = LensSave.Save(design, original, output, Catalog);

        Assert.Empty(notes);
        Assert.Equal(File.ReadAllText(original), File.ReadAllText(output));
    }

    [Fact]
    public void ADesignTheFormatCannotCarryIsRefusedAndNothingWritten()
    {
        string original = Fixture("G1_finite_r2.zmx", "coefficient-reference");
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "r2.len");

        var ex = Assert.Throws<NotSupportedException>(() => LensSave.Save(design, original, output, Catalog));
        Assert.Contains("OSLO", ex.Message);
        Assert.Contains("r²", ex.Message);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void AFormatItDoesNotWriteIsRefused()
    {
        string original = Fixture("KingslakeDG.zmx", "lenses");
        var design = LensFile.Read(original, Catalog);
        string output = Path.Combine(_dir, "lens.txt");

        var ex = Assert.Throws<NotSupportedException>(() => LensSave.Save(design, original, output, Catalog));
        Assert.Contains(".txt", ex.Message);
        Assert.False(File.Exists(output));
    }
}
