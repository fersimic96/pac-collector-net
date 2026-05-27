namespace PacCollector.Domain.ValueObjects;

public readonly struct PacChecksum : IEquatable<PacChecksum>
{
    private readonly string _value;

    private PacChecksum(string value) => _value = value;

    // suma de bytes mod 256, complemento a dos, hex de 4 digitos
    public static PacChecksum FromBytes(ReadOnlySpan<byte> input)
    {
        uint sum = 0;
        foreach (var b in input)
            sum = (sum + b) & 0xFF;
        var result = ((sum ^ 0xFF) + 1) & 0xFF;
        return new PacChecksum($"{result:X4}");
    }

    public static PacChecksum FromString(string input)
        => FromBytes(System.Text.Encoding.UTF8.GetBytes(input));

    public string AsString => _value ?? "0000";

    public override string ToString() => AsString;

    public bool Equals(PacChecksum other) => string.Equals(AsString, other.AsString, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is PacChecksum o && Equals(o);
    public override int GetHashCode() => AsString.GetHashCode(StringComparison.Ordinal);
    public static bool operator ==(PacChecksum a, PacChecksum b) => a.Equals(b);
    public static bool operator !=(PacChecksum a, PacChecksum b) => !a.Equals(b);
}
