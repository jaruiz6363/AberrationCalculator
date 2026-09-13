using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AberrationCalculator.Optimize.Operands;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// Every operand is documented, and the check is mechanical because the alternative did not work.
///
/// <para><c>ASBLT</c> was added, given inline help, wired into the evaluator and checked against a
/// central difference - and left out of the operand table in <c>docs/optimizer.md</c>, which the
/// README describes as listing every operand. Nothing failed, because nothing was looking. The
/// usage text has had <c>TheUsageTextMentionsEveryCommand</c> guarding it for exactly this
/// reason; the documentation had no equivalent.</para>
/// </summary>
public class OperandDocumentationTests
{
    /// <summary>
    /// The repository root, found by walking up from the test assembly until the docs folder
    /// appears. Returns null when the tests run somewhere the sources are not, in which case
    /// there is nothing to check rather than something to fail.
    /// </summary>
    private static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "optimizer.md")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void EveryOperandAppearsInTheOptimizerDocument()
    {
        string? root = RepoRoot();
        if (root == null) return;

        string doc = File.ReadAllText(Path.Combine(root, "docs", "optimizer.md"));

        // Whole word rather than backtick-delimited: the ray operands are documented as one
        // grouped row, `PX PY PZ PL PM PN`, so requiring backticks around each name would flag
        // six that are there.
        var missing = OperandHelp.All
                          .Select(t => t.ToString())
                          .Where(name => !Regex.IsMatch(doc, @"\b" + Regex.Escape(name) + @"\b"))
                          .ToArray();

        Assert.True(missing.Length == 0,
            "docs/optimizer.md does not mention: " + string.Join(", ", missing));
    }

    /// <summary>
    /// And every operand answers <c>HELP</c>, which is the other place one could be forgotten:
    /// the inline help is hand written per operand, not generated.
    /// </summary>
    [Fact]
    public void EveryOperandHasInlineHelpAndAnExample()
    {
        foreach (var type in OperandHelp.All)
        {
            string what = OperandHelp.Summary(type);
            Assert.False(string.IsNullOrWhiteSpace(what), $"{type} has no description");
            Assert.DoesNotContain("TODO", what, StringComparison.OrdinalIgnoreCase);

            string example = OperandHelp.Example(type);
            Assert.StartsWith(type.ToString(), example, StringComparison.Ordinal);
        }
    }
}
