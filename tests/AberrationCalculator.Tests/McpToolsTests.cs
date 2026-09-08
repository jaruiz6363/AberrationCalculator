using System;
using System.IO;
using AberrationCalculator.Mcp;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The MCP server's tools.
///
/// <para>The protocol layer is three methods of JSON-RPC and is exercised by running the
/// server; what is worth pinning here is the layer underneath - that every advertised tool
/// actually runs on a real lens and returns something, and that the failures a caller will
/// hit come back as clear messages rather than as an empty result.</para>
/// </summary>
public class McpToolsTests
{
    private static string Lens => Fixtures.Lens("CookeTriplet");

    /// <summary>
    /// Every tool in the list runs and produces output. A tool that is advertised and then
    /// throws is worse than one that is not advertised at all, because the caller has no way
    /// to tell the difference from a lens it cannot analyse.
    /// </summary>
    [Fact]
    public void EveryAdvertisedToolRuns()
    {
        var writer = Tools.Open(Lens, null);

        Assert.NotEmpty(Tools.All);
        foreach (var tool in Tools.All)
        {
            string text = tool.Run(writer);
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"tool '{tool.Name}' returned nothing");
            Assert.False(string.IsNullOrWhiteSpace(tool.Description),
                $"tool '{tool.Name}' has no description");
        }
    }

    /// <summary>Every tool is named once, since the client dispatches by name.</summary>
    [Fact]
    public void ToolNamesAreUnique()
    {
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in Tools.All)
            Assert.True(seen.Add(tool.Name), $"duplicate tool name: {tool.Name}");
    }

    /// <summary>
    /// The tables really are tab-separated, because a caller will parse them. A report that
    /// quietly stopped emitting tabs would still look fine to a reader and break every
    /// consumer.
    /// </summary>
    [Fact]
    public void TheTableToolsEmitTabs()
    {
        var writer = Tools.Open(Lens, null);
        foreach (var tool in Tools.All)
        {
            // Prose, not tables: the whole report, a verdict with reasons, and the
            // coefficient breakdown, which is a nest of groups and int/fig/ind/tot rows that
            // reads far better ruled than flattened into one row per cell.
            if (tool.Name is "analyse_lens" or "aspheric_screen" or "seventh_order"
                          or "distortion") continue;
            Assert.Contains('\t', tool.Run(writer));
        }
    }

    /// <summary>A missing lens is named in the message, not swallowed.</summary>
    [Fact]
    public void AMissingLensFileSaysSo()
    {
        var e = Assert.Throws<FileNotFoundException>(
            () => Tools.Open(Path.Combine(Fixtures.LensDir, "no-such-lens.zmx"), null));
        Assert.Contains("no-such-lens.zmx", e.Message);
    }

    /// <summary>
    /// So is a missing glass folder. Silently falling back to the bundled catalogs would be
    /// worse than failing: the caller asked for particular glasses, and getting different
    /// ones changes the answer without changing its appearance.
    /// </summary>
    [Fact]
    public void AMissingGlassFolderSaysSo()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => Tools.Open(Lens, Path.Combine(Fixtures.LensDir, "no-such-catalog-folder")));
    }

    /// <summary>An empty path is a caller error and is reported as one.</summary>
    [Fact]
    public void AnEmptyPathIsRejected()
    {
        Assert.Throws<ArgumentException>(() => Tools.Open("", null));
    }
}
