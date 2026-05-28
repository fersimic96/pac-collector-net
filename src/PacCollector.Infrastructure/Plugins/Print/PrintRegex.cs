using System.Text.RegularExpressions;

namespace PacCollector.Infrastructure.Plugins.Print;

// regex compartidos para reportes print. Compilados via source generator (cero reflection).
internal static partial class PrintRegex
{
    // header: marker S/N: <serial> V <firmware>
    public static Regex BuildHeaderRegex(string marker)
        => new(
            $@"{Regex.Escape(marker)}\s+S/N:\s*(\S+)\s+V\s+(\S+)",
            RegexOptions.Compiled);

    [GeneratedRegex(@"(?m)^Date\s*:\s*(\d{1,2}\s+\w{3}\s+\d{4})\s+(\d{1,2}:\d{2})")]
    public static partial Regex ResultDate();

    [GeneratedRegex(@"(?m)^Operator\s*:\s*(\S+(?:\s+\S+)*?)\s*$")]
    public static partial Regex Operator();

    [GeneratedRegex(@"(?m)^Sample ID\s*:\s*(\S+(?:\s+\S+)*?)\s*$")]
    public static partial Regex SampleId();

    [GeneratedRegex(@"(?m)^Product\s*:\s*(.+?)\s*$")]
    public static partial Regex Product();

    [GeneratedRegex(@"Cycle/Result\s*:\s*(\d+)\s*/\s*(\d+)")]
    public static partial Regex CycleResult();

    [GeneratedRegex(@"(?m)^\s*(\d{1,2}\s+\w{3}\s+\d{4})\s+(\d{1,2}:\d{2})\s+AtmPrs")]
    public static partial Regex PmdRunDate();

    [GeneratedRegex(@"\s{2,}")]
    public static partial Regex MultipleSpaces();

    [GeneratedRegex(@"^(-?\d+(?:\.\d+)?)\s*C\s*$")]
    public static partial Regex NumericCelsius();

    // fila de tabla de destilacion: IBP/FBP/N% + temperatura
    [GeneratedRegex(@"^\s*(IBP|FBP|\d+\s*%)\s+(-?\d+(?:\.\d+)?)")]
    public static partial Regex DistillationRow();

    // fila de recovery: %R N % %r N %
    [GeneratedRegex(@"%R\s+(\d+(?:\.\d+)?)\s*%\s+%r\s+(\d+(?:\.\d+)?)\s*%")]
    public static partial Regex RecoveryResidue();
}
