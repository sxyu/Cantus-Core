using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using Cantus.Core.Exceptions;
namespace Cantus.Core.CommonTypes
{
    /// <summary>
    /// Arbitrary precision decimal.
    /// All operations are exact, except for division. Division never determines more digits than the given precision.
    /// Based on http://stackoverflow.com/a/4524254
    /// Author: Jan Christoph Bernack (contact: jc.bernack at googlemail.com)
    /// (Slightly modified for the evaluator)
    /// </summary>
    public struct BigDecimal : IComparable, IComparable<BigDecimal>, IEquatable<BigDecimal>, IConvertible
    {
        /// <summary>
        /// Specifies whether the significant digits should be truncated to the given precision after each operation.
        /// </summary>

        public static bool AlwaysTruncate = false;
        /// <summary>
        /// Sets the maximum precision of division operations.
        /// If AlwaysTruncate is set to true all operations are affected.
        /// </summary>

        public const int PRECISION = 50;
        /// <summary>
        /// Sets the maximum order of magnitude at which we display the full value of the BigDecimal
        /// </summary>

        public const double MAX_FULL_DISP = 10000000000.0;
        /// <summary>
        /// Sets the minimum order of magnitude at which we display the full value of the BigDecimal
        /// </summary>

        public const double MIN_FULL_DISP = 1E-09;
        
        /// <summary>
        /// A BigDecimal representing the undefined (NaN) value
        /// </summary>
        /// <returns></returns>
        public static BigDecimal Undefined {
            get
            {
                return new BigDecimal(0, 0, undefined: true);
            }
        }

        /// <summary>
        /// If true, this bigdecimal represents an undefined value
        /// </summary>
        /// <returns></returns>
        public bool IsUndefined { get; }

        /// <summary>
        /// The number of sig figs to preserve after operations. Integer.MaxValue if not to be used
        /// </summary>
        /// <returns></returns>
        public int SigFigs { get; set; }

        /// <summary>
        /// Represents a type of operator
        /// </summary>
        public enum OperatorType
        {
            /// <summary>
            /// Set when no operator has been applied or the last operator was neither an add/sub nor a mul/div operation
            /// </summary>
            none = 0,
            /// <summary>
            /// Represents an addition or subtraction operation
            /// </summary>
            addsub = 1,
            /// <summary>
            /// Represents a multiplication or division operation
            /// </summary>
            muldiv = 2
        }

        /// <summary>
        /// The type of the last operation performed on this BigDecimal
        /// </summary>
        public OperatorType LastOperation { get; set; }

        /// <summary>
        /// Decimal separator
        /// </summary>
        private static string DecimalSep { get; } = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        /// <summary>
        /// Get the index of the lowest sig fig in the number. 
        /// </summary>
        /// <returns></returns>
        public int LeastSigFig
        {
            get
            {
                if (IsUndefined)
                    return 0;
                if (SigFigs == int.MaxValue)
                    return int.MinValue;
                return HighestDigit() - SigFigs + 1;
            }
        }

        public BigInteger Mantissa
        {
            get
            {
                if (IsUndefined)
                    return 0;
                return m_Mantissa;
            }
            set { m_Mantissa = value; }
        }

        private BigInteger m_Mantissa;
        public int Exponent
        {
            get { return m_Exponent; }
            set { m_Exponent = value; }
        }


        private int m_Exponent;
        public BigDecimal(BigInteger mantissa, int exponent, bool undefined = false, int sigFigs = int.MaxValue) : this()
        {
            this.Mantissa = mantissa;
            this.Exponent = exponent;
            this.IsUndefined = undefined;
            this.SigFigs = sigFigs;

            Normalize();
            if (AlwaysTruncate)
            {
                Truncate();
            }

            this.LastOperation = OperatorType.none;
        }

        public BigDecimal(double value, int sigFigs = int.MaxValue) : this()
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                this.IsUndefined = true;
                return;
            }

            BigInteger mantissa = (BigInteger)value;
            int exponent = 0;
            double scaleFactor = 1;

            while (Math.Abs(value * scaleFactor - (double)(mantissa)) > 0)
            {
                exponent -= 1;
                scaleFactor *= 10;
                mantissa = (BigInteger)(value * scaleFactor);
            }

            this.Mantissa = mantissa;
            this.Exponent = exponent;
            this.SigFigs = sigFigs;
        }

        /// <summary>
        /// Removes trailing zeros on the mantissa
        /// </summary>
        public void Normalize()
        {
            if (Mantissa.IsZero)
            {
                Exponent = 0;
            }
            else
            {
                BigInteger remainder = 0;
                while (remainder == 0)
                {
                    BigInteger shortened = BigInteger.DivRem(Mantissa, 10, out remainder);
                    if (remainder == 0)
                    {
                        Mantissa = shortened;
                        Exponent += 1;
                    }
                }
            }
        }

        /// <summary>
        /// Truncate the number to the given precision by removing the least significant digits.
        /// </summary>
        /// <returns>The truncated number</returns>
        /// <param name="noRound">If true, rounds the BigDecimal to the nearest even number instead of truncating it.</param>
        public BigDecimal Truncate(int precision, bool round = true)
        {
            if (IsUndefined || precision <= 0 || precision == int.MaxValue) return this;

            // copy this instance (remember its a struct)
            BigDecimal shortened = this;
            // save some time because the number of digits is not needed to remove trailing zeros
            shortened.Normalize();

            // remove the least significant digits, as long as the number of digits is higher than the given Precision
            int noD = NumberOfDigits(shortened.Mantissa);
            int expo = 0;
            while (noD > precision)
            {
                if (noD - 1 == precision)
                {
                    int mod1 = (int)(BigInteger.Abs(shortened.Mantissa) % 10);
                    if (round)
                    {
                        if (mod1 > 5)
                        {
                            shortened.Mantissa += shortened.Mantissa.Sign * 10;
                        }
                        else if (mod1 == 5)
                        {
                            // decide if to round up or down
                            if (BigInteger.Abs(this.Mantissa) % BigInteger.Pow(10, expo) > 0 || (int)(BigInteger.Abs(shortened.Mantissa) / 10 % 2) == 1)
                            {
                                shortened.Mantissa += shortened.Mantissa.Sign * 10;
                            }
                        }
                    }
                }
                shortened.Mantissa /= 10;
                expo += 1;
                noD = NumberOfDigits(shortened.Mantissa);
            }
            shortened.Exponent += expo;

            // normalize again to make sure there are no trailing zeros left
            shortened.Normalize();
            return shortened;
        }

        public BigDecimal Truncate()
        {
            if (IsUndefined)
                return this;
            return Truncate(PRECISION);
        }

        /// <summary>
        /// Truncate this BigDecimal to the adjacent integer nearest zero
        /// </summary>
        public BigDecimal TruncateInt()
        {
            if (this.HighestDigit() < 0) return 0;
            return Truncate(HighestDigit()+1, false);
        }

        /// <summary>
        /// Round this BigDecimal to the nearest even integer
        /// </summary>
        public BigDecimal Round()
        {
            if (this.HighestDigit() < 0)
            {
                if (this > 0.5) return 1;
                else if (this < -0.5) return -1;
                else return 0;
            }
            return Truncate(HighestDigit()+1);
        }

        private static int NumberOfDigits(BigInteger value)
        {
            if (value == 0)
                return 0;
            // deal with zero (prevent Log(0))
            // do not count the sign
            // faster version
            return (int)(Math.Ceiling(BigInteger.Log10(value * value.Sign)));
        }

        /// <summary>
        /// Get the number of digits in this BigDecimal
        /// </summary>
        /// <returns></returns>
        public int Digits
        {
            get { return NumberOfDigits(this.Mantissa); }
        }

        #region "Conversions"

        public static implicit operator BigDecimal(int value)
        {
            return new BigDecimal(value, exponent: 0);
        }

        public static implicit operator BigDecimal(double value)
        {
            return new BigDecimal(value);
        }

        public static implicit operator BigDecimal(decimal value)
        {
            BigInteger mantissa = (BigInteger)value;
            int exponent = 0;
            decimal scaleFactor = 1;
            while ((decimal)(mantissa) != value * scaleFactor)
            {
                exponent -= 1;
                scaleFactor *= 10;
                mantissa = (BigInteger)(value * scaleFactor);
            }
            return new BigDecimal(mantissa, exponent);
        }

        public static explicit operator double (BigDecimal value)
        {
            if (value.IsUndefined)
                return double.NaN;
            return (double)(value.Mantissa) * Math.Pow(10, value.Exponent);
        }

        public static explicit operator float (BigDecimal value)
        {
            if (value.IsUndefined)
                return float.NaN;
            return (Single)((double)(value));
        }

        public static explicit operator decimal (BigDecimal value)
        {
            if (value.IsUndefined)
                return 0;
            return (decimal)(value.Mantissa) * (decimal)(Math.Pow(10, value.Exponent));
        }

        public static explicit operator int (BigDecimal value)
        {
            if (value.IsUndefined)
                return 0;
            return (int)(value.Mantissa * BigInteger.Pow(10, value.Exponent));
        }

        public static explicit operator uint (BigDecimal value)
        {
            if (value.IsUndefined)
                return 0;
            return (uint)(value.Mantissa * BigInteger.Pow(10, value.Exponent));
        }

        public static explicit operator long (BigDecimal value)
        {
            if (value.IsUndefined)
                return 0;
            return (long)((double)(value.Mantissa) * Math.Pow(10, value.Exponent));
        }

        public static explicit operator ulong (BigDecimal value)
        {
            if (value.IsUndefined)
                return 0;
            return (ulong)(value.Mantissa * BigInteger.Pow(10, value.Exponent));
        }
        #endregion

        #region "Operators"

        public static BigDecimal operator +(BigDecimal value)
        {
            return value;
        }

        public static BigDecimal operator -(BigDecimal value)
        {
            value.Mantissa *= -1;
            return value;
        }

        public static BigDecimal operator +(BigDecimal left, BigDecimal right)
        {
            return Add(left, right);
        }

        public static BigDecimal operator -(BigDecimal left, BigDecimal right)
        {
            return Add(left, -right);
        }

        private static BigDecimal Add(BigDecimal left, BigDecimal right)
        {
            if (left.IsUndefined) return Undefined;
            if (right.IsUndefined) return Undefined;

            if (left.LastOperation == OperatorType.muldiv) left.Truncate(left.SigFigs);
            if (right.LastOperation == OperatorType.muldiv) right.Truncate(right.SigFigs);

            int digit = Math.Max(left.LeastSigFig, right.LeastSigFig);

            BigDecimal bn = left.Exponent > right.Exponent ?
                new BigDecimal(AlignExponent(left, right) + right.Mantissa, right.Exponent) :
                new BigDecimal(AlignExponent(right, left) + left.Mantissa, left.Exponent);

            if (left.SigFigs == int.MaxValue || right.SigFigs == int.MaxValue)
            {
                bn.SigFigs = int.MaxValue;
                return bn;
            }
            else
            {
                bn.SigFigs = bn.HighestDigit() - digit + 1;
                bn.LastOperation = OperatorType.addsub;
                return bn;
            }
        }

        public static BigDecimal operator *(BigDecimal left, BigDecimal right)
        {
            if (left.IsUndefined) return Undefined;
            if (right.IsUndefined) return Undefined;

            if (left.LastOperation == OperatorType.addsub) left.Truncate(left.SigFigs);
            if (right.LastOperation == OperatorType.addsub) right.Truncate(right.SigFigs);

            BigDecimal prod = new BigDecimal(left.Mantissa * right.Mantissa, left.Exponent + right.Exponent, false, Math.Min(left.SigFigs, right.SigFigs));
            prod.LastOperation = OperatorType.muldiv;
            return prod;
        }

        public static BigDecimal operator /(BigDecimal dividend, BigDecimal divisor)
        {
            if (dividend.IsUndefined) return Undefined;
            if (divisor.IsUndefined) return Undefined;

            if (dividend.LastOperation == OperatorType.addsub) dividend.Truncate(dividend.SigFigs);
            if (divisor.LastOperation == OperatorType.addsub) divisor.Truncate(divisor.SigFigs);

            if (divisor == 0) 
                throw new MathException("Division by Zero");

            try
            {
                int delta = PRECISION - (NumberOfDigits(dividend.Mantissa) - NumberOfDigits(divisor.Mantissa));
                if (delta < 0)
                {
                    delta = 0;
                }
                dividend.Mantissa *= BigInteger.Pow(10, delta);
                BigDecimal quotient = new BigDecimal(dividend.Mantissa / divisor.Mantissa,
                    dividend.Exponent - divisor.Exponent - delta, false, Math.Min(dividend.SigFigs, divisor.SigFigs));
                quotient.LastOperation = OperatorType.muldiv;
                return quotient;
            }
            catch (ArithmeticException)
            {
                throw new Exceptions.MathException("Division by Zero");
            }
        }

        public static BigDecimal operator %(BigDecimal left, BigDecimal right)
        {
            if (left.IsUndefined) return Undefined;
            if (right.IsUndefined) return Undefined;

            if (left.LastOperation == OperatorType.addsub) left.Truncate(left.SigFigs);
            if (right.LastOperation == OperatorType.addsub) right.Truncate(right.SigFigs);

            int leftSF = left.SigFigs;
            int rightSF = right.SigFigs;

            left.SigFigs = right.SigFigs = int.MaxValue;
            BigDecimal quot =  left / right;
            quot = quot.TruncateInt();

            BigDecimal result =  left - quot * right;
            
            left.SigFigs = leftSF;
            right.SigFigs = rightSF;

            result.SigFigs = Math.Min(leftSF, rightSF);
            result.LastOperation = OperatorType.muldiv;
            return result;
        }

        public static bool operator ==(BigDecimal left, BigDecimal right)
        {
            return left.Exponent == right.Exponent && left.Mantissa == right.Mantissa;
        }

        public static bool operator !=(BigDecimal left, BigDecimal right)
        {
            return left.Exponent != right.Exponent || left.Mantissa != right.Mantissa;
        }

        public static bool operator <(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent ? AlignExponent(left, right) < right.Mantissa : left.Mantissa < AlignExponent(right, left);
        }

        public static bool operator >(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent ? AlignExponent(left, right) > right.Mantissa : left.Mantissa > AlignExponent(right, left);
        }

        public static bool operator <=(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent ? AlignExponent(left, right) <= right.Mantissa : left.Mantissa <= AlignExponent(right, left);
        }

        public static bool operator >=(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent ? AlignExponent(left, right) >= right.Mantissa : left.Mantissa >= AlignExponent(right, left);
        }

        /// <summary>
        /// Returns the mantissa of value, aligned to the exponent of reference.
        /// Assumes the exponent of value is larger than of reference.
        /// </summary>
        private static BigInteger AlignExponent(BigDecimal value, BigDecimal reference)
        {
            return value.Mantissa * BigInteger.Pow(10, value.Exponent - reference.Exponent);
        }

        /// <summary>
        /// Get the base 10 logarithm of one on the highest digit of a bigdecimal
        /// </summary>
        public int HighestDigit()
        {
            if (Mantissa == 0)
                return 0;
            return Exponent + (int)(Math.Floor(BigInteger.Log10(BigInteger.Abs(Mantissa))));
        }
        #endregion

        #region "Additional mathematical functions"

        public static BigDecimal Exp(BigDecimal exponent)
        {
            BigDecimal tmp = (BigDecimal)1;
            while (Abs(exponent) > 100)
            {
                int diff = exponent > 0 ? 100 : -100;
                tmp *= Pow(CantusEvaluator.E, diff);
                exponent -= diff;
            }
            return tmp * Pow(CantusEvaluator.E, exponent);
        }

        /// <summary>
        /// Calculate the basis raised to the exponent. Note that this does not
        /// actually allow bases/exponents above the double precision range.
        /// Significant figures are supported.
        /// </summary>
        public static BigDecimal Pow(BigDecimal basis, BigDecimal exponent)
        {
            BigDecimal tmp = (BigDecimal)1;

            double expo = (double)(exponent);
            double @base = (double)(basis);

            int ct = 0;
            int MAX_CT = 300;
            while (Math.Abs(expo) > 100)
            {
                int diff = exponent > 0 ? 100 : -100;
                tmp *= Math.Pow(@base, diff);
                expo -= diff;
                ++ct;
                if (ct > MAX_CT)
                    throw new MathException("Warning: Exponentiation resulted in too many computations. Terminated to prevent freeze.");
            }

            tmp *= Math.Pow(@base, expo);
            if (basis.SigFigs == int.MaxValue && exponent.SigFigs < int.MaxValue)
            {
                tmp.SigFigs = exponent.SigFigs - exponent.HighestDigit() - 1;
            }
            else
            {
                tmp.SigFigs = basis.SigFigs;
            }

            return tmp;
        }

        public static BigDecimal Abs(BigDecimal value)
        {
            if (value < 0) return -value;
            else return value;
        }
        
        private static BigDecimal mod(BigDecimal x, BigDecimal m)
        {
            return (x % m + m) % m;
        }

        public static BigDecimal Sin(BigDecimal value)
        {
            value = mod(value, CantusEvaluator.PI * 2);
            return Math.Sin((double)value);
        }
        public static BigDecimal Cos(BigDecimal value)
        {
            value = mod(value, CantusEvaluator.PI * 2);
            return Math.Cos((double)value);
        }
        public static BigDecimal Tan(BigDecimal value)
        {
            value = mod(value, CantusEvaluator.PI * 2);
            return Math.Tan((double)value);
        }

        #endregion

        /// <summary>
        /// Returns string containing the full decimal representation of this number
        /// </summary>
        /// <returns></returns>
        public string FullDecimalRepr()
        {
            if (this.IsUndefined) return "Undefined";
            this.Normalize();
            // special case: for 0 nothing needs to be inserted
            if (this.Mantissa == 0)
            {
                string zeroStr = "0";
                if (this.SigFigs > 1 && this.SigFigs < int.MaxValue)
                {
                    zeroStr += DecimalSep;
                    zeroStr += "".PadRight(this.SigFigs - 1, '0');
                }
                return zeroStr;
            }

            bool neg = false;
            StringBuilder str = default(StringBuilder);
            BigDecimal trunc = this.Truncate(SigFigs);

            if (trunc.Mantissa < 0)
            {
                str = new StringBuilder((-trunc.Mantissa).ToString());
                neg = true;
            }
            else
            {
                str = new StringBuilder(trunc.Mantissa.ToString());
            }

            int curlen = 0;
            // we need to add 0's to left
            if (trunc.Exponent < 0 && str.Length + trunc.Exponent <= 0)
            {
                string left = "".PadLeft(-trunc.Exponent - str.Length, '0');
                // generate string of zeros
                str.Insert(0, left);
                str.Insert(0, "0" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                curlen = str.Length - 2;
                // just insert point
            }
            else if (trunc.Exponent < 0)
            {
                str.Insert(str.Length + trunc.Exponent, CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                curlen = str.Length - 1;
                // we need to append 0's to right
            }
            else
            {
                str.Append("".PadLeft(trunc.Exponent, '0'));
                // generate string of zeros
                curlen = str.Length;
                if (curlen < SigFigs && SigFigs < int.MaxValue)
                    str.Append(DecimalSep);
            }

            if (SigFigs < int.MaxValue)
            {
                str.Append("".PadLeft(Math.Max(SigFigs - curlen, 0), '0'));
                if (new CantusEvaluator.InternalFunctions(null).CountSigFigs(str.ToString()) != SigFigs)
                {
                    // we tried, this is unrepresentable. use scientific notation.
                    return ToScientific();
                }
            }

            if (neg)
                str.Insert(0, "-");

            return str.ToString();
        }

        /// <summary>
        /// Convert the BigDecimal to scientific notation
        /// </summary>
        /// <returns></returns>
        public string ToScientific()
        {
            if (this.IsUndefined) return "Undefined";

            this.Normalize();
            if (this.Mantissa == 0)
            {
                string zeroStr = "0";
                if (this.SigFigs > 1 && this.SigFigs < int.MaxValue)
                {
                    zeroStr += DecimalSep;
                    zeroStr += "".PadRight(this.SigFigs - 1, '0');
                }
                return zeroStr;
            }

            BigDecimal tmp = default(BigDecimal);
            if (this.SigFigs == int.MaxValue)
            {
                tmp = this.Truncate();
            }
            else
            {
                tmp = this.Truncate(SigFigs);
            }

            bool neg = false;
            StringBuilder val = default(StringBuilder);
            if (tmp.Mantissa < 0)
            {
                val = new StringBuilder((-tmp.Mantissa).ToString());
                tmp.Mantissa = -tmp.Mantissa;
                neg = true;
            }
            else
            {
                val = new StringBuilder(tmp.Mantissa.ToString());
            }

            if (val.Length > 1)
                val.Insert(1, CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

            int expo = tmp.Exponent + (int)(Math.Floor(BigInteger.Log10(tmp.Mantissa)));

            string valRepr = val.ToString();
            if (SigFigs < int.MaxValue)
            {
                string zeros = "".PadLeft(Math.Max((int)(SigFigs - new CantusEvaluator.InternalFunctions(null).CountSigFigs(valRepr)), 0), '0');
                if (zeros.Length > 0 && !valRepr.Contains(DecimalSep))
                    val.Append(DecimalSep);
                val.Append(zeros);
            }

            return string.Concat(neg ? "-" : "", val.ToString(), " E ", expo);
        }

        public bool IsOutsideDispRange()
        {
            return this > new BigDecimal(MAX_FULL_DISP) || this < new BigDecimal(-MAX_FULL_DISP) || Math.Abs((double)(this)) < MIN_FULL_DISP;
        }

        public override string ToString()
        {
            if (this.IsUndefined) return "Undefined";
            if (this.IsOutsideDispRange())
            {
                return this.ToScientific();
            }
            else
            {
                return this.FullDecimalRepr();
            }
        }

        public bool Equals(BigDecimal other)
        {
            return other.Mantissa.Equals(Mantissa) && other.Exponent == Exponent;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is BigDecimal && Equals((BigDecimal)obj);
        }

        public override int GetHashCode()
        {
            return (Mantissa.GetHashCode() * 397) ^ Exponent;
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(obj, null) || !(obj is BigDecimal))
            {
                throw new ArgumentException();
            }
            return CompareTo((BigDecimal)obj);
        }

        public int CompareTo(BigDecimal other)
        {
            return this < other ? -1 : (this > other ? 1 : 0);
        }

        public TypeCode GetTypeCode()
        {
            return TypeCode.Double;
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            return this.Mantissa != 0;
        }

        public char ToChar(IFormatProvider provider)
        {
            return (char)(int)this;
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            return (sbyte)(int)this;
        }

        public byte ToByte(IFormatProvider provider)
        {
            return (byte)(int)this;
        }

        public short ToInt16(IFormatProvider provider)
        {
            return (short)(int)this;
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            return (ushort)(int)this;
        }

        public int ToInt32(IFormatProvider provider)
        {
            return (int)this;
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            return (uint)this;
        }

        public long ToInt64(IFormatProvider provider)
        {
            return (long)this;
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            return (ulong)this;
        }

        public float ToSingle(IFormatProvider provider)
        {
            return (float)this;
        }

        public double ToDouble(IFormatProvider provider)
        {
            return (double)this;
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            return (decimal)this;
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            return DateTime.MinValue;
        }

        public string ToString(IFormatProvider provider)
        {
            return this.ToString();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            return Convert.ChangeType(this, conversionType);
        }
    }

    ///// <summary>
    ///// Arbitrarily Precise Complex Number
    ///// </summary>
    //public struct BigComplex
    //{
    //    public BigDecimal Imaginary { get; set; }
    //    public BigDecimal Real { get; set; }

    //    private BigDecimal mod(BigDecimal x, BigDecimal m)
    //    {
    //        return (x % m + m) % m;
    //    }

    //    public BigDecimal Phase
    //    {
    //        get
    //        {
    //            if (Real > 0) return Math.Atan((double)(mod((Imaginary / Real) , CantusEvaluator.PI * 2)));
    //            else if (Real == 0)
    //            {
    //                if (Imaginary > 0) return CantusEvaluator.PI / 2;
    //                else if (Imaginary < 0) return -CantusEvaluator.PI / 2;
    //                else return BigDecimal.Undefined;
    //            }
    //            else { 
    //                if (Imaginary >= 0) return Math.Atan((double)(mod((Imaginary / Real) , CantusEvaluator.PI * 2))) + CantusEvaluator.PI;
    //                else return Math.Atan((double)(mod((Imaginary / Real) , CantusEvaluator.PI * 2))) - CantusEvaluator.PI;
    //            }
    //        }
    //        set
    //        {
    //            value = mod(value, CantusEvaluator.PI * 2);
    //            BigDecimal mag = Magnitude;
    //            Imaginary =  mag * Math.Sin((double)value);
    //            Real = mag * Math.Cos((double)value);
    //        }
    //    }

    //    public BigDecimal Magnitude { get
    //        {
    //            return Imaginary * Imaginary + Real * Real;
    //        }
    //        set
    //        {
    //            BigDecimal phi = Phase;
    //            Imaginary =  value * Math.Sin((double)phi);
    //            Real = value * Math.Cos((double)phi); 
    //        }
    //    }
    //    /// <summary>
    //    /// Create a BigComplex object from real and imaginary parts
    //    /// </summary>
    //    public BigComplex(BigDecimal? real = null, BigDecimal? imag = null)
    //    {
    //        if (real == null) real = 0.0;
    //        this.Real = (BigDecimal)real;
    //        if (imag == null) imag = 0.0;
    //        this.Imaginary = (BigDecimal)imag;
    //    }

    //    /// <summary>
    //    /// Get a BigComplex object from a magnitude and a phase
    //    /// </summary>
    //    public static BigComplex FromMagnitudePhase(BigDecimal magnitude, BigDecimal phase)
    //    {
    //        BigComplex bc = new BigComplex(0);
    //        bc.Magnitude = magnitude;
    //        bc.Phase = phase;
    //        return bc;
    //    }

    //    public static implicit operator BigComplex(int val)
    //    {
    //        return new BigComplex(val);
    //    }

    //    public static implicit operator BigComplex(double val)
    //    {
    //        return new BigComplex(val);
    //    }

    //    public static implicit operator BigComplex(BigDecimal val)
    //    {
    //        return new BigComplex(val);
    //    }

    //    public static implicit operator BigComplex(Complex val)
    //    {
    //        return new BigComplex(val.Real, val.Imaginary);
    //    }

    //    public static BigComplex operator -(BigComplex value)
    //    {
    //        return new BigComplex(-value.Real, -value.Imaginary);
    //    }

    //    public static BigComplex operator +(BigComplex left, BigComplex right)
    //    {
    //        return new BigComplex(left.Real + right.Real, left.Imaginary + right.Imaginary);
    //    }

    //    public static BigComplex operator -(BigComplex left, BigComplex right)
    //    {
    //        return new BigComplex(left.Real - right.Real, left.Imaginary - right.Imaginary);
    //    }

    //    public static BigComplex operator *(BigComplex left, BigComplex right)
    //    {
    //        return new BigComplex(left.Real * right.Real - left.Imaginary * right.Imaginary,
    //                              left.Real * right.Imaginary + left.Imaginary * right.Real);
    //    }

    //    public static BigComplex operator /(BigComplex left, BigComplex right)
    //    {
    //        BigDecimal denom = right.Real * right.Real + right.Imaginary * right.Imaginary;
    //        return new BigComplex((left.Real * right.Real + left.Imaginary * right.Imaginary) / denom ,
    //                              (left.Imaginary * right.Real - left.Real * right.Imaginary) / denom );
    //    }

    //    public BigComplex Reciprocal {
    //        get 
    //        {
    //            return 1 / this;
    //        }
    //    }

    //    public static BigComplex Exp(BigComplex z)
    //    {
    //        BigDecimal value = BigDecimal.Exp(z.Real);
    //        return new BigComplex(value * BigDecimal.Cos(z.Imaginary), value * BigDecimal.Sin(z.Imaginary));
    //    }


    //};
}

