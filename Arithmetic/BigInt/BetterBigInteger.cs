using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private int _signBit;

    private uint _smallValue; // Если число маленькое, храним его прямо в этом поле, а _data == null.
    private uint[]? _data;

    public bool IsNegative => _signBit == 1;
    private bool IsSmall => _data == null;

    /// От массива цифр (little endian)
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        throw new NotImplementedException("Implement storage logic with Small Integer Optimization");
    }

    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        throw new NotImplementedException();
    }

    public BetterBigInteger(string value, int radix)
    {
        throw new NotImplementedException();
    }

    public ReadOnlySpan<uint> GetDigits()
    {
        return _data ?? [_smallValue];
    }

    public int CompareTo(IBigInteger? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (IsNegative != other.IsNegative)
        {
            return IsNegative ? -1 : 1;
        }

        return (IsNegative ? -1 : 1) * CompareAbs(this, other as BetterBigInteger);
    }

    public bool Equals(IBigInteger? other)
    {
        return other is not null && IsNegative == other.IsNegative && EqualsAbs(this, other as BetterBigInteger);
    }
    
    public override bool Equals(object? obj) => obj is IBigInteger other && Equals(other);
    
    public override int GetHashCode() => throw new NotImplementedException();
    
    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        if (a.IsNegative == b.IsNegative)
        {
            return AddAbs(a, b);
        }
        
        var cmp = CompareAbs(a, b);
        
        if (cmp == 0)
        {
            return new BetterBigInteger(Array.Empty<uint>());
        }

        var big = cmp > 0 ? a : b;
        var small = cmp > 0 ? b : a;

        return SubAbs(big, small);
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
    {
        return a + -b;
    }

    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        return new BetterBigInteger(a.GetDigits().ToArray(), !a.IsNegative);
    }

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b) =>
        throw new NotImplementedException();

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b) =>
        throw new NotImplementedException();


    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
        => throw new NotImplementedException(
            "Умножение делегируется стратегии, выбирать необходимо в зависимости от размеров чисел");

    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        return ApplyOnBits(a, first => ~first);
    }

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        return ApplyOnBits(a, b, (first, second) => first & second);
    }

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        return ApplyOnBits(a, b, (first, second) => first | second);
    }

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        return ApplyOnBits(a, b, (first, second) => first ^ second);
    }

    private static BetterBigInteger ApplyOnBits(BetterBigInteger a, Func<uint, uint> func)
    {
        ReadOnlySpan<uint> aDigits = a.GetDigits();
        
        var length = aDigits.Length;
        var digits = new uint[length];

        for (var i = 0; i < length; i++)
        {
            digits[i] = func(aDigits[i]);
        }

        return new BetterBigInteger(digits, a.IsNegative);
    }
    
    private static BetterBigInteger ApplyOnBits(BetterBigInteger a, BetterBigInteger b, Func<uint, uint, uint> func)
    {
        ReadOnlySpan<uint> aDigits = a.GetDigits(), bDigits = b.GetDigits();
        
        var length = Math.Max(aDigits.Length, bDigits.Length);
        var digits = new uint[length];

        for (var i = 0; i < length; i++)
        {
            var aDigit = i < aDigits.Length ? aDigits[i] : 0;
            var bDigit = i < bDigits.Length ? bDigits[i] : 0;
            
            digits[i] = func(aDigit, bDigit);
        }

        return new BetterBigInteger(digits, a.IsNegative && b.IsNegative);
    }
    
    public static BetterBigInteger operator <<(BetterBigInteger a, int shift) => throw new NotImplementedException();
    public static BetterBigInteger operator >> (BetterBigInteger a, int shift) => throw new NotImplementedException();

    public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => Equals(a, b);
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !Equals(a, b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;

    public override string ToString() => ToString(10);
    public string ToString(int radix) => throw new NotImplementedException();

    private static BetterBigInteger AddAbs(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        
        var length = Math.Max(aDigits.Length, bDigits.Length);
        var result = new uint[length + 1];

        uint carry = 0;
        
        for (var index = 0; index < length; ++index)
        {
            var aDigit = index < aDigits.Length ? aDigits[index] : 0;
            var bDigit = index < bDigits.Length ? bDigits[index] : 0;

            var sum = aDigit + bDigit + carry;
            result[index] = sum % uint.MaxValue;
            carry = sum / uint.MaxValue;
        }

        result[length] = carry;
        
        return new BetterBigInteger(result, a.IsNegative);
    }
    
    // Предполагается, что |a| >= |b|
    private static BetterBigInteger SubAbs(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        
        var length = Math.Max(aDigits.Length, bDigits.Length);
        var result = new uint[length];
        
        uint carry = 0;

        for (var index = 0; index < length; ++index)
        {
            var aDigit = index < aDigits.Length ? aDigits[index] : 0;
            var bDigit = index < bDigits.Length ? bDigits[index] : 0;
            
            var sum = aDigit - bDigit - carry;
            
            result[index] = sum % uint.MaxValue;
            carry = sum / uint.MaxValue;
        }
        
        result[length] = carry;
        
        return new BetterBigInteger(result, a.IsNegative);
    }

    private static bool EqualsAbs(BetterBigInteger a, BetterBigInteger b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        if (aDigits.Length != bDigits.Length)
        {
            return false;
        }

        for (var index = 0; index < aDigits.Length; ++index)
        {
            if (aDigits[index] != bDigits[index])
            {
                return false;
            }
        }

        return true;
    }
    
    private static int CompareAbs(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        if (aDigits.Length != bDigits.Length)
        {
            return aDigits.Length.CompareTo(bDigits.Length);
        }

        for (int index = aDigits.Length - 1; index >= 0; --index)
        {
            if (aDigits[index] != bDigits[index])
            {
                return aDigits[index].CompareTo(bDigits[index]);
            }
        }

        return 0;
    }

    private static void Normalize(BetterBigInteger a)
    {
        // delete leading zeros
    }
    
    private static void SmallIntegerOptimization(BetterBigInteger a)
    {
        // small integer optimization (SIO): if _data can be stored in _smallValue => store it!
    }
}