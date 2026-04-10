using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        var result = new uint[aDigits.Length + bDigits.Length];

        for (var i = 0; i < aDigits.Length; i++)
        {
            ulong carry = 0;

            for (var j = 0; j < bDigits.Length; j++)
            {
                var product = (ulong)aDigits[i] * bDigits[j] + result[i + j] + carry;

                result[i + j] = (uint)product;
                carry = product >> 32;
            }

            result[i + bDigits.Length] = (uint) carry;
        }

        return new BetterBigInteger(result, a.IsNegative ^ b.IsNegative);
    }
}