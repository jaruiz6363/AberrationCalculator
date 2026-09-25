using System;
using System.IO;
using System.Linq;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.IO;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The Optalix reader against what Optalix writes - its fictitious-glass codes, PRI glasses, FH
/// clipping apertures, RAIM codes and lens modules (pairs of SUT L surfaces) - and the in-place
/// save, which must leave all of that as it found it while writing the design's new values into
/// the right SUR blocks.
/// </summary>
public class OptalixFileTests
{
    // In Optalix's own line forms (the fictitious glass and PRI of Eye/EYE_NEW_CHROMATIC.OTX, the
    // tube lens of Gross-HOS/Misc/45-132_Chromat-with-tube-lens.otx), with a private glass 'GE'.
    private const string OptalixStyle = @"VERS 11.82
RAIM  2
EPD  10.0000
WL   0.54600     0.48600     0.65000
WTW  100 100 100
REF    1
FTYP    1
NFLD    2
FLD    1   0.000000000       0.000000000      100  1        2594861
FLD    2   0.000000000       2.000000000      100  1        2594861
SUR   0
SUT S
CUY 0.0000000000000
THI  0.1000000000E+21
SUR   1
SUT S
CUY  0.0100000000000
THI   5.000000000
GLA 613369
APE  1   10.00000000       10.00000000      0.000000000      0.000000000      0.000000000        1   0   0   1
FH    1  1
SUR   2
SUT S
CUY -0.0100000000000
THI   2.000000000
GLA 'GE'
APE  1   10.00000000       10.00000000      0.000000000      0.000000000      0.000000000        1   0   0   1
SUR   3
SUT L
CUY 0.0000000000000
THI 0.000000000
LMOD  0.1000000000E-01   0.000000000       0.000000000       0.000000000       0.000000000
STO
SUR   4
SUT L
CUY 0.0000000000000
THI 100.0000000
LMOD   0.000000000       0.000000000       0.000000000       0.000000000       0.000000000
SUR   5
SUT S
CUY  0.0020000000000
THI  50.00000000
PRI   1.336000000       1.336000000       1.336000000
SUR   6
SUT S
CUY 0.0000000000000
THI 0.000000000
";

    private static string Temp(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), $"optalix_{Guid.NewGuid():N}.otx");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void ReadsWhatOptalixWrites()
    {
        string path = Temp(OptalixStyle);
        try
        {
            var sys = OptalixReader.Read(path);
            Assert.Equal(RayAimingMode.Real, sys.RayAiming);                       // RAIM 2: the real stop
            Assert.True(sys.Surfaces[1].ModelIndexEnabled);                         // GLA 613369
            Assert.Equal(1.613, sys.Surfaces[1].ModelNd, 12);
            Assert.Equal(36.9, sys.Surfaces[1].ModelVd, 12);
            Assert.Equal(SemiDiameterMode.Fixed, sys.Surfaces[1].SemiDiameterMode); // FH 1
            Assert.Equal(SemiDiameterMode.Auto, sys.Surfaces[2].SemiDiameterMode);  // no FH
            Assert.Equal("GE", sys.Surfaces[2].Material);

            Assert.Equal(6, sys.Surfaces.Count);                                    // the L pair is one lens
            Assert.Equal(SurfaceType.Paraxial, sys.Surfaces[3].Type);
            Assert.Equal(100.0, sys.Surfaces[3].FocalLength, 9);                   // LMOD is a power
            Assert.Equal(100.0, sys.Surfaces[3].Thickness, 12);
            Assert.True(sys.Surfaces[3].IsStop);
            Assert.True(sys.Surfaces[4].ModelIndexEnabled);                         // PRI
            Assert.Equal(1.336, sys.Surfaces[4].ModelNd, 12);

            Assert.True(OptalixReader.TryFictitiousGlass("6204.603", out double nd, out double vd));
            Assert.Equal(1.6204, nd, 12);
            Assert.Equal(60.3, vd, 12);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SavingInPlaceWritesEachValueIntoItsOwnSurface()
    {
        string path = Temp(OptalixStyle);
        string output = Path.ChangeExtension(Temp(""), ".otx");
        try
        {
            var sys = OptalixReader.Read(path);
            sys.Surfaces[1].Curvature = 0.011;   // file SUR 1
            sys.Surfaces[3].Thickness = 110.0;   // the ideal lens: file SUR 4's THI
            sys.Surfaces[4].Curvature = 0.003;   // file SUR 5, a number above it here

            LensPatcher.Save(sys, path, output);
            var lines = File.ReadAllLines(output).Select(l => l.Trim()).ToArray();
            string[] Block(int n)
            {
                int start = Array.FindIndex(lines, l => l.StartsWith("SUR") && l.EndsWith(" " + n));
                int end = Array.FindIndex(lines, start + 1, l => l.StartsWith("SUR"));
                return lines[start..(end < 0 ? lines.Length : end)];
            }
            double Number(string[] block, string keyword) =>
                double.Parse(block.First(l => l.StartsWith(keyword + " ")).Split(' ', StringSplitOptions.RemoveEmptyEntries)[1],
                             System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(0.011, Number(Block(1), "CUY"), 12);
            Assert.Contains("GLA 613369", Block(1));      // the fictitious glass stands
            Assert.Contains("FH    1  1", Block(1));
            Assert.Contains("GLA 'GE'", Block(2));        // the private glass keeps its quotes
            Assert.Equal(0.0, Number(Block(3), "THI"), 12); // the gap between the principal planes is untouched
            Assert.Equal(110.0, Number(Block(4), "THI"), 12);
            Assert.Equal(0.0, Number(Block(4), "CUY"), 12);                // the lens module stays flat
            Assert.Equal(0.003, Number(Block(5), "CUY"), 12);
            Assert.Contains("PRI   1.336000000       1.336000000       1.336000000", Block(5));

            // A model glass the design changed is rewritten, in Optalix's code.
            sys.Surfaces[1].ModelNd = 1.62;
            LensPatcher.Save(sys, path, output);
            Assert.Contains("GLA 620.369", File.ReadAllLines(output).Select(l => l.Trim()));
        }
        finally { File.Delete(path); File.Delete(output); }
    }
}
