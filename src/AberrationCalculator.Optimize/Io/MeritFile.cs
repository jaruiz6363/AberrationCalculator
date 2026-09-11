using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using AberrationCalculator.Optimize.Operands;

namespace AberrationCalculator.Optimize.Io;

/// <summary>
/// The merit function file (.mf): what the design is being asked to be, and nothing else.
///
/// <para>Common to every lens format. It does NOT hold variables or pickups - those describe what
/// may CHANGE rather than what is wanted, they belong with the design, and they live in the lens
/// file for a .lhlt and in a .var file for everything else. Keeping the two apart means a merit
/// function can be moved from one design to another without dragging along surface numbers that
/// meant something else.</para>
///
/// <para>One operand per line:</para>
///
/// <code>
///     TYPE, WEIGHT, TAR x, INPUTS
///     TYPE, WEIGHT, MIN x, INPUTS
///     TYPE, WEIGHT, MAX x, INPUTS
///     TYPE, WEIGHT, MIN x, MAX x, INPUTS
/// </code>
///
/// <code>
///     EFL,   100, TAR 50,           2          # focal length, in wavelength 2
///     EGT,    10, MIN 1,            2, 4       # glass edges over surfaces 2 to 4
///     EAT,    10, MIN 0.1,          2, 4       # and the air gaps
///     DTRGT,  10, MIN 1.5, MAX 12,  2, 4       # diameter-to-thickness ratio
///     PRMSA,   1, TAR 0                        # the predicted spot: no inputs
///     TTL,     5, MAX 60                       # total track
///     AXC,     2, TAR 0                        # axial colour
///     LCF,     5, TAR 0,            1.0        # lateral colour at the full field
///     DISTF,  10, MIN -2, MAX 2,    0.7        # distortion at seven tenths
///     RY,      1, TAR 0,            7, 0, 1, 0, 1
/// </code>
///
/// <para><b>The inputs are positional</b>, and which ones an operand takes is stated in exactly
/// one place - <see cref="OperandInputs"/> - so the parser, the writer and the error messages
/// cannot disagree. Trailing inputs may be left off and take their defaults: <c>RY, 1, TAR 0, 7</c>
/// is surface seven at the reference colour, the maximum field and the chief ray.</para>
///
/// <para><b>Targets and boundaries are different things.</b> An operand with <c>TAR</c> is driven
/// to it and weighed against everything else. One with <c>MIN</c> or <c>MAX</c> costs exactly
/// zero - in the merit and in the Jacobian - while it is satisfied. It does not pull the design
/// gently toward the middle of its range; it is simply not there until it is threatened.</para>
/// </summary>
public static class MeritFile
{
    public static List<Operand> Read(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        return Parse(File.ReadAllLines(path), path);
    }

    public static List<Operand> Parse(IEnumerable<string> lines, string? origin = null)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));

        var operands = new List<Operand>();
        int number = 0;

        foreach (string raw in lines)
        {
            number++;
            string line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            try
            {
                operands.Add(ParseOperand(line));
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new FormatException(
                    $"{origin ?? "merit function"}, line {number}: {ex.Message}\n  {raw.Trim()}");
            }
        }
        return operands;
    }

    internal static string StripComment(string line)
    {
        int hash = line.IndexOf('#');
        return hash >= 0 ? line.Substring(0, hash) : line;
    }

    private static Operand ParseOperand(string line)
    {
        var field = new List<string>();
        foreach (string part in line.Split(',')) field.Add(part.Trim());

        if (field.Count < 2)
            throw new FormatException(
                "an operand needs at least a type and a weight, separated by a comma");

        if (!Enum.TryParse<OperandType>(field[0], true, out var type))
            throw new FormatException(
                $"'{field[0]}' is not an operand. The types are: "
              + string.Join(", ", Enum.GetNames(typeof(OperandType))));

        if (!TryNumber(field[1], out double weight))
            throw new FormatException($"'{field[1]}' is not a weight");

        // MIN, MAX and TAR come next, in any order and any combination bar the contradictory one.
        double target = 0.0;
        double? min = null, max = null;
        bool haveTarget = false;
        int i = 2;

        for (; i < field.Count; i++)
        {
            var (word, rest) = SplitKeyword(field[i]);
            if (word == null) break;

            switch (word)
            {
                case "TAR": target = Value(rest, "TAR"); haveTarget = true; break;
                case "MIN": min = Value(rest, "MIN"); break;
                case "MAX": max = Value(rest, "MAX"); break;
                default: throw new FormatException($"unexpected '{field[i]}'");
            }
        }

        if (!haveTarget && min == null && max == null)
            throw new FormatException(
                $"{type} has no TAR, MIN or MAX, so it is not asking for anything");
        if (haveTarget && (min != null || max != null))
            throw new FormatException(
                $"{type} has both a TAR and a limit. An operand is either driven to a value or "
              + "held inside a range, and asking for both is ambiguous");
        if (min != null && max != null && min > max)
            throw new FormatException($"MIN {min} is above MAX {max}");

        // Whatever is left is the input list, positionally.
        var wanted = OperandInputs.For(type);
        int given = field.Count - i;
        if (given > wanted.Count)
            throw new FormatException(
                $"{type} takes {(wanted.Count == 0 ? "no inputs" : wanted.Count + " inputs (" + OperandInputs.Describe(type) + ")")}"
              + $", and {given} were given");

        int surface = 0, surface2 = 0, wave = 0;
        double hy = 1.0, px = 0.0, py = 0.0;

        for (int k = 0; k < given; k++)
        {
            string text = field[i + k];
            if (!TryNumber(text, out double v))
                throw new FormatException($"'{text}' is not a number, for the "
                                        + $"{wanted[k].ToString().ToLowerInvariant()} of {type}");

            switch (wanted[k])
            {
                case OperandInput.Surface1: surface = Whole(v, "surface"); break;
                case OperandInput.Surface2: surface2 = Whole(v, "surface2"); break;

                case OperandInput.Wave:
                    wave = Whole(v, "wave");
                    // Wavelengths are numbered from one. A merit function should never contain
                    // an index that is not an index: to mean the reference colour, leave the
                    // wavelength off altogether.
                    if (wave < 1)
                        throw new FormatException(
                            $"wavelengths are numbered from 1, so {wave} is not one. Leave the "
                          + "wavelength off to use the design's reference colour");
                    break;

                case OperandInput.Hy: hy = v; break;
                case OperandInput.Px: px = v; break;
                case OperandInput.Py: py = v; break;
            }
        }

        // A span given only its first surface is that surface alone.
        if (surface2 == 0) surface2 = surface;

        return new Operand
        {
            Type = type,
            Surface = surface,
            Surface2 = surface2,
            Wave = wave,
            Hy = hy,
            Px = px,
            Py = py,
            Target = target,
            Weight = weight,
            Min = min,
            Max = max,
        };
    }

    /// <summary>
    /// Splits <c>MIN 1.5</c> into its keyword and its value. Returns a null keyword for a field
    /// that is not one of these, which is how the parser knows the inputs have started.
    /// </summary>
    private static (string? Word, string Value) SplitKeyword(string field)
    {
        int space = field.IndexOf(' ');
        string head = (space < 0 ? field : field.Substring(0, space)).ToUpperInvariant();
        if (head != "TAR" && head != "MIN" && head != "MAX") return (null, string.Empty);
        return (head, space < 0 ? string.Empty : field.Substring(space + 1).Trim());
    }

    private static double Value(string text, string keyword)
    {
        if (!TryNumber(text, out double v))
            throw new FormatException($"{keyword} needs a number, not '{text}'");
        return v;
    }

    private static int Whole(double v, string what)
    {
        int i = (int)Math.Round(v);
        if (Math.Abs(v - i) > 1e-9)
            throw new FormatException($"the {what} has to be a whole number, not {v}");
        return i;
    }

    internal static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    // ── Writing ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the merit function out. What comes out parses back to what went in, so a merit
    /// function built by a command can be saved, read and argued with like any other.
    /// </summary>
    public static string Write(IEnumerable<Operand> operands, string? header = null)
    {
        if (operands == null) throw new ArgumentNullException(nameof(operands));

        var sb = new StringBuilder();
        sb.AppendLine("# abcalc merit function.");
        if (!string.IsNullOrWhiteSpace(header)) sb.AppendLine("# " + header);
        sb.AppendLine("# TYPE, WEIGHT, TAR x | MIN x | MAX x, INPUTS");
        sb.AppendLine();

        foreach (var op in operands) sb.AppendLine(Line(op));
        return sb.ToString();
    }

    /// <summary>
    /// One operand as its merit-function line. Exposed because a listing should show the user
    /// exactly what is in the file, not a paraphrase of it - the line they see is the line they
    /// could have typed.
    /// </summary>
    public static string Line(Operand op)
    {
        if (op == null) throw new ArgumentNullException(nameof(op));

        var sb = new StringBuilder();
        sb.Append(op.Type.ToString()).Append(", ").Append(N(op.Weight));

        if (op.IsBoundary)
        {
            if (op.Min.HasValue) sb.Append(", MIN ").Append(N(op.Min.Value));
            if (op.Max.HasValue) sb.Append(", MAX ").Append(N(op.Max.Value));
        }
        else
        {
            sb.Append(", TAR ").Append(N(op.Target));
        }

        // Inputs are written only as far as the last one that says anything. Padding the rest out
        // with their defaults would mean writing a wavelength of zero for an operand that never
        // named a wavelength - a number the parser rightly refuses, so the file would not read
        // back. Since the inputs are positional and omission is always a trailing run, stopping
        // at the last meaningful one is exactly what round-trips.
        var inputs = OperandInputs.For(op.Type);
        int last = -1;
        for (int k = 0; k < inputs.Count; k++)
            if (Says(op, inputs[k])) last = k;

        for (int k = 0; k <= last; k++)
        {
            sb.Append(", ");
            sb.Append(inputs[k] switch
            {
                OperandInput.Surface1 => N(op.Surface),
                OperandInput.Surface2 => N(op.Surface2),
                // Forced to write a wavelength for an operand that named none - which only a
                // command-built operand can be - the first is the honest guess.
                OperandInput.Wave => N(op.Wave < 1 ? 1 : op.Wave),
                OperandInput.Hy => N(op.Hy),
                OperandInput.Px => N(op.Px),
                _ => N(op.Py),
            });
        }
        return sb.ToString();
    }

    /// <summary>Parses a single operand line, for a command that adds one.</summary>
    public static Operand ParseLine(string line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        var parsed = Parse(new[] { line });
        if (parsed.Count != 1)
            throw new FormatException("expected one operand, found " + parsed.Count);
        return parsed[0];
    }

    /// <summary>
    /// Whether this input carries information, or is merely sitting at the value it would take
    /// if it were left out.
    ///
    /// <para>A surface always says something - an operand that takes one is meaningless without
    /// it. The rest are compared against what omission would have given, so an operand written
    /// with every default writes none of them and reads back identical.</para>
    /// </summary>
    private static bool Says(Operand op, OperandInput input) => input switch
    {
        OperandInput.Surface1 => true,
        OperandInput.Surface2 => op.Surface2 != op.Surface,
        OperandInput.Wave => op.Wave >= 1,
        OperandInput.Hy => op.Hy != 1.0,
        OperandInput.Px => op.Px != 0.0,
        _ => op.Py != 0.0,
    };

    private static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);
}
