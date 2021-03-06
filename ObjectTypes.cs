﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Reflection;
using System.Text;
using Cantus.Core.CommonTypes;
using System.Linq;

using static Cantus.Core.CantusEvaluator.ObjectTypes;
using static Cantus.Core.Scoping;
using Cantus.Core.Exceptions;

namespace Cantus.Core
{

    public sealed partial class CantusEvaluator
    {
        // To define a new type for use with the evaluator, add a class implementing IEvalType in this namespace, 
        //    change the StrDetectType function and add converter functions as necessary

        /// <summary>
        /// Defines types, such as numbers and matrices, available in the evaluator
        /// </summary>
        public class ObjectTypes
        {
            // a pre-computed list of valid object types
            private static IEnumerable<Type> _types = from t in typeof(ObjectTypes).GetNestedTypes(BindingFlags.Public)
                                                      where !t.IsInterface && !t.IsAbstract && t.IsSubclassOf(typeof(EvalObjectBase))
                                                      select t;
            /// <summary>
            /// Automatically converts the object to a IEvalObject
            /// </summary>
            /// <param name="obj">The string</param>
            /// <param name="identifierAsText">If true, parses strings into Text's instead of Identifiers</param>
            /// <returns></returns>
            public static EvalObjectBase DetectType(object obj, bool identifierAsText = false)
            {
                if (obj == null)
                    return null;
                // null

                if (obj.GetType().ToString().StartsWith("Cantus.Core.CantusEvaluator+ObjectTypes") && !obj.GetType().ToString().EndsWith("[]"))
                {
                    return (EvalObjectBase)obj;
                }

                foreach (Type t in _types)
                {
                    // identifiers & text are both strings, so we need to look at the identifierAsText flag
                    if (identifierAsText && t == typeof(Identifier))
                        continue;
                    if (!identifierAsText && t == typeof(Text))
                        continue;

                    if (Convert.ToBoolean(t.GetMethod("IsType").Invoke(t, new[] { obj })))
                    {
                        return (EvalObjectBase)Activator.CreateInstance(t, new[] { obj });
                    }
                }

                throw new EvaluatorException("Type " + obj.GetType().Name + " is not understood by the evaluator.");
            }

            /// <summary>
            /// Automatically parses the string into an IEvalObject
            /// </summary>
            /// <param name="str">The string</param>
            /// <param name="identifierAsText">If true, parses remaining into Text's instead of Identifiers</param>
            /// <param name="primitiveOnly">If true, only checks number, boolean, text types</param>
            /// <returns></returns>
            public static EvalObjectBase Parse(string str, CantusEvaluator eval = null, bool identifierAsText = false, bool primitiveOnly = true, bool numberPreserveSigFigs = false)
            {
                if (Number.StrIsType(str))
                {
                    return new Number(str, numberPreserveSigFigs);
                }
                else if (Boolean.StrIsType(str))
                {
                    return new Boolean(str);
                }
                else
                {
                    if (!primitiveOnly)
                    {
                        foreach (Type t in _types)
                        {
                            try {
                                // We don't need to parse strings or primitives
                                if (t == typeof(Identifier) || t == typeof(Text) || t == typeof (Number) || t == typeof (Boolean))
                                    continue;

                                if (Convert.ToBoolean(t.GetMethod("StrIsType").Invoke(t, new string[] { str })))
                                {
                                    try
                                    {
                                        t.GetConstructor(new[] { typeof(string) });
                                        return (EvalObjectBase)Activator.CreateInstance(t, new string[] { str });
                                    }
                                    catch
                                    {
                                        return (EvalObjectBase)Activator.CreateInstance(t, new object[] { str, eval });
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        // if none work then try text
                        if (identifierAsText && Text.StrIsType(str) && str.StartsWith("\"") && str.EndsWith("\""))
                        {
                            return new Text(str);
                            //AndAlso ObjectTypes.Identifier.StrIsType(str) Then
                        }
                        else if (!identifierAsText)
                        {
                            return new Identifier(str);
                        }
                    }

                    return null;
                }
            }

            public abstract class EvalObjectBase : object, IEquatable<EvalObjectBase>, IComparable, IComparable<EvalObjectBase>
            {

                /// <summary>
                /// Get the system type value represented by this object. 
                /// If the object represents no specific system type, then this should return the object itself.
                /// </summary>
                public abstract object GetValue();

                /// <summary>
                /// Set the value represented by this object.
                /// If the object represents no specific system type, this should be used to copy from another object of the same type
                /// </summary>
                public abstract void SetValue(object obj);

                private new string ToString() { return ""; }
                /// <summary>
                /// Convert this object to a human readable string
                /// </summary>
                public abstract string ToString(CantusEvaluator eval);

                /// <summary>
                /// Function used to detect if an object is of or is represented by the type. Used in DetectType()
                /// </summary>
                public static bool IsType(object obj)
                {
                    return false;
                }

                /// <summary>
                /// Function used to detect if a string is of or is represented by the type. Used in StrDetectType()
                /// </summary>
                public static bool StrIsType(string str)
                {
                    return false;
                }

                /// <summary>
                /// Generate a (usually) unique integer identifying the object
                /// </summary>
                public override abstract int GetHashCode();

                /// <summary>
                /// Create a brand new copy of this object, so that the new copy will not affect the old
                /// </summary>
                /// <returns></returns>
                public virtual EvalObjectBase GetDeepCopy()
                {
                    return DeepCopy();
                }

                /// <summary>
                /// Create a brand new copy of this object, so that the new copy will not affect the old
                /// </summary>
                protected abstract EvalObjectBase DeepCopy();

                public abstract bool Equals(EvalObjectBase other);

                public virtual int CompareTo(object other)
                {
                    return ((IComparable)GetValue()).CompareTo(other);
                }

                public virtual int CompareTo(EvalObjectBase other)
                {
                    return ((IComparable)GetValue()).CompareTo(other.GetValue());
                }
            }

            public sealed class Number : EvalObjectBase
            {

                private BigDecimal _value;
                public string DecimalSep { get; } = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                public override object GetValue()
                {
                    return (double)(_value);
                }

                public BigDecimal BigDecValue()
                {
                    return _value;
                }

                public override void SetValue(object obj)
                {
                    if (obj is BigDecimal)
                    {
                        _value = (BigDecimal)obj;
                    }
                    else
                    {
                        _value = (double)(obj);
                    }
                }

                public static new bool IsType(object obj)
                {
                    return obj is Number | obj is double | obj is int | obj is BigDecimal;
                }

                public static new bool StrIsType(string str)
                {
                    str = str.ToLowerInvariant();
                    double ___tmp;
                    return str == "null" || str == "undefined" || str.StartsWith("0x") || (double.TryParse(str.Trim(), out ___tmp) && !str.Contains("e"));
                }

                public override bool Equals(EvalObjectBase other)
                {
                    if (IsType(other))
                    {
                        return ((Number)other).BigDecValue() == _value;
                    }
                    else
                    {
                        return false;
                    }
                }

                public override string ToString(CantusEvaluator eval)
                {
                    return _value.ToString();
                }

                protected override EvalObjectBase DeepCopy()
                {
                    this._value.Normalize();
                    BigDecimal bn = new BigDecimal(this._value.Mantissa, this._value.Exponent,
                        this._value.IsUndefined, this._value.SigFigs);
                    bn.LastOperation = this._value.LastOperation;
                    Number n =  new Number(bn);
                    return n;
                }

                public Number(double value)
                {
                    this._value = value;
                }

                public Number(BigDecimal value)
                {
                    this._value = value;
                }

                public Number(string str, bool numberPreserveSigFigs = false)
                {
                    str = str.ToLowerInvariant().Replace(" ", "").Replace("\n", "").Replace("\r", "");

                    if (str == "undefined" || str == "null")
                    {
                        this._value = BigDecimal.Undefined;
                    }
                    else if (str.StartsWith("0x") && str.Length > 2 && !str.Contains(DecimalSep))
                    {
                        this._value = (double)long.Parse(str.Substring(2), System.Globalization.NumberStyles.HexNumber);
                    }
                    else if (str.StartsWith("00") && str.Length > 2 && !str.Contains(DecimalSep))
                    {
                        this._value = (double)Convert.ToInt64(str.Substring(2), 8);
                    }
                    else
                    {
                        try
                        {
                            str = str.Trim();
                            BigInteger mantissa = BigInteger.Parse(str.Replace(DecimalSep, ""));
                            int expo = 0;
                            string tmp = str.Trim(new[]{
                            '-',
                            '0'
                        });
                            int ind = str.IndexOf(DecimalSep);
                            if (ind == 0)
                            {
                                expo = -str.Length;
                            }
                            else if (ind < 0)
                            {
                                expo = 0;
                            }
                            else
                            {
                                expo = -str.Substring(ind + DecimalSep.Length).Length;
                            }

                            if (numberPreserveSigFigs)
                            {
                                this._value = new BigDecimal(mantissa, expo, sigFigs: (int)(new InternalFunctions(null).CountSigFigs(str.Trim())));
                            }
                            else
                            {
                                this._value = new BigDecimal(mantissa, expo);
                            }
                        }
                        catch (Exception)
                        {
                            this._value = BigDecimal.Undefined;
                        }
                    }
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }
            }

            public sealed class Complex : EvalObjectBase
            {
                private System.Numerics.Complex _value;
                public override object GetValue()
                {
                    return _value;
                }
                public double Real
                {
                    get { return _value.Real; }
                    set { this._value = new System.Numerics.Complex(value, this.Imag); }
                }
                public double Imag
                {
                    get { return _value.Imaginary; }
                    set { this._value = new System.Numerics.Complex(this.Real, value); }
                }
                public override void SetValue(object obj)
                {
                    if (Matrix.IsType(obj))
                    {
                        List<Reference> lst = (List<Reference>)obj;
                        this._value = new System.Numerics.Complex((double)(lst[0].GetValue()), (double)(lst[1].GetValue()));
                    }
                    else if (IsType(obj))
                    {
                        this._value = (System.Numerics.Complex)obj;
                    }
                    else if (Number.IsType(obj))
                    {
                        this._value = (double)(obj);
                    }
                }

                protected override EvalObjectBase DeepCopy()
                {
                    return new Complex(new System.Numerics.Complex(Real, Imag));
                }

                public static new bool IsType(object obj)
                {
                    return obj is Complex | obj is System.Numerics.Complex;
                }

                public static new bool StrIsType(string str)
                {
                    str = str.Trim();
                    return str.EndsWith(")") && str.StartsWith("(") && str.Contains("i") && (str.Contains("+") || str.Contains("-"));
                }

                public override string ToString(CantusEvaluator eval)
                {
                    return string.Format("({0} {1} {2}i)", _value.Real, _value.Imaginary >= 0 ? "+" : "-", Math.Abs(_value.Imaginary));
                }

                public override bool Equals(EvalObjectBase other)
                {
                    if (IsType(other))
                    {
                        return (System.Numerics.Complex)other.GetValue() == _value;
                    }
                    else
                    {
                        return false;
                    }
                }

                public Complex(double real, double imag = 0)
                {
                    this._value = new System.Numerics.Complex(real, imag);
                }
                public Complex(System.Numerics.Complex value)
                {
                    this._value = value;
                }
                public Complex(string str, CantusEvaluator eval)
                {
                    if (StrIsType(str))
                    {
                        str = str.Trim().Remove(str.Length - 1).Substring(1).Trim();
                        string[] split = str.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (split.Length != 3)
                            throw new EvaluatorException("Invalid complex format");

                        int neg = split[1].Contains("-") ? -1 : 1;

                        this._value = new System.Numerics.Complex((double)((BigDecimal)eval.EvalExprRaw(split[0], true)), (double)(neg * new Number(split[2].Replace("i", "").Trim()).BigDecValue()));
                    }
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }
            }

            public sealed class Boolean : EvalObjectBase
            {
                private bool _value;
                public override object GetValue()
                {
                    return _value;
                }
                public override void SetValue(object obj)
                {
                    _value = Convert.ToBoolean(obj);
                }
                public static new bool IsType(object obj)
                {
                    return obj is Boolean | obj is bool;
                }
                public static new bool StrIsType(string str)
                {
                    str = str.Trim().ToLower();
                    return str == "true" || str == "false";
                }

                public override bool Equals(EvalObjectBase other)
                {
                    if (IsType(other))
                    {
                        return Convert.ToBoolean(other.GetValue()) == _value;
                    }
                    else
                    {
                        return false;
                    }
                }

                protected override EvalObjectBase DeepCopy()
                {
                    return new ObjectTypes.Boolean(this._value);
                }

                public Boolean(bool value)
                {
                    this._value = value;
                }
                public Boolean(string str)
                {
                    this._value = bool.Parse(str.Trim());
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }

                public override string ToString(CantusEvaluator eval)
                {
                    return _value.ToString();
                }
            }

            /// <summary>
            /// A piece of text
            /// </summary>
            public sealed class Text : EvalObjectBase
            {
                private string _value;
                public override object GetValue()
                {
                    return _value;
                }
                public override void SetValue(object obj)
                {
                    _value = Convert.ToString(obj);
                }
                public static new bool IsType(object obj)
                {
                    return obj is string | obj is Text;
                }
                public static new bool StrIsType(string str)
                {
                    return true;
                }

                /// <summary>
                /// If the index is within bounds then set the character. Otherwise append the character to the end.
                /// </summary>
                private void SetOrAppend(ref StringBuilder sb, char chr, int idx = -1)
                {
                    if (idx < sb.Length && idx >= 0)
                    {
                        sb[idx] = chr;
                    }
                    else
                    {
                        sb.Append(chr);
                    }
                }

                /// <summary>
                /// Resolve all escape sequences in this Text object
                /// </summary>
                /// <param name="raw">If true, only escapes \ \' and \"</param>
                public Text Escape(bool raw = false)
                {
                    StringBuilder newstr = new StringBuilder();
                    bool escNxt = false;
                    int idx = 0;

                    for (int i = 0; i <= _value.Length - 1; i++)
                    {
                        if (escNxt)
                        {
                            if (raw)
                            {
                                switch (char.ToLowerInvariant(_value[i]))
                                {
                                    case '\'':
                                    case '"':
                                        SetOrAppend(ref newstr, _value[i], idx);
                                        break;
                                    default:
                                        SetOrAppend(ref newstr, '\\', idx);
                                        idx += 1;
                                        SetOrAppend(ref newstr, _value[i], idx);
                                        break;
                                }
                            }
                            else
                            {
                                // c-like escape sequence
                                string charId = "";
                                int id = 0;
                                switch (char.ToLowerInvariant(_value[i]))
                                {
                                    case 'a':
                                        SetOrAppend(ref newstr, (char)(7), idx);
                                        break;
                                    case 'b':
                                        if (idx > 1)
                                            idx -= 3;
                                        // non-destructive backspace
                                        break;
                                    case 'f':
                                        SetOrAppend(ref newstr, (char)(12), idx);
                                        break;
                                    case 'n':
                                        SetOrAppend(ref newstr, '\n', idx);
                                        break;
                                    case 'r':
                                        SetOrAppend(ref newstr, '\r', idx);
                                        break;
                                    case 't':
                                        SetOrAppend(ref newstr, '\t', idx);
                                        break;
                                    case 'v':
                                        SetOrAppend(ref newstr, '\v', idx);
                                        break;
                                    case 'x':
                                        i += 1;
                                        charId = "&H";
                                        while (i < _value.Length && (((int)(_value[i]) >= (int)('0') && (int)(_value[i]) <= (int)('9')) || ((int)(_value[i]) >= (int)('a') && (int)(_value[i]) <= (int)('f')) || ((int)(_value[i]) >= (int)('A') && (int)(_value[i]) <= (int)('F'))) && charId.Length < 7)
                                        {
                                            charId += char.ToUpperInvariant(_value[i]);
                                            i += 1;
                                        }
                                        i -= 1;
                                        SetOrAppend(ref newstr, (char)(int.Parse(charId)), idx);
                                        break;
                                    case '0':
                                    case '1':
                                    case '2':
                                    case '3':
                                    case '4':
                                    case '5':
                                    case '6':
                                    case '7':
                                    case '8':
                                    case '9':
                                        charId = "&O";
                                        while (i < _value.Length && charId.Length < 5 && ((int)(_value[i]) >= (int)('0') && (int)(_value[i]) <= (int)('7')))
                                        {
                                            charId += char.ToUpperInvariant(_value[i]);
                                            i += 1;
                                        }
                                        i -= 1;
                                        SetOrAppend(ref newstr, (char)(int.Parse(charId)), idx);
                                        break;
                                    case 'd':
                                        i += 1;
                                        while (i < _value.Length && (int)(_value[i]) >= (int)('0') && (int)(_value[i]) <= (int)('9'))
                                        {
                                            id = id * 10 + (int)(_value[i]) - (int)('0');
                                            i += 1;
                                        }
                                        i -= 1;
                                        SetOrAppend(ref newstr, (char)(id), idx);
                                        break;
                                    case 'u':
                                        i += 1;
                                        while (i < _value.Length && (int)(_value[i]) >= (int)('0') && (int)(_value[i]) <= (int)('9'))
                                        {
                                            id = id * 10 + (int)(_value[i]) - (int)('0');
                                            i += 1;
                                        }
                                        i -= 1;
                                        SetOrAppend(ref newstr, char.ConvertFromUtf32(id)[0], idx);
                                        break;
                                    case '\\':
                                    case '\'':
                                    case '\"':
                                    case '?':
                                        SetOrAppend(ref newstr, _value[i], idx);
                                        break;
                                    case '/':
                                        SetOrAppend(ref newstr, System.IO.Path.DirectorySeparatorChar, idx);
                                        break;
                                    default:
                                        throw new EvaluatorException("Invalid escape sequence");
                                }
                            }
                            escNxt = false;
                        }
                        else if (_value[i] == '\\')
                        {
                            if (i == _value.Length - 1)
                                SetOrAppend(ref newstr, '\\', idx);
                            // do not escape if this is the last character
                            escNxt = true;
                        }
                        else
                        {
                            SetOrAppend(ref newstr, _value[i], idx);
                        }
                        idx += 1;
                    }
                    this._value = newstr.ToString();
                    return this;
                }

                public override string ToString(CantusEvaluator eval)
                {
                    return '\'' + _value + '\'';
                }

                public override bool Equals(EvalObjectBase other)
                {
                    if (IsType(other))
                    {
                        return other.GetValue().ToString() == _value;
                    }
                    else
                    {
                        return false;
                    }
                }

                protected override EvalObjectBase DeepCopy()
                {
                    return new ObjectTypes.Text(this._value);
                }


                public Text(string value)
                {
                    if (value.Length > 1 && (value.StartsWith("\"") && value.EndsWith("\"") || value.StartsWith("'") && value.EndsWith("'")))
                    {
                        this._value = value.Substring(1, value.Length - 2);
                    }
                    else
                    {
                        this._value = value;
                    }
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }
            }

            /// <summary>
            /// A piece of text that represents a function or variable
            /// </summary>
            public sealed class Identifier : EvalObjectBase
            {

                private string _value;
                public override object GetValue()
                {
                    return _value;
                }

                public override void SetValue(object obj)
                {
                    _value = obj.ToString();
                }

                public static new bool IsType(object obj)
                {
                    return obj is string | obj is Identifier;
                }

                public static new bool StrIsType(string str)
                {
                    if (string.IsNullOrWhiteSpace(str.Trim()))
                        return false;
                    // check if empty
                    if (char.IsDigit(str[0]))
                        return false;
                    // check if starts with number

                    if (str.ToLowerInvariant().StartsWith("operator") || str.ToLowerInvariant().Contains(".operator")) return true;
                    char[] disallowed = "&+-*/{}[]()';^$@#!%=<>,:|\\`~ ".ToCharArray();
                    foreach (char c in str)
                    {
                        if (disallowed.Contains(c))
                            return false;
                    }
                    return true;
                }

                protected override EvalObjectBase DeepCopy()
                {
                    return new ObjectTypes.Identifier(this._value);
                }

                public override bool Equals(EvalObjectBase other)
                {
                    if (IsType(other))
                    {
                        return other.GetValue().ToString() == _value;
                    }
                    else
                    {
                        return false;
                    }
                }

                public Identifier(string value)
                {
                    this._value = value.Trim();
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }

                public override string ToString(CantusEvaluator eval)
                {
                    return _value;
                }
            }

            /// <summary>
            /// A single class that is able to represent both absolute points in time and time spans
            /// </summary>
            public sealed class DateTime : EvalObjectBase
            {
                private System.TimeSpan _value;
                /// <summary>
                /// The date from which absolute datetimes are calculated
                /// </summary>
                /// <returns></returns>
                public static System.DateTime BASE_DATE { get; }
                /// <summary>
                /// The length of time in days after which absolute datetimes are returned instead of timespans
                /// </summary>
                /// <returns></returns>
                public static int TIMESPAN_DIVIDER { get; } = 36500;

                public override object GetValue()
                {
                    if (_value.Days > TIMESPAN_DIVIDER)
                    {
                        return BASE_DATE.Add(_value);
                    }
                    return _value;
                }

                public override void SetValue(object obj)
                {
                    if (obj is DateTime)
                    {
                        _value = Convert.ToDateTime(obj).Subtract(BASE_DATE);
                    }
                    else if (obj is TimeSpan)
                    {
                        _value = (TimeSpan)obj;
                    }
                }

                public override EvalObjectBase GetDeepCopy()
                {
                    if (this.GetValue() is System.DateTime)
                    {
                        return new DateTime(new System.DateTime(Convert.ToDateTime(this.GetValue()).Ticks));
                    }
                    else
                    {
                        return new DateTime(new TimeSpan(((TimeSpan)this.GetValue()).Ticks));
                    }
                }

                public static new bool IsType(object obj)
                {
                    return obj is TimeSpan | obj is System.DateTime || obj is System.DateTime | obj is DateTime;
                }

                public static new bool StrIsType(string str)
                {
                    TimeSpan ___t;
                    System.DateTime ___d;
                    return System.TimeSpan.TryParse(str.Trim(), out ___t) | System.DateTime.TryParse(str.Trim(), out ___d);
                }

                public override string ToString(CantusEvaluator eval)
                {
                    if (_value.Days > TIMESPAN_DIVIDER)
                    {
                        return BASE_DATE.Add(_value).ToString();
                    }
                    return _value.ToString();
                }

                protected override EvalObjectBase DeepCopy()
                {
                    return new ObjectTypes.DateTime(this._value);
                }

                public override bool Equals(EvalObjectBase other)
                {
                    if (IsType(other))
                    {
                        object val = other.GetValue();
                        if (val is TimeSpan)
                        {
                            return (TimeSpan)val == _value;
                        }
                        else if (val is DateTime)
                        {
                            return Convert.ToDateTime(val) == BASE_DATE.Add(_value);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                public DateTime(System.TimeSpan value)
                {
                    this._value = value;
                }
                public DateTime(System.DateTime value)
                {
                    this._value = value.Subtract(BASE_DATE);
                }
                public DateTime(string str)
                {
                    if (!System.TimeSpan.TryParse(str.Trim(), out this._value))
                    {
                        System.DateTime tmp = default(System.DateTime);
                        System.DateTime.TryParse(str.Trim(), out tmp);
                        this._value = tmp.Subtract(BASE_DATE);
                    }
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }
            }

            /// <summary>
            /// A fixed list of numbers
            /// </summary>
            public sealed class Tuple : EvalObjectBase
            {

                private List<Reference> _value;
                public override object GetValue()
                {
                    return _value.ToArray();
                }

                public override void SetValue(object obj)
                {
                    if (obj is List<Reference>)
                    {
                        _value = (List<Reference>)obj;
                    }
                    else
                    {
                        if (obj is Tuple)
                            obj = ((Tuple)obj).GetValue();
                        if (!(obj is Reference[]))
                            obj = new[] { new Reference(obj) };
                        Reference[] reflst = (Reference[])obj;

                        // tuple assignment
                        for (int i = 0; i <= _value.Count - 1; i++)
                        {
                            if (reflst.Length <= i)
                            {
                                if (reflst[reflst.Length - 1].GetRefObject() is Reference)
                                {
                                    _value[i].SetValue(reflst[reflst.Length - 1]);
                                }
                                else
                                {
                                    _value[i].SetValue(reflst[i].Resolve());
                                }
                            }
                            else
                            {
                                if (reflst[i].GetRefObject() is Reference) { 
                                    _value[i].SetValue(reflst[i]);
                                }
                                else
                                {
                                    _value[i].SetValue(reflst[i].Resolve());
                                }
                            }
                        }
                    }
                }

                protected override EvalObjectBase DeepCopy()
                {
                    List<Reference> lst = new List<Reference>();
                    foreach (Reference r in _value)
                    {
                        lst.Add((Reference)r.GetDeepCopy());
                    }
                    return new Tuple(lst);
                }

                public static new bool IsType(object obj)
                {
                    return obj is Tuple | obj is Reference[];
                }
                public static new bool StrIsType(string str)
                {
                    str = str.Trim();
                    return str.StartsWith("(") && str.EndsWith(")");
                }

                public override string ToString(CantusEvaluator eval)
                {
                    StringBuilder str = new StringBuilder("(");
                    foreach (Reference k in _value)
                    {
                        if (!(str.Length == 1))
                            str.Append(", ");
                        InternalFunctions ef = eval.Internals;
                        str.Append(ef.O(k.GetRefObject()));
                    }
                    str.Append(")");
                    return str.ToString();
                }

                public override bool Equals(EvalObjectBase other)
                {
                    return false;
                }

                public Tuple(List<Reference> value)
                {
                    this._value = value;
                }
                public Tuple(Reference[] value)
                {
                    this._value = value.ToList();
                }
                public Tuple(IEnumerable<object> value)
                {
                    this._value = new List<Reference>();
                    foreach (object v in value)
                    {
                        if (v is Reference && !(((Reference)v).GetRefObject() is Reference))
                        {
                            this._value.Add((Reference)v);
                        }
                        else
                        {
                            this._value.Add(new Reference(ObjectTypes.DetectType(v, true)));
                        }
                    }
                }

                /// <summary>
                /// Create a new tuple
                /// </summary>
                /// <param name="conditions">If true, all members are evaluated in condition mode into booleans</param>
                public Tuple(string str, CantusEvaluator eval, bool conditions = true)
                {
                    //Try
                    if (StrIsType(str))
                    {
                        str = str.Trim().Remove(str.Length - 1).Substring(1).Trim(',');
                        this._value = new List<Reference>();
                        if (!string.IsNullOrWhiteSpace(str))
                        {
                            EvalObjectBase res = ObjectTypes.DetectType(eval.EvalExprRaw("0," + str, true, conditions), true);
                            if (IsType(res))
                            {
                                List<Reference> lst = ((Reference[])res.GetValue()).ToList();
                                this._value.AddRange(lst.GetRange(1, lst.Count - 1));
                            }
                            else
                            {
                                this._value.Add(new Reference(res));
                            }
                        }
                    }
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }
            }

            /// <summary>
            /// A vector or matrix 
            /// </summary>
            public sealed class Matrix : EvalObjectBase
            {
                private List<Reference> _value;
                public int Height
                {
                    get { return _value.Count; }
                }
                private int _width;
                public int Width
                {
                    get
                    {
                        return _width;
                    }
                }
                public Size Size
                {
                    get { return new Size(Width, Height); }
                }

                public override object GetValue()
                {
                    return _value;
                }

                public override void SetValue(object obj)
                {
                    // tuple
                    if (obj is Reference[])
                    {
                        for (int i = 0; i <= Math.Min(_value.Count, ((Reference[])obj).Length) - 1; i++)
                        {
                            _value[i].ResolveRef().SetValue(((Reference[])obj)[i].ResolveObj());
                        }
                    }
                    else
                    {
                        _value = (List<Reference>)obj;
                    }
                    this.Normalize();
                }

                /// <summary>
                /// Get the object at the specified row and column in the matrix
                /// </summary>
                /// <returns></returns>
                public object GetCoord(int row, int col)
                {
                    EvalObjectBase r = _value[row].GetRefObject();
                    if (r is Matrix)
                    {
                        return ((List<Reference>)((Matrix)r).GetValue())[col].Resolve();
                    }
                    else
                    {
                        if (col == 0)
                        {
                            if (r is Reference)
                                return ((Reference)r).Resolve();
                            if (r is Number)
                                return ((Number)r).BigDecValue();
                            return r.GetValue();
                        }
                        else
                        {
                            return double.NaN;
                        }
                    }
                }

                /// <summary>
                /// Get the object at the specified row and column in the matrix as a reference
                /// </summary>
                /// <returns></returns>
                public Reference GetCoordRef(int row, int col)
                {
                    EvalObjectBase r = _value[row].GetRefObject();
                    if (r is Matrix)
                    {
                        return ((List<Reference>)((Matrix)r).GetValue())[col];
                    }
                    else
                    {
                        if (col == 0)
                        {
                            return new Reference(r);
                        }
                        else
                        {
                            return new Reference(double.NaN);
                        }
                    }
                }

                /// <summary>
                /// Set the object at the specified row and column in the matrix
                /// </summary>
                public void SetCoord(int row, int col, object obj)
                {
                    EvalObjectBase r = _value[row].ResolveObj();
                    if (obj is Reference)
                    {
                        if (r is Matrix)
                        {
                            ((List<Reference>)((Matrix)r).GetValue())[col] = (Reference)obj;
                        }
                        else
                        {
                            if (col == 0)
                                _value[row] = (Reference)obj;
                        }
                    }
                    else
                    {
                        if (r is Matrix)
                        {
                            ((List<Reference>)((Matrix)r).GetValue())[col].SetValue(obj);
                        }
                        else
                        {
                            if (col == 0)
                                r.SetValue(obj);
                        }
                    }
                }

                /// <summary>
                /// Get the transpose of this matrix
                /// </summary>
                /// <returns></returns>
                public Matrix Transpose()
                {
                    this.Normalize();
                    Matrix mat = (Matrix)this.GetDeepCopy();
                    Matrix m2 = new Matrix((List<Reference>)((Matrix)this.GetDeepCopy()).GetValue());
                    mat.Resize(Width, Height);
                    for (int i = 0; i <= mat.Height - 1; i++)
                    {
                        for (int j = 0; j <= mat.Width - 1; j++)
                        {
                            mat.SetCoord(i, j, m2.GetCoord(j, i));
                        }
                    }
                    return mat;
                }

                public object Determinant()
                {
                    try
                    {
                        // can only calculate det for square matrices
                        if (Width != Height)
                            throw new MathException("Can only calculate determinant for square matrices");

                        // base cases
                        if (Width == 0 && Height == 0)
                            return 1.0;
                        if (Width == 1 && Height == 1)
                            return GetCoord(0, 0);

                        object ans = 0.0;

                        int coeff = 1;
                        for (int i = 0; i <= this.Width - 1; i++)
                        {
                            Matrix newmat = new Matrix(Height - 1, Width - 1);
                            for (int j = 1; j <= this.Height - 1; j++)
                            {
                                for (int k = 0; k <= this.Width - 1; k++)
                                {
                                    if (i == k)
                                        continue;
                                    newmat.SetCoord(j - 1, k > i ? k - 1 : k, GetCoord(j, k));
                                }
                            }
                            object i1 = GetCoord(0, i);
                            object i2 = newmat.Determinant();

                            if (ans is System.Numerics.Complex || i1 is System.Numerics.Complex || i2 is System.Numerics.Complex)
                            {
                                if (!(ans is System.Numerics.Complex))
                                    ans = new System.Numerics.Complex((double)(ans), 0);
                                if (!(i1 is System.Numerics.Complex))
                                    i1 = new System.Numerics.Complex((double)(i1), 0);
                                if (!(i2 is System.Numerics.Complex))
                                    i2 = new System.Numerics.Complex((double)i2, 0);
                                ans = (System.Numerics.Complex)ans + coeff * (System.Numerics.Complex)i1 * (System.Numerics.Complex)i2;
                            }
                            else if (i1 is double && i2 is double)
                            {
                                ans = (double)ans + coeff * (double)i1 * (double)i2;
                            }
                            else if (i1 is BigDecimal && i2 is BigDecimal)
                            {
                                if (ans is double) ans = (BigDecimal)(double)ans;
                                ans = (BigDecimal)ans + coeff * (BigDecimal)i1 * (BigDecimal)i2;
                            }
                            else
                            {
                                return double.NaN;
                            }
                            coeff = -coeff;
                        }
                        return ans;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        throw new SyntaxException();
                    }
                }

                /// <summary>
                /// Multiply two matrices, if the width of the first is equal to the height of the second.
                /// </summary>
                /// <param name="mb"></param>
                /// <returns></returns>
                public Matrix Multiply(Matrix mb)
                {
                    if (Width == mb.Height)
                    {
                        List<Reference> res = new List<Reference>();
                        for (int row = 0; row <= Height - 1; row++)
                        {
                            List<Reference> currow = new List<Reference>();
                            for (int col = 0; col <= mb.Width - 1; col++)
                            {
                                object curitm = new BigDecimal(0.0);
                                for (int i = 0; i <= Width - 1; i++)
                                {
                                    object i1 = GetCoord(row, i);
                                    object i2 = mb.GetCoord(i, col);

                                    if (i1 is double) i1 = (BigDecimal)(double)i1;
                                    if (i2 is double) i2 = (BigDecimal)(double)i2;
                                    if (curitm is double) i2 = (BigDecimal)(double)i2;

                                    if (curitm is System.Numerics.Complex || i1 is System.Numerics.Complex || i2 is System.Numerics.Complex)
                                    {
                                        if (!(curitm is System.Numerics.Complex))
                                            curitm = new System.Numerics.Complex((double)(curitm), 0);
                                        if (!(i1 is System.Numerics.Complex))
                                            i1 = new System.Numerics.Complex((double)(i1), 0);
                                        if (!(i2 is System.Numerics.Complex))
                                            i2 = new System.Numerics.Complex((double)(i2), 0);
                                        curitm = (System.Numerics.Complex)curitm + (System.Numerics.Complex)i1 * (System.Numerics.Complex)i2;
                                    }
                                    else if (i1 is BigDecimal && i2 is BigDecimal)
                                    {
                                        curitm = (BigDecimal)curitm + (BigDecimal)i1 * (BigDecimal)i2;
                                    }
                                    else
                                    {
                                        throw new EvaluatorException("Invalid type in matrix for multiplication. Only numbers and complex values are allowed.");
                                    }
                                }
                                currow.Add(new Reference(curitm));
                            }
                            res.Add(new Reference(new Matrix(currow)));
                        }
                        this._value = res;
                        return this;
                    }
                    else
                    {
                        throw new MathException("Width of first matrix must equal height of second");
                    }
                }

                /// <summary>
                /// Multiply the matrix by a scalar quantity
                /// </summary>
                /// <param name="b"></param>
                /// <returns></returns>
                public Matrix MultiplyScalar(object b)
                {
                    if (b is double)
                        b = (BigDecimal)(double)(b);

                    for (int row = 0; row <= Height - 1; row++)
                    {
                        List<Reference> currow = new List<Reference>();
                        for (int col = 0; col <= Width - 1; col++)
                        {
                            object cur = GetCoord(row, col);
                            if (cur is System.Numerics.Complex || b is System.Numerics.Complex)
                            {
                                if (!(cur is System.Numerics.Complex))
                                    cur = new System.Numerics.Complex((double)(cur), 0);
                                if (!(b is System.Numerics.Complex))
                                    b = new System.Numerics.Complex((double)(b), 0);
                                SetCoord(row, col, (System.Numerics.Complex)cur * (System.Numerics.Complex)b);
                            }
                            else if (cur is BigDecimal && b is BigDecimal)
                            {
                                SetCoord(row, col, (BigDecimal)cur * (BigDecimal)b);
                            }
                            else if (cur is double && b is BigDecimal)
                            {
                                SetCoord(row, col, (BigDecimal)(double)(cur) * (BigDecimal)b);
                            }
                            else
                            {
                                throw new EvaluatorException("Invalid type in matrix for scalar multiplication. Only numbers and complex values are allowed.");
                            }
                        }
                    }
                    return this;
                }

                /// <summary>
                /// Compute the inner product of two vectors
                /// </summary>
                public Matrix Inner(Matrix other)
                {
                    try
                    {
                        if (Width != 1 || other.Width != 1)
                        {
                            throw new MathException("Can only calculate dot product of two column vectors");
                        }
                        List<Reference> valueR = (List<Reference>)other.GetValue();
                        Matrix result = (Matrix)DeepCopy();

                        for (int i = 0; i <= Math.Min(Height, other.Height) - 1; i++)
                        {
                            object a = _value[i].Resolve();
                            object b = valueR[i].Resolve();

                            if (a is System.Numerics.Complex || b is System.Numerics.Complex)
                            {
                                if (a is double || a is BigDecimal)
                                    a = new System.Numerics.Complex((double)(a), 0);
                                if (b is double || b is BigDecimal)
                                    b = new System.Numerics.Complex((double)(b), 0);
                                result._value[i] = new Reference((System.Numerics.Complex)a * (System.Numerics.Complex)b);
                            }
                            else if ((a is BigDecimal || a is double) && (b is BigDecimal || b is double))
                            {
                                if (a is double) a = (BigDecimal)(double)a;
                                if (b is double) b = (BigDecimal)(double)b;

                                result._value[i] = new Reference((BigDecimal)a * (BigDecimal)b);
                            }
                        }
                        return result;
                    }
                    catch 
                    {
                        return null;
                    }
                }

                /// <summary>
                /// Compute the dot product of two vectors
                /// </summary>
                /// <param name="other"></param>
                /// <returns></returns>
                public object Dot(Matrix other)
                {
                    if (Width != 1 || other.Width != 1)
                    {
                        throw new MathException("Can only calculate dot product of two column vectors");
                    }
                    List<Reference> valueR = (List<Reference>)other.GetValue();
                    object ans = 0.0;

                    for (int i = 0; i <= Math.Min(Height, other.Height) - 1; i++)
                    {
                        object a = _value[i].Resolve();
                        object b = valueR[i].Resolve();

                        if (a is System.Numerics.Complex || b is System.Numerics.Complex || ans is System.Numerics.Complex)
                        {
                            if (a is double || a is BigDecimal)
                                a = new System.Numerics.Complex((double)(a), 0);
                            if (b is double || b is BigDecimal)
                                b = new System.Numerics.Complex((double)(b), 0);
                            if (ans is double || ans is BigDecimal)
                                ans = new System.Numerics.Complex((double)(ans), 0);
                            ans = (System.Numerics.Complex)ans + (System.Numerics.Complex)a * (System.Numerics.Complex)b;
                        }
                        else if ((a is BigDecimal || a is double) && (b is BigDecimal || b is double))
                        {
                            if (ans is double) ans = (BigDecimal)(double)ans;
                            if (a is double) a = (BigDecimal)(double)a;
                            if (b is double) b = (BigDecimal)(double)b;

                            ans = (BigDecimal)ans + (BigDecimal)a * (BigDecimal)b;
                        }
                    }

                    if (ans is double) ans = (BigDecimal)(double)ans;
                    return ans;
                }

                /// <summary>
                /// Sets the current vector to the cross product with another vector. 
                /// Only works for vectors in R3 (lower dimension vectors are padded with 0 in unspecified dimensions).
                /// </summary>
                /// <param name="other"></param>
                /// <returns></returns>
                public Matrix Cross(Matrix other)
                {
                    if (Width != 1 || other.Width != 1 || Height > 3 || other.Height > 3)
                    {
                        throw new MathException("Can only calculate cross product of two column vectors in R3");
                    }

                    Resize(3, 1);
                    other.Resize(3, 1);

                    List<Reference> newValue = new List<Reference>(3);

                    for (int i = 0; i <= 2; i++)
                    {
                        int j = (i + 1) % 3;
                        int k = (j + 1) % 3;
                        if (k == i)
                            k += 1;
                        string evalStr = GetCoord(j, 0).ToString() + "*" +
                            other.GetCoord(k, 0).ToString() + "-" + GetCoord(k, 0).ToString() +
                            "*" + other.GetCoord(j, 0).ToString();

                        EvalObjectBase result = ObjectTypes.DetectType(
                            new CantusEvaluator(reloadDefault: false).EvalExprRaw(evalStr.ToString(), true));
                        if (result is Reference)
                        {
                            newValue.Add((Reference)result);
                        }
                        else
                        {
                            newValue.Add(new Reference(result));
                        }
                    }
                    _value = newValue;
                    return this;
                }

                /// <summary>
                /// Finds the norm of the vector (actually gives square of the magnitude)
                /// </summary>
                /// <returns></returns>
                public object Norm()
                {
                    if (this.Width != 1)
                        throw new MathException("Can only get norm of column vectors");

                    object result = 0.0;
                    for (int i = 0; i <= this.Height - 1; i++)
                    {
                        object a = GetCoord(i, 0);
                        if (a is Reference)
                            a = ((Reference)a).Resolve();


                        if (result is System.Numerics.Complex || a is System.Numerics.Complex)
                        {
                            if (!(a is System.Numerics.Complex))
                                a = new System.Numerics.Complex((double)(a), 0);
                            if (!(result is System.Numerics.Complex))
                                a = new System.Numerics.Complex((double)(result), 0);

                            result = (System.Numerics.Complex)result + (System.Numerics.Complex)a * (System.Numerics.Complex)a;

                        }
                        else if (a is double)
                        {
                            if (result is double)
                                result = (BigDecimal)(double)(result);
                            result = (BigDecimal)result + (double)(a) * (double)(a);

                        }
                        else if (a is BigDecimal)
                        {
                            if (result is double)
                                result = (BigDecimal)(double)(result);
                            result = (BigDecimal)result + (BigDecimal)a * (BigDecimal)a;

                        }
                    }

                    return result;
                }

                public object Magnitude()
                {
                    object norm = this.Norm();
                    if (norm is System.Numerics.Complex)
                    {
                        return System.Numerics.Complex.Sqrt((System.Numerics.Complex)norm);
                    }
                    else if (norm is BigDecimal)
                    {
                        return (BigDecimal)Math.Sqrt((double)((BigDecimal)norm));
                    }
                    else if (norm is double)
                    {
                        return (BigDecimal)Math.Sqrt((double)(norm));
                    }
                    else
                    {
                        return BigDecimal.Undefined;
                    }
                }

                /// <summary>
                /// Retrieve the specified column as a column vector
                /// </summary>
                /// <returns></returns>
                public Matrix Col(int id)
                {
                    List<Reference> tmp = new List<Reference>();

                    for (int i = 0; i <= Height - 1; i++)
                    {
                        if (this._value[i].ResolveObj() is Matrix)
                        {
                            tmp.Add(((List<Reference>)this._value[i].Resolve())[id]);
                        }
                        else if (this._value[i].ResolveObj() is Reference)
                        {
                            tmp.Add((Reference)this._value[i].ResolveObj());
                        }
                        else
                        {
                            tmp.Add(new Reference(this._value[i].ResolveObj()));
                        }
                    }

                    return new Matrix(tmp);
                }

                /// <summary>
                /// Retrieve the specied row as a column vector
                /// </summary>
                /// <param name="id"></param>
                /// <returns></returns>
                public Matrix Row(int id)
                {
                    if (this._value[id].ResolveObj() is Matrix)
                    {
                        return (Matrix)this._value[id].ResolveObj();
                    }
                    else
                    {
                        return new Matrix(new[] { this._value[id].ResolveObj() });
                    }
                }

                /// <summary>
                /// Swap two matrix rows and return the matrix
                /// </summary>
                /// <param name="aug">Matrix representing right side of augmented matrix, if available</param>
                /// <returns></returns>
                public Matrix SwapRows(int a, int b, Matrix aug = null)
                {
                    Reference tmp = _value[a];
                    _value[a] = _value[b];
                    _value[b] = tmp;
                    if (aug != null)
                        aug.SwapRows(a, b);
                    return this;
                }

                /// <summary>
                /// Swap two matrix columns and return the matrix
                /// </summary>
                /// <returns></returns>
                public Matrix SwapCols(int a, int b)
                {
                    for (int i = 0; i <= Height - 1; i++)
                    {
                        if (this._value[i].ResolveObj() is Matrix)
                        {
                            Reference tmp = null;
                            tmp = ((List<Reference>)this._value[i].Resolve())[b];
                            ((List<Reference>)this._value[i].Resolve())[b] = ((List<Reference>)this._value[i].Resolve())[a];
                            ((List<Reference>)this._value[i].Resolve())[a] = tmp;
                        }
                        else
                        {
                            if (a != 0 || b != 0)
                                throw new MathException("Index is out of bounds");
                        }
                    }
                    return this;
                }

                /// <summary>
                /// Scale the specified row by the specified factor
                /// </summary>
                /// <param name="aug">Right side of augmented matrix, if applicable</param>
                public void ScaleRow(int row, object scale, Matrix aug = null)
                {
                    if (scale is double) scale = (BigDecimal)(double)scale;
                    for (int i = 0; i < Width; i++)
                    {
                        object orig = GetCoord(row, i);
                        if (scale is System.Numerics.Complex || orig is System.Numerics.Complex)
                        {
                            if (!(scale is System.Numerics.Complex))
                                scale = new System.Numerics.Complex((double)(scale), 0);
                            if (!(orig is System.Numerics.Complex))
                                orig = new System.Numerics.Complex((double)(orig), 0);
                            SetCoord(row, i, new Reference((System.Numerics.Complex)orig * (System.Numerics.Complex)scale));
                        }
                        else if (orig is double)
                        {
                            SetCoord(row, i, new Reference(((double)orig * (BigDecimal)scale * 1E15).Round()/1E15));
                        }
                        else if (orig is BigDecimal)
                        {
                            SetCoord(row, i, new Reference(((BigDecimal)orig * (BigDecimal)scale * 1E15).Round()/1E15));
                        }
                    }
                    if ((aug != null))
                        aug.ScaleRow(row, scale);
                }

                /// <summary>
                /// Subtract row b from row a and assign the values to row a
                /// </summary>
                /// <param name="aug">Right side of augmented matrix, if applicable</param>
                public void SubtractRow(int a, int b, object scale, Matrix aug = null)
                {
                    if (scale is double) scale = (BigDecimal)(double)scale;
                    for (int i = 0; i < Width; i++)
                    {
                        object av = GetCoord(a, i);
                        object bv = GetCoord(b, i);
                        if (av is double) av = (BigDecimal)(double)av;
                        if (bv is double) bv = (BigDecimal)(double)bv;

                        if (av is System.Numerics.Complex || bv is System.Numerics.Complex)
                        {
                            if (!(av is System.Numerics.Complex))
                                av = new System.Numerics.Complex((double)(BigDecimal)av, 0);
                            if (!(bv is System.Numerics.Complex))
                                bv = new System.Numerics.Complex((double)(BigDecimal)bv, 0);
                            if (!(scale is System.Numerics.Complex))
                                scale = new System.Numerics.Complex((double)(BigDecimal)scale, 0);

                            bv = ((System.Numerics.Complex)bv) * (System.Numerics.Complex)scale;
                            SetCoord(a, i, (System.Numerics.Complex)av - (System.Numerics.Complex)bv);
                        }
                        else if (av is double && bv is double)
                        {
                            if (scale is System.Numerics.Complex) scale = (BigDecimal)((System.Numerics.Complex)scale).Real;
                            bv = ((BigDecimal)bv) * (BigDecimal)scale;
                            SetCoord(a, i, new Reference((BigDecimal)(double)(av) - (double)(bv)));
                        }
                        else if (av is BigDecimal && bv is BigDecimal)
                        {
                            if (scale is System.Numerics.Complex) scale = (BigDecimal)((System.Numerics.Complex)scale).Real;
                            bv = ((BigDecimal)bv) * (BigDecimal)scale;
                            SetCoord(a, i, new Reference(((BigDecimal)av - (BigDecimal)bv)));
                        }
                    }
                    if ((aug != null))
                        aug.SubtractRow(a, b, scale);
                }

                /// <summary>
                /// Helper function for getting the reciprocal of various types
                /// </summary>
                /// <returns></returns>
                private object AutoReciprocal(object a)
                {
                    if (a is double)
                    {
                        return 1 / (BigDecimal)(double)(a);
                    }
                    else if (a is BigDecimal)
                    {
                        return 1 / (BigDecimal)a;
                    }
                    else if (a is System.Numerics.Complex)
                    {
                        return 1 / (System.Numerics.Complex)a;
                    }
                    else
                    {
                        return 1;
                    }
                }

                /// <summary>
                /// Find the reduced row echelon form of the matrix
                /// </summary>
                /// <param name="augmented">Matrix to modify along with the current matrix as an augmented matrix</param>
                /// <returns></returns>
                public Matrix Rref(Matrix augmented = null)
                {

                    // deep copy everything before doing anything to avoid messing up due to references
                    Matrix mat = (Matrix)this.GetDeepCopy();
                    mat.Normalize();

                    int lead = 0;

                    for (int r = 0; r < mat.Height; ++r)
                    {
                        if (lead >= mat.Width) break;
                        int i = r;
                        while (ObjectComparer.CompareObjs(mat.GetCoord(i, lead), (BigDecimal)0.0) == 0)
                        {
                            i++;
                            if (i == mat.Height)
                            {
                                i = r;
                                lead++;
                                if (mat.Width == lead)
                                {
                                    return mat;
                                }
                            }
                        }
                        mat.SwapRows(i, r, augmented);
                        object div = mat.GetCoord(r, lead);
                        mat.ScaleRow(r, AutoReciprocal(div), augmented);

                        for (i = 0; i < mat.Height; i++)
                        {
                            if (i != r)
                            {
                                object sub = mat.GetCoord(i, lead);
                                mat.SubtractRow(i, r, sub, augmented);
                            }
                        }
                        lead++;

                    }

                    for (int i = 0; i < mat.Height; ++i)
                    {
                        for (int j = 0; j < mat.Width; ++j)
                        {
                            object obj = mat.GetCoord(i, j);

                            if (obj is Reference) obj = ((Reference)obj).Resolve();
                            if (obj is double) obj = (BigDecimal)(double)obj;

                            if (obj is BigDecimal)
                                mat.SetCoord(i, j,
                                    ((((BigDecimal)obj).Truncate(19)) * 1E11).Round() / 1E11);
                        }
                    }

                    return mat;
                }

                /// <summary>
                /// Invert this matrix and return it
                /// </summary>
                /// <returns></returns>
                public Matrix Inverse()
                {
                    Matrix r = Matrix.IdentityMatrix(Height, Width);
                    Matrix l = (Matrix)this.GetDeepCopy();
                    l = l.Rref(r);

                    if (!l.IsIdentityMatrix())
                        return new Matrix(new object[] { double.NaN });

                    this._value = (List<Reference>)r.GetValue();
                    return this;
                }

                /// <summary>
                /// Exponentiate the matrix. Or, if the exponent is -1, inverts the matrix. 
                /// </summary>
                /// <param name="p">The exponent</param>
                /// <returns></returns>
                public Matrix Expo(int p)
                {
                    if (p == -1)
                        return Inverse();

                    if (p < 0)
                        throw new MathException("Negative exponents of matrices not defined (except -1" + "which is interpreted as matrix inverse)");
                    if (Width != Height)
                        throw new MathException("Only square matrices may be exponenciated.");

                    int curp = 2;
                    Matrix origmat = (Matrix)this.GetDeepCopy();

                    while (curp <= p)
                    {
                        this.Multiply(this);
                        curp *= 2;
                    }
                    curp /= 2;

                    while (curp < p)
                    {
                        this.Multiply(origmat);
                        curp += 1;
                    }

                    return this;
                }

                protected override EvalObjectBase DeepCopy()
                {
                    List<Reference> lst = new List<Reference>();
                    foreach (Reference r in _value)
                    {
                        lst.Add((Reference)r.GetDeepCopy());
                    }
                    return new Matrix(lst);
                }

                public static new bool IsType(object obj)
                {
                    return obj is Matrix || obj is System.Collections.Generic.IList<object> || obj is System.Collections.Generic.IList<Reference>;
                }

                public static new bool StrIsType(string str)
                {
                    str = str.Trim();
                    return str.StartsWith("[") && str.EndsWith("]");
                }

                /// <summary>
                /// Get the maximum length of a subitem in the matrix, used for conversion to a human readable string
                /// </summary>
                private int MaxItemLen(CantusEvaluator eval, Matrix m = null)
                {
                    if (m == null) m = this;

                    InternalFunctions ef = new InternalFunctions(eval);
                    int maxLen = 0;

                    int ct = 0;
                    foreach (Reference k in m._value)
                    {
                        if (k.GetRefObject() is Matrix)
                        {
                            maxLen = Math.Max(maxLen, MaxItemLen(eval, (Matrix)k.GetRefObject()));
                        }
                        else
                            maxLen = Math.Max(maxLen, ef.O(k.GetValue()).Length);
                        ++ct;
                    }

                    return maxLen;
                }

                /// <summary>
                /// Get the human readable string representation of the matrix
                /// </summary>
                private string StringRepr(int itemWid, CantusEvaluator eval, Matrix m = null)
                {
                    if (m == null) m = this;

                    StringBuilder str = new StringBuilder("[");

                    InternalFunctions ef = eval.Internals;

                    int ct = 0;
                    foreach (Reference k in m._value)
                    {
                        if (str.Length != 1)
                        {
                            str.Append(',');
                            if (m.Width > 1) str.Append(" \\\n ");
                            else str.Append(' ');
                        }
                        string tostr = "";
                        EvalObjectBase ro = k.GetRefObject();
                        if (ro is Matrix)
                        {
                            tostr = StringRepr(itemWid, eval, (Matrix)ro);
                        }
                        else if (ro is Number)
                        {
                            tostr = ef.O(((Number)ro).BigDecValue());
                        }
                        else
                        {
                            tostr = ef.O(k.GetValue());
                        }

                        str.Append(tostr.PadRight(itemWid));
                        ++ct;
                    }
                    str.Append("]");

                    return str.ToString();
                }

                public string Readable
                {
                    get
                    {
                        return ToString(new CantusEvaluator(reloadDefault:false));
                    }
                }
                public override string ToString(CantusEvaluator eval)
                {
                    int maxLen = MaxItemLen(eval);
                    return StringRepr(maxLen, eval);
                }

                public override bool Equals(EvalObjectBase other)
                {
                    if (other is Matrix)
                    {
                        return ObjectComparer.CompareLists(_value, (List<Reference>)other.GetValue()) == 0;
                    }
                    else
                    {
                        return false;
                    }
                }

                /// <summary>
                /// Make this a proper matrix by making all the rows and columns the same length, respectively
                /// </summary>
                public void Normalize()
                {
                    int max_len = 0;
                    foreach (Reference r in _value)
                    {
                        if (r.GetRefObject() is Matrix)
                        {
                            int ct = ((List<Reference>)((Matrix)r.GetRefObject()).GetValue()).Count;
                            if (ct > max_len)
                                max_len = ct;
                        }
                        else
                        {
                            if (1 > max_len)
                                max_len = 1;
                        }
                    }
                    Resize(Height, max_len);
                }

                /// <summary>
                /// Resize the matrix by cropping out extra rows/columns and adding empty rows/columns filled with zeros as necessary
                /// </summary>
                public void Resize(Size size)
                {
                    Resize(size.Height, size.Width);
                }

                /// <summary>
                /// Resize the matrix by cropping out extra rows/columns and adding empty rows/columns filled with zeros as necessary
                /// </summary>
                public void Resize(int height, int width)
                {
                    _width = width;

                    // fit height
                    while (this.Height < height)
                    {
                        _value.Add(new Reference(0.0));
                    }
                    while (this.Height > height)
                    {
                        _value.RemoveAt(_value.Count - 1);
                    }

                    // fit width
                    for (int i = 0; i <= height - 1; i++)
                    {
                        Reference r = _value[i];
                        if (width > 1)
                        {
                            if (!(r.ResolveObj() is Matrix))
                            {
                                r.SetValue(new Matrix(new[] { r.GetValue() }));
                            }
                            List<Reference> inner = (List<Reference>)((Matrix)r.GetRefObject()).GetValue();
                            while (inner.Count < width)
                            {
                                inner.Add(new Reference(0.0));
                            }
                            while (inner.Count > width)
                            {
                                inner.RemoveAt(inner.Count - 1);
                            }
                        }
                        else if (width == 1)
                        {
                            // if single column, expand to column vector
                            if (r.ResolveObj() is Matrix)
                            {
                                List<Reference> lst = (List<Reference>)r.Resolve();
                                if (lst.Count == 0)
                                    r.SetValue(double.NaN);
                                else
                                    r.SetValue(lst[0].ResolveObj());
                            }
                        }
                    }
                }

                /// <summary>
                /// Returns true if the current matrix represents an identity matrix
                /// </summary>
                public bool IsIdentityMatrix()
                {
                    if (Height != Width) return false;

                    for (int i = 0; i <= Height - 1; i++)
                    {
                        for (int j = 0; j <= Width - 1; j++)
                        {
                            int expected = ((i == j) ? 1 : 0);

                            object obj = GetCoord(i, j);

                            if (obj is System.Numerics.Complex)
                            {
                                if (Math.Round(((System.Numerics.Complex)obj).Magnitude, 12) != expected)
                                    return false;
                            }
                            else if (obj is double || obj is BigDecimal)
                            {
                                BigDecimal epsi = 1e-4;
                                if (BigDecimal.Abs((((BigDecimal)obj).Truncate(12) - expected)) > epsi)
                                    return false;
                            }
                        }
                    }
                    return true;
                }

                /// <summary>
                /// Get the identity matrix (matrix with all zeros except on the main diagonal, which contains all ones) 
                /// with the specified number of rows and cols (if cols is not specified, a square matrix is returned)
                /// </summary>
                public static Matrix IdentityMatrix(int rows, int cols = -1)
                {
                    if (cols == -1)
                        cols = rows;

                    Matrix mat = new Matrix(rows, cols);
                    for (int i = 0; i <= Math.Min(rows, cols) - 1; i++)
                    {
                        mat.SetCoord(i, i, 1.0);
                    }
                    return mat;
                }

                /// <summary>
                /// Create a new matrix with the specified number of rows and columns, filled with 0.
                /// If cols is not specified, then a square matrix is returned.
                /// </summary>
                public Matrix(int rows, int cols = -1)
                {
                    if (cols == -1)
                        cols = rows;
                    this._value = new List<Reference>(rows);
                    this.Resize(rows, cols);
                }

                /// <summary>
                /// Create a new matrix from a list of references
                /// </summary>
                public Matrix(IEnumerable<Reference> value)
                {
                    if (value is List<Reference>)
                    {
                        this._value = (List<Reference>)value;
                    }
                    else
                    {
                        this._value = value.ToList();
                    }
                    this.Normalize();
                }

                /// <summary>
                /// Create a new matrix from a list of evaluator objects
                /// </summary>
                public Matrix(IEnumerable<EvalObjectBase> value)
                {
                    this._value = new List<Reference>();
                    foreach (EvalObjectBase v in value)
                    {
                        if (v is Reference)
                        {
                            this._value.Add((Reference)v);
                        }
                        else
                        {
                            this._value.Add(new Reference(v));
                        }
                    }
                    this.Normalize();
                }

                /// <summary>
                /// Create a new matrix from a list of system objects
                /// </summary>
                public Matrix(IEnumerable<object> value)
                {
                    this._value = new List<Reference>();
                    foreach (object v in value)
                    {
                        this._value.Add(new Reference(ObjectTypes.DetectType(v, true)));
                    }
                    this.Normalize();
                }

                /// <summary>
                /// Create a new matrix from a string in matrix format: [[1,2,3],[2,3,4],[3,4,5]]
                /// </summary>
                public Matrix(string str, CantusEvaluator eval)
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder(str.Trim());
                        //if (StrIsType(str))
                        //    sb = sb.Remove(str.Length - 1, 1).Remove(0, 1);

                        this._value = new List<Reference>();
                        if (sb.Length != 0)
                        {
                            // add zeros to fill blanks for convenience
                            sb.Insert(0, ",");
                            sb.Append(",");
                            while (sb.ToString().Contains(", "))
                            {
                                // prepare so that we can detect ,,'s later
                                sb = sb.Replace(", ", ",");
                            }
                            while (sb.ToString().Contains(",,"))
                            {
                                // add zeros to fill blanks for convenience
                                sb = sb.Replace(",,", ",0,");
                            }

                            // ignore hanging commas
                            if (sb.Length > 0 && sb[sb.Length-1] == ',')
                                sb = sb.Remove(sb.Length - 1,1);
                            if (sb.Length > 0 && sb[0] == ',')
                                sb = sb.Remove(0, 1);

                            object res = eval.EvalExprRaw("0," + sb.ToString(), true, true);

                            if (Tuple.IsType(res))
                            {
                                Reference[] lst = (Reference[])res;
                                for (int i = 1; i < lst.Length; ++i)
                                {
                                    this._value.Add(lst[i]);
                                }
                            }
                            else
                            {
                                this._value.Add(new Reference(res));
                            }
                        }
                        this.Normalize();
                        //ex As Exception
                    }
                    catch
                    {
                    }
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }
            }

            /// <summary>
            /// A dictionary/set of objects
            /// </summary>
            public sealed class Set : EvalObjectBase
            {
                private System.Collections.Generic.SortedDictionary<Reference, Reference> _value;
                public override object GetValue()
                {
                    return _value;
                }
                public override void SetValue(object obj)
                {
                    _value = (SortedDictionary<Reference, Reference>)obj;
                }
                public static new bool IsType(object obj)
                {
                    return obj is Set || obj is System.Collections.Generic.SortedDictionary<Reference, Reference>;
                }
                public static new bool StrIsType(string str)
                {
                    str = str.Trim();
                    return str.StartsWith("{") && str.EndsWith("}");
                }

                protected override EvalObjectBase DeepCopy()
                {
                    SortedDictionary<Reference, Reference> dict = new SortedDictionary<Reference, Reference>(new ObjectComparer());
                    foreach (KeyValuePair<Reference, Reference> k in _value)
                    {
                        Reference key = (Reference)k.Key.GetDeepCopy();
                        if (k.Value == null)
                        {
                            dict[key] = null;
                        }
                        else
                        {
                            dict[key] = (Reference)k.Value.GetDeepCopy();
                        }
                    }
                    return new Set(dict);
                }

                private void ConvertFrom(EvalObjectBase obj)
                {
                    if (obj is Set)
                    {
                        _value = new SortedDictionary<Reference, Reference>((SortedDictionary<Reference, Reference>)obj.GetValue(), new ObjectComparer());
                    }
                    else if (obj is Matrix)
                    {
                        _value = new SortedDictionary<Reference, Reference>(new ObjectComparer());
                        foreach (Reference o in (List<Reference>)obj.GetValue())
                        {
                            _value[o] = null;
                        }
                    }
                    else
                    {
                        _value = new SortedDictionary<Reference, Reference>(new ObjectComparer());
                        _value[new Reference(obj)] = null;
                    }
                }

                public override string ToString(CantusEvaluator eval)
                {
                    StringBuilder str = new StringBuilder("{");
                    foreach (KeyValuePair<Reference, Reference> k in _value)
                    {
                        if (!(str.Length == 1))
                            str.Append(", ");
                        InternalFunctions ef = eval.Internals;
                        EvalObjectBase ko = k.Key.GetRefObject();
                        str.Append(ko is Number ? ef.O(((Number)ko).BigDecValue()) : ef.O(k.Key.GetValue()));
                        if ((k.Value != null))
                        {
                            EvalObjectBase ro = k.Value.GetRefObject();
                            str.Append(":" + (ro is Number ? ef.O(((Number)ro).BigDecValue()) :
                                ef.O(ro.GetValue())));
                        }
                    }
                    str.Append("}");
                    return str.ToString();
                }

                public override bool Equals(EvalObjectBase other)
                {
                    return false;
                }

                public Set(System.Collections.Generic.IEnumerable<Reference> value)
                {
                    this.ConvertFrom(new Matrix(value));
                }
                public Set(System.Collections.Generic.IDictionary<Reference, Reference> value)
                {
                    this._value = new SortedDictionary<Reference, Reference>(value, new ObjectComparer());
                }
                public Set(System.Collections.Generic.IDictionary<object, object> value)
                {
                    this._value = new SortedDictionary<Reference, Reference>();
                    foreach (KeyValuePair<object, object> k in value)
                    {
                        this._value[new Reference(ObjectTypes.DetectType(k.Key, true))] = new Reference(ObjectTypes.DetectType(k.Value, true));
                    }
                }
                public Set(string str, CantusEvaluator eval)
                {
                    try
                    {
                        if (StrIsType(str)) str = str.Trim().Remove(str.Length - 1).Substring(1).Trim(',');
                        this._value = new SortedDictionary<Reference, Reference>(new ObjectComparer());
                        if (string.IsNullOrWhiteSpace(str))
                            return;
                        List<Reference> lst = new List<Reference>((Reference[])new Tuple("(" + str + ")", eval).GetValue());
                        foreach (EvalObjectBase obj in lst)
                        {
                            EvalObjectBase o = obj;
                            if (o is Reference)
                                o = ((Reference)o).GetRefObject();
                            if (Tuple.IsType(o))
                            {
                                Reference[] innerlst = (Reference[])o.GetValue();
                                if (innerlst.Count() == 2)
                                {
                                    this._value[innerlst[0]] = innerlst[1];
                                    continue;
                                }
                            }
                            this._value[new Reference(o)] = null;
                        }
                        //ex As Exception
                    }
                    catch
                    {
                    }
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }
            }

            /// <summary>
            /// A hashed dictionary/set of objects
            /// </summary>
            public sealed class HashSet : EvalObjectBase
            {
                private System.Collections.Generic.Dictionary<Reference, Reference> _value;
                public override object GetValue()
                {
                    return _value;
                }
                public override void SetValue(object obj)
                {
                    _value = (Dictionary<Reference, Reference>)obj;
                }
                public static new bool IsType(object obj)
                {
                    return obj is HashSet || obj is System.Collections.Generic.Dictionary<Reference, Reference>;
                }
                public static new bool StrIsType(string str)
                {
                    str = str.Trim();
                    return str.StartsWith("HashSet(new[]{") && str.EndsWith("})");
                }

                protected override EvalObjectBase DeepCopy()
                {
                    Dictionary<Reference, Reference> dict = new Dictionary<Reference, Reference>(new ObjectComparer());
                    foreach (KeyValuePair<Reference, Reference> k in _value)
                    {
                        Reference key = (Reference)k.Key.GetDeepCopy();
                        if (k.Value == null)
                        {
                            dict[key] = null;
                        }
                        else
                        {
                            dict[key] = (Reference)k.Value.GetDeepCopy();
                        }
                    }
                    return new HashSet(dict);
                }

                private void ConvertFrom(EvalObjectBase obj)
                {
                    if (obj is Set)
                    {
                        _value = new Dictionary<Reference, Reference>((Dictionary<Reference, Reference>)obj.GetValue(), new ObjectComparer());
                    }
                    else if (obj is Matrix)
                    {
                        _value = new Dictionary<Reference, Reference>(new ObjectComparer());
                        foreach (Reference o in (List<Reference>)obj.GetValue())
                        {
                            _value[o] = null;
                        }
                    }
                    else
                    {
                        _value = new Dictionary<Reference, Reference>(new ObjectComparer());
                        _value[new Reference(obj)] = null;
                    }
                }

                public override string ToString(CantusEvaluator eval)
                {
                    StringBuilder str = new StringBuilder("HashSet({");
                    foreach (KeyValuePair<Reference, Reference> k in _value)
                    {
                        if (!(str[str.Length - 1] == '{'))
                            str.Append(", ");
                        InternalFunctions ef = eval.Internals;
                        EvalObjectBase ko = k.Key.GetRefObject();
                        str.Append(ko is Number ? ef.O(((Number)ko).BigDecValue()) :
                            ef.O(ko.GetValue()));
                        if ((k.Value != null))
                        {
                            EvalObjectBase ro = k.Value.GetRefObject();
                            str.Append(":" + (ro is Number ? ef.O(((Number)ro).BigDecValue()) :
                                ef.O(ro.GetValue())));
                        }
                    }
                    str.Append("})");
                    return str.ToString();
                }

                public override bool Equals(EvalObjectBase other)
                {
                    return false;
                }

                public HashSet(System.Collections.Generic.IEnumerable<Reference> value)
                {
                    this.ConvertFrom(new Matrix(value));
                }
                public HashSet(System.Collections.Generic.IDictionary<Reference, Reference> value)
                {
                    this._value = new Dictionary<Reference, Reference>(value, new ObjectComparer());
                }
                public HashSet(System.Collections.Generic.IDictionary<object, object> value)
                {
                    this._value = new Dictionary<Reference, Reference>();
                    foreach (KeyValuePair<object, object> k in value)
                    {
                        this._value[new Reference(ObjectTypes.DetectType(k.Key, true))] = new Reference(ObjectTypes.DetectType(k.Value, true));
                    }
                }
                public HashSet(string str, CantusEvaluator eval)
                {
                    try
                    {
                        if (StrIsType(str)) str = str.Trim().Remove(str.Length - 2).Substring("HashSet({".Length).Trim(',');
                        this._value = new Dictionary<Reference, Reference>(new ObjectComparer());
                        if (string.IsNullOrWhiteSpace(str))
                            return;
                        List<Reference> lst = new List<Reference>((Reference[])new Tuple("(" + str + ")", eval).GetValue());
                        foreach (EvalObjectBase obj in lst)
                        {
                            EvalObjectBase o = obj;
                            if (o is Reference)
                                o = ((Reference)o).GetRefObject();
                            if (Tuple.IsType(o))
                            {
                                Reference[] innerlst = (Reference[])o.GetValue();
                                if (innerlst.Count() == 2)
                                {
                                    this._value[innerlst[0]] = innerlst[1];
                                    continue;
                                }
                            }
                            this._value[new Reference(o)] = null;
                        }
                    }
                    //ex As Exception
                    catch
                    {
                    }
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }
            }

            /// <summary>
            /// A linked list of objects
            /// </summary>
            public sealed class LinkedList : EvalObjectBase
            {

                private LinkedList<Reference> _value;
                private int _index;
                public int Index
                {
                    get
                    {
                        return Index;
                    }
                }
                public int Count
                {
                    get { return _value.Count; }
                }


                private LinkedListNode<Reference> _node;
                public override object GetValue()
                {
                    return _value;
                }

                public override void SetValue(object obj)
                {
                    this._value = new LinkedList<Reference>();
                    foreach (Reference r in (LinkedList<Reference>)obj)
                    {
                        this._value.AddLast(new Reference(r));
                    }
                    GoToFirst();
                }

                /// <summary>
                /// Go to the first item in the linked list
                /// </summary>
                public void GoToFirst()
                {
                    _index = 0;
                    if (Count == 0)
                        return;
                    _node = _value.First;
                }

                /// <summary>
                /// Go to the last item in the linked list
                /// </summary>
                public void GoToLast()
                {
                    _index = 0;
                    if (Count == 0)
                        return;
                    _node = _value.Last;
                    _index = _value.Count - 1;
                }

                /// <summary>
                /// Go to the next item in the linked list
                /// </summary>
                public void Next()
                {
                    if (Count == 0)
                        return;
                    if (_index < _value.Count - 1)
                    {
                        _node = _node.Next;
                        _index += 1;
                    }
                }

                /// <summary>
                /// Go to the previous item in the linked list
                /// </summary>
                public void Previous()
                {
                    if (Count == 0)
                        return;
                    if (_index > 0)
                    {
                        _node = _node.Previous;
                        _index -= 1;
                    }
                }

                /// <summary>
                /// Remove the current item from the linked list
                /// </summary>
                public void Remove()
                {
                    if (Count > 0)
                    {
                        _value.Remove(_node);
                        if (Count > 0)
                            _node = _node.Next;
                    }
                }

                /// <summary>
                /// Remove the last item from the linked list
                /// </summary>
                public void RemoveLast()
                {
                    if (Count > 0)
                        _value.RemoveLast();
                }

                /// <summary>
                /// Remove the first item from the linked list
                /// </summary>
                public void RemoveFirst()
                {
                    if (Count > 0)
                        _value.RemoveFirst();
                }

                /// <summary>
                /// Get the current item in the linked list
                /// </summary>
                public Reference Current()
                {
                    return _node.Value;
                }

                public static new bool IsType(object obj)
                {
                    return obj is LinkedList || obj is System.Collections.Generic.LinkedList<Reference>;
                }

                public static new bool StrIsType(string str)
                {
                    str = str.Trim();
                    return str.StartsWith("linkedlist(") && str.EndsWith(")");
                }

                protected override EvalObjectBase DeepCopy()
                {
                    LinkedList<Reference> lst = new LinkedList<Reference>();
                    foreach (Reference r in _value)
                    {
                        lst.AddLast((Reference)r.GetDeepCopy());
                    }
                    return new LinkedList(lst);
                }

                public override string ToString(CantusEvaluator eval)
                {
                    StringBuilder str = new StringBuilder("linkedlist([");
                    bool init = true;
                    foreach (Reference r in _value)
                    {
                        if (!init)
                            str.Append(", ");
                        else
                            init = false;
                        InternalFunctions ef = eval.Internals;
                        EvalObjectBase ro = r.GetRefObject();
                        str.Append(ro is Number ? ef.O(((Number)ro).BigDecValue()) :
                            ef.O(ro.GetValue()));

                    }
                    str.Append("])");
                    return str.ToString();
                }

                public override bool Equals(EvalObjectBase other)
                {
                    return false;
                }

                public LinkedList(System.Collections.Generic.IList<object> value)
                {
                    this._value = new LinkedList<Reference>();
                    foreach (object obj in value)
                    {
                        this._value.AddLast(new Reference(obj));
                    }
                    GoToFirst();
                }

                public LinkedList(System.Collections.Generic.IList<Reference> value)
                {
                    this._value = new LinkedList<Reference>();
                    foreach (Reference r in value)
                    {
                        this._value.AddLast(new Reference(r));
                    }
                    GoToFirst();
                }

                public LinkedList(System.Collections.Generic.LinkedList<Reference> value)
                {
                    SetValue(value);
                }

                public override int GetHashCode()
                {
                    return _value.GetHashCode();
                }

                // to create from text, just use the normal linkedlist() internal function
            }

            /// <summary>
            /// A message to be sent through the evaluator
            /// </summary>
            public sealed class SystemMessage : EvalObjectBase
            {
                public enum MessageType
                {
                    /// Defer messages may be returned by operator definitions to indicate that
                    ///   the evaluator should look for an overloaded operator at a lower precedence.
                    /// For example:
                    /// = means comparison -> comparison precedence
                    /// a = 3 will not resolve because it is an assignment operation
                    /// Thus, it is deferred to the assignment operator at assignment precedence
                    defer
                }
                public MessageType Type { get; set; }
                public object Content { get; set; }
                public override bool Equals(EvalObjectBase other)
                {
                    return other is SystemMessage && ((SystemMessage)other).Type == Type && Content == ((SystemMessage)other).Content;
                }

                public override int GetHashCode()
                {
                    return Content.GetHashCode();
                }

                public override object GetValue()
                {
                    return Content;
                }

                public override void SetValue(object obj)
                {
                    Content = obj;
                }

                protected override EvalObjectBase DeepCopy()
                {
                    return new SystemMessage(Type, Content);
                }

                public new static bool IsType(object obj)
                {
                    return obj is SystemMessage;
                }

                public new static bool StrIsType(string str)
                {
                    return false;
                }

                public override string ToString(CantusEvaluator eval)
                {
                    return Type.ToString();
                }

                /// <summary>
                /// Create a new system message with the given type and content.
                /// Currently, only 'defer' messages are supported.
                /// These messages may be returned by operator definitions to indicate that
                ///   the evaluator should look for an overloaded operator at a lower precedence.
                /// For example:
                /// = means comparison -> comparison precedence
                /// a = 3 will not resolve because it is an assignment operation
                /// Thus, it is deferred to the assignment operator at assignment precedence
                /// </summary>
                public SystemMessage(MessageType type, object content = null)
                {
                    this.Type = type;
                    this.Content = content;
                }
            }

            /// <summary>
            /// A reference to another object
            /// </summary>
            public sealed class Reference : EvalObjectBase
            {

                private LinkedListNode<Reference> _node;
                /// <summary>
                /// The linked list node that this refers to. Only used for linked lists.
                /// </summary>
                public LinkedListNode<Reference> Node
                {
                    get
                    {
                        return _node;
                    }
                }

                private EvalObjectBase _value;
                /// <summary>
                /// Get the value of the object pointed to by the reference
                /// </summary>
                /// <returns></returns>
                public override object GetValue()
                {
                    if (_value == null)
                        return double.NaN;
                    return _value.GetValue();
                }

                /// <summary>
                /// Get the object pointed to by the reference
                /// </summary>
                /// <returns></returns>
                public EvalObjectBase GetRefObject()
                {
                    return _value;
                }

                /// <summary>
                /// Get the final value pointed to by the reference, resolving any multiple indirection
                /// </summary>
                /// <returns></returns>
                public object Resolve()
                {
                    object res = _value;
                    while (res is Reference && (!object.ReferenceEquals(res, ((Reference)res).GetValue())))
                    {
                        res = ((Reference)res).GetValue();
                    }
                    if (res == null)
                        return double.NaN;
                    if (res is EvalObjectBase)
                    {
                        if (res is Number)
                            res = ((Number)res).BigDecValue();
                        else
                            res = ((EvalObjectBase)res).GetValue();
                    }
                    return res;
                }

                /// <summary>
                /// Get the final evaluator type value pointed to by the reference, resolves any multiple indirection
                /// </summary>
                /// <returns></returns>
                public EvalObjectBase ResolveObj()
                {
                    EvalObjectBase res = _value;
                    while (res is Reference)
                    {
                        res = ((Reference)res).GetRefObject();
                    }
                    return res;
                }

                /// <summary>
                /// Resolves any multiple indirection and returns the child reference that does not point at another reference
                /// </summary>
                /// <returns></returns>
                public Reference ResolveRef()
                {
                    EvalObjectBase res = _value;
                    if (!(res is Reference))
                        return this;
                    while (((Reference)res).GetRefObject() is Reference)
                    {
                        res = ((Reference)res).GetRefObject();
                    }
                    return (Reference)res;
                }

                /// <summary>
                /// Set the value pointed to by the reference, resolving any multiple indirection
                /// </summary>
                public override void SetValue(object obj)
                {
                    this.ResolveRef().SetRefObj(obj);
                }

                /// <summary>
                /// Set the linked list node pointed to by the reference and the value to the value of that node
                /// </summary>
                public void SetNode(LinkedListNode<Reference> node)
                {
                    this._node = node;
                    this._value = node.Value.ResolveObj();
                }

                /// <summary>
                /// Adds an item after the node in the linked list, if available
                /// </summary>
                public void NodeAddAfter(Reference value)
                {
                    this._node.List.AddAfter(_node, value);
                }

                /// <summary>
                /// Adds an item before the node in the linked list, if available
                /// </summary>
                public void NodeAddBefore(Reference value)
                {
                    this._node.List.AddBefore(_node, value);
                }

                /// <summary>
                /// Create a new reference containing a new copy of the referenced object
                /// </summary>
                /// <returns></returns>
                protected override EvalObjectBase DeepCopy()
                {
                    EvalObjectBase obj = _value.GetDeepCopy();
                    if (obj is Reference)
                    {
                        return obj;
                    }
                    else
                    {
                        return new Reference(obj);
                    }
                }


                /// <summary>
                /// Set the value pointed to by the reference
                /// </summary>
                /// <param name="obj"></param>
                public void SetRefObj(object obj)
                {
                    if ((_value != null) && _value.GetValue().GetType() == obj.GetType())
                    {
                        _value.SetValue(obj);
                    }
                    else
                    {
                        _value = ObjectTypes.DetectType(obj, true);
                    }
                }

                // linkedlist special stuff
                /// <summary>
                /// If this node is within a linked list, then this moves this reference forwards in the list
                /// </summary>
                public void NodeNext()
                {
                    try
                    {
                        if ((Node != null))
                        {
                            _node = Node.Next;
                            _value = Node.Value.GetRefObject();
                        }
                    }
                    catch
                    {
                        _value = new Number(double.NaN);
                    }
                }

                /// <summary>
                /// If this node is within a linked list, then this moves this reference backwards in the list
                /// </summary>
                public void NodePrevious()
                {
                    try
                    {
                        if ((Node != null))
                        {
                            _node = Node.Previous;
                            _value = Node.Value.GetRefObject();
                        }
                    }
                    catch
                    {
                        _value = new Number(double.NaN);
                    }
                }

                /// <summary>
                /// If this node is within a linked list, then this moves this reference to the front of the list
                /// </summary>
                public void NodeFirst()
                {
                    try
                    {
                        if ((Node != null))
                        {
                            while ((Node.Previous != null))
                            {
                                _node = Node.Previous;
                            }
                            _value = Node.Value.GetRefObject();
                        }
                    }
                    catch
                    {
                        _value = new Number(double.NaN);
                    }
                }

                /// <summary>
                /// If this node is within a linked list, then this moves this reference to the back of the list
                /// </summary>
                public void NodeLast()
                {
                    try
                    {
                        if ((Node != null))
                        {
                            while ((Node.Next != null))
                            {
                                _node = Node.Next;
                            }
                            _value = Node.Value.GetRefObject();
                        }
                    }
                    catch
                    {
                        _value = new Number(double.NaN);
                    }
                }

                /// <summary>
                /// If this node is within a linked list, then this removes the node referenced
                /// </summary>
                public void NodeRemove()
                {
                    try
                    {
                        if ((Node != null))
                        {
                            LinkedListNode<Reference> tmp = _node.Next;
                            _node.List.Remove(Node);
                            _node = tmp;
                            _value = Node.Value.GetRefObject();
                        }
                    }
                    catch
                    {
                        _value = new Number(double.NaN);
                    }
                }

                /// <summary>
                /// If this node is within a linked list, then gets the list linked to by the node
                /// </summary>
                public LinkedList<Reference> NodeList()
                {
                    try
                    {
                        return Node.List;
                    }
                    catch
                    {
                        return null;
                    }
                }

                public static new bool IsType(object obj)
                {
                    return obj is Reference || obj is double || obj is BigDecimal;
                }
                public static new bool StrIsType(string str)
                {
                    return false;
                }

                public override string ToString(CantusEvaluator eval)
                {
                    if (_value == null || object.ReferenceEquals(_value, this))
                        return "Undefined";
                    return _value.ToString(eval);
                }
                public override bool Equals(EvalObjectBase other)
                {
                    return _value.Equals(other);
                }

                public Reference(EvalObjectBase value)
                {
                    this._node = null;
                    this._value = value;
                }

                public Reference(object value)
                {
                    this._node = null;
                    this._value = ObjectTypes.DetectType(value, true);
                }

                public Reference(LinkedListNode<Reference> node)
                {
                    this._node = node;
                    this._value = new Reference(node.Value);
                }

                public override int GetHashCode()
                {
                    return this._value.GetHashCode();
                }
            }

            /// <summary>
            /// A lambda function/function pointer
            /// </summary>
            public sealed class Lambda : EvalObjectBase
            {

                private IEnumerable<string> _args;
                public IEnumerable<string> Args
                {
                    get
                    {
                        return _args;
                    }
                }

                public string _value;

                /// <summary>
                /// True if this lambda expression is considered to be a function pointer
                /// </summary>
                internal bool IsFunctionPtr { get; set; } = false;

                /// <summary>
                /// True if this lambda expression is a flattened function pointer
                /// </summary>
                internal bool IsFlattened { get; set; } = false;

                /// <summary>
                /// The name of the function this is pointing to
                /// </summary>
                internal string FunctionName { get; set; } = "";

                /// <summary>
                /// Returns self
                /// </summary>
                /// <returns></returns>
                public override object GetValue()
                {
                    return this;
                }

                /// <summary>
                /// Get the value of the lambda expression
                /// </summary>
                public string GetExpr()
                {
                    return _value;
                }

                public override void SetValue(object obj)
                {
                    if (obj is Lambda)
                    {
                        Lambda lamb = (Lambda)obj;
                        if (lamb.IsFunctionPtr)
                        {
                            if (lamb.IsFlattened)
                            {
                                this.SetLambdaExprRaw(lamb.GetExpr());
                                this._args = lamb.Args;
                                this.IsFlattened = true;
                                this.IsFunctionPtr = true;
                            }
                            else
                            {
                                this.SetFunctionPtr(lamb.FunctionName, lamb.Args);
                                this.FunctionName = lamb.FunctionName;
                                this.IsFlattened = false;
                            }
                        }
                        else
                        {
                            this.SetLambdaExprRaw(lamb.ToString(new CantusEvaluator(reloadDefault:false)));
                        }
                    }
                    else
                    {
                        this.SetLambdaExprRaw(obj.ToString());
                    }
                }

                /// <summary>
                /// Run this function on the specified evaluator,
                /// automatically parsing the text in the format 1,2,a=3,b=4 as arguments
                /// and return the result
                /// </summary>
                public object Execute(CantusEvaluator eval, string inner, string executingScope = "")
                {
                    List<object> args = new List<object>();
                    Dictionary<string, object> kwargs = new Dictionary<string, object>();

                    int lastIdx = 0;
                    for (int i = 0; i <= inner.Length; ++i)
                    {
                        char c = ',';
                        if (i < inner.Length)
                        {
                            c = inner[i];
                            if (eval.OperatorRegistar.OperatorExists(c.ToString()))
                            {
                                OperatorRegistar.Operator op = eval.OperatorRegistar.OperatorWithSign(c.ToString());
                                if (op is OperatorRegistar.Bracket &&
                                    ((OperatorRegistar.Bracket)op).OpenBracket == c.ToString())
                                {
                                    i += ((OperatorRegistar.Bracket)op).FindCloseBracket(
                                        inner.Substring(i + 1), eval.OperatorRegistar) +
                                        ((OperatorRegistar.Bracket)op).CloseBracket.Length;
                                    continue;
                                }
                            }
                        }

                        if (c == ',')
                        {
                            string lastSect = inner.Substring(lastIdx, i - lastIdx).Trim();

                            lastIdx = i + 1;
                            string optVar = "";
                            if (lastSect.Contains(":=") &&
                                IsValidIdentifier(lastSect.Remove(lastSect.IndexOf(":=")).Trim()))
                            {
                                optVar = lastSect.Remove(lastSect.IndexOf(":="));
                                if (lastSect.IndexOf(":=") + 2 == lastSect.Length)
                                {
                                    lastSect = "";
                                }
                                else
                                {
                                    lastSect = lastSect.Substring(lastSect.IndexOf(":=") + 2);
                                }
                            }

                            object resObj = eval.EvalExprRaw(lastSect, true, true);
                            if (!string.IsNullOrEmpty(optVar))
                            {
                                kwargs[optVar] = resObj;
                            }
                            else
                            {
                                args.Add(DetectType(resObj));
                            }
                        }
                    }
                    if(args.Count == 1 && kwargs.Count == 0 &&
                        args[0] is Tuple)
                    {
                        args = ((object[])((Tuple)args[0]).GetValue()).ToList(); 
                    }
                    return Execute(eval, args, kwargs, executingScope);
                }

                /// <summary>
                /// Run this function on the specified evaluator and return the result
                /// </summary>
                public object Execute(CantusEvaluator eval, IEnumerable<object> args,
                    IDictionary<string, object> kwargs = null, string executingScope = "")
                {
                    CantusEvaluator tmpEval = eval.SubEvaluator();
                    if (!string.IsNullOrEmpty(executingScope))
                        tmpEval.Scope = executingScope;

                    if (IsFunctionPtr)
                    {
                        string fnName = FunctionName;
                        if (eval.UserFunctions.ContainsKey(fnName))
                        {
                            UserFunction uf = eval.GetUserFunction(fnName);
                            for (int i = 0; i < uf.Defaults.Count; ++i)
                            {
                                if (uf.Defaults[i] == null) continue;
                                tmpEval.SetVariable(_args.ElementAt(i), uf.Defaults[i]);
                            }
                        }
                    }

                    for (int i = 0; i < Math.Min(_args.Count(), args.Count()); i++)
                    {
                        tmpEval.SetVariable(_args.ElementAt(i), args.ElementAt(i));
                    }

                    if (kwargs != null)
                    {
                        foreach (string k in kwargs.Keys)
                        {
                            tmpEval.SetVariable(k, kwargs[k]);
                        }
                    }

                    for (int i = args.Count(); i < _args.Count(); ++i)
                    {
                        if (!tmpEval.HasVariable(_args.ElementAt(i)))
                            throw new EvaluatorException("(Lambda): " + _args.Count() + " parameters expected");
                    }

                    object res = tmpEval.EvalRaw(_value, noSaveAns: true);

                    if (res is Reference && !(((Reference)res).GetRefObject() is Reference))
                    {

                        Reference @ref = (Reference)res;
                        return @ref.GetRefObject();
                    }
                    else
                    {
                        return res;
                    }
                }

                public int ExecuteAsync(CantusEvaluator eval, IEnumerable<object> args,
                   IDictionary<string, object> kwargs = null, Lambda callBack = null, string executingScope = "")
                {
                    CantusEvaluator tmpEval = eval.SubEvaluator();
                    if (!string.IsNullOrEmpty(executingScope))
                        tmpEval.Scope = executingScope;

                    if (IsFunctionPtr)
                    {
                        string fnName = FunctionName;
                        if (eval.UserFunctions.ContainsKey(fnName))
                        {
                            UserFunction uf = eval.GetUserFunction(fnName);
                            for (int i = 0; i < uf.Defaults.Count; ++i)
                            {
                                if (uf.Defaults[i] == null) continue;
                                tmpEval.SetVariable(_args.ElementAt(i), uf.Defaults[i]);
                            }
                        }
                    }

                    for (int i = 0; i <= _args.Count() - 1; i++)
                    {
                        tmpEval.SetVariable(_args.ElementAt(i), args.ElementAt(i));
                    }

                    if (kwargs != null)
                    {
                        foreach (string k in kwargs.Keys)
                        {
                            tmpEval.SetVariable(k, kwargs[k]);
                        }
                    }

                    tmpEval.EvalComplete += (object sender, AnswerEventArgs e) =>
                    {
                        object result = e.Result;
                        if ((callBack != null))
                        {
                            if (result is Reference && !(((Reference)result).GetRefObject() is Reference))
                            {
                                Reference @ref = (Reference)result;
                                callBack.Execute(eval, new[] { @ref.GetRefObject() });
                            }
                            else
                            {
                                callBack.Execute(eval, new[] { result });
                            }
                        }
                    };
                    return tmpEval.EvalAsync(_value, noSaveAns: true);
                }

                /// <summary>
                /// Sets this lambda object to the function specified
                /// </summary>
                public void SetFunctionPtr(string uf, IEnumerable<string> args)
                {
                    if (uf.Contains("("))
                        uf = uf.Remove(uf.IndexOf("("));
                    this._args = args;

                    this._value = uf + "(";
                    foreach (string a in args)
                    {
                        if (!this._value.EndsWith("("))
                            this._value += ",";
                        this._value += a;
                    }
                    this._value += ")";
                    this.IsFunctionPtr = true;
                    this.IsFlattened = false;
                    this.FunctionName = uf;
                }

                /// <summary>
                /// Sets this lambda object to the lambda expression specified
                /// </summary>
                public void SetLambdaExpr(string expr, IEnumerable<string> args)
                {
                    this._args = args;
                    this._value = expr;
                    this.IsFunctionPtr = false;
                    this.IsFlattened = false;
                    this.FunctionName = "";
                }

                /// <summary>
                /// Sets this lambda object to the lambda expression specified, in raw lambda expression notation
                /// </summary>
                public void SetLambdaExprRaw(string lambda)
                {

                    if (StrIsType(lambda))
                    {
                        lambda = lambda.Trim().Remove(lambda.Length - 1).Substring(1);
                        string args = "";
                        string expr = "";

                        if (lambda.Contains("=>"))
                        {
                            args = lambda.Remove(lambda.IndexOf("=>")).ToLowerInvariant();
                            expr = lambda.Substring(lambda.IndexOf("=>") + 2);
                        }
                        else
                        {
                            expr = lambda;
                        }

                        if (string.IsNullOrWhiteSpace(args))
                        {
                            this.SetLambdaExpr(expr, new List<string>());
                        }
                        else
                        {
                            args = args.Trim().Trim(new[]{
                    '(',
                            ')'
                        });
                            this.SetLambdaExpr(expr, args.Split(','));
                        }
                        this.IsFunctionPtr = false;
                        this.IsFlattened = false;
                        this.FunctionName = "";
                    }
                    else
                    {
                        throw new SyntaxException("Invalid lambda expression \"" + lambda + "\" (correct format: `var, var2, ...:expression` OR" + " `var: expression` OR `expression`");
                    }
                }

                public static new bool IsType(object obj)
                {
                    return obj is Lambda || obj is UserFunction;
                }

                public static new bool StrIsType(string str)
                {
                    str = str.Trim();
                    return str.StartsWith("`") && str.EndsWith("`");
                }

                public override string ToString(CantusEvaluator eval)
                {
                    if (this.IsFunctionPtr)
                    {
                        return this.FunctionName;
                    }
                    else
                    {
                        if (this.Args.Count() == 0)
                        {
                            return "`" + this._value + "`";
                        }
                        else
                        {
                            return "`" + string.Join(",", this.Args) + " => " + this._value.TrimStart() + "`";
                        }
                    }
                }

                public override bool Equals(EvalObjectBase other)
                {
                    return _value.Equals(other);
                }

                /// <summary>
                /// Create a new lambda expression from a string expression
                /// </summary>
                /// <param name="expr">Either the lambda expression or the function name</param>
                /// <param name="args">The list of argument names</param>
                /// <param name="fnptr">If true, intreprets as function pointer</param>
                public Lambda(string expr, IEnumerable<string> args, 
                    bool fnptr = false, bool flatten = false, string fnName = "")
                {
                    if (fnptr)
                    {
                        if (flatten)
                        {
                            this.SetLambdaExpr(expr, args);
                            this.IsFunctionPtr = true;
                            this.IsFlattened = true;
                            this.FunctionName = fnName;
                        }
                        else
                        {
                            this.SetFunctionPtr(expr, args);
                            this.IsFlattened = false;
                        }
                    }
                    else
                    {
                        this.SetLambdaExpr(expr, args);
                    }
                }

                /// <summary>
                /// Create a function pointer to a user function. 
                /// If flatten is set to true, creates a new lambda expression
                /// with the user function's definition instead.
                /// </summary>
                public Lambda(UserFunction uf, bool flatten)
                {
                    if (flatten)
                    {
                        this.SetLambdaExpr(uf.Body, uf.Args);
                        this.IsFunctionPtr = true;
                        this.IsFlattened = true;
                        this.FunctionName = uf.FullName;
                    }
                    else
                    {
                        this.IsFlattened = false;
                        this.SetFunctionPtr(uf.FullName, uf.Args);
                    }
                }
                /// <summary>
                /// Create a function pointer to a user function. 
                /// </summary>
                public Lambda(UserFunction uf)
                {
                    this.SetFunctionPtr(uf.FullName, uf.Args);
                }

                /// <summary>
                /// Create a new lambda expression from lambda expression syntax
                /// </summary>
                public Lambda(string lambda)
                {
                    this.SetLambdaExprRaw(lambda);
                }

                public override int GetHashCode()
                {
                    return this._value.GetHashCode();
                }

                protected override EvalObjectBase DeepCopy()
                {
                    return new Lambda(this._value, this.Args, this.IsFunctionPtr,
                       this.IsFlattened, this.FunctionName);
                }
            }

            /// <summary>
            /// An instance of a user-defined class
            /// </summary>
            public sealed class ClassInstance : EvalObjectBase, IDisposable
            {

                private UserClass _userClass;
                /// <summary>
                /// Class of this object
                /// </summary>
                public UserClass UserClass
                {
                    get
                    {
                        return _userClass;
                    } 
                }

                private Dictionary<string, Reference> _fields = new Dictionary<string, Reference>();
                /// <summary>
                /// Values of the object's fields
                /// </summary>
                public Dictionary<string, Reference> Fields
                {
                    get
                    {
                        return _fields;
                    }
                }

                private string _innerScope;
                /// <summary>
                /// Gets the internal scope of this instance used to store fields, etc.
                /// </summary>
                public string InnerScope
                {
                    get
                    {
                        return _innerScope;
                    }
                }

                /// <summary>
                /// Indicates if the instance is disposed
                /// </summary>

                private bool _disposed = false;
                /// <summary>
                /// Returns self
                /// </summary>
                /// <returns></returns>
                public override object GetValue()
                {
                    return this;
                }

                /// <summary>
                /// Copy from another ClassInstance
                /// </summary>
                public override void SetValue(object obj)
                {
                    if (obj is ClassInstance)
                    {
                        this.Dispose();
                        ClassInstance ci = (ClassInstance)obj;
                        this._disposed = false;
                        this._userClass = ci.UserClass;
                        ci.UserClass.RegisterInstance(this);

                        _fields = new Dictionary<string, Reference>();
                        this.UserClass.Evaluator.SubScope("__instance_" + this.UserClass.Name + "_" + RandomInstanceId());

                        bool imported = this.UserClass.Evaluator.Imported.Contains(UserClass.InnerScope);
                        if (!imported)
                            this.UserClass.Evaluator.Import(UserClass.InnerScope);

                        try
                        {
                            this._innerScope = this.UserClass.Evaluator.Scope;

                            foreach (KeyValuePair<string, Reference> f in ci.Fields)
                            {
                                Reference newVal = null;
                                if (UserClass.AllFields[f.Key].Modifiers.Contains("static"))
                                {
                                    newVal = (Reference)f.Value;
                                }
                                else
                                {
                                    newVal = (Reference)f.Value.GetDeepCopy();
                                }
                                this.UserClass.Evaluator.SetVariable(this.InnerScope + SCOPE_SEP + f.Key, newVal, modifiers: new[] { "internal" });
                                this._fields[f.Key] = this.UserClass.Evaluator.GetVariableRef(this.InnerScope + SCOPE_SEP + f.Key);
                            }
                        }
                        catch
                        {
                        }

                        if (!imported)
                            this.UserClass.Evaluator.Unimport(UserClass.InnerScope);

                        this.UserClass.Evaluator.ParentScope();
                        InitInstanceId();
                    }

                }

                /// <summary>
                /// Recursively get the value of the field with the specified name under this instance
                /// </summary>
                public Reference ResolveField(string fieldName, string scope)
                {
                    if (string.IsNullOrWhiteSpace(fieldName))
                        throw new Exception("Field name cannot be blank");
                    try
                    {
                        string[] spl = fieldName.Split(SCOPE_SEP);
                        Reference curVar = new Reference(this);

                        for (int i = 0; i <= spl.Length - 1; i++)
                        {
                            if (curVar.GetRefObject() is ClassInstance)
                            {
                                ClassInstance ci = (ClassInstance)curVar.ResolveObj();
                                if (!ci.UserClass.AllFields[spl[i]].Modifiers.Contains("private") || IsParentScopeOf(ci.InnerScope, scope))
                                {
                                    curVar = ci.Fields[spl[i]];
                                }
                                else
                                {
                                    throw new EvaluatorException("Field " + fieldName + " is private.");
                                }
                            }
                        }

                        if (curVar.GetRefObject() is Reference)
                            return (Reference)curVar.GetRefObject();
                        return curVar;

                    }
                    catch (EvaluatorException ex)
                    {
                        throw ex;
                    }
                    catch (Exception)//ex)
                    {
                        throw new Exception(fieldName + " is not a field of " + UserClass.Name);
                    }
                }
                /// <summary>
                /// Cast safely to another type (allow narrowing)
                /// </summary>
                public void Cast(UserClass targetType)
                {
                    _userClass = targetType;
                    Dictionary<string, Variable> allFlds = _userClass.AllFields;
                    // do casting
                    foreach (string k in _fields.Keys.ToArray())
                    {
                        if (!allFlds.ContainsKey(k))
                            throw new EvaluatorException("Narrowing cast to " + _userClass.Name +
                                " not allowed. Use unsafecast or (!as <type>) to force casting.");
                    }
                    foreach (string k in allFlds.Keys)
                    {
                        if (!_fields.ContainsKey(k))
                            _fields[k] = allFlds[k].Reference;
                    }
                }

                /// <summary>
                /// Cast unsafely to another type (allow narrowing)
                /// </summary>
                public void UnsafeCast(UserClass targetType)
                {
                    _userClass = targetType;
                    Dictionary<string, Variable> allFlds = _userClass.AllFields;
                    // do casting
                    foreach (string k in _fields.Keys.ToArray())
                    {
                        if (!allFlds.ContainsKey(k))
                            _fields.Remove(k);
                    }
                    foreach (string k in allFlds.Keys)
                    {
                        if (!_fields.ContainsKey(k))
                            _fields[k] = allFlds[k].Reference;
                    }
                }

                /// <summary>
                /// Get a random instance id
                /// </summary>
                private string RandomInstanceId()
                {
                    return Guid.NewGuid().ToString().Replace("-", "") + System.DateTime.Now.Millisecond;
                }
                private string GenerateSubscope()
                {
                    return RandomInstanceId();
                }


                public static new bool IsType(object obj)
                {
                    return obj is ClassInstance || obj is UserClass;
                }

                public static new bool StrIsType(string str)
                {
                    return false;
                }

                public override string ToString(CantusEvaluator eval)
                {
                    if (this._disposed)
                        return "";
                    if (this.Fields.ContainsKey("text") && this.Fields["text"].ResolveObj() is Lambda)
                    {
                        // use the "text" function within the class definition
                        CantusEvaluator tmpEval = UserClass.Evaluator.SubEvaluator();
                        tmpEval.Scope = this.InnerScope;
                        //tmpEval.Import(UserClass.InnerScope)
                        tmpEval.SubScope();
                        tmpEval.SetDefaultVariable(new Reference(this));
                        return UserClass.Evaluator.Internals.O(((Lambda)this.Fields["text"].ResolveObj()).Execute(tmpEval, new object[] { },
                            executingScope: tmpEval.Scope));
                    }
                    else
                    {
                        // default instance info
                        return "# instance of " + this.UserClass.Name + " with id " + new InternalFunctions(null).InstanceId(this);
                    }
                }

                public override bool Equals(EvalObjectBase other)
                {
                    return this.UserClass.Name.Equals(other);
                }

                /// <summary>
                /// Set up the 'instanceid' member function
                /// </summary>
                private void InitInstanceId()
                {
                    // add 'instaneid' function
                    UserFunction iidFn = new UserFunction("instanceid", string.Format(
                        "return {0}{1}instanceid(this)", CantusEvaluator.ROOT_NAMESPACE, SCOPE_SEP), new List<string>(), this.InnerScope);
                    iidFn.Modifiers.Add("internal");
                    this.Fields[iidFn.Name] = new Reference(new Lambda(iidFn, true));
                }

                /// <summary>
                /// Create an identical class instance from another class instance
                /// </summary>
                public ClassInstance(ClassInstance ci)
                {
                    this._fields = new Dictionary<string, Reference>();
                    this.SetValue(ci);
                    InitInstanceId();
                }

                /// <summary>
                /// Create a new instance of a class
                /// </summary>
                public ClassInstance(UserClass uc)
                {
                    this._fields = new Dictionary<string, Reference>();
                    this._userClass = uc;
                    uc.RegisterInstance(this);

                    uc.Evaluator.SubScope("__instance_" + this.UserClass.Name + "_" + RandomInstanceId());
                    try
                    {
                        this._innerScope = uc.Evaluator.Scope;

                        // load default values 
                        foreach (KeyValuePair<string, Variable> kvp in uc.AllFields)
                        {
                            this.Fields[kvp.Key] = kvp.Value.Reference;
                            uc.Evaluator.SetVariable(kvp.Key, kvp.Value.Reference, modifiers: new[] { "internal" });
                        }
                    }
                    catch
                    {
                    }
                    uc.Evaluator.ParentScope();
                    InitInstanceId();
                }

                public ClassInstance(UserClass uc, IEnumerable<object> args, IDictionary<string, object> kwargs = null)
                {
                    this._fields = new Dictionary<string, Reference>();
                    this._userClass = uc;
                    uc.RegisterInstance(this);

                    Lambda constructor = uc.Constructor;

                    CantusEvaluator tmpEval = uc.Evaluator.SubEvaluator();
                    tmpEval.Scope = uc.Evaluator.Scope + SCOPE_SEP + "__instance_" + this.UserClass.Name + "_" + RandomInstanceId();
                    this._innerScope = tmpEval.Scope;

                    tmpEval.Import(uc.InnerScope);

                    // load default values 
                    foreach (KeyValuePair<string, Variable> kvp in uc.AllFields)
                    {
                        this.Fields[kvp.Key] = kvp.Value.Reference;
                        tmpEval.SetVariable(kvp.Key, kvp.Value.Reference);
                    }

                    // run constructor
                    tmpEval.SubScope();
                    tmpEval.SetDefaultVariable(new Reference(this));

                    constructor.Execute(tmpEval, args, kwargs, tmpEval.Scope);

                    foreach (Variable var in tmpEval.Variables.Values)
                    {
                        if (var.DeclaringScope == this.InnerScope)
                        {
                            var.Modifiers.Add("internal");
                            uc.Evaluator.Variables[var.FullName] = var;
                            this.Fields[var.Name] = var.Reference;
                        }
                    }
                    InitInstanceId();
                }

                public override int GetHashCode()
                {
                    if (this._disposed)
                        throw new EvaluatorException("This user class instance is disposed");
                    return this._disposed.GetHashCode();
                }

                /// <summary>
                /// Create a deep copy of this class instance
                /// </summary>
                /// <returns></returns>
                protected override EvalObjectBase DeepCopy()
                {
                    if (this._disposed)
                        throw new EvaluatorException("This user class instance is disposed");
                    return new ClassInstance(this);
                }

                public void Dispose()
                {
                    try
                    {
                        if (this._disposed)
                            return;
                        this._disposed = true;
                        foreach (string f in this.Fields.Keys)
                        {
                            this.UserClass.Evaluator.SetVariable(this.InnerScope + SCOPE_SEP + f, double.NaN);
                        }
                        this._userClass = null;
                    }
                    catch { }
                }
            }
        }
    }
}

namespace Cantus.Core.CommonTypes
{
    /// <summary>
    /// Custom comparer that works for all types, used for our non type-specific sets, dictionaries, and sorts
    /// </summary>
    public sealed class ObjectComparer : IComparer<object>, IEqualityComparer<object>
    {
        private static int TypeToId(object obj)
        {
            switch (obj.GetType().Name)
            {
                case "BigDecimal":
                    return 0;
                case "Double":
                case "Integer":
                    return 1;
                case "String":
                    return 2;
                case "DateTime":
                case "Date":
                    return 3;
                case "TimeSpan":
                    return 4;
                default:
                    if (obj.GetType().Name.StartsWith("List"))
                    {
                        return 5;
                    }
                    else if (obj.GetType().Name.StartsWith("Dictionary"))
                    {
                        return 6;
                    }
                    return 7;
            }
        }

        /// <summary>
        /// Compare two lists by element
        /// </summary>
        public static int CompareLists(IEnumerable<Reference> a, IEnumerable<Reference> b)
        {
            for (int i = 0; i <= Math.Min(a.Count(), b.Count()) - 1; i++)
            {
                if (CompareObjs(a.ElementAt(i), b.ElementAt(i)) != 0)
                    return CompareObjs(a.ElementAt(i), b.ElementAt(i));
            }
            return CompareObjs(a.Count(), b.Count());
        }

        /// <summary>
        /// Compare two linked lists by element
        /// </summary>
        public static int CompareLinkedLists(LinkedList<Reference> a, LinkedList<Reference> b)
        {
            for (int i = 0; i <= Math.Min(a.Count, b.Count) - 1; i++)
            {
                if (CompareObjs(a.ElementAt(i), b.ElementAt(i)) != 0)
                    return CompareObjs(a.ElementAt(i), b.ElementAt(i));
            }
            return CompareObjs(a.Count, b.Count);
        }

        /// <summary>
        /// Compare two generic objects
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static int CompareObjs(object x, object y)
        {
            if (x.GetType().ToString().
                StartsWith("Cantus.Core.CantusEvaluator+Obj") &&
                !x.GetType().ToString().EndsWith("[]"))
            {
                if (x.GetType() == typeof(Number))
                {
                    x = ((Number)x).BigDecValue();
                }
                else
                {
                    x = ((EvalObjectBase)x).GetValue();
                }
            }
            if (y.GetType().ToString().StartsWith(
                "Cantus.Core.CantusEvaluator+Obj") &&
                !y.GetType().ToString().EndsWith("[]"))
            {
                if (y.GetType() == typeof(Number))
                {
                    y = ((Number)y).BigDecValue();
                }
                else
                {
                    y = ((EvalObjectBase)y).GetValue();
                }
            }

            if (TypeToId(x) != TypeToId(y))
            {
                return TypeToId(x) > TypeToId(y) ? 1 : -1;

            }

            else if (x is BigDecimal && y is BigDecimal)
            {
                BigDecimal xd = (BigDecimal)x;
                BigDecimal yd = (BigDecimal)y;
                if (xd.IsUndefined ^ yd.IsUndefined) return -1;
                return xd > yd ? 1 : xd == yd ? 0 : -1;
            }
            else if (x is double && y is double)
            {
                return ((double)x).CompareTo((double)(y));
            }
            else if (x is int && y is int)
            {
                return ((int)x).CompareTo((int)(y));
            }

            else if (x is bool && y is bool)
            {
                return ((bool)x).CompareTo((bool)y);

            }
            else if (x is string && y is string)
            {
                return Convert.ToString(x).CompareTo(Convert.ToString(y));
            }
            else if (x is System.DateTime && y is System.DateTime)
            {
                return Convert.ToDateTime(x).CompareTo(Convert.ToDateTime(y));
            }

            else if (x is TimeSpan && y is TimeSpan)
            {
                return ((TimeSpan)x).CompareTo((TimeSpan)y);

            }
            else if (x is IList<Reference> && y is IList<Reference>)
            {
                return CompareLists((IList<Reference>)x, (IList<Reference>)y);

            }

            else if (x is LinkedList<Reference> && y is LinkedList<Reference>)
            {
                return CompareLinkedLists((LinkedList<Reference>)x, (LinkedList<Reference>)y);

            }

            else if (x is Reference[] && y is Reference[])
            {
                return CompareLists((Reference[])x, (Reference[])y);

            }

            else if (x is ReadOnlyCollection<Reference> && y is ReadOnlyCollection<Reference>)
            {
                return CompareLists((ReadOnlyCollection<Reference>)x, (ReadOnlyCollection<Reference>)y);

            }

            else if (x is KeyValuePair<object, object> && y is KeyValuePair<object, object>)
            {
                int cmpKv = CompareObjs(((KeyValuePair<object, object>)x).Key, ((KeyValuePair<object, object>)y).Key);
                if (cmpKv != 0)
                    return cmpKv;
                return CompareObjs(((KeyValuePair<object, object>)x).Value, ((KeyValuePair<object, object>)y).Value);

            }

            else if (x is IDictionary<Reference, Reference> && y is IDictionary<Reference, Reference>)
            {
                int cmpDict = CompareLists(((IDictionary<Reference, Reference>)x).Keys.ToList(), ((IDictionary<Reference, Reference>)y).Keys.ToList());
                if (cmpDict != 0)
                    return cmpDict;

                try
                {
                    return CompareLists(((IDictionary<Reference, Reference>)x).Values.ToList(), ((IDictionary<Reference, Reference>)y).Values.ToList());
                }
                catch
                {
                    return 0;
                }
            }

            else
            {
                return 1;
            }
        }

        /// <summary>
        /// Compare two generic objects
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int Compare(object x, object y)
        {
            return CompareObjs(x, y);
        }

        public new bool Equals(object x, object y)
        {
            return CompareObjs(x, y) == 0;
        }

        public int GetHashCode(object obj)
        {
            if (obj is EvalObjectBase)
            {
                return ((EvalObjectBase)obj).GetHashCode();
            }
            else
            {
                return obj.GetHashCode();
            }
        }

    }
}
