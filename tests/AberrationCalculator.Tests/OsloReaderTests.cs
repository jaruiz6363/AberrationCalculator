using System;
using System.IO;
using System.Linq;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.IO;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The OSLO reader against what OSLO 6.6 itself writes: EBR/ANG for an object at infinity, NAO/OBH
/// for a finite one, RD on a curved object surface, PFL for its perfect lens, GLA MOD with an index
/// per wavelength for a model glass, AP CHK for an aperture that blocks rays and AP for one that
/// only sizes the surface. And OSLO's primary wavelength is the first on the WV line.
/// </summary>
public class OsloReaderTests
{
    // Written by OSLO 6.6 EDU itself (2026-09-24).
    private const string OslosOwnFile = @"// OSLO 6.6 58447     0     0
LEN NEW ""No name"" 100 5
NAO  0.05
OBH  10.0
DES  ""OSLO""
UNI  1.0
// SRF 0
AIR
RD   -300.0
TH   200.0
AP  10.0
NXT  // SRF 1
AIR
TH   100.0
PFL    100.0
NXT  // SRF 2
GLA BAF4
RD   200.0
TH   10.0
AP CHK 0.0
NXT  // SRF 3
WV 0.58756 0.48613 0.65627
GLA MOD MODEL1      1.6201 1.6272360458395 1.6169694895481
TCE  236.0
TH   20.0
NXT  // SRF 4
AIR
TH   30.0
AP  0.4595340390767
NXT  // SRF 5
AIR
WV 0.58756 0.48613 0.65627
WW 1.0 1.0 1.0
END  5
";

    private static T WithFile<T>(string text, Func<string, T> read)
    {
        string path = Path.Combine(Path.GetTempPath(), $"oslo_{Guid.NewGuid():N}.len");
        File.WriteAllText(path, text);
        try { return read(path); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReadsWhatOsloWrites()
    {
        var sys = WithFile(OslosOwnFile, OsloReader.Read);

        Assert.Equal(ApertureType.ObjectSpaceNA, sys.Aperture.Type);
        Assert.Equal(0.05, sys.Aperture.Value, 12);
        Assert.Equal(FieldType.ObjectHeight, sys.FieldType);
        Assert.Equal(10.0, sys.Fields.Max(f => f.Y), 12);
        Assert.Equal(-300.0, sys.Surfaces[0].Radius, 12);
        Assert.Equal(SurfaceType.Paraxial, sys.Surfaces[1].Type);
        Assert.Equal(100.0, sys.Surfaces[1].FocalLength, 12);
        Assert.Equal("BAF4", sys.Surfaces[2].Material);
        Assert.Equal(SemiDiameterMode.Fixed, sys.Surfaces[2].SemiDiameterMode);   // AP CHK
        Assert.True(sys.Surfaces[3].ModelIndexEnabled);
        Assert.Equal(1.6201, sys.Surfaces[3].ModelNd, 9);
        Assert.Equal(60.4, sys.Surfaces[3].ModelVd, 3);
        Assert.Equal(SemiDiameterMode.Auto, sys.Surfaces[4].SemiDiameterMode);    // AP: sizes, never clips
        Assert.Equal(3, sys.Wavelengths.Count);                                   // the repeated WV replaced
    }

    [Fact]
    public void ThePrimaryWavelengthIsTheFirst()
    {
        // OSLO has no primary-wavelength keyword: wavelength 1 is the primary. The same lens written
        // d, F, C and F, d, C is a d-line lens and an F-line lens, as OSLO traces them.
        var dFirst = WithFile(OslosOwnFile, OsloReader.Read);
        var fFirst = WithFile(OslosOwnFile.Replace("WV 0.58756 0.48613 0.65627", "WV 0.48613 0.58756 0.65627"),
                              OsloReader.Read);
        Assert.Equal(0.58756, dFirst.Wavelengths[dFirst.PrimaryWavelengthIndex].Value, 5);
        Assert.Equal(0.48613, fFirst.Wavelengths[fFirst.PrimaryWavelengthIndex].Value, 5);
    }
}
