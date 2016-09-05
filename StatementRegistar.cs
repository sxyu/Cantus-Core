using System;
using System.Collections.Generic;
using System.Linq;

using Cantus.Core.Exceptions;

using static Cantus.Core.StatementRegistar.StatementResult;
using static Cantus.Core.CantusEvaluator.ObjectTypes;
using Cantus.Core.CommonTypes;
using static Cantus.Core.CantusEvaluator;
using static Cantus.Core.Scoping;

namespace Cantus.Core
{
    internal class StatementRegistar : IDisposable
    {

        #region "Statement Classes & Structs"
        /// <summary>
        /// Class representing a block like if or else
        /// </summary>
        public class Block
        {
            /// <summary>
            /// The keyword that started the block, e.g. if
            /// </summary>
            /// <returns></returns>
            public string Keyword { get; set; }

            /// <summary>
            /// The arguments to the block
            /// </summary>
            /// <returns></returns>
            public string Argument { get; set; }

            /// <summary>
            /// The content of the block as a string
            /// </summary>
            /// <returns></returns>
            public string Content { get; set; }

            public Block(string keyword, string argument, string content)
            {
                this.Keyword = keyword;
                this.Argument = argument;
                this.Content = content;
            }
        }

        /// <summary>
        /// Structure representing the result of processing a statement
        /// </summary>
        public struct StatementResult
        {
            public enum ExecCode
            {
                /// <summary>
                /// continue executing script
                /// </summary>
                resume = 0,
                /// <summary>
                /// stop executing and directly return the value to the highest level
                /// </summary>
                @return = 1,
                /// <summary>
                /// stop executing and continue executing the parent loop
                /// </summary>
                @continue = 2,
                /// <summary>
                /// stop executing and break out of the parent loop
                /// </summary>
                @break = 3,
                /// <summary>
                /// stop executing and break out of this level only 
                /// </summary>
                breakLevel = 4
            }

            /// <summary>
            /// Code determining what the evaluator does after executing this statement
            /// </summary>
            public ExecCode Code { get; }

            /// <summary>
            /// Value representing the value obtained by executing this statement
            /// </summary>
            public object Value { get; }

            public StatementResult(object value, ExecCode code = ExecCode.resume)
            {
                this.Code = code;
                this.Value = value;
            }
        }

        public class Statement
        {
            /// <summary>
            /// The main keywords (e.g. if) for this statement. These keywords start off the statement. 
            /// All keywords must be lower case.
            /// </summary>
            /// <returns></returns>
            public string[] MainKeywords { get; }
            // e.g. if

            /// <summary>
            /// The auxiliary keywords (e.g. elif, else) for this statement. These keywords continue the statement. 
            /// All keywords must be lower case.
            /// </summary>
            /// <returns></returns>
            public string[] AuxKeywords { get; }
            // e.g. elif, else

            /// <summary>
            /// If true, this keyword will be processed as a block if possible
            /// </summary>
            /// <returns></returns>
            public bool BlockLevel { get; }

            /// <summary>
            /// If true, this keyword is allowed in declarative mode (when creating classes, for example
            /// </summary>
            /// <returns></returns>
            public bool Declarative { get; }

            /// <summary>
            /// Dictionary specifying whether each key needs a argument
            /// </summary>
            /// <returns></returns>
            public Dictionary<string, bool> ArgumentExpected { get; set; }

            /// <summary>
            /// Process this keyword
            /// </summary>
            /// <returns></returns>
            public Func<List<Block>, StatementResult> Execute { get; }

            /// <summary>
            /// Create a new Statement
            /// </summary>
            /// <param name="mainKeywords">The lmain keywords (e.g. if) for this statement. 
            /// These keywords start off the statement. All keywords must be lower case.</param>
            /// <param name="auxKeywords">The auxiliary keywords (e.g. elif, else) for this statement.
            /// These keywords continue the statement. All keywords must be lower case.</param>
            /// <param name="execute">The definition of the statement to execute when processing</param>
            ///
            /// <param name="blockLevel">If true, allows attaching of indented blocks to this statement 
            /// (e.g. for if statement; not required for statements like return)</param>
            /// <param name="argumentExpected">If the value of this dictionary with 
            ///  key name equal to the block name
            ///  is false, forbids arguments to this block
            ///  (raises an error when an argument is found) for use with statements like continue, try</param>
            public Statement(IEnumerable<string> mainKeywords, IEnumerable<string> auxKeywords, Func<List<Block>, StatementResult> execute, bool blockLevel = true, Dictionary<string, bool> argumentExpected = null, bool declarative = false)
            {
                this.MainKeywords = mainKeywords.ToArray();
                this.AuxKeywords = auxKeywords.ToArray();
                this.Execute = execute;
                this.BlockLevel = blockLevel;
                this.Declarative = declarative;
                if (argumentExpected == null)
                {
                    this.ArgumentExpected = new Dictionary<string, bool>();
                }
                else
                {
                    this.ArgumentExpected = argumentExpected;
                }
            }
            /// <summary>
            /// Create a new Statement
            /// </summary>
            /// <param name="mainKeywords">The lmain keywords (e.g. if) for this statement. 
            /// These keywords start off the statement. All keywords must be lower case.</param>
            /// <param name="execute">The definition of the statement to execute when processing</param>
            ///
            /// <param name="blockLevel">If true, allows attaching of indented blocks to this statement 
            /// (e.g. for if statement; not required for statements like return)</param>
            /// <param name="argumentExpected">If the value of this dictionary with 
            ///  key name equal to the block name
            ///  is false, forbids arguments to this block
            ///  (raises an error when an argument is found) for use with statements like continue, try</param>
            public Statement(IEnumerable<string> mainKeywords, Func<List<Block>, StatementResult> execute, bool blockLevel = true, Dictionary<string, bool> argumentExpected = null, bool declarative = false)
            {
                this.MainKeywords = mainKeywords.ToArray();
                this.AuxKeywords = new string[]{ };
                this.Execute = execute;
                this.BlockLevel = blockLevel;
                this.Declarative = declarative;
                if (argumentExpected == null)
                {
                    this.ArgumentExpected = new Dictionary<string, bool>();
                }
                else
                {
                    this.ArgumentExpected = argumentExpected;
                }
            }
        }

        #endregion

        #region "Variable & Const Declarations"

        /// <summary>
        /// Maximum length in characters of a single statement
        /// </summary>

        public const int MAX_STATEMENT_LENGTH = 9;
        /// <summary>
        /// Maximum times to loop
        /// </summary>
        public int LoopLimit { get; set; }

        /// <summary>
        /// If true, limits all loops to at most 10000 repetitions
        /// </summary>
        public bool LimitLoops { get; set; }


        private CantusEvaluator _eval;
        private Dictionary<string, Statement> _keywords;

        private Dictionary<string, Statement> _mainKeywords;
        #endregion
        private bool _die = false;

        #region "Registration"

        /// <summary>
        /// Create a new Statement Registar for registering and accessing statements like if/else
        /// </summary>
        public StatementRegistar(CantusEvaluator parent)
        {
            _eval = parent;
            _keywords = new Dictionary<string, Statement>();
            _mainKeywords = new Dictionary<string, Statement>();
            RegisterStatements();
        }

        /// <summary>
        /// Register all statements
        /// </summary>
        private void RegisterStatements()
        {
            // register statements here
            // all keywords must be lower case

            // FORMAT Register(New Statement(new[]{[main keywords]}, new[]{[aux keywords]},
            //                {[allowed argument numbers]}, AddressOf [definition], [block level?], 
            //                 [dictionary: argument expected for each keyword?], [declarative?]))
            // or     Register(New Statement(new[]{[main keywords]}, new[]{[allowed argument numbers]}, 
            //                 AddressOf [definition], [block level?], 
            //                 [dictionary: argument expected for each keyword?], [declarative?]))

            Register(new Statement(new[]{ "if" }, new[]{
                "elif",
				"else"
            }, StatementIfElifElse));
            Register(new Statement(new[]{ "while" }, StatementWhile));
            Register(new Statement(new[]{ "until" }, StatementUntil));
            Register(new Statement(new[]{ "repeat" }, StatementRepeat));
            Register(new Statement(new[]{ "run" }, StatementRun, true, new Dictionary<string, bool> { {
                "run",
                false
            } }));
            Register(new Statement(new[]{ "for" }, StatementFor));

            Register(new Statement(new[]{ "try" }, new[]{
                "catch",
				"finally"
            }, StatementTryCatchFinally, true, new Dictionary<string, bool> {
                {
                    "try",
                    false
                },
                {
                    "catch",
                    true
                },
                {
                    "finally",
                    false
                }
            }));
            Register(new Statement(new[]{ "with" }, StatementWith));

            Register(new Statement(new[]{ "switch" }, StatementSwitch, true));
            Register(new Statement(new[]{ "case" }, StatementCase, true, declarative: true));

            Register(new Statement(new[]{ "return" }, StatementReturn, false));
            Register(new Statement(new[]{ "break" }, StatementBreak, false, new Dictionary<string, bool> { {
                "break",
                false
            } }));
            Register(new Statement(new[]{ "continue" }, StatementContinue, false, new Dictionary<string, bool> { {
                "continue",
                false
            } }));

            // use to declare local scoped variables or override global variable names
            Register(new Statement(new[]{ "let" }, StatementDeclare, false, declarative: true));
            Register(new Statement(new[]{ "global" }, StatementDeclareGlobal, false, declarative: true));
            Register(new Statement(new[]{
                "private",
                "public",
                "static"
            }, StatementDeclareModifier, true, declarative: true));

            // declare functions
            Register(new Statement(new[]{ "function" }, StatementDeclareFunction, declarative: true));

            // import stuff
            Register(new Statement(new[]{ "import" }, StatementImport, false));
            Register(new Statement(new[]{ "load" }, StatementLoad, false));

            // namespacing
            Register(new Statement(new[]{ "namespace" }, StatementNamespace, true));

            // classes
            Register(new Statement(new[]{ "class" }, StatementClass, true));
        }

        /// <summary>
        /// Register a statement
        /// </summary>
        public void Register(Statement statement)
        {
            foreach (string kwd in statement.MainKeywords)
            {
                _keywords.Add(kwd, statement);
                _mainKeywords.Add(kwd, statement);
            }
            foreach (string kwd in statement.AuxKeywords)
            {
                _keywords.Add(kwd, statement);
            }
        }
        #endregion

        #region "Methods"
        /// <summary>
        /// Returns true if the specified keyword is registered as a main keyword
        /// </summary>
        public bool HasMainKeyword(string kwd)
        {
            return _mainKeywords.ContainsKey(kwd.ToLowerInvariant());
        }

        /// <summary>
        /// Returns true if the specified keyword is registered (as any type of keyword)
        /// </summary>
        /// <returns></returns>
        public bool HasKeyword(string kwd)
        {
            return _keywords.ContainsKey(kwd.ToLowerInvariant());
        }

        /// <summary>
        /// Returns the statement with the keyword, or nothing if it is not registered.
        /// </summary>
        /// <param name="mainOnly">If true, only returns if the keyword is a main keyword</param>
        public Statement StatementWithKeyword(string kwd, bool mainOnly = false)
        {
            if (HasMainKeyword(kwd) || HasKeyword(kwd) && !mainOnly)
            {
                return _keywords[kwd];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the keyword that the specified expression starts with, or blank if it does not match any statements
        /// Also sets expr to the rest of the expression (the argument)
        /// </summary>
        /// <param name="mainOnly">If true, only returns if the keyword is a main keyword</param>
        public string KeywordFromExpr(ref string expr, bool mainOnly = false)
        {
            expr = expr.Trim();
            for (int i = Math.Min(expr.Length, MAX_STATEMENT_LENGTH); i >= 1; i += -1)
            {
                // only try to resolve if this takes up the whole expression or ends with a space
                if (i != expr.Length && expr[i] != ' ')
                    continue;
                string kwd = expr;
                if (i < kwd.Length)
                    kwd = kwd.Remove(i).Trim();
                if (HasMainKeyword(kwd) || HasKeyword(kwd) && !mainOnly)
                {
                    expr = " " + expr.Substring(kwd.Length).Trim();
                    return kwd.ToLowerInvariant();
                }
            }
            return "";
        }
        #endregion

        #region "Statement Declarations"

        #region "Helpers"
        /// <summary>
        /// Evaluates the given expression and determines if it is truthy
        /// </summary>
        private bool TestCond(string expr)
        {
            string scope = _eval.Scope;
            _eval.ParentScope();
            bool res = _eval.Internals.IsTrue(_eval.EvalExprRaw(expr, true, true));
            _eval.Scope = scope;
            return res;
        }

        /// <summary>
        /// Evaluates the given script in a child evaluator and returns the result
        /// </summary>
        private StatementResult Run(string expr, Dictionary<string, object> vars = null, Reference @default = null, bool declarative = false)
        {
            CantusEvaluator newEval = _eval.SubEvaluator();
            if ((vars != null))
            {
                foreach (string k in vars.Keys)
                {
                    newEval.SetVariable(k, vars[k]);
                }
            }
            if ((@default != null))
                newEval.SetDefaultVariable(@default);
            return (StatementResult)newEval.EvalRaw(expr, noSaveAns: true, declarative: declarative, @internal: true);
        }

        /// <summary>
        /// Given a line of text, finds the level of indentation. 
        /// </summary>
        /// <returns></returns>
        private int LineIndentLevel(string line)
        {
            int res = 0;
            for (int i = 0; i <= line.Length - 1; i++)
            {
                if (line[i] == ' ')
                {
                    res += 1;
                }
                else if (line[i] == '\t')
                {
                    res += _eval.SpacesPerTab;
                }
                else
                {
                    return res;
                }
            }
            return 0;
        }

        #endregion

        // statement definitions here
        // FORMAT Private Function Statement[main keywords][aux keywords](blocks As List(Of Block)) As StatementResult

        #region "Block-Level Flow Control Statements"
        private StatementResult StatementIfElifElse(List<Block> blocks)
        {
            Block ifBlock = null;
            List<Block> elifBlocks = new List<Block>();
            Block elseBlock = null;
            foreach (Block block in blocks)
            {
                if (block.Keyword == "if")
                {
                    if (ifBlock == null)
                        ifBlock = block;
                }
                else if (block.Keyword == "else")
                {
                    if (elseBlock == null)
                        elseBlock = block;
                    // elif
                }
                else
                {
                    elifBlocks.Add(block);
                }
            }

            // original if true
            if (TestCond(ifBlock.Argument))
            {
                return Run(ifBlock.Content);
            }
            else
            {
                foreach (Block elifBlock in elifBlocks)
                {
                    // elif true
                    if (TestCond(elifBlock.Argument))
                    {
                        return Run(elifBlock.Content);
                    }
                }
                if (elseBlock != null)
                {
                    return Run(elseBlock.Content);
                }
            }

            return new StatementResult(double.NaN);
        }

        private StatementResult LoopWhile(List<Block> blocks, bool until = false)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("While statement is invalid");
            StatementResult result = new StatementResult(double.NaN);
            int ct = 0;

            while (until ? !TestCond(blocks[0].Argument) : TestCond(blocks[0].Argument))
            {
                if (LimitLoops && ct > LoopLimit)
                    throw new EvaluatorException("Loop limit reached");
                if (_die)
                    throw new EvaluatorException("");

                result = Run(blocks[0].Content);
                switch (result.Code)
                {
                    case ExecCode.@break:
                    case ExecCode.@return:
                    case ExecCode.breakLevel:
                        return new StatementResult(result.Value);
                    case ExecCode.@continue:
                        continue;
                }
                ct += 1;
            }
            if (result.Code == ExecCode.@continue) result = new StatementResult(result.Value);
            return result;
        }

        private StatementResult StatementWhile(List<Block> blocks)
        {
            return LoopWhile(blocks);
        }

        private StatementResult StatementUntil(List<Block> blocks)
        {
            return LoopWhile(blocks, true);
        }

        private StatementResult StatementRepeat(List<Block> blocks)
        {
            BigDecimal times = BigDecimal.Undefined;
            object obj = _eval.EvalExprRaw(blocks[0].Argument, true);
            if (obj is double)
            {
                times = (double)(obj);
            }
            else if (obj is BigDecimal)
            {
                times = (BigDecimal)obj;
            }
            if (times < 1)
                throw new EvaluatorException("Repeat statement: cannot repeat a negative number of times");
            StatementResult result = new StatementResult(double.NaN);
            int ct = 0;

            for (BigDecimal i = 1; i <= times; i += 1)
            {
                if (LimitLoops && ct > LoopLimit)
                    throw new EvaluatorException("Loop limit reached");
                if (_die)
                    throw new EvaluatorException("");

                result = Run(blocks[0].Content);
                switch (result.Code)
                {
                    case ExecCode.@break:
                    case ExecCode.@return:
                    case ExecCode.breakLevel:
                        return new StatementResult(result.Value);
                    case ExecCode.@continue:
                        continue;
                }
                ct += 1;
            }

            if (result.Code == ExecCode.@continue) result = new StatementResult(result.Value);
            return result;
        }

        private StatementResult StatementRun(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Run statement is invalid");

            StatementResult result = Run(blocks[0].Content);
            if (result.Code == ExecCode.@continue) result = new StatementResult(result.Value);
            return result;
        }

        private StatementResult StatementFor(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("For statement is invalid");

            string arg = blocks[0].Argument;
            StatementResult result = new StatementResult(double.NaN);
            int ct = 0;

            // for ... in ...
            if (arg.ToLowerInvariant().Contains(" in "))
            {
                string[] vars = arg.Remove(arg.ToLowerInvariant().IndexOf(" in ")).Split(',');
                for (int i = 0; i <= vars.Length - 1; i++)
                {
                    vars[i] = vars[i].Trim();
                }

                object lstName = _eval.EvalExprRaw(arg.Substring(arg.ToLowerInvariant().IndexOf(" in ") + 4), true);
                List<List<EvalObjectBase>> lstNames = new List<List<EvalObjectBase>>();

                if (lstName is IEnumerable<Reference>)
                {
                    foreach (Reference r in (IEnumerable<Reference>)lstName)
                    {
                        lstNames.Add(new List<EvalObjectBase>(new[]{ r.ResolveObj() }));
                    }
                }
                else if (lstName is IDictionary<Reference, Reference>)
                {
                    foreach (KeyValuePair<Reference, Reference> k in (IDictionary<Reference, Reference>)lstName)
                    {
                        if (k.Value == null)
                        {
                            lstNames.Add(new List<EvalObjectBase>(new[]{ k.Key.ResolveObj() }));
                        }
                        else
                        {
                            lstNames.Add(new List<EvalObjectBase>(new[]{
                                k.Key.ResolveObj(),
                                k.Value.ResolveObj()
                            }));
                        }
                    }
                }
                else if (lstName is string)
                {
                    foreach (char c in (string)lstName)
                    {
                        lstNames.Add(new List<EvalObjectBase>(new[]{ new Text(c.ToString()) }));
                    }
                }
                else
                {
                    lstNames.Add(new List<EvalObjectBase>(new[]{ DetectType(lstName) }));
                }

                foreach (List<EvalObjectBase> lst in lstNames)
                {
                    if (LimitLoops && ct > LoopLimit)
                        throw new EvaluatorException("Loop limit reached");
                    if (_die)
                        throw new EvaluatorException("");

                    CantusEvaluator tmpEval = _eval.SubEvaluator();
                    for (int i = 0; i <= vars.Length - 1; i++)
                    {
                        if (i >= lst.Count)
                        {
                            tmpEval.SetVariable(vars[i], double.NaN);
                        }
                        else
                        {
                            tmpEval.SetVariable(vars[i], lst[i]);
                        }
                    }

                    result = (StatementResult)tmpEval.EvalRaw(blocks[0].Content, noSaveAns: true, @internal: true);

                    switch (result.Code)
                    {
                        case ExecCode.@break:
                        case ExecCode.@return:
                        case ExecCode.breakLevel:
                            return new StatementResult(result.Value);
                        case ExecCode.@continue:
                            continue;
                    }
                    ct += 1;
                }

            }
            else if (arg.ToLowerInvariant().Contains(" to "))
            {
                // for ... = ... to ... step ...
                string varname = arg.Remove(arg.ToLowerInvariant().IndexOf(" to "));

                BigDecimal var = (BigDecimal)_eval.EvalExprRaw(varname, true);

                if (varname.Contains("="))
                    varname = varname.Remove(varname.IndexOf("=")).Trim();

                BigDecimal lim = 0;
                BigDecimal delta = 1;


                if (arg.ToLowerInvariant().Contains(" step ") && arg.ToLowerInvariant().IndexOf(" to ") < arg.ToLowerInvariant().IndexOf(" step "))
                {
                    delta = new Number((BigDecimal)_eval.EvalExprRaw(arg.Substring(arg.ToLowerInvariant().IndexOf(" step ") + 6), true)).BigDecValue();
                    lim = new Number((BigDecimal)_eval.EvalExprRaw(arg.Remove(arg.ToLowerInvariant().IndexOf(" step ")).Substring(arg.ToLowerInvariant().IndexOf(" to ") + 4), true)).BigDecValue().Truncate();
                }
                else
                {
                    lim = new Number((BigDecimal)_eval.EvalExprRaw(arg.Substring(arg.ToLowerInvariant().IndexOf(" to ") + 4), true)).BigDecValue().Truncate();
                }

                if (delta == 0)
                    throw new SyntaxException("Step of 0 not allowed");

                for (BigDecimal i = var; i <= lim; i += delta)
                {
                    i = i.Truncate(10);
                    if (i == lim)
                        break; 
                    if (LimitLoops && ct > LoopLimit)
                        throw new EvaluatorException("Loop limit reached");
                    if (_die)
                        throw new EvaluatorException("");

                    CantusEvaluator tmpEval = _eval.SubEvaluator();
                    tmpEval.SetVariable(varname, i);

                    result = (StatementResult)tmpEval.EvalRaw(blocks[0].Content, noSaveAns: true, @internal: true);

                    switch (result.Code)
                    {
                        case ExecCode.@break:
                        case ExecCode.@return:
                        case ExecCode.breakLevel:
                            return new StatementResult(result.Value);
                        case ExecCode.@continue:
                            continue;
                    }
                    ct += 1;
                }
            }
            else
            {
                throw new SyntaxException("Invalid \"for\" statement syntax");
            }
            if (result.Code == ExecCode.@continue) result = new StatementResult(result.Value);
            return result;
        }

        private StatementResult StatementTryCatchFinally(List<Block> blocks)
        {
            Block tryBlock = null;
            Block catchBlock = null;
            string catchVar = "error";
            Block finallyBlock = null;
            foreach (Block block in blocks)
            {
                if (block.Keyword == "try")
                {
                    if (tryBlock == null)
                        tryBlock = block;
                }
                else if (block.Keyword == "catch")
                {
                    if (catchBlock == null)
                    {
                        catchBlock = block;
                        if (!string.IsNullOrWhiteSpace(block.Argument))
                            catchVar = block.Argument;
                    }
                }
                else if (block.Keyword == "finally")
                {
                    if (finallyBlock == null)
                        finallyBlock = block;
                }
            }

            StatementResult result = new StatementResult(double.NaN);
            try
            {
                result = Run(tryBlock.Content);
            }
            catch (Exception ex)
            {
                if ((catchBlock != null))
                    result = Run(catchBlock.Content, new Dictionary<string, object> { {
                        catchVar,
                        ex.Message
                    } });
            }

            if ((finallyBlock != null))
                result = Run(finallyBlock.Content);
            return result;
        }

        private StatementResult StatementSwitch(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Switch statement is invalid");
            return Run(blocks[0].Content, new Dictionary<string, object> { {
                "__switch",
                _eval.EvalExprRaw(blocks[0].Argument, true)
            } }, declarative: true);
        }

        private StatementResult StatementCase(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Case statement is invalid");
            _eval.SetVariable("__case", _eval.EvalExprRaw(blocks[0].Argument, true));
            object varcheck = _eval.GetVariable("__case");
            object varval = _eval.GetVariable("__switch");
            if (varcheck.GetType() == varval.GetType() && varcheck.ToString() == varval.ToString())
            {
                StatementResult res = Run(blocks[0].Content);
                if (res.Code == ExecCode.@return)
                    return res;
                return new StatementResult(res.Value, ExecCode.breakLevel);
            }
            else
            {
                return new StatementResult(double.NaN);
            }
        }

        private StatementResult StatementWith(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("With statement is invalid");
            return Run(blocks[0].Content, null, _eval.GetVariableRef(blocks[0].Argument.Trim()));
        }

        #endregion

        #region "Inline Flow Control Statements"
        private StatementResult StatementReturn(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Return statement is invalid");
            return new StatementResult(_eval.EvalExprRaw(blocks[0].Argument, true), ExecCode.@return);
        }

        private StatementResult StatementBreak(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Break statement is invalid ");
            return new StatementResult(double.NaN, ExecCode.@break);
        }

        private StatementResult StatementContinue(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Continue statement is invalid ");
            return new StatementResult(double.NaN, ExecCode.@continue);
        }
        #endregion

        #region "Variable/Function Declaration Statements"
        private StatementResult StatementDeclare(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Variable declaration is invalid ");
            string var = blocks[0].Argument.Trim();
            object def = double.NaN;
            if (var.Contains("="))
            {
                try
                {
                    def = ObjectTypes.DetectType(_eval.EvalExprRaw(var.Substring(var.IndexOf('=') + 1).Trim(), true));
                }
                catch
                {
                    // do nothing
                }
                var = var.Remove(var.IndexOf('=')).Trim();
                if (var.EndsWith(":"))
                    var = var.Remove(var.Length - 1);
            }
            var = var.Trim();
            _eval.SetVariable(var, def);
            return new StatementResult(_eval.GetVariableRef(var).GetValue(), ExecCode.resume);
        }

        private StatementResult StatementDeclareGlobal(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Global declaration is invalid ");
            string var = blocks[0].Argument.Trim();
            object def = double.NaN;
            if (var.Contains("="))
            {
                try
                {
                    def = ObjectTypes.DetectType(_eval.EvalExprRaw(var.Substring(var.IndexOf('=') + 1).Trim(), true));
                }
                catch
                {
                    // do nothing
                }
                var = var.Remove(var.IndexOf('=')).Trim().Trim(new[]{ ':' });
                if (var.EndsWith(":"))
                    var = var.Remove(var.Length - 1);
            }
            var = var.Trim();

            Reference @ref = ((Reference)_eval.Root.GetVariableRef(var)).ResolveRef();
            @ref.SetValue(def);
            _eval.SetVariable(var, @ref);

            return new StatementResult(_eval.Root.GetVariableRef(var), ExecCode.resume);
        }

        private StatementResult StatementDeclareModifier(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Modified variable/function declaration is invalid ");
            string var = blocks[0].Argument.Trim();
            HashSet<string> keywords = new HashSet<string>(new[]{ blocks[0].Keyword.Trim() });

            // find additional keywords
            string[] validKeywords = {
                "public",
                "private",
                "static"
            };
            while (true)
            {
                foreach (string kwd in validKeywords)
                {
                    if (var.ToLower().StartsWith(kwd))
                    {
                        var = var.Substring(kwd.Length).Trim();
                        keywords.Add(kwd);
                        continue;
                    }
                }
                break; 
            }

            // functions
            if (var.Contains("function "))
            {
                var = var.Substring("function ".Length).Trim();
                if (blocks.Count != 1)
                    throw new SyntaxException("Function declaration is invalid ");

                _eval.DefineUserFunction(var, blocks[0].Content, keywords);
                if (string.IsNullOrWhiteSpace(blocks[0].Content))
                {
                    return new StatementResult(blocks[0].Keyword + blocks[0].Argument + " (undefined)", ExecCode.resume);
                }
                else
                {
                    return new StatementResult(blocks[0].Keyword + blocks[0].Argument + " ...", ExecCode.resume);
                }

                // classes
            }
            else if (var.Contains("class "))
            {
                var = var.Substring("class ".Length).Trim();
                if (blocks.Count != 1)
                    throw new SyntaxException("Class declaration is invalid ");

                if (var.Contains(":"))
                {
                    int inhIdx = var.IndexOf(":");
                    if (!CantusEvaluator.IsValidIdentifier(var.Remove(inhIdx).Trim()))
                    {
                        throw new EvaluatorException("Invalid class name");
                    }
                    _eval.DefineUserClass(var.Trim(), blocks[0].Content, var.Substring(inhIdx + 1).Split(','), keywords);
                }
                else
                {
                    if (!CantusEvaluator.IsValidIdentifier(var.Trim()))
                        throw new EvaluatorException("Invalid class name");
                    _eval.DefineUserClass(var, blocks[0].Content, modifiers: keywords);
                }

                return new StatementResult("Class " + var.Trim() + " (declared)", ExecCode.resume);

                // variables
            }
            else
            {

                if (var.StartsWith("let "))
                    var = var.Substring("let ".Length).Trim();
                // ignore let

                bool isGlobal = false;
                // if global, declare global
                if (var.StartsWith("global "))
                {
                    var = var.Substring("global ".Length).Trim();
                    isGlobal = true;
                }

                object def = double.NaN;
                if (var.Contains("="))
                {
                    try
                    {
                        def = ObjectTypes.DetectType(_eval.EvalExprRaw(var.Substring(var.IndexOf('=') + 1).Trim(), true));
                    }
                    catch
                    {
                        // do nothing
                    }
                    var = var.Remove(var.IndexOf('=')).Trim();
                    if (var.EndsWith(":"))
                        var = var.Remove(var.Length - 1);
                }
                var = var.Trim();

                if (isGlobal)
                {
                    Reference @ref = ((Reference)_eval.Root.GetVariableRef(var)).ResolveRef();
                    @ref.SetValue(def);
                    _eval.SetVariable(var, @ref, modifiers: keywords);
                }
                else
                {
                    _eval.SetVariable(var, def, modifiers: keywords);
                }

                return new StatementResult(_eval.GetVariableRef(var).GetValue(), ExecCode.resume);
            }
        }

        private StatementResult StatementDeclareFunction(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Function declaration is invalid ");
            _eval.DefineUserFunction(blocks[0].Argument, blocks[0].Content);
            if (string.IsNullOrWhiteSpace(blocks[0].Content))
            {
                return new StatementResult(blocks[0].Keyword + blocks[0].Argument + " (undefined)", ExecCode.resume);
            }
            else
            {
                return new StatementResult(blocks[0].Keyword + blocks[0].Argument + " ...", ExecCode.resume);
            }
        }

        private StatementResult StatementImport(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Import statement is invalid ");
            foreach (string p in blocks[0].Argument.Split(','))
            {
                string path = p;
                path = path.Trim();
                if (!_eval.Loaded.Contains(path.Trim()))
                {
                    try
                    {
                        // if not loaded, try loading the scope
                        _eval.Load(path, false, true);
                    }
                    catch
                    {
                        bool done = false;
                        // load failed? check if it is some scope we don't know (i.e. below file level) by looking through things
                        foreach (UserFunction fn in _eval.UserFunctions.Values)
                        {
                            if (IsParentScopeOf(path, fn.DeclaringScope))
                            {
                                _eval.Import(path);
                                // confirmed, import
                                done = true;
                                break;
                                       // import relative to current scope?
                            }
                            else if (IsParentScopeOf(path, fn.DeclaringScope, _eval.Scope))
                            {
                                _eval.Import(_eval.Scope + SCOPE_SEP + path);
                                // confirmed, import
                                done = true;
                                break; // import relative to current scope?
                            }
                        }

                        if (!done)
                        {
                            foreach (Variable var in _eval.Variables.Values)
                            {
                                if (IsParentScopeOf(path, var.DeclaringScope))
                                {
                                    _eval.Import(path);
                                    // confirmed, import
                                    break; // import relative to current scope?
                                }
                                else if (IsParentScopeOf(path, var.DeclaringScope, _eval.Scope))
                                {
                                    _eval.Import(_eval.Scope + SCOPE_SEP + path);
                                    // confirmed, import
                                    break; 
                                }
                            }
                            // nope, does not exist. complain to the user.
                            throw new EvaluatorException("Import: Cantus package \"" + path + "\" does not exist");
                        }
                    }
                }
                else
                {
                    // already loaded, import namespace
                    _eval.Import(path);
                }
            }
            return new StatementResult("Import: imported " + blocks[0].Argument.Trim(), ExecCode.resume);
        }

        private StatementResult StatementLoad(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Load statement is invalid ");
            foreach (string path in blocks[0].Argument.Split(','))
            {
                try
                {
                    _eval.Load(path, false);
                }
                catch
                {
                    throw new EvaluatorException("Load: package \"" + path.Trim() + "\" does not exist");
                }
            }
            return new StatementResult("Load: loaded " + blocks[0].Argument.Trim(), ExecCode.resume);
        }

        private StatementResult StatementNamespace(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Namespace declaration is invalid ");
            if (!CantusEvaluator.IsValidIdentifier(blocks[0].Argument.Trim()))
                throw new EvaluatorException("Invalid namespace name");

            _eval.SubScope(blocks[0].Argument.Trim());

            StatementResult res = default(StatementResult);
            try
            {
                res = (StatementResult)_eval.EvalRaw(blocks[0].Content, true, false, true);
                _eval.ParentScope();
            }
            catch (Exception ex)
            {
                _eval.ParentScope();
                throw ex;
            }
            return res;
        }

        private StatementResult StatementClass(List<Block> blocks)
        {
            if (blocks.Count != 1)
                throw new SyntaxException("Class declaration is invalid ");

            if (blocks[0].Argument.ToLower().Contains(":"))
            {
                int inhIdx = blocks[0].Argument.ToLower().IndexOf(":");
                if (!CantusEvaluator.IsValidIdentifier(blocks[0].Argument.Remove(inhIdx).Trim()))
                {
                    throw new EvaluatorException("Invalid class name");
                }
                _eval.DefineUserClass(blocks[0].Argument.Remove(inhIdx).Trim(), blocks[0].Content, blocks[0].Argument.Substring(inhIdx + 1).Split(','));
            }
            else
            {
                if (!CantusEvaluator.IsValidIdentifier(blocks[0].Argument.Trim()))
                    throw new EvaluatorException("Invalid class name");
                _eval.DefineUserClass(blocks[0].Argument, blocks[0].Content);
            }

            return new StatementResult("Class " + blocks[0].Argument.Trim() + " (declared)", ExecCode.resume);
        }

        /// <summary>
        /// Stop all threads running statements and disallow spawning of new ones
        /// </summary>
        public void Dispose()
        {
            this._die = true;
            System.Threading.Thread.Sleep(50);
        }

        /// <summary>
        /// Stop all threads running statements and continue
        /// </summary>
        public void StopAll()
        {
            this._die = true;
            System.Threading.Thread.Sleep(50);
            this._die = false;
        }
        #endregion
        #endregion

    }
}
