using System;
using System.Collections.Generic;
using System.Linq;
using Cantus.Core.Exceptions;
using Cantus.Core.CommonTypes;
using static Cantus.Core.CantusEvaluator;

namespace Cantus.Core
{
    internal class OperatorRegistar
    {
        #region "Enums"
        /// <summary>
        /// Represents the precedence of an operator. The higher, the earlier the operator is executed.
        /// Note that brackets do not have precedence. They are pre-evaluated during tokenization.
        /// </summary>
        public enum Precedence
        {
            /// <summary>
            /// Represents the precedence of assignment operators like =.
            /// Note: assignment operators are evaluate RTL so you can do a=b=c chaining, whereas all others are LTR
            /// </summary>
            assignment = 0,
            /// <summary>
            /// Tupling opera
            /// </summary>
            tupling,
            /// <summary>
            /// represents the precedence of the logical and operator
            /// </summary>
            or,
            /// <summary>
            /// represents the precedence of the logical not operator
            /// </summary>
            and,
            /// <summary>
            /// represents the precedence of the logical or operator
            /// </summary>
            not,
            /// <summary>
            /// A comparison operator like =, &lt;
            /// </summary>
            comparison,
            /// <summary>
            /// Represents the precedence of the operators &lt;&lt; &amp; and \ (below +- but above comparison)
            /// </summary>
            bitshift_concat_frac,
            /// <summary>
            /// Represents the precedence of the operators + and -
            /// </summary>
            add_sub,
            /// <summary>
            /// Represents the precedence of the operators *, /, and mod, as well as most bitwise operators like || &amp;&amp;
            /// </summary>
            mul_div,
            /// <summary>
            /// Represents the precedence of the ^ operator (EVALUATED RTL)
            /// </summary>
            exponent,
            /// <summary>
            /// Represents the precedence of the E operator
            /// </summary>
            exp10,
            /// <summary>
            /// Represents the precedence of the unary minus operator
            /// </summary>
            unaryminus,
            /// <summary>
            /// Represents the precedence of some very high precedence operators like ! and %
            /// evaluated first
            /// </summary>
            factorial_percent
        }
        #endregion

        #region "Operator Types"

        /// <summary>
        /// Base class for all operators
        /// </summary>
        public abstract class Operator
        {
            public abstract List<string> Signs { get; }
            public abstract Precedence Precedence { get; }
            /// <summary>
            /// If true, values are passed by the Reference class which allows the value within to be manipulated
            /// </summary>
            public bool ByReference { get; set; }

            /// <summary>
            /// If true, the identifier before are evaluated as a single variable and not multiplied i.e. abc -> abc instead of a*b*c
            /// </summary>
            public bool AssignmentOperator { get; set; }
        }

        /// <summary>
        /// An operator involving two values
        /// </summary>
        public class BinaryOperator : Operator
        {
            public override List<string> Signs { get; }
            public override Precedence Precedence { get; }
            public Func<ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase> Execute { get; }
            /// <summary>
            /// Initialize a new binary operator
            /// </summary>
            /// <param name="signs">The list of signs to register for the operator</param>
            /// <param name="precedence">The precedence of the operator</param>
            /// <param name="execute">The operator definition, specified as a function (use AddressOf ...)</param>
            public BinaryOperator(string[] signs, Precedence precedence, Func<ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase> execute)
            {
                this.Signs = new List<string>(signs);
                this.Precedence = precedence;
                this.Execute = execute;
            }
        }

        /// <summary>
        /// An operator involving a single value
        /// </summary>
        public abstract class UnaryOperator : Operator
        {
            public override List<string> Signs { get; }
            public override Precedence Precedence { get; }
            public Func<ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase> Execute { get; }
            /// <summary>
            /// Initialize a new unary operator
            /// </summary>
            /// <param name="signs">The list of signs to register for the operator</param>
            /// <param name="execute">The operator definition, specified as a function (use AddressOf ...)</param>
            public UnaryOperator(string[] signs, Precedence precedence, Func<ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase> execute)
            {
                this.Signs = new List<string>(signs);
                this.Precedence = precedence;
                this.Execute = execute;
            }
        }

        /// <summary>
        /// An operator involving a single value before it (for example, x!)
        /// </summary>
        public class UnaryOperatorBefore : UnaryOperator
        {
            public UnaryOperatorBefore(string[] signs, Precedence precedence, Func<ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase> execute) : base(signs, precedence, execute)
            {
            }
        }

        /// <summary>
        /// An operator involving a single value after it (for example, 'not x')
        /// </summary>
        public class UnaryOperatorAfter : UnaryOperator
        {
            public UnaryOperatorAfter(string[] signs, Precedence precedence, Func<ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase> execute) : base(signs, precedence, execute)
            {
            }
        }

        /// <summary>
        /// A 'bracket' type operator (e.g. (), [], ||)
        /// Evaluated before all other operators, on tokenization
        /// </summary>
        public class Bracket : Operator
        {
            /// <summary>
            /// A list of signs for this bracket
            /// </summary>
            public override List<string> Signs { get; }

            /// <summary>
            /// Get the open bracket of this bracket
            /// </summary>
            public string OpenBracket
            {
                get { return Signs[0]; }
            }

            /// <summary>
            /// Get the close bracket of this bracket
            /// </summary>
            public string CloseBracket
            {
                get
                {
                    if (Signs.Count > 1)
                    {
                        return Signs[1];
                    }
                    else
                    {
                        return Signs[0];
                    }
                }
            }

            public override Precedence Precedence
            {
                // not used
                get { return Precedence.factorial_percent; }
            }
            public delegate ObjectTypes.EvalObjectBase BracketDelegate(string inner, ref ObjectTypes.EvalObjectBase left);

            /// <summary>
            /// If true, allow stacking (like ((1)+2)). Operators with a single sign cannot stack.
            /// </summary>
            /// <returns></returns>
            public bool Stackable { get; }

            /// If true, ignores all brackets inside the bracket 
            /// (i.e. "hello(wo" + "rld)" should give 'hello(world)' not 'hello(wo" + "rld' )
            /// </summary>
            /// <returns></returns>
            public bool IsString { get; }

            public BracketDelegate Execute { get; }

            /// <summary>
            /// Create a new bracket operator with two signs (for example '(x)')
            /// </summary>
            /// <param name="signStart">The sign that begins the operator (eg. '(')</param>
            /// <param name="signEnd">The sign that ends the bracket (eg. ')')</param>
            /// <param name="execute">The operator definition (use AddressOf)</param>
            /// <param name="stackable">If true, this operator can be stacked like [[]]. 
            /// Operators with single signs cannot be stacked.</param>
            /// <param name="isString">If true, this operator is treated as a string
            ///  and brackets with in the range are ignored.</param>
            public Bracket(string signStart, string signEnd, BracketDelegate execute,
                bool stackable = true, bool isString = false)
            {
                this.Signs = new List<string>();
                this.Signs.Add(signStart);
                this.Signs.Add(signEnd);
                this.Execute = execute;
                this.Stackable = stackable;
                this.IsString = isString;
            }

            /// <summary>
            /// Create a new bracket operator with one sign (for example '|x|')
            /// </summary>
            /// <param name="sign">The sign that begins and ends the operator (eg. '|')</param>
            /// <param name="execute">The operator definition (use AddressOf)</param>
            /// <param name="isString">If true, this operator is treated as a string
            ///  and brackets with in the range are ignored.</param>
            public Bracket(string sign, BracketDelegate execute,
                bool isString = false)
            {
                this.Signs = new List<string>();
                this.Signs.Add(sign);
                this.Execute = execute;
                this.Stackable = false;
                this.IsString = isString;
            }

            /// <summary>
            /// Given the remaining part of the expression not including the open bracket,
            /// finds the index of the matching close bracket.
            /// </summary>
            /// <param name="expr">The part of the expression after (not including) the open bracket</param>
            /// <returns></returns>
            public int FindCloseBracket(string expr, OperatorRegistar opReg)
            {
                string startSign = this.OpenBracket;
                HashSet<string> endSign = new HashSet<string>(new[] { this.Signs.Count < 2 ? startSign : CloseBracket });
                for (int i = 2; i <= this.Signs.Count - 1; i++)
                {
                    endSign.Add(this.Signs[i]);
                }

                int endIdx = 0;
                int stackHeight = 0;
                int lastFound = -1;

                for (endIdx = 0; endIdx <= expr.Length - 1; endIdx++)
                {
                    bool foundEndSign = false;
                    // try to find end sign
                    foreach (string s in endSign)
                    {
                        if (expr.Substring(endIdx).StartsWith(s))
                        {
                            stackHeight -= 1;
                            if (stackHeight < 0)
                                return endIdx;
                            endIdx += s.Length - 1;
                            foundEndSign = true;
                            break;
                        }
                    }

                    // end sign not found, try to find start sign
                    if (!foundEndSign)
                    {
                        if (expr[endIdx] == '\\')
                        {
                            // start escape sequence
                            lastFound = endIdx + 1;
                            // save the time we last found an escaped end sign
                            endIdx += 1;
                        }
                        else if (!IsString)
                        {
                            if (expr.Substring(endIdx).StartsWith(startSign) && Stackable)
                            {
                                stackHeight += 1;
                                endIdx += startSign.Length - 1;

                                // try to find other brackets
                            }
                            else
                            {
                                foreach (Bracket b in opReg.Brackets)
                                {
                                    if (expr.Substring(endIdx).StartsWith(b.OpenBracket))
                                    {
                                        endIdx +=
                                            b.FindCloseBracket(expr.Substring(endIdx +
                                            b.OpenBracket.Length), opReg) + b.CloseBracket.Length;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                // if we can't find anything unescaped we'll ignore the last escape sequence
                if (lastFound == -1)
                {
                    return expr.Length;
                }
                else
                {
                    return lastFound;
                }
            }
        }

        #endregion

        #region "Constants & Variable Declarations"
        private Dictionary<char, int> _maxOperatorLength =  new Dictionary<char, int>();
        /// <summary>
        /// Maximum length in characters of a single operator
        /// </summary>
        public Dictionary<char, int> MaxOperatorLength { get { return _maxOperatorLength; } }

        private CantusEvaluator _eval;
        /// <summary>
        /// A list of lists operators, with each inner list containing all operators of the precedence level of its index
        /// </summary>

        public List<List<Operator>> Operators = new List<List<Operator>>();
        /// <summary>
        /// A list of brackets
        /// </summary>

        public List<Bracket> Brackets = new List<Bracket>();
        /// <summary>
        /// Condition mode: if on, the = operator is always seen as a comparison operator
        /// </summary>
        public static bool ConditionMode { get; set; }

        /// <summary>
        /// The default operator to use when none is specified, normally *
        /// </summary>
        /// <returns></returns>
        public Operator DefaultOperator { get; set; }

        /// <summary>
        /// A hashset of operator signs to quickly check if a given name is an operator
        /// </summary>
        #endregion
        private Dictionary<string, List<Operator>> _operatorSigns = new Dictionary<string, List<Operator>>();

        #region "Public Methods"
        /// <summary>
        /// Create a new Operator Registar for registering and accessing operators like if/else
        /// </summary>
        public OperatorRegistar(CantusEvaluator parent)
        {
            this._eval = parent;
            RegisterOperators();
        }

        /// <summary>
        /// Tests if there is an operator with the specified sign
        /// </summary>
        /// <param name="sign"></param>
        /// <returns></returns>
        public bool OperatorExists(string sign)
        {
            return _operatorSigns.ContainsKey(sign);
        }


        /// <summary>
        /// Return all operators with the sign given
        /// </summary>
        public IEnumerable<Operator> OperatorsWithSign(string sign)
        {
            return _operatorSigns[sign];
        }

        /// <summary>
        /// Returns the operator that registered the sign and precedence
        /// </summary>
        /// <returns></returns>
        public Operator OperatorWithSign(string sign, Precedence? precedence = null)
        {
            try
            {
                foreach (Operator op in _operatorSigns[sign])
                {
                    if (precedence == null || op.Precedence == precedence)
                        return op;
                }
            }
            catch { }
            return null;
        }
        #endregion

        #region "Operator Registration"
        /// <summary>
        /// Register all operators
        /// </summary>
        private void RegisterOperators()
        {
            Operators.Clear();
            foreach (int lvl in Enum.GetValues(typeof(Precedence)))
            {
                Operators.Add(new List<Operator>());
            }

            // Register operators here 
            // FORMAT: 
            // Register(New [Type]Operator(new[]{[List of signs to register]}, Precedence.[Precedence], AddressOf [Definition]))

            Register(new BinaryOperator(new[] { "+" }, Precedence.add_sub, BinaryOperatorAdd));
            Register(new BinaryOperator(new[] { "-" }, Precedence.unaryminus, BinaryOperatorUnaryMinus));
            Register(new BinaryOperator(new[] { "-" }, Precedence.add_sub, BinaryOperatorSubtract));

            Register(new BinaryOperator(new[] { "*" }, Precedence.mul_div, BinaryOperatorMultiply));
            Register(new BinaryOperator(new[] { "/" }, Precedence.mul_div, BinaryOperatorDivide));
            Register(new BinaryOperator(new[] { "**" }, Precedence.mul_div, BinaryOperatorDuplicateCross));
            Register(new BinaryOperator(new[] { "//" }, Precedence.mul_div, BinaryOperatorDivideFloor));
            Register(new BinaryOperator(new[] { " mod " }, Precedence.mul_div, BinaryOperatorModulo));

            Register(new BinaryOperator(new[] { "^" }, Precedence.exponent, BinaryOperatorExponent));
            Register(new BinaryOperator(new[] { "&" }, Precedence.bitshift_concat_frac, BinaryOperatorConcat));
            Register(new BinaryOperator(new[] { "\\" }, Precedence.bitshift_concat_frac, BinaryOperatorDivide));
            Register(new BinaryOperator(new[] { "<<" }, Precedence.bitshift_concat_frac, BinaryOperatorShl));
            Register(new BinaryOperator(new[] { ">>" }, Precedence.bitshift_concat_frac, BinaryOperatorShr));

            Register(new BinaryOperator(new[] { " or " }, Precedence.or, BinaryOperatorOr));
            Register(new BinaryOperator(new[] { "||" }, Precedence.mul_div, BinaryOperatorBitwiseOr));
            Register(new BinaryOperator(new[] { " and " }, Precedence.and, BinaryOperatorAnd));
            Register(new BinaryOperator(new[] { "&&" }, Precedence.mul_div, BinaryOperatorBitwiseAnd));
            Register(new BinaryOperator(new[] { " xor " }, Precedence.or, BinaryOperatorXor));
            Register(new BinaryOperator(new[] { "^^" }, Precedence.mul_div, BinaryOperatorBitwiseXor));

            // use number group separator for ,
            RegisterByRef(new BinaryOperator(new[] { "," }, Precedence.tupling, BinaryOperatorCommaTuple));

            RegisterByRef(new BinaryOperator(new[] { ":" }, Precedence.tupling, BinaryOperatorColon));

            Register(new BinaryOperator(new[]{
                " choose ",
                " c "
            }, Precedence.mul_div, BinaryOperatorChoose));
            Register(new BinaryOperator(new[] { " e " }, Precedence.exp10, BinaryOperatorExp10));

            RegisterByRef(new BinaryOperator(new[] { "=" }, Precedence.comparison, BinaryOperatorAutoEqual));
            Register(new BinaryOperator(new[] { "==" }, Precedence.comparison, BinaryOperatorEqualTo));
            Register(new BinaryOperator(new[]{
                "!=",
                "<>"
            }, Precedence.comparison, BinaryOperatorNotEqualTo));
            Register(new BinaryOperator(new[] { ">" }, Precedence.comparison, BinaryOperatorGreaterThan));
            Register(new BinaryOperator(new[] { ">=" }, Precedence.comparison, BinaryOperatorGreaterThanOrEqualTo));
            Register(new BinaryOperator(new[] { "<" }, Precedence.comparison, BinaryOperatorLessThan));
            Register(new BinaryOperator(new[] { "<=" }, Precedence.comparison, BinaryOperatorLessThanOrEqualTo));

            Register(new BinaryOperator(new[] { "?:" }, Precedence.assignment, BinaryOperatorElvis));

            // assignment (use RegisterByRef, with same format)

            RegisterAssignment(new BinaryOperator(new[] { "=" }, Precedence.assignment, BinaryOperatorAssign));
            RegisterAssignment(new BinaryOperator(new[] { ":=" }, Precedence.assignment, BinaryOperatorAssign));
            RegisterAssignment(new BinaryOperator(new[] { "+=" }, Precedence.assignment, BinaryOperatorAddAssign));
            RegisterAssignment(new BinaryOperator(new[] { "-=" }, Precedence.assignment, BinaryOperatorSubtractAssign));
            RegisterAssignment(new BinaryOperator(new[] { "*=" }, Precedence.assignment, BinaryOperatorMultiplyAssign));
            RegisterAssignment(new BinaryOperator(new[] { "/=" }, Precedence.assignment, BinaryOperatorDivideAssign));
            RegisterAssignment(new BinaryOperator(new[] { "**=" }, Precedence.assignment, BinaryOperatorDuplicateAssign));
            RegisterAssignment(new BinaryOperator(new[] { "//=" }, Precedence.assignment, BinaryOperatorDivideFloorAssign));
            RegisterAssignment(new BinaryOperator(new[]{
                " mod=",
                " mod ="
            }, Precedence.assignment, BinaryOperatorModuloAssign));
            RegisterAssignment(new BinaryOperator(new[] { "^=" }, Precedence.assignment, BinaryOperatorExponentAssign));

            RegisterAssignment(new BinaryOperator(new[] { "&=" }, Precedence.assignment, BinaryOperatorConcatAssign));

            RegisterAssignment(new BinaryOperator(new[] { "||=" }, Precedence.assignment, BinaryOperatorBitwiseOrAssign));
            RegisterAssignment(new BinaryOperator(new[] { "&&=" }, Precedence.assignment, BinaryOperatorBitwiseAndAssign));
            RegisterAssignment(new BinaryOperator(new[] { "^^=" }, Precedence.assignment, BinaryOperatorBitwiseXorAssign));
            RegisterAssignment(new BinaryOperator(new[] { "<<=" }, Precedence.assignment, BinaryOperatorShlAssign));
            RegisterAssignment(new BinaryOperator(new[] { ">>=" }, Precedence.assignment, BinaryOperatorShrAssign));

            RegisterAssignment(new BinaryOperator(new[] { "++" }, Precedence.add_sub, BinaryOperatorIncrement));
            RegisterAssignment(new BinaryOperator(new[] { "--" }, Precedence.add_sub, BinaryOperatorDecrement));

            // unary

            Register(new UnaryOperatorBefore(new[] { "!" }, Precedence.factorial_percent, UnaryOperatorFactorial));
            Register(new UnaryOperatorBefore(new[] { "%" }, Precedence.factorial_percent, UnaryOperatorPercent));
            Register(new UnaryOperatorAfter(new[] { "not " }, Precedence.not, UnaryOperatorNot));
            Register(new UnaryOperatorAfter(new[] { "~" }, Precedence.factorial_percent, UnaryOperatorBitwiseNot));

            // ref keyword: create reference to object (reference not saved after session)
            RegisterByRef(new UnaryOperatorAfter(new[] { "ref " }, Precedence.factorial_percent, UnaryOperatorReference));

            // deref keyword: dereference the reference
            RegisterByRef(new UnaryOperatorAfter(new[] { "deref " }, Precedence.factorial_percent, UnaryOperatorDereference));

            // Brackets:
            // Register(New Bracket([start], [end], AddressOf [Definition]))
            // OR
            // Register(New Bracket([sign], AddressOf [Definition]))

            Register(new Bracket("$(", ")", BracketOperatorSilent));
            Register(new Bracket("`", BracketOperatorLambdaExpression));

            RegisterByRef(new Bracket("[", "]", BracketOperatorListIndexSlice));
            Register(new Bracket("{", "}", BracketOperatorDictionary));

            Register(new Bracket("(", ")", BracketOperatorRoundBracket));
            Register(new Bracket("(as ", ")", BracketOperatorCast));
            Register(new Bracket("(!as ", ")", BracketOperatorUnsafeCast));
            Register(new Bracket("(is ", ")", BracketOperatorIsType));
            Register(new Bracket("|", BracketOperatorAbsoluteValue));

            // strings
            Register(new Bracket("\"", BracketOperatorQuotedText, true));
            Register(new Bracket("'", BracketOperatorQuotedText, true));

            Register(new Bracket("r'", "'", BracketOperatorRawText, false, true));
            Register(new Bracket("r\"", "\"", BracketOperatorRawText, false, true));

            // multiline / triple-quoted
            Register(new Bracket("\"\"\"", BracketOperatorQuotedText, true));
            Register(new Bracket("r'''", "'''", BracketOperatorRawText, true));

            Register(new Bracket("'''", BracketOperatorQuotedText, true));
            Register(new Bracket("r'''", "'''", BracketOperatorRawText, false, true));

            this.DefaultOperator = OperatorWithSign("*");
        }

        /// <summary>
        /// Register an operator
        /// </summary>
        /// <param name="op"></param>
        public void Register(Operator op)
        {
            if (op is Bracket)
            {
                Brackets.Add((Bracket)op);
                // for brackets, add to list of brackets instead
            }
            else
            {
                Operators[(int)op.Precedence].Add(op);
            }

            foreach (string sign in op.Signs)
            {
                if (sign.Length > 0)
                {
                    if (!_maxOperatorLength.ContainsKey(sign[0]))
                        _maxOperatorLength[sign[0]] = 0;
                    _maxOperatorLength[sign[0]] = Math.Max(sign.Length,_maxOperatorLength[sign[0]]);
                }

                if (!_operatorSigns.ContainsKey(sign))
                    _operatorSigns[sign] = new List<Operator>();

                _operatorSigns[sign].Add(op);
            }
        }

        /// <summary>
        /// Register an operator that passes values by a 'reference' class
        /// </summary>
        /// <param name="op"></param>
        public void RegisterByRef(Operator op)
        {
            op.ByReference = true;
            Register(op);
        }

        /// <summary>
        /// Register an assignment operator
        /// </summary>
        /// <param name="op"></param>
        public void RegisterAssignment(Operator op)
        {
            op.AssignmentOperator = true;
            RegisterByRef(op);
        }
        #endregion

        #region "Bracket Operator Definitions"
        // Define BRACKET OPERATORS here:
        // format is always: Private Function BracketOperator[name](inner As String, left As EvalTypes.IEvalObject) As EvalTypes.IEvalObject
        private ObjectTypes.EvalObjectBase BracketOperatorRoundBracket(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            if (left is ObjectTypes.Lambda)
            {
                ObjectTypes.Lambda lambda = (ObjectTypes.Lambda)left;

                left = ObjectTypes.DetectType(lambda.Execute(_eval, inner));
                return left;
            }
            else
            {
                object res = _eval.EvalExprRaw(inner, true, ConditionMode);
                return ObjectTypes.DetectType(res, true);
            }
        }

        private ObjectTypes.EvalObjectBase BracketOperatorCast(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            object lv = left is ObjectTypes.Number ?
                ((ObjectTypes.Number)left).BigDecValue() :
                left.GetValue();
            left = ObjectTypes.DetectType(_eval.Internals.Cast(lv, inner.Trim()));
            return left;
        }

        private ObjectTypes.EvalObjectBase BracketOperatorUnsafeCast(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            object lv = left is ObjectTypes.Number ?
                ((ObjectTypes.Number)left).BigDecValue() :
                left.GetValue();
            left = ObjectTypes.DetectType(_eval.Internals.UnsafeCast(lv, inner.Trim()));
            return left;
        }

        private ObjectTypes.EvalObjectBase BracketOperatorIsType(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            object lv = left is ObjectTypes.Number ?
                ((ObjectTypes.Number)left).BigDecValue() :
                left.GetValue();
            left = new ObjectTypes.Boolean(
                _eval.Internals.Type(lv).ToLowerInvariant() ==
                    inner.Trim().ToLowerInvariant());
            return left;
        }

        private ObjectTypes.EvalObjectBase BracketOperatorAbsoluteValue(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            try
            {
                object result = _eval.EvalExprRaw(inner, true);
                return ObjectTypes.DetectType(_eval.Internals.Abs(result));
            }
            catch (Exception)
            {
                throw new SyntaxException("Invalid use of the || (absolute value) operator");
            }
        }

        private ObjectTypes.EvalObjectBase BracketOperatorSilent(string inner, ref ObjectTypes.EvalObjectBase left)
        {

            return ObjectTypes.DetectType(_eval.Internals.Silent(inner));
        }

        private ObjectTypes.EvalObjectBase BracketOperatorQuotedText(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            return new ObjectTypes.Text(inner).Escape();
        }

        private ObjectTypes.EvalObjectBase BracketOperatorRawText(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            return new ObjectTypes.Text(inner).Escape(true);
        }

        private ObjectTypes.EvalObjectBase BracketOperatorListIndexSlice(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            try
            {
                // first dereference
                if (ObjectTypes.Reference.IsType(left))
                    left = ((ObjectTypes.Reference)left).ResolveObj();
                // lists
                if (ObjectTypes.Matrix.IsType(left))
                {
                    if (string.IsNullOrWhiteSpace(inner))
                        throw new EvaluatorException("No Index Specified");
                    // slicing 
                    if (inner.Contains(":"))
                    {
                        try
                        {
                            double resultL = (double)((BigDecimal)_eval.EvalExprRaw(inner.Remove(inner.IndexOf(":")), true));
                            double resultR = (double)((BigDecimal)_eval.EvalExprRaw(inner.Substring(inner.IndexOf(":") + 1), true));
                            left = new ObjectTypes.Matrix((List<ObjectTypes.Reference>)_eval.Internals.Slice((List<ObjectTypes.Reference>)left.GetValue(), resultL, resultR));
                        }
                        catch
                        {
                            throw new EvaluatorException("Illegal Slicing Operation");
                        }
                    }
                    else
                    {
                        object result = _eval.EvalExprRaw(inner, true);
                        if (!(result is BigDecimal))
                            throw new EvaluatorException("Invalid index");
                        left = ObjectTypes.DetectType(_eval.Internals.Index(
                            (List<ObjectTypes.Reference>)left.GetValue(),
                            (int)((BigDecimal)result)), true);
                    }

                }
                // lists
                else if (ObjectTypes.LinkedList.IsType(left))
                {
                    if (string.IsNullOrWhiteSpace(inner))
                        throw new EvaluatorException("No Index Specified");
                    object result = _eval.EvalExprRaw(inner, true);
                    left = ObjectTypes.DetectType(_eval.Internals.Index((LinkedList<ObjectTypes.Reference>)left.GetValue(), (int)((BigDecimal)result)), true);
                }
                // tuples
                else if (ObjectTypes.Tuple.IsType(left))
                {
                    if (string.IsNullOrWhiteSpace(inner))
                        throw new EvaluatorException("No Index Specified");
                    // slicing 
                    if (inner.Contains(":"))
                    {
                        try
                        {
                            double resultL = Math.Truncate((double)((BigDecimal)_eval.EvalExprRaw(inner.Remove(inner.IndexOf(":")), true)));
                            double resultR = Math.Truncate((double)((BigDecimal)_eval.EvalExprRaw(inner.Substring(inner.IndexOf(":") + 1), true)));
                            left = new ObjectTypes.Tuple((List<ObjectTypes.Reference>)_eval.Internals.Slice((ObjectTypes.Reference[])left.GetValue(), resultL, resultR));
                        }
                        catch
                        {
                            throw new EvaluatorException("Illegal Slicing Operation");
                        }
                    }
                    else
                    {
                        object result = _eval.EvalExprRaw(inner, true);
                        if (!(result is BigDecimal))
                            throw new EvaluatorException("Invalid index");
                        left = ObjectTypes.DetectType(_eval.Internals.Index((ObjectTypes.Reference[])left.GetValue(), (int)((BigDecimal)result)), true);
                    }

                }
                // sets
                else if (ObjectTypes.Set.IsType(left) || ObjectTypes.HashSet.IsType(left))
                {
                    object result = _eval.EvalExprRaw(inner, true);
                    IDictionary<ObjectTypes.Reference, ObjectTypes.Reference> ld =
                        (IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)left.GetValue();

                    if (!ld.ContainsKey(new ObjectTypes.Reference(result)) && !ConditionMode)
                    {
                        ld[new ObjectTypes.Reference(result)] =
                            new ObjectTypes.Reference(BigDecimal.Undefined);
                    }

                    left = ObjectTypes.DetectType(
                                _eval.Internals.Index(ld,
                                    result), true);
                }
                // strings
                else if (ObjectTypes.Text.IsType(left))
                {
                    if (string.IsNullOrWhiteSpace(inner))
                        throw new EvaluatorException("No index specified");
                    // slicing 
                    if (inner.Contains(":"))
                    {
                        try
                        {
                            double resultL = Math.Truncate((double)((BigDecimal)_eval.EvalExprRaw(inner.Remove(inner.IndexOf(":")), true)));
                            double resultR = Math.Truncate((double)((BigDecimal)_eval.EvalExprRaw(inner.Substring(inner.IndexOf(":") + 1), true)));
                            left = new ObjectTypes.Text(_eval.Internals.Slice(left.GetValue().ToString(), resultL, resultR).ToString());
                        }
                        catch
                        {
                            throw new EvaluatorException("Illegal Slicing Operation");
                        }
                        // normal indexing
                    }
                    else
                    {
                        object result = _eval.EvalExprRaw(inner, true);
                        if (!(result is BigDecimal))
                            throw new EvaluatorException("Invalid index");
                        left = new ObjectTypes.Text(left.GetValue().ToString()[(int)((BigDecimal)result)].ToString());
                    }
                }
                else
                {
                    try
                    {
                        return new ObjectTypes.Matrix(inner, _eval);
                    }
                    catch
                    {
                        throw new SyntaxException("Invalid list format");
                    }
                }
                return left;
            }
            catch (Exception ex)
            {
                if (ex is ArgumentOutOfRangeException)
                {
                    throw new EvaluatorException("Index is out of range");
                }
                else if (inner.Contains(","))
                {
                    try
                    {
                        return new ObjectTypes.Matrix(inner, _eval);
                    }
                    catch
                    {
                        throw new EvaluatorException("Operator [] Error: " + ex.Message);
                    }
                }
                else
                {
                    throw new EvaluatorException("Operator [] Error: " + ex.Message);
                }
            }
        }

        private ObjectTypes.EvalObjectBase BracketOperatorDictionary(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            try
            {
                return new ObjectTypes.Set(inner, _eval);
            }
            catch
            {
                throw new SyntaxException("Invalid dictionary format");
            }
        }

        private ObjectTypes.EvalObjectBase BracketOperatorLambdaExpression(string inner, ref ObjectTypes.EvalObjectBase left)
        {
            return new ObjectTypes.Lambda("`" + inner + "`");
        }
        #endregion

        #region "Unary Operator Definitions"
        // Define UNARY OPERATORS here:
        // format is always: Private Function UnaryOperator[name](value As EvalTypes.IEvalObject) As EvalTypes.IEvalObject

        private ObjectTypes.EvalObjectBase UnaryOperatorFactorial(ObjectTypes.EvalObjectBase value)
        {
            object v = value.GetValue();
            if (ObjectTypes.Number.IsType(value))
            {
                return new ObjectTypes.Number(_eval.Internals.Factorial((double)(v)));
            }
            else
            {
                throw new SyntaxException("Only Number types may be used with the ! (factorial) operator");
            }
        }

        private ObjectTypes.EvalObjectBase UnaryOperatorPercent(ObjectTypes.EvalObjectBase value)
        {
            object v = value.GetValue();
            if (ObjectTypes.Number.IsType(value))
            {
                BigDecimal bn = ((ObjectTypes.Number)value).BigDecValue();
                bn = new BigDecimal(bn.Mantissa, bn.Exponent - 2, sigFigs: bn.SigFigs);
                bn.Normalize();
                return new ObjectTypes.Number(bn);
            }
            else
            {
                throw new SyntaxException("Only Number types may be used with the ! (factorial) operator");
            }
        }

        private ObjectTypes.EvalObjectBase UnaryOperatorNot(ObjectTypes.EvalObjectBase value)
        {
            if (value is ObjectTypes.Reference) value = ((ObjectTypes.Reference)value).ResolveObj();
            object v = value.GetValue();
            if (ObjectTypes.Boolean.IsType(value))
            {
                try
                {
                    return new ObjectTypes.Boolean(!Convert.ToBoolean(_eval.EvalExprRaw(v.ToString(), true)));
                }
                catch
                {
                    throw new SyntaxException("Operator not: Expression must produce a boolean value");
                }
            }
            else if (ObjectTypes.Number.IsType(value) && !double.IsNaN((double)(v)))
            {
                return new ObjectTypes.Boolean(Math.Round((double)(Convert.ToInt64(v)), 15) == 0);
            }
            else if (value == null) return new ObjectTypes.Boolean(true);
            else
            {
                throw new SyntaxException("Invalid type for the logical not operator (only booleans and numbers allowed)");
            }
        }

        private ObjectTypes.EvalObjectBase UnaryOperatorBitwiseNot(ObjectTypes.EvalObjectBase value)
        {
            object v = value.GetValue();
            if (ObjectTypes.Boolean.IsType(value))
            {
                try
                {
                    return new ObjectTypes.Boolean(!Convert.ToBoolean(_eval.EvalExprRaw(v.ToString(), true)));
                }
                catch
                {
                    throw new SyntaxException("Invalid type for the bitwise not operator");
                }
            }
            else if (ObjectTypes.Number.IsType(value) && !double.IsNaN((double)(v)))
            {
                return new ObjectTypes.Number(~Convert.ToInt64(v));
            }
            else
            {
                throw new SyntaxException("Invalid type for the bitwise not operator");
            }
        }

        /// <summary>
        /// Operator to create a new reference
        /// </summary>
        private ObjectTypes.EvalObjectBase UnaryOperatorReference(ObjectTypes.EvalObjectBase value)
        {
            return new ObjectTypes.Reference(value);
        }

        /// <summary>
        /// Operator to dereference a reference
        /// </summary>
        private ObjectTypes.EvalObjectBase UnaryOperatorDereference(ObjectTypes.EvalObjectBase value)
        {
            if (ObjectTypes.Reference.IsType(value))
            {
                return ((ObjectTypes.Reference)value).GetRefObject();
            }
            else
            {
                throw new SyntaxException("Dereference (deref) operator works only for references");
            }
        }
        #endregion

        #region "Binary Operator Definitions"
        // Define BINARY OPERATORS here:
        // format is always: Private Function BinaryOperator[name](left As EvalTypes.IEvalObject, 
        //                                                   right As EvalTypes.IEvalObject) As EvalTypes.IEvalObject

        private ObjectTypes.EvalObjectBase BinaryOperatorAdd(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null)
                left = new ObjectTypes.Number(0);

            if (ObjectTypes.Number.IsType(left) & ObjectTypes.Complex.IsType(right))
                return BinaryOperatorAdd(right, left);
            object lv = left.GetValue();
            object rv = right.GetValue();

            if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Number(((ObjectTypes.Number)left).BigDecValue() + ((ObjectTypes.Number)right).BigDecValue());

            }
            else if (ObjectTypes.Complex.IsType(left) & ObjectTypes.Complex.IsType(right))
            {
                return new ObjectTypes.Complex((System.Numerics.Complex)lv + (System.Numerics.Complex)lv);
            }
            else if (ObjectTypes.Complex.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Complex((System.Numerics.Complex)lv + (double)(rv));

            }
            else if (ObjectTypes.DateTime.IsType(left) && ObjectTypes.DateTime.IsType(right))
            {
                if (lv is DateTime)
                    lv = new TimeSpan(Convert.ToDateTime(lv).Ticks);
                if (rv is DateTime)
                    rv = new TimeSpan(Convert.ToDateTime(rv).Ticks);
                return new ObjectTypes.DateTime((TimeSpan)lv + ((TimeSpan)rv));

            }
            else if (ObjectTypes.Matrix.IsType(left) & ObjectTypes.Matrix.IsType(right))
            {
                List<ObjectTypes.Reference> lstl = (List<ObjectTypes.Reference>)lv;
                List<ObjectTypes.Reference> lstr = (List<ObjectTypes.Reference>)rv;
                List<ObjectTypes.Reference> newLst = new List<ObjectTypes.Reference>();

                for (int i = 0; i <= Math.Max(lstl.Count, lstr.Count) - 1; i++)
                {
                    ObjectTypes.EvalObjectBase objL = i >= lstl.Count ? new ObjectTypes.Number(0) : lstl[i].ResolveObj();
                    ObjectTypes.EvalObjectBase objR = i >= lstr.Count ? new ObjectTypes.Number(0) : lstr[i].ResolveObj();

                    if (!(((ObjectTypes.Matrix)left).Height == 1 && ((ObjectTypes.Matrix)right).Height == 1))
                    {
                        if (!ObjectTypes.Matrix.IsType(objL))
                            objL = new ObjectTypes.Matrix(new[] { objL });
                        if (!ObjectTypes.Matrix.IsType(objR))
                            objR = new ObjectTypes.Matrix(new[] { objR });
                    }

                    ObjectTypes.EvalObjectBase sum = BinaryOperatorAdd(objL, objR);

                    if (ObjectTypes.Reference.IsType(sum))
                    {
                        newLst.Add((ObjectTypes.Reference)sum);
                    }
                    else
                    {
                        newLst.Add(new ObjectTypes.Reference(sum));
                    }
                }
                return new ObjectTypes.Matrix(newLst);

            }
            else if (ObjectTypes.Matrix.IsType(left) & (ObjectTypes.Set.IsType(right) || ObjectTypes.HashSet.IsType(right)))
            {
                List<ObjectTypes.Reference> lst = (List<ObjectTypes.Reference>)lv;
                lst.AddRange(_eval.Internals.ToMatrix((IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)rv));
                return new ObjectTypes.Matrix(lst);

            }
            else if (ObjectTypes.Matrix.IsType(left))
            {
                List<ObjectTypes.Reference> lst = (List<ObjectTypes.Reference>)lv;
                lst.Add(new ObjectTypes.Reference(right));
                return new ObjectTypes.Matrix(lst);

            }
            else if (ObjectTypes.LinkedList.IsType(left))
            {
                LinkedList<ObjectTypes.Reference> lst = (LinkedList<ObjectTypes.Reference>)lv;
                lst.AddLast(new ObjectTypes.Reference(right));
                return new ObjectTypes.LinkedList(lst);

            }
            else if (ObjectTypes.Set.IsType(left) || ObjectTypes.HashSet.IsType(left))
            {
                IDictionary<ObjectTypes.Reference, ObjectTypes.Reference> dict = (IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)lv;

                if (ObjectTypes.Set.IsType(right) || ObjectTypes.HashSet.IsType(right))
                {
                    // union
                    dict = _eval.Internals.Union(dict, (IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)rv);
                }
                else if (ObjectTypes.Matrix.IsType(right))
                {
                    dict = _eval.Internals.Union(dict, _eval.Internals.ToSet((List<ObjectTypes.Reference>)rv));
                }
                else
                {
                    dict[new ObjectTypes.Reference(rv)] = null;
                }
                return ObjectTypes.DetectType(dict);

            }
            else if (ObjectTypes.Matrix.IsType(right) || right is ObjectTypes.LinkedList)
            {
                return BinaryOperatorAdd(right, left);

            }
            else if (ObjectTypes.Boolean.IsType(left) & ObjectTypes.Boolean.IsType(right))
            {
                return BinaryOperatorOr(left, right);
                // + = OR

            }
            else if (ObjectTypes.Text.IsType(left) || ObjectTypes.Text.IsType(right))
            {
                // do not append "NaN"
                if (lv.ToString() == "NaN")
                    lv = "";
                if (rv.ToString() == "NaN")
                    rv = "";
                return new ObjectTypes.Text((left is ObjectTypes.Text ? lv.ToString() : left.ToString(_eval)) +
                    (right is ObjectTypes.Text ? rv.ToString() : right.ToString(_eval)));
            }
            else
            {
                throw new SyntaxException("Invalid Addition");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorUnaryMinus(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null)
            {
                object rv = right.GetValue();
                if (ObjectTypes.Number.IsType(right))
                {
                    return new ObjectTypes.Number(-((ObjectTypes.Number)right).BigDecValue());
                }
                else if (ObjectTypes.Complex.IsType(right))
                {
                    return new ObjectTypes.Complex(-(System.Numerics.Complex)rv);
                }
                else if (ObjectTypes.Matrix.IsType(right))
                {
                    List<ObjectTypes.Reference> lstr = (List<ObjectTypes.Reference>)rv;
                    for (int i = 0; i < lstr.Count; i++)
                    {
                        ObjectTypes.EvalObjectBase neg = BinaryOperatorUnaryMinus(null, lstr[i].ResolveObj());
                        if (ObjectTypes.Reference.IsType(neg))
                        {
                            lstr[i] = (ObjectTypes.Reference)neg;
                        }
                        else
                        {
                            lstr[i] = new ObjectTypes.Reference(neg);
                        }
                    }
                    ObjectTypes.Matrix mat = new ObjectTypes.Matrix(lstr);
                    return mat;

                }
                else if (ObjectTypes.DateTime.IsType(right))
                {
                    if (rv is DateTime)
                        rv = new TimeSpan(-Convert.ToDateTime(rv).Ticks);
                    return new ObjectTypes.DateTime(-(TimeSpan)rv);
                }
                else
                {
                    return new ObjectTypes.SystemMessage(ObjectTypes.SystemMessage.MessageType.defer);
                }
            }
            else
            {
                return new ObjectTypes.SystemMessage(ObjectTypes.SystemMessage.MessageType.defer);
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorSubtract(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Number.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Number(((ObjectTypes.Number)left).BigDecValue() - ((ObjectTypes.Number)right).BigDecValue());

            }
            else if (ObjectTypes.Complex.IsType(left) & ObjectTypes.Complex.IsType(right))
            {
                return new ObjectTypes.Complex((System.Numerics.Complex)lv - (System.Numerics.Complex)rv);
            }
            else if (ObjectTypes.Complex.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Complex((System.Numerics.Complex)lv - (double)(rv));
            }
            else if (ObjectTypes.Number.IsType(left) & ObjectTypes.Complex.IsType(right))
            {
                return new ObjectTypes.Complex((double)(lv) - (System.Numerics.Complex)rv);

            }
            else if (ObjectTypes.Set.IsType(left) || ObjectTypes.HashSet.IsType(left))
            {
                IDictionary<ObjectTypes.Reference, ObjectTypes.Reference> dict = (IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)lv;
                if (ObjectTypes.Set.IsType(right) || ObjectTypes.HashSet.IsType(right))
                {
                    // difference
                    dict = _eval.Internals.Difference(dict, (IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)rv);
                }
                else if (ObjectTypes.Matrix.IsType(right))
                {
                    dict = _eval.Internals.Difference(dict, _eval.Internals.ToSet((List<ObjectTypes.Reference>)rv));
                }
                else
                {
                    dict[new ObjectTypes.Reference(rv)] = null;
                }
                return new ObjectTypes.Set(dict);

            }
            else if (ObjectTypes.Matrix.IsType(left) & ObjectTypes.Matrix.IsType(right))
            {
                List<ObjectTypes.Reference> lstl = (List<ObjectTypes.Reference>)lv;
                List<ObjectTypes.Reference> lstr = (List<ObjectTypes.Reference>)rv;
                for (int i = 0; i <= Math.Min(lstl.Count, lstr.Count) - 1; i++)
                {
                    ObjectTypes.EvalObjectBase sum = BinaryOperatorSubtract(lstl[i].ResolveObj(), lstr[i].ResolveObj());
                    if (ObjectTypes.Reference.IsType(sum))
                    {
                        lstl[i] = (ObjectTypes.Reference)sum;
                    }
                    else
                    {
                        lstl[i] = new ObjectTypes.Reference(sum);
                    }
                }
                ObjectTypes.Matrix mat = new ObjectTypes.Matrix(lstl);
                return mat;

            }
            else if (ObjectTypes.DateTime.IsType(left) & ObjectTypes.DateTime.IsType(right))
            {
                if (lv is DateTime)
                    lv = new TimeSpan(Convert.ToDateTime(lv).Ticks);
                if (rv is DateTime)
                    rv = new TimeSpan(Convert.ToDateTime(rv).Ticks);
                return new ObjectTypes.DateTime((TimeSpan)lv - (TimeSpan)rv);

            }
            else
            {
                throw new SyntaxException("Invalid Subtraction");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorMultiply(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {

            if (left == null)
                throw new SyntaxException("Invalid Multiplication");
            if ((ObjectTypes.Number.IsType(left) && ObjectTypes.DateTime.IsType(right)) ||
                    (ObjectTypes.Number.IsType(left) && ObjectTypes.Matrix.IsType(right)) ||
                    (ObjectTypes.Number.IsType(left) && ObjectTypes.Complex.IsType(right)) ||
                    (ObjectTypes.Number.IsType(left) && ObjectTypes.Text.IsType(right)))
            {
                return BinaryOperatorMultiply(right, left);
            }
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Number.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Number(((ObjectTypes.Number)left).BigDecValue() * ((ObjectTypes.Number)right).BigDecValue());

            }
            else if (ObjectTypes.Complex.IsType(left) & ObjectTypes.Complex.IsType(right))
            {
                return new ObjectTypes.Complex((System.Numerics.Complex)lv * (System.Numerics.Complex)lv);
            }
            else if (ObjectTypes.Complex.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Complex((System.Numerics.Complex)lv * (double)(rv));

            }
            else if (ObjectTypes.DateTime.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                if (lv is DateTime)
                    lv = new TimeSpan(Convert.ToDateTime(lv).Ticks);
                return new ObjectTypes.DateTime(new TimeSpan(Convert.ToInt64((double)(rv) * ((TimeSpan)lv).Ticks)));

            }
            else if (ObjectTypes.Text.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                string origstr = Convert.ToString(lv);
                string strcat = origstr;
                while (strcat.Length * 2 < origstr.Length * (double)(rv))
                {
                    strcat += strcat;
                }
                while (strcat.Length < origstr.Length * (double)(rv))
                {
                    strcat += origstr;
                }
                return new ObjectTypes.Text(strcat);

            }
            else if (ObjectTypes.Matrix.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                // scalar multiply, use ** to duplicate
                return ObjectTypes.DetectType(_eval.Internals.Scale((List<ObjectTypes.Reference>)lv, rv));

            }
            else if (ObjectTypes.Matrix.IsType(left) & ObjectTypes.Matrix.IsType(right))
            {
                // matrix multiplication (for appropriate matrices) or dot product (for vectors)
                return ObjectTypes.DetectType(_eval.Internals.Multiply(
                    (List<ObjectTypes.Reference>)left.GetValue(),
                    (List<ObjectTypes.Reference>)right.GetValue()));

            }
            else if (ObjectTypes.Set.IsType(left) & ObjectTypes.Matrix.IsType(right) || ObjectTypes.HashSet.IsType(left) & ObjectTypes.Matrix.IsType(right))
            {
                return new ObjectTypes.Set(_eval.Internals.Intersect((IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)lv, _eval.Internals.ToSet((List<ObjectTypes.Reference>)rv)));

            }
            else if (ObjectTypes.Matrix.IsType(left) & ObjectTypes.Set.IsType(right) || ObjectTypes.HashSet.IsType(right))
            {
                return BinaryOperatorMultiply(right, left);

            }
            else if (ObjectTypes.Set.IsType(left) & ObjectTypes.Set.IsType(right) || ObjectTypes.HashSet.IsType(left) & ObjectTypes.Set.IsType(right))
            {
                return new ObjectTypes.Set(_eval.Internals.Intersect((IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)lv, (IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)rv));

            }
            else if (ObjectTypes.Boolean.IsType(left) & ObjectTypes.Boolean.IsType(right))
            {
                return BinaryOperatorAnd(left, right);
                // * = AND

            }
            else
            {
                throw new SyntaxException("Invalid Multiplication");
            }
        }

        // used for duplication
        private ObjectTypes.EvalObjectBase BinaryOperatorDuplicateCross(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if ((ObjectTypes.Number.IsType(left) & ObjectTypes.Matrix.IsType(right)) || (ObjectTypes.Complex.IsType(left) & ObjectTypes.Matrix.IsType(right)))
            {
                return BinaryOperatorDuplicateCross(right, left);
            }

            if (ObjectTypes.Matrix.IsType(left) & (ObjectTypes.Number.IsType(right) || ObjectTypes.Complex.IsType(right)))
            {
                // duplicate
                List<ObjectTypes.Reference> lst = new List<ObjectTypes.Reference>((List<ObjectTypes.Reference>)left.GetValue());
                object lv = left.GetDeepCopy().GetValue();
                object rv = right.GetValue();
                List<ObjectTypes.Reference> origlst = new List<ObjectTypes.Reference>(lst);
                while (lst.Count * 2 < origlst.Count * (double)(rv))
                {
                    lst.AddRange((List<ObjectTypes.Reference>)new ObjectTypes.Matrix(lst).GetDeepCopy().GetValue());
                }
                while (lst.Count < origlst.Count * (double)(rv))
                {
                    lst.AddRange((List<ObjectTypes.Reference>)new ObjectTypes.Matrix(origlst).GetDeepCopy().GetValue());
                }
                return new ObjectTypes.Matrix(lst);

            }
            else if (ObjectTypes.Matrix.IsType(left) & ObjectTypes.Matrix.IsType(right))
            {
                // cross product
                return new ObjectTypes.Matrix((List<ObjectTypes.Reference>)_eval.Internals.Cross((List<ObjectTypes.Reference>)left.GetValue(), (List<ObjectTypes.Reference>)right.GetValue()));
            }
            else
            {
                return BinaryOperatorMultiply(left, right);
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorDivide(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null || right == null)
                throw new SyntaxException("Invalid Division");
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Number.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Number(((ObjectTypes.Number)left).BigDecValue() / ((ObjectTypes.Number)right).BigDecValue());

            }
            else if (ObjectTypes.Complex.IsType(left) & ObjectTypes.Complex.IsType(right))
            {
                return new ObjectTypes.Complex((System.Numerics.Complex)lv / (System.Numerics.Complex)lv);
            }
            else if (ObjectTypes.Complex.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Complex((System.Numerics.Complex)lv / (double)(rv));
            }
            else if (ObjectTypes.Number.IsType(left) & ObjectTypes.Complex.IsType(right))
            {
                return new ObjectTypes.Complex((double)(lv) / (System.Numerics.Complex)rv);

            }
            else if (ObjectTypes.Matrix.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                // scalar division

                return ObjectTypes.DetectType(_eval.Internals.Scale((List<ObjectTypes.Reference>)lv, BinaryOperatorDivide(new ObjectTypes.Number(1), right).GetValue()));

            }
            else if (ObjectTypes.DateTime.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                if (lv is DateTime)
                    lv = new TimeSpan(Convert.ToDateTime(lv).Ticks);
                return new ObjectTypes.DateTime(new TimeSpan(Convert.ToInt64(((TimeSpan)lv).Ticks / (double)(rv))));

            }
            else
            {
                throw new SyntaxException("Invalid Division");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorDivideFloor(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null || right == null)
                throw new SyntaxException("Invalid Floor Div.");
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Number.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Number(Math.Floor((double)(((ObjectTypes.Number)left).BigDecValue() / ((ObjectTypes.Number)right).BigDecValue())));

            }
            else if (ObjectTypes.Complex.IsType(left) | ObjectTypes.Complex.IsType(right))
            {
                ObjectTypes.Complex cplx = (ObjectTypes.Complex)BinaryOperatorDivide(left, right);
                return new ObjectTypes.Complex(Math.Floor(cplx.Real), Math.Floor(cplx.Imag));

            }
            else
            {
                throw new SyntaxException("Invalid Floor Div.");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorModulo(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null)
                throw new SyntaxException("Invalid Modulo");
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Number.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Number(_eval.Internals.Modulo(((ObjectTypes.Number)left).BigDecValue(),
                    ((ObjectTypes.Number)right).BigDecValue()));

            }
            else if (ObjectTypes.DateTime.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                if (lv is DateTime)
                    lv = new TimeSpan(Convert.ToDateTime(lv).Ticks);
                return new ObjectTypes.DateTime(new TimeSpan(Convert.ToInt64(((TimeSpan)lv).Ticks % (double)(rv))));
            }
            else
            {
                throw new SyntaxException("Invalid Modulo");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorExponent(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null)
                throw new SyntaxException("Invalid Exponent");
            try
            {
                if (left is ObjectTypes.Number && right is ObjectTypes.Number)
                {
                    return ObjectTypes.DetectType(_eval.Internals.Pow(((ObjectTypes.Number)left).BigDecValue(), ((ObjectTypes.Number)right).BigDecValue()));
                }
                else
                {
                    return ObjectTypes.DetectType(_eval.Internals.Pow(left.GetValue(), right.GetValue()));
                }
            }
            catch (MathException ex)
            {
                throw ex;
            }
            catch
            {
                throw new SyntaxException("Invalid Exponent");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorOr(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Boolean.IsType(left) & ObjectTypes.Boolean.IsType(right))
            {
                try
                {
                    return new ObjectTypes.Boolean(Convert.ToBoolean(_eval.Internals.Eval(lv.ToString())) || Convert.ToBoolean(_eval.Internals.Eval(rv.ToString())));
                }
                catch
                {
                    throw new SyntaxException("Operator or: Expression must produce a boolean value");
                }

            }
            else if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right) && !double.IsNaN((double)(lv)) && !double.IsNaN((double)(rv)))
            {
                return new ObjectTypes.Boolean(Math.Round((double)(lv), 15) != 0 | Math.Round((double)(rv), 15) != 0);
            }
            else
            {
                throw new SyntaxException("Invalid logical or operation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorBitwiseOr(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null || right == null)
                return new ObjectTypes.Number(double.NaN);
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Boolean.IsType(left) & ObjectTypes.Boolean.IsType(right))
            {
                try
                {
                    return new ObjectTypes.Boolean((bool)lv || (bool)rv);
                }
                catch
                {
                    throw new SyntaxException("Invalid bitwise or operation");
                }

            }
            else if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right) && !double.IsNaN((double)(lv)) && !double.IsNaN((double)(rv)))
            {
                return new ObjectTypes.Number(Convert.ToInt64(lv) | Convert.ToInt64(rv));
            }
            else
            {
                throw new SyntaxException("Invalid bitwise or operation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorAnd(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Boolean.IsType(left) && ObjectTypes.Boolean.IsType(right))
            {
                return new ObjectTypes.Boolean((bool)lv && (bool)rv);
            }
            else if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right) && !double.IsNaN((double)(lv)) && !double.IsNaN((double)(rv)))
            {
                return new ObjectTypes.Boolean(Math.Round((double)(lv), 15) != 0 & Math.Round((double)(rv), 15) != 0);
            }
            else
            {
                throw new SyntaxException("Invalid logical and operation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorBitwiseAnd(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Boolean.IsType(left) && ObjectTypes.Boolean.IsType(right))
            {
                try
                {
                    return new ObjectTypes.Boolean(Convert.ToBoolean(_eval.Internals.Eval(lv.ToString())) && Convert.ToBoolean(_eval.Internals.Eval(rv.ToString())));
                }
                catch
                {
                    throw new SyntaxException("Invalid bitwise and operation");
                }
            }
            else if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right) && !double.IsNaN((double)(lv)) && !double.IsNaN((double)(rv)))
            {
                return new ObjectTypes.Number(Convert.ToInt64(lv) & Convert.ToInt64(rv));
            }
            else
            {
                throw new SyntaxException("Invalid bitwise and operation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorXor(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Boolean.IsType(left) & ObjectTypes.Boolean.IsType(right))
            {
                try
                {
                    return new ObjectTypes.Boolean((bool)lv ^ (bool)rv);
                }
                catch
                {
                    throw new SyntaxException("Operator xor: Expression must produce a boolean value");
                }
            }
            else if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right) && !double.IsNaN((double)(lv)) && !double.IsNaN((double)(rv)))
            {
                return new ObjectTypes.Boolean(Math.Round((double)(lv), 15) != 0 ^ Math.Round((double)(rv), 15) != 0);
            }
            else
            {
                throw new SyntaxException("Invalid logical xor operation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorBitwiseXor(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Boolean.IsType(left) & ObjectTypes.Boolean.IsType(right))
            {
                try
                {
                    return new ObjectTypes.Boolean(Convert.ToBoolean(_eval.Internals.Eval(lv.ToString())) ^ Convert.ToBoolean(_eval.Internals.Eval(rv.ToString())));
                }
                catch
                {
                    throw new SyntaxException("Invalid bitwise xor operation");
                }
            }
            else if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right) && !double.IsNaN((double)(lv)) && !double.IsNaN((double)(rv)))
            {
                return new ObjectTypes.Number(Convert.ToInt64(lv) ^ Convert.ToInt64(rv));
                // this operator doubles as the symmetric difference operator for sets
            }
            else if (ObjectTypes.Set.IsType(left) || ObjectTypes.HashSet.IsType(left))
            {
                IDictionary<ObjectTypes.Reference, ObjectTypes.Reference> dict = (IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)lv;

                if (ObjectTypes.Set.IsType(right) || ObjectTypes.HashSet.IsType(right))
                {
                    // symmetric difference
                    dict = _eval.Internals.DifferenceSymmetric(dict, (IDictionary<ObjectTypes.Reference, ObjectTypes.Reference>)rv);
                }
                else if (ObjectTypes.Matrix.IsType(right))
                {
                    dict = _eval.Internals.DifferenceSymmetric(dict, _eval.Internals.ToSet((List<ObjectTypes.Reference>)rv));
                }
                else
                {
                    dict[new ObjectTypes.Reference(rv)] = null;
                }
                return new ObjectTypes.Set(dict);
            }
            else
            {
                throw new SyntaxException("Invalid bitwise xor operation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorShl(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right) && !double.IsNaN((double)(lv)) && !double.IsNaN((double)(rv)))
            {
                return new ObjectTypes.Number(Convert.ToInt64(lv) << Convert.ToInt32(rv));
            }
            else
            {
                throw new SyntaxException("Invalid << (bitwise shl) operation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorShr(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Number.IsType(left) && ObjectTypes.Number.IsType(right) && !double.IsNaN((double)(lv)) && !double.IsNaN((double)(rv)))
            {
                return new ObjectTypes.Number(Convert.ToInt64(lv) >> Convert.ToInt32(rv));
            }
            else
            {
                throw new SyntaxException("Invalid >> (bitwise shl) operation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorChoose(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            if (ObjectTypes.Number.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                return new ObjectTypes.Number(_eval.Internals.Comb((double)(lv), (double)(rv)));
            }
            else
            {
                throw new SyntaxException("Invalid types for the choose (combinations) operator");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            try
            {
                if (ObjectTypes.Reference.IsType(left) && ObjectTypes.Reference.IsType(right) &&
                            ObjectTypes.Reference.IsType(((ObjectTypes.Reference)right).GetRefObject()))
                {
                    ObjectTypes.Reference lr = (ObjectTypes.Reference)left;
                    ObjectTypes.Reference rr = (ObjectTypes.Reference)right;
                    if (!(lr.ResolveObj() is ObjectTypes.Lambda))
                    {
                        // if we are assigning a reference

                        // try to avoid circular references

                        if (!object.ReferenceEquals(lr, rr))
                        {
                            if (rr.Node != null)
                            {
                                // set node
                                lr.SetNode(rr.Node);
                                lr.SetValue(new ObjectTypes.Reference(rr.Node));
                            }
                            else
                            {
                                // set object
                                left.SetValue(new ObjectTypes.Reference(rr.ResolveObj()));
                            }
                        }
                    }
                    else
                    {
                        left = ((ObjectTypes.Reference)right).ResolveObj();
                    }
                }
                if (left is ObjectTypes.Lambda)
                {
                    if (right is ObjectTypes.Reference)
                        right = ((ObjectTypes.Reference)right).ResolveObj();
                    ObjectTypes.Lambda lamb = (ObjectTypes.Lambda)left;
                    if (right is ObjectTypes.Lambda)
                    {
                        ObjectTypes.Lambda lambr = (ObjectTypes.Lambda)right;
                        _eval.SetVariable(lamb.FunctionName,
                            lambr.ToString(_eval));
                    }
                    else
                    {
                        _eval.SetVariable(lamb.FunctionName, right is ObjectTypes.Number ?
                            ((ObjectTypes.Number)right).BigDecValue() :
                             right.GetValue());
                    }
                    return lamb;
                }
                else
                {
                    if (ObjectTypes.Reference.IsType(right))
                        right = ((ObjectTypes.Reference)right).ResolveObj();
                    if (ObjectTypes.Reference.IsType(left))
                        left = ((ObjectTypes.Reference)left).ResolveRef();
                    // if we are assigning a plain value
                    if (right is ObjectTypes.Number)
                        left.SetValue(((ObjectTypes.Number)right.GetDeepCopy()).BigDecValue());
                    else
                        left.SetValue(right.GetDeepCopy().GetValue());
                }
                return left;
                //ex As Exception
            }
            catch(NullReferenceException)
            {
                throw new EvaluatorException("Empty assignment disallowed.");
            }
            catch(Exception)
            {
                throw new EvaluatorException("Assignment operation failed.");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorOpAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right, Func<ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase, ObjectTypes.EvalObjectBase> op)
        {

            try
            {
                if (ObjectTypes.Reference.IsType(left) || ObjectTypes.Tuple.IsType(left))
                {
                    ObjectTypes.Reference lr = (ObjectTypes.Reference)left;
                    // assign object
                    if (ObjectTypes.Reference.IsType(right))
                        right = ((ObjectTypes.Reference)right).ResolveObj();
                    return BinaryOperatorAssign(left, op(lr.ResolveObj().GetDeepCopy(), right.GetDeepCopy()));

                }
                else
                {
                    return op(left, right);
                    // not a reference? just use normal operator
                }
                //ex As Exception
            }
            catch
            {
                throw new EvaluatorException("Operator and assignment ([op]=) operation failed");
            }
        }


        private ObjectTypes.EvalObjectBase BinaryOperatorAddAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorAdd);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorSubtractAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorSubtract);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorMultiplyAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorMultiply);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorDivideAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorDivide);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorDuplicateAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorDuplicateCross);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorDivideFloorAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorDivideFloor);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorModuloAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorModulo);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorExponentAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorExponent);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorBitwiseOrAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorBitwiseOr);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorBitwiseAndAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorBitwiseAnd);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorBitwiseXorAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorBitwiseXor);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorShlAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorShl);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorShrAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorShr);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorConcatAssign(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return BinaryOperatorOpAssign(left, right, BinaryOperatorConcat);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorIncrement(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            try
            {
                if (ObjectTypes.Reference.IsType(right))
                    right = ((ObjectTypes.Reference)right).GetRefObject();
                if (ObjectTypes.Reference.IsType(left) && (right == null || right.ToString(_eval) == "NaN"))
                {
                    object lv = ((ObjectTypes.Reference)left).Resolve();
                    if (lv is double)
                    {
                        // add one to numbers
                        left.SetValue((double)(lv) + 1);
                    }
                    else if (lv is BigDecimal)
                    {
                        // add one to numbers
                        left.SetValue((BigDecimal)lv + 1);
                    }
                    else if (lv is System.DateTime)
                    {
                        left.SetValue(Convert.ToDateTime(lv).AddDays(1));
                        // add one day to dates
                    }
                    else if (lv is TimeSpan)
                    {
                        left.SetValue(((TimeSpan)lv).Add(new TimeSpan(0, 1, 0)));
                        // add one minute to timespans
                    }
                    else
                    {
                        //otherwise ??? we don't know what to do, so we'll try the normal add operation.
                        return BinaryOperatorAdd(left, right);
                    }
                    return left;
                }
                else
                {
                    if (ObjectTypes.Reference.IsType(left))
                        left = ((ObjectTypes.Reference)left).ResolveObj();
                    if (ObjectTypes.Reference.IsType(right))
                        right = ((ObjectTypes.Reference)right).ResolveObj();
                    // if it is not a reference we see the operation as ++, for example in 1++1 = 2
                    return BinaryOperatorAdd(left, right);
                }
            }
            catch(Exception ex)
            {
                throw new EvaluatorException("Invalid increment (++) operation\n" + ex.ToString());
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorDecrement(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            try
            {
                if (ObjectTypes.Reference.IsType(right))
                    right = ((ObjectTypes.Reference)right).GetRefObject();
                if (ObjectTypes.Reference.IsType(left) && (right == null || right.ToString(_eval) == "NaN"))
                {
                    object lv = ((ObjectTypes.Reference)left).Resolve();
                    if (lv is double)
                    {
                        left.SetValue((double)(lv) - 1);
                        // subtract one from numbers
                    }
                    else if (lv is BigDecimal)
                    {
                        left.SetValue((BigDecimal)lv - 1);
                        // subtract one from numbers
                    }
                    else if (lv is System.DateTime)
                    {
                        left.SetValue(Convert.ToDateTime(lv).AddDays(-1));
                        // subtract one day from dates
                    }
                    else if (lv is TimeSpan)
                    {
                        left.SetValue(((TimeSpan)lv).Add(new TimeSpan(0, -1, 0)));
                        // subtract one minute from timespans
                    }
                    else
                    {
                        //otherwise ??? we don't know what to do, so we'll try the normal subtract operation.
                        return BinaryOperatorSubtract(left, right);
                    }
                    return left;
                }
                else
                {
                    // if it is not a reference we see the operation as --, for example in 1--1 = 2
                    return BinaryOperatorAdd(left, right);
                }
            }
            catch
            {
                throw new EvaluatorException("Invalid decrement (--) operation");
            }
        }

        /// <summary>
        /// 'Smart' equals operator that functions as an assignment or equalTo operator as needed
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        private ObjectTypes.EvalObjectBase BinaryOperatorAutoEqual(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (!(ObjectTypes.Reference.IsType(left) || ObjectTypes.Tuple.IsType(left) ||
                        ObjectTypes.Tuple.IsType(right)) || ConditionMode)
            {
                if (ObjectTypes.Reference.IsType(left)) left = ((ObjectTypes.Reference)left).ResolveObj();
                if (ObjectTypes.Reference.IsType(right)) right = ((ObjectTypes.Reference)right).ResolveObj();
                if (left is ObjectTypes.Lambda)
                    return new ObjectTypes.SystemMessage(ObjectTypes.SystemMessage.MessageType.defer);
                return BinaryOperatorEqualTo(left, right);
            }
            else
            {
                return new ObjectTypes.SystemMessage(ObjectTypes.SystemMessage.MessageType.defer);
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorEqualTo(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return new ObjectTypes.Boolean(ObjectComparer.CompareObjs(left, right) == 0);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorNotEqualTo(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return UnaryOperatorNot(BinaryOperatorEqualTo(left, right));
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorGreaterThan(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return new ObjectTypes.Boolean(ObjectComparer.CompareObjs(left, right) == 1);
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorGreaterThanOrEqualTo(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return new ObjectTypes.Boolean(ObjectComparer.CompareObjs(left, right) >= 0);
        }


        private ObjectTypes.EvalObjectBase BinaryOperatorLessThan(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return UnaryOperatorNot(BinaryOperatorGreaterThanOrEqualTo(left, right));
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorLessThanOrEqualTo(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            return UnaryOperatorNot(BinaryOperatorGreaterThan(left, right));
        }


        private ObjectTypes.EvalObjectBase BinaryOperatorConcat(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            object lv = left.GetValue();
            object rv = right.GetValue();
            try
            {
                // do not append NaN
                if (rv.ToString() == "NaN")
                    rv = "";
                if (lv.ToString() == "NaN")
                    lv = "";
                return new ObjectTypes.Text((left is ObjectTypes.Text ? lv.ToString() : left.ToString(_eval)) +
                    (right is ObjectTypes.Text ? rv.ToString() : right.ToString(_eval)));
            }
            catch
            {
                throw new SyntaxException("Invalid concatenation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorCommaTuple(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;

            object lv = left.GetValue();
            object rv = right.GetValue();

            try
            {
                if (ObjectTypes.Tuple.IsType(left))
                {
                    return new ObjectTypes.Tuple((((object[])lv).Concat(new[] { right })));
                }
                else
                {
                    return new ObjectTypes.Tuple(new[]{
                        left,
                        right
                    });
                }
                //ex As Exception
            }
            catch
            {
                throw new SyntaxException("Invalid comma concatenation");
            }
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorColon(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;
            object lv = left.GetValue();
            object rv = right.GetValue();
            try
            {
                if (rv == null || rv.ToString() == "NaN")
                    return left;
                if (ObjectTypes.Tuple.IsType(left))
                {
                    List<ObjectTypes.Reference> lst = new List<ObjectTypes.Reference>((ObjectTypes.Reference[])lv);
                    lst[lst.Count - 1] = new ObjectTypes.Reference(BinaryOperatorCommaTuple(lst[lst.Count - 1].GetRefObject(), right));
                    return new ObjectTypes.Tuple(lst);
                }
                else
                {
                    ObjectTypes.Reference @ref = new ObjectTypes.Reference(new ObjectTypes.Tuple(new[]{
                        lv,
                        rv
                    }));
                    return new ObjectTypes.Tuple(new[] { @ref });
                }
                //ex As Exception
            }
            catch
            {
                throw new SyntaxException("Invalid comma concatenation");
            }
        }

        /// <summary>
        /// The elvis operator
        /// </summary>
        private ObjectTypes.EvalObjectBase BinaryOperatorElvis(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (_eval.Internals.IsTrue(left.GetValue()))
                return left;
            else
                return right;
        }

        private ObjectTypes.EvalObjectBase BinaryOperatorExp10(ObjectTypes.EvalObjectBase left, ObjectTypes.EvalObjectBase right)
        {
            if (ObjectTypes.Number.IsType(left) & ObjectTypes.Number.IsType(right))
            {
                BigDecimal lv = ((ObjectTypes.Number)left).BigDecValue();
                BigDecimal rv = ((ObjectTypes.Number)right).BigDecValue();
                return new ObjectTypes.Number(new BigDecimal(mantissa: lv.Mantissa,
                    exponent: (int)(lv.Exponent + rv), sigFigs: lv.SigFigs));
            }
            else
            {
                throw new SyntaxException("Invalid types for the E (exp10) operator");
            }
        }
        #endregion
    }
}