using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    private static readonly SimpleMultiplier SimpleMultiplierStrategy = new();
    
    private const int KaratsubaThresholdWords = 32;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var absA = new BetterBigInteger(a.GetDigits().ToArray());
        var absB = new BetterBigInteger(b.GetDigits().ToArray());

        var magnitude = MultiplyKaratsuba(absA, absB);
        
        return new BetterBigInteger(magnitude.GetDigits().ToArray(), a.IsNegative ^ b.IsNegative);
    }

    private static BetterBigInteger MultiplyKaratsuba(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        if (aDigits.Length == 0 || bDigits.Length == 0)
        {
            return new BetterBigInteger([]);
        }

        if (Math.Max(aDigits.Length, bDigits.Length) <= KaratsubaThresholdWords)
        {
            return SimpleMultiplierStrategy.Multiply(a, b);
        }

        var split = Math.Max(aDigits.Length, bDigits.Length) / 2;

        var aLow = new BetterBigInteger(aDigits[..Math.Min(split, aDigits.Length)].ToArray());
        var aHigh = split < aDigits.Length ? new BetterBigInteger(aDigits[split..].ToArray()) : new BetterBigInteger([]);
        var bLow = new BetterBigInteger(bDigits[..Math.Min(split, bDigits.Length)].ToArray());
        var bHigh = split < bDigits.Length ? new BetterBigInteger(bDigits[split..].ToArray()) : new BetterBigInteger([]);

        var z0 = MultiplyKaratsuba(aLow, bLow);
        var z2 = MultiplyKaratsuba(aHigh, bHigh);
        var z1 = MultiplyKaratsuba(aLow + aHigh, bLow + bHigh) - z0 - z2;

        return z0 + (z1 << (split * 32)) + (z2 << (split * 64));
    }
}