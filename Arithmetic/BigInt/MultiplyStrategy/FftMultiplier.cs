using System.Numerics;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    private static readonly SimpleMultiplier SimpleMultiplierStrategy = new();

    private const int FftThresholdWords = 64;
    private const uint Base16 = 1u << 16;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        if (aDigits.Length == 0 || bDigits.Length == 0)
        {
            return new BetterBigInteger([]);
        }

        if (Math.Max(aDigits.Length, bDigits.Length) < FftThresholdWords)
        {
            return SimpleMultiplierStrategy.Multiply(a, b);
        }

        var a16 = SplitTo16BitWords(aDigits);
        var b16 = SplitTo16BitWords(bDigits);

        var convolution = Convolve(a16, b16);
        var magnitude = PackTo32BitWords(convolution);

        return new BetterBigInteger(magnitude, a.IsNegative ^ b.IsNegative);
    }

    private static uint[] SplitTo16BitWords(ReadOnlySpan<uint> words)
    {
        var result = new uint[words.Length * 2];

        for (var i = 0; i < words.Length; ++i)
        {
            var value = words[i];
            result[2 * i] = value & 0xFFFF;
            result[2 * i + 1] = value >> 16;
        }

        return result;
    }

    private static uint[] Convolve(uint[] a, uint[] b)
    {
        var requiredLength = a.Length + b.Length;
        var fftLength = 1;

        while (fftLength < requiredLength)
        {
            fftLength <<= 1;
        }

        var fa = new Complex[fftLength];
        var fb = new Complex[fftLength];

        for (var i = 0; i < a.Length; ++i)
        {
            fa[i] = new Complex(a[i], 0);
        }

        for (var i = 0; i < b.Length; ++i)
        {
            fb[i] = new Complex(b[i], 0);
        }

        Fft(fa, invert: false);
        Fft(fb, invert: false);

        for (var i = 0; i < fftLength; ++i)
        {
            fa[i] *= fb[i];
        }

        Fft(fa, invert: true);

        var result = new uint[requiredLength + 2];
        long carry = 0;
        var writeIndex = 0;

        for (var i = 0; i < requiredLength; ++i)
        {
            var value = (long)Math.Round(fa[i].Real) + carry;

            if (value < 0)
            {
                value = 0;
            }

            result[writeIndex++] = (uint)(value & (Base16 - 1));
            carry = value >> 16;
        }

        while (carry > 0)
        {
            result[writeIndex++] = (uint)(carry & (Base16 - 1));
            carry >>= 16;
        }

        return result[..writeIndex];
    }

    private static uint[] PackTo32BitWords(ReadOnlySpan<uint> words16)
    {
        if (words16.Length == 0)
        {
            return [];
        }

        var result = new uint[(words16.Length + 1) / 2];

        for (var i = 0; i < result.Length; ++i)
        {
            var low = words16[2 * i];
            var high = (2 * i + 1) < words16.Length ? words16[2 * i + 1] : 0u;
            result[i] = low | (high << 16);
        }

        var length = result.Length;

        while (length > 0 && result[length - 1] == 0)
        {
            --length;
        }

        return length == result.Length ? result : result[..length];
    }

    private static void Fft(Complex[] data, bool invert)
    {
        var n = data.Length;
        var j = 0;

        for (var i = 1; i < n; ++i)
        {
            var bit = n >> 1;

            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }

            j ^= bit;

            if (i >= j)
            {
                continue;
            }

            (data[i], data[j]) = (data[j], data[i]);
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var angle = 2 * Math.PI / len * (invert ? -1 : 1);
            var wLen = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (var i = 0; i < n; i += len)
            {
                var w = Complex.One;
                var half = len >> 1;

                for (var k = 0; k < half; ++k)
                {
                    var u = data[i + k];
                    var v = data[i + k + half] * w;

                    data[i + k] = u + v;
                    data[i + k + half] = u - v;

                    w *= wLen;
                }
            }
        }

        if (!invert)
        {
            return;
        }

        for (var i = 0; i < n; ++i)
        {
            data[i] /= n;
        }
    }
}