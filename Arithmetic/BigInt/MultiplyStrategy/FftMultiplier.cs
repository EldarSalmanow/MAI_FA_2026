using System.Numerics;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    private static readonly KaratsubaMultiplier KaratsubaMultiplierStrategy = new();

    private const int FftThresholdWords = 64;

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
            return KaratsubaMultiplierStrategy.Multiply(a, b);
        }

        var a16 = To16BitWords(aDigits);
        var b16 = To16BitWords(bDigits);

        var convolution = Convolve(a16, b16);
        
        var magnitude = To32BitWords(convolution);

        return new BetterBigInteger(magnitude, a.IsNegative ^ b.IsNegative);
    }

    private static uint[] Convolve(uint[] a, uint[] b)
    {
        var n = a.Length + b.Length;
        var fftLength = 1;
        for (; fftLength < n; fftLength <<= 1) {}

        var coefficientsA = new Complex[fftLength];
        var coefficientsB = new Complex[fftLength];

        for (var i = 0; i < a.Length; ++i)
        {
            coefficientsA[i] = new Complex(a[i], 0);
        }

        for (var i = 0; i < b.Length; ++i)
        {
            coefficientsB[i] = new Complex(b[i], 0);
        }

        Fft(coefficientsA, false);
        Fft(coefficientsB, false);

        for (var i = 0; i < fftLength; ++i)
        {
            coefficientsA[i] *= coefficientsB[i];
        }

        Fft(coefficientsA, true);

        var result = new uint[n + 2];
        long carry = 0;
        var length = 0;

        for (var i = 0; i < n; ++i, ++length)
        {
            var value = Math.Max(0, (long) Math.Round(coefficientsA[i].Real)) + carry;
            result[length] = (uint) (value & 0xFFFF);
            carry = value >> 16;
        }

        for (; carry > 0; carry >>= 16, ++length)
        {
            result[length] = (uint) (carry & 0xFFFF);
        }

        return result[..length];
    }

    private static void Fft(Complex[] data, bool invert)
    {
        var n = data.Length;

        for (int i = 1, j = 0; i < n; ++i)
        {
            for (var bit = n >> 1; (j ^= bit) < bit; bit >>= 1);
            
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var angle = 2 * Math.PI / len * (invert ? -1 : 1);
            var wLen = new Complex(Math.Cos(angle), Math.Sin(angle));
            var half = len >> 1;

            for (var i = 0; i < n; i += len)
            {
                var w = Complex.One;
                
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
    
    private static uint[] To16BitWords(ReadOnlySpan<uint> words)
    {
        var result = new uint[words.Length * 2];
        
        for (var i = 0; i < words.Length; ++i)
        {
            result[2 * i] = words[i] & 0xFFFF;
            result[2 * i + 1] = words[i] >> 16;
        }
        
        return result;
    }
    
    private static uint[] To32BitWords(ReadOnlySpan<uint> words16)
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
            
            result[i] = (high << 16) | low;
        }

        return result;
    }
}