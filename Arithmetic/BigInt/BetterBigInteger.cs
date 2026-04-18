using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private const int BitsPerWord = 32;
    private const int KaratsubaThresholdWords = 32;
    private const int FftThresholdWords = 256;

    private static readonly IMultiplier SimpleMultiplierStrategy = new SimpleMultiplier();
    private static readonly IMultiplier KaratsubaMultiplierStrategy = new KaratsubaMultiplier();
    private static readonly IMultiplier FftMultiplierStrategy = new FftMultiplier();

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
        : this([..digits], isNegative) {}

    public BetterBigInteger(string value, int radix)
    {
        if (radix is < 2 or > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36");
        }

        ArgumentNullException.ThrowIfNull(value);
        
        var trimmed = value.AsSpan().Trim();

        if (trimmed.IsEmpty)
        {
            throw new FormatException("Input string was empty.");
        }

        var start = 0;
        var isNegative = false;

        if (trimmed[0] is ('+' or '-'))
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

        for (var i = start; i < trimmed.Length; ++i)
        {
            var digit = CharToDigit(trimmed[i]);
        
            if (digit >= radix)
            {
                throw new FormatException($"Invalid digit '{trimmed[i]}' for radix {radix}.");
            }

            result = result * radixBig + new BetterBigInteger([digit]);
        }

        _smallValue = result._smallValue;
        _data = result._data; 
        _signBit = isNegative && !IsZero(result.GetDigits()) ? 1 : 0;
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
        var maxWords = Math.Max(a.GetDigits().Length, b.GetDigits().Length);

        var strategy = maxWords switch
        {
            >= FftThresholdWords => FftMultiplierStrategy,
            >= KaratsubaThresholdWords => KaratsubaMultiplierStrategy,
            _ => SimpleMultiplierStrategy
        };

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

        var wordShift = shift / BitsPerWord;
        var bitShift = shift % BitsPerWord;
        var hasRemainder = wordShift >= aDigits.Length;
        
        for (var i = 0; !hasRemainder && i < wordShift; ++i)
        {
            hasRemainder = aDigits[i] != 0;
        }
        
        if (!hasRemainder && bitShift != 0)
        {
            hasRemainder = (aDigits[wordShift] & ((1u << bitShift) - 1)) != 0;
        }
        
        var quotient = ShiftRightAbs(aDigits, shift);

        if (hasRemainder)
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
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36");
        }

        var digits = GetDigits();
        
        if (IsZero(digits))
        {
            return "0";
        }

        var current = new BetterBigInteger(digits.ToArray());
        var rBig = new BetterBigInteger([(uint) radix]);
    
        var result = new List<char>();

        while (!IsZero(current.GetDigits()))
        {
            var (quotient, remainder) = DivMod(current, rBig);
        
            current = quotient;

            var digit = remainder.GetDigits()[0];
            
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
            
            if (diff < 0)
            {
                diff &= ~(1 << 31);
                // diff += 0x100000000L; // 2^32
                
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

        switch (CompareAbs(dividend, divisor))
        {
            case < 0:
                return ([], dividend.ToArray());
            case 0:
                return ([1], []);
        }

        var quotient = new uint[dividend.Length];
        uint[] remainder = [];

        for (var index = dividend.Length - 1; index >= 0; --index)
        {
            var word = dividend[index];

            for (var bit = 31; bit >= 0; --bit)
            {
                remainder = ShiftLeftAbs(remainder, 1); 

                if (((word >> bit) & 1) != 0)
                {
                    remainder[0] |= 1; 
                }
                
                remainder = TrimLeadingZeros(remainder);

                if (CompareAbs(remainder, divisor) < 0)
                {
                    continue;
                }

                remainder = TrimLeadingZeros(SubAbs(remainder, divisor));
                quotient[index] |= 1u << bit;
            }
        }

        return (quotient, remainder);
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
    
    private static uint[] ShiftLeftAbs(ReadOnlySpan<uint> digits, int shift)
    {
        var wordShift = shift / BitsPerWord;
        var bitShift = shift % BitsPerWord;
        
        var result = new uint[digits.Length + wordShift + 1];

        ulong carry = 0;

        for (var index = 0; index < digits.Length; ++index)
        {
            var value = ((ulong) digits[index] << bitShift) | carry;
            
            result[index + wordShift] = (uint) value;
            
            carry = value >> BitsPerWord;
        }

        result[digits.Length + wordShift] = (uint)carry;

        return result;
    }

    private static uint[] ShiftRightAbs(ReadOnlySpan<uint> digits, int shift)
    {
        var wordShift = shift / BitsPerWord;
        var bitShift = shift % BitsPerWord;

        if (wordShift >= digits.Length)
        {
            return [];
        }

        var result = new uint[digits.Length - wordShift];

        uint carry = 0;

        for (var index = digits.Length - 1; index >= wordShift; --index)
        {
            var current = digits[index];
            
            result[index - wordShift] = (current >> bitShift) | carry;
            
            carry = current << (BitsPerWord - bitShift);
        }

        return result;
    }
    
    private static BetterBigInteger ApplyOnBits(BetterBigInteger a, BetterBigInteger b, Func<uint, uint, uint> func)
    {
        ReadOnlySpan<uint> aDigits = a.GetDigits(), bDigits = b.GetDigits();

        var length = Math.Max(aDigits.Length, bDigits.Length) + 1;

        var aWords = ToTwosComplementWords(aDigits, a.IsNegative, length);
        var bWords = ToTwosComplementWords(bDigits, b.IsNegative, length);
        
        var resultWords = new uint[length];

        for (var i = 0; i < length; i++)
        {
            resultWords[i] = func(aWords[i], bWords[i]);
        }

        return FromTwosComplement(resultWords);
    }

    private static uint[] ToTwosComplementWords(ReadOnlySpan<uint> magnitude, bool isNegative, int length)
    {
        var words = new uint[length];

        if (!isNegative)
        {
            magnitude.CopyTo(words);

            return words;
        }

        ulong carry = 1;
        
        for (var i = 0; i < length; ++i)
        {
            var src = i < magnitude.Length ? magnitude[i] : 0u;
            var value = ~src + carry;
            
            words[i] = (uint) value;
            carry = value >> BitsPerWord;
        }

        return words;
    }

    private static BetterBigInteger FromTwosComplement(ReadOnlySpan<uint> words)
    {
        if (words.IsEmpty)
        {
            return new BetterBigInteger([]);
        }

        var isNegative = (words[^1] >> 31) == 1;
        
        if (!isNegative)
        {
            return new BetterBigInteger(words.ToArray());
        }

        var magnitude = new uint[words.Length];
        ulong carry = 1;
        
        for (var i = 0; i < words.Length; ++i)
        {
            var value = ~words[i] + carry;
            
            magnitude[i] = (uint) value;
            
            carry = value >> BitsPerWord;
        }

        return new BetterBigInteger(magnitude, true);
    }
    
    private static uint[] TrimLeadingZeros(ReadOnlySpan<uint> digits)
    {
        var length = digits.Length;

        for (; length > 0 && digits[length - 1] == 0; --length) {}

        if (length == digits.Length)
        {
            return digits.ToArray();
        }
        
        return digits[..length].ToArray();
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