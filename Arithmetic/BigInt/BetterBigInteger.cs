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
        ArgumentNullException.ThrowIfNull(digits);

        _data = digits.ToArray();
        _smallValue = 0;
        _signBit = isNegative ? 1 : 0;

        SmallIntegerOptimization(this);
    }

    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
        : this(digits is null ? throw new ArgumentNullException(nameof(digits)) : [.. digits], isNegative)
    {
    }

    public BetterBigInteger(string value, int radix)
    {
        if (radix is < 2 or > 36)
        {
            throw new ArgumentException("Radix must be between 2 and 36");
        }

        ArgumentNullException.ThrowIfNull(value);
        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            throw new FormatException("Input string was empty.");
        }

        var start = 0;
        var isNegative = false;

        if (trimmed[0] is '+' or '-')
        {
            isNegative = trimmed[0] == '-';
            start = 1;

            if (trimmed.Length == 1)
            {
                throw new FormatException("Input string does not contain digits.");
            }
        }

        var result = new BetterBigInteger([]);
        var radixBig = new BetterBigInteger([(uint)radix]);

        for (var i = start; i < trimmed.Length; i++)
        {
            var digit = CharToDigit(trimmed[i]);
            if (digit < 0 || digit >= radix)
            {
                throw new FormatException($"Invalid digit '{trimmed[i]}' for radix {radix}.");
            }

            result = result * radixBig + new BetterBigInteger([(uint)digit]);
        }

        _smallValue = result._smallValue;
        _data = result._data?.ToArray();
        var resultDigits = result.GetDigits();
        _signBit = isNegative && !IsZero(resultDigits) ? 1 : 0;
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

        return (IsNegative ? -1 : 1) * CompareAbs(GetDigits(), other.GetDigits());
    }

    public bool Equals(IBigInteger? other)
        => other is BetterBigInteger bigInteger && IsNegative == other.IsNegative && EqualsAbs(this, bigInteger);
    
    public override bool Equals(object? obj)
        => obj is IBigInteger other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        
        hash.Add(_signBit);

        foreach (var digit in GetDigits())
        {
            hash.Add(digit);
        }
        
        return hash.ToHashCode();
    }
    
    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        
        if (a.IsNegative == b.IsNegative)
        {
            return new BetterBigInteger(AddAbs(aDigits, bDigits), a.IsNegative);
        }
        
        var cmp = CompareAbs(aDigits, bDigits);
        
        if (cmp == 0)
        {
            return new BetterBigInteger([]);
        }

        return cmp > 0 
            ? new BetterBigInteger(SubAbs(aDigits, bDigits), a.IsNegative)
            : new BetterBigInteger(SubAbs(bDigits, aDigits), b.IsNegative);
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
        => a + (-b);

    public static BetterBigInteger operator -(BetterBigInteger a)
        => new(a.GetDigits().ToArray(), !a.IsNegative);

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
        => DivMod(a, b).Quotient;

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
        => DivMod(a, b).Remainder;

    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        IMultiplier strategy = new SimpleMultiplier();
        
        return strategy.Multiply(a, b);
    }

    public static BetterBigInteger operator ~(BetterBigInteger a)
        => ApplyOnBits(a, a, (first, _) => ~first);

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
        => ApplyOnBits(a, b, (first, second) => first & second);

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
        => ApplyOnBits(a, b, (first, second) => first | second);

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
        => ApplyOnBits(a, b, (first, second) => first ^ second);
    
    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        var aDigits = a.GetDigits();

        if (shift < 0)
        {
            return a >> -shift;
        }

        if (shift == 0 || IsZero(aDigits))
        {
            return new BetterBigInteger(aDigits.ToArray(), a.IsNegative);
        }

        return new BetterBigInteger(ShiftLeftAbs(aDigits, shift), a.IsNegative);
    }
    
    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        var aDigits = a.GetDigits();

        if (shift < 0)
        {
            return a << -shift;
        }

        if (shift == 0 || IsZero(aDigits))
        {
            return new BetterBigInteger(aDigits.ToArray(), a.IsNegative);
        }

        if (!a.IsNegative)
        {
            return new BetterBigInteger(ShiftRightAbs(aDigits, shift));
        }

        var divisorDigits = new uint[shift / 32 + 1];
        divisorDigits[shift / 32] = 1u << (shift % 32);
        var (quotient, remainder) = DivModAbs(aDigits, divisorDigits);

        if (!IsZero(remainder))
        {
            quotient = AddAbs(quotient, [1]);
        }

        return new BetterBigInteger(quotient, true);
    }

    public static bool operator ==(BetterBigInteger a, BetterBigInteger b)
        => Equals(a, b);
    
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b)
        => !Equals(a, b);
    
    public static bool operator <(BetterBigInteger a, BetterBigInteger b)
        => a.CompareTo(b) < 0;
    
    public static bool operator >(BetterBigInteger a, BetterBigInteger b)
        => a.CompareTo(b) > 0;
    
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b)
        => a.CompareTo(b) <= 0;
    
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b)
        => a.CompareTo(b) >= 0;

    public override string ToString()
        => ToString(10);

    public string ToString(int radix)
    {
        if (radix is < 2 or > 36)
        {
            throw new ArgumentException("Radix must be between 2 and 36");
        }

        var digits = GetDigits();
        if (IsZero(digits))
        {
            return "0";
        }

        var current = new BetterBigInteger(digits.ToArray());
        var rBig = new BetterBigInteger([(uint) radix]);
        var result = new List<char>();
        var zero = new BetterBigInteger([]);

        while (current > zero)
        {
            var r = current % rBig;

            current /= rBig;

            var digit = r.GetDigits()[0];

            result.Add(DigitToChar(digit));
        }

        if (IsNegative)
        {
            result.Add('-');
        }

        result.Reverse();

        return new string(result.ToArray());
    }

    private static (BetterBigInteger Quotient, BetterBigInteger Remainder) DivMod(BetterBigInteger dividend, BetterBigInteger divisor)
    {
        var (qDigits, rDigits) = DivModAbs(dividend.GetDigits(), divisor.GetDigits());
        
        return (
            new BetterBigInteger(qDigits, dividend.IsNegative ^ divisor.IsNegative),
            new BetterBigInteger(rDigits, dividend.IsNegative)
        );
    }
    
    private static uint[] AddAbs(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        var length = Math.Max(a.Length, b.Length);
        var result = new uint[length + 1];

        ulong carry = 0;
        
        for (var index = 0; index < length; ++index)
        {
            var aDigit = index < a.Length ? a[index] : 0;
            var bDigit = index < b.Length ? b[index] : 0;

            var sum = (ulong) aDigit + bDigit + carry;
            
            result[index] = (uint) sum;
            carry = sum >> 32;
        }

        result[length] = (uint) carry;
        
        return result;
    }
    
    // Предполагается, что |a| >= |b|
    private static uint[] SubAbs(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        var length = Math.Max(a.Length, b.Length);
        var result = new uint[length];
        
        long borrow = 0;
        
        for (var index = 0; index < length; ++index)
        {
            var aDigit = index < a.Length ? a[index] : 0;
            var bDigit = index < b.Length ? b[index] : 0;
            
            var diff = aDigit - bDigit - borrow;
            
            if (diff < 0) {
                diff += 0x100000000L;
                
                borrow = 1;
            } else {
                borrow = 0;
            }
            
            result[index] = (uint) diff;
        }

        return result;
    }

    private static (uint[] Quotient, uint[] Remainder) DivModAbs(ReadOnlySpan<uint> dividend, ReadOnlySpan<uint> divisor)
    {
        if (IsZero(divisor))
        {
            throw new DivideByZeroException();
        }

        int cmp = CompareAbs(dividend, divisor);
        if (cmp < 0) return ([], dividend.ToArray());
        if (cmp == 0) return ([1],[]);

        var q = new uint[dividend.Length];
        uint[] r =[];

        for (int i = dividend.Length - 1; i >= 0; i--)
        {
            uint word = dividend[i];
            for (int bit = 31; bit >= 0; bit--)
            {
                // Эффективный сдвиг остатка и добавление нового бита без аллокаций BigInteger
                uint bitVal = (word >> bit) & 1;
                r = ShiftLeftAbs(r, 1);
                if (bitVal != 0)
                {
                    r = AddAbs(r, [bitVal]);
                }
                r = TrimLeadingZeros(r);

                if (CompareAbs(r, divisor) >= 0)
                {
                    r = SubAbs(r, divisor); // SubAbs ожидает |r| >= |divisor|, что здесь выполняется
                    r = TrimLeadingZeros(r);
                    
                    q[i] |= (1u << bit);
                }
            }
        }
        
        return (q, r);
    }
    
    private static uint[] TrimLeadingZeros(ReadOnlySpan<uint> digits)
    {
        var len = digits.Length;
        while (len > 0 && digits[len - 1] == 0)
        {
            len--;
        }

        return len == digits.Length ? digits.ToArray() : digits[..len].ToArray();
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
    
    private static int CompareAbs(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        if (a.Length != b.Length)
        {
            return a.Length.CompareTo(b.Length);
        }

        for (var index = a.Length - 1; index >= 0; --index)
        {
            if (a[index] != b[index])
            {
                return a[index].CompareTo(b[index]);
            }
        }

        return 0;
    }
    
    private static BetterBigInteger ApplyOnBits(BetterBigInteger a, BetterBigInteger b, Func<uint, uint, uint> func)
    {
        ReadOnlySpan<uint> aDigits = a.GetDigits(), bDigits = b.GetDigits();

        // +1 word keeps the sign-extension boundary for two's-complement operations.
        var length = Math.Max(aDigits.Length, bDigits.Length) + 1;
        var aWords = ToTwosComplementWords(aDigits, a.IsNegative, length);
        var bWords = ToTwosComplementWords(bDigits, b.IsNegative, length);
        var digits = new uint[length];

        for (var i = 0; i < length; i++)
        {
            digits[i] = func(aWords[i], bWords[i]);
        }

        return FromTwosComplement(digits);
    }

    private static uint[] ToTwosComplementWords(ReadOnlySpan<uint> magnitude, bool isNegative, int length)
    {
        var words = new uint[length];

        if (!isNegative)
        {
            for (var i = 0; i < magnitude.Length && i < length; i++)
            {
                words[i] = magnitude[i];
            }

            return words;
        }

        ulong carry = 1;
        for (var i = 0; i < length; i++)
        {
            var src = i < magnitude.Length ? magnitude[i] : 0u;
            var value = (ulong)~src + carry;
            words[i] = (uint)value;
            carry = value >> 32;
        }

        return words;
    }

    private static BetterBigInteger FromTwosComplement(ReadOnlySpan<uint> words)
    {
        if (words.IsEmpty)
        {
            return new BetterBigInteger([]);
        }

        var isNegative = (words[^1] & 0x80000000u) != 0;
        if (!isNegative)
        {
            return new BetterBigInteger(TrimLeadingZeros(words));
        }

        var magnitude = new uint[words.Length];
        ulong carry = 1;
        for (var i = 0; i < words.Length; i++)
        {
            var value = (ulong)~words[i] + carry;
            magnitude[i] = (uint)value;
            carry = value >> 32;
        }

        return new BetterBigInteger(TrimLeadingZeros(magnitude), true);
    }
    
    private static void Normalize(BetterBigInteger a)
    {
        if (a._data == null)
        {
            return;
        }
        
        var last = a._data.Length - 1;
        for (; last >= 0 && a._data[last] == 0; --last) {}
        
        if (last < 0) {
            a._data = null;
            a._smallValue = 0;
            a._signBit = 0;
        } else if (last < a._data.Length - 1) {
            a._data = a._data.AsSpan(0, last + 1).ToArray();
        }
    }
    
    private static void SmallIntegerOptimization(BetterBigInteger a)
    {
        Normalize(a);

        if (a._data is not { Length: 1 })
        {
            return;
        }
        
        a._smallValue = a._data[0];
        a._data = null;
    }

    private static uint[] ShiftLeftAbs(ReadOnlySpan<uint> digits, int shift)
    {
        var wordShift = shift / 32;
        var bitShift = shift % 32;
        var result = new uint[digits.Length + wordShift + 1];

        if (bitShift == 0)
        {
            for (var i = 0; i < digits.Length; i++)
            {
                result[i + wordShift] = digits[i];
            }

            return result;
        }

        ulong carry = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var value = ((ulong)digits[i] << bitShift) | carry;
            result[i + wordShift] = (uint)value;
            carry = value >> 32;
        }

        result[digits.Length + wordShift] = (uint)carry;
        return result;
    }

    private static uint[] ShiftRightAbs(ReadOnlySpan<uint> digits, int shift)
    {
        var wordShift = shift / 32;
        var bitShift = shift % 32;

        if (wordShift >= digits.Length)
        {
            return [];
        }

        var result = new uint[digits.Length - wordShift];

        if (bitShift == 0)
        {
            for (var i = wordShift; i < digits.Length; i++)
            {
                result[i - wordShift] = digits[i];
            }

            return result;
        }

        uint carry = 0;
        for (var i = digits.Length - 1; i >= wordShift; i--)
        {
            var current = digits[i];
            result[i - wordShift] = (current >> bitShift) | carry;
            carry = current << (32 - bitShift);
        }

        return result;
    }

    private static uint CharToDigit(char symbol)
    {
        return symbol switch
        {
            >= '0' and <= '9' => (uint) (symbol - '0'),
            >= 'A' and <= 'Z' => (uint) (symbol - 'A' + 10),
            >= 'a' and <= 'z' => (uint) (symbol - 'a' + 10),
            _ => throw new ArgumentException($"Invalid symbol value: {symbol}")
        };
    }

    private static char DigitToChar(uint digit)
    {
        return digit switch
        {
            >= 0 and <= 9 => (char) ('0' + digit),
            >= 10 and <= 35 => (char) ('A' + digit - 10),
            _ => throw new ArgumentException($"Invalid digit value: {digit}")
        };
    }

    private static bool IsZero(ReadOnlySpan<uint> digits)
    {
        foreach (var digit in digits)
        {
            if (digit != 0)
            {
                return false;
            }
        }

        return true;
    }

}