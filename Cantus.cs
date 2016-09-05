using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Cantus.Core.CommonTypes;
using Cantus.Core.Exceptions;
using static Cantus.Core.Scoping;

using static Cantus.Core.CantusEvaluator.ObjectTypes;
using static Cantus.Core.StatementRegistar;
using System;

namespace Cantus.Core
{

    /// <summary>
    /// Class used for evaluating Cantus scripts on-demand, line-by-line, used in consoles to evaluate script blocks.
    /// </summary>
    public sealed class ScriptFeeder
    {

        public delegate void ResultReceivedDelegate(object sender, string result);
        /// <summary>
        /// Occurs when a result is received from the execution.
        /// </summary>
        public event ResultReceivedDelegate ResultReceived;

        public delegate void WaitDelegate(object sender, string lastExpr);
        /// <summary>
        /// Occurs when all lines have been run and the evaluator is waiting for more lines to execute.
        /// </summary>
        public event WaitDelegate Waiting;

        private CantusEvaluator _eval;
        private Queue<string> _q = new Queue<string>();
        private List<string> _scr = new List<string>();
        private Thread _thd;

        private bool _endAfterQueue = false;
        /// <summary>
        /// Returns true if parts of the script are currently being executed
        /// </summary>
        public bool IsBusy
        {
            get { return _q.Count > 0; }
        }

        private bool _isStarted;
        public bool IsStarted
        {
            get
            {
                return _isStarted;
            }
        }

        private int _currentLine;
        /// <summary>
        /// Get the first line of the script that has not yet been enqueued for execution
        /// </summary>
        public int CurrentLine
        {
            get
            {
                return _currentLine;
            }
        }

        /// <summary>
        /// Get the entire script managed by the scripthost
        /// </summary>
        public string Script
        {
            get { return string.Join(Environment.NewLine, _scr); }
        }

        /// <summary>
        /// The ManualResetEvent used to suspend/restart the script
        /// </summary>
        internal ManualResetEventSlim ResetEvent { get; set; } = new ManualResetEventSlim(false);

        /// <summary>
        /// Raise the waiting event, used from the evaluator
        /// </summary>
        internal void RaiseWaiting(string expr)
        {
            if (Waiting != null) Waiting(this, expr);
        }

        /// <summary>
        /// Begin executing the script. All lines already fed will be executed immediately.
        /// </summary>
        public void BeginExecution()
        {
            _isStarted = true;
            _thd = new Thread(() =>
            {
                try
                {
                    string res = _eval.Eval("", feederScript: this);
                    if (ResultReceived != null)
                    {
                        ResultReceived(this, res);
                    }
                }
                catch (Exception ex)
                {
                    if (ResultReceived != null)
                    {
                        ResultReceived(this, ex.Message);
                    }
                }
            });
            _thd.IsBackground = true;
            _thd.Start();
        }

        /// <summary>
        /// Dequeue an item from the queue and return the content
        /// </summary>
        internal string GetLine()
        {
            string nxt = _q.Dequeue();
            if (_endAfterQueue && _q.Count == 0)
            {
                _isStarted = false;
                _endAfterQueue = false;
            }
            return nxt;
        }

        /// <summary>
        /// Feed all of the currently available script to the evaluator to be executed as soon as possible
        /// </summary>
        public void SendAllLines(int numLines)
        {
            while (CurrentLine < _scr.Count)
            {
                _q.Enqueue(_scr[CurrentLine]);
                _currentLine += 1;
            }
        }

        private void Enqueue(string line)
        {
            _q.Enqueue(line);
            if (IsStarted && _q.Count > 0)
            {
                ResetEvent.Set();
            }
        }

        /// <summary>
        /// Feed the next specified number of lines to the evaluator for execution
        /// </summary>
        public void SendLines(int numLines)
        {
            for (int i = 0; i <= numLines - 1; i++)
            {
                Enqueue(_scr[CurrentLine]);
                _currentLine += 1;
            }
        }

        /// <summary>
        /// Feed the next line to the evaluator for execution
        /// </summary>
        public void SendNextLine()
        {
            Enqueue(_scr[CurrentLine]);
            _currentLine += 1;
        }

        /// <summary>
        /// Append to the script. If enqueue is set to true, the lines are enqueued directly.
        /// </summary>
        public void Append(string script, bool enqueueAfter = false)
        {
            string[] spl = script.TrimEnd().Split('\n');
            if (enqueueAfter)
            {
                foreach (string line in spl)
                {
                    Enqueue(line);
                }
            }
            else
            {
                foreach (string line in spl)
                {
                    _scr.Add(line);
                }
            }
        }

        /// <summary>
        /// Kill the executing threads
        /// </summary>
        public void Terminate()
        {
            _q.Clear();
            try
            {
                _thd.Abort();
            }
            catch
            {
            }
            _isStarted = false;
        }

        /// <summary>
        /// Pause the execution of the script after finishing the current line
        /// </summary>
        public void PauseExecution()
        {
            _isStarted = false;
        }

        /// <summary>
        /// End the execution of the script after finishing the current line
        /// </summary>
        public void EndExecution()
        {
            _q.Clear();
            _isStarted = false;
        }

        /// <summary>
        /// End the execution of the script after no more lines are available for execution.
        /// </summary>
        public void EndAfterQueueDone()
        {
            _endAfterQueue = true;
        }

        /// <summary>
        /// Create a new Cantus script host.
        /// </summary>
        /// <param name="initialScript">The initial script to add to the script</param>
        /// <param name="beginExecuting">If true, feeds the script and begins executing immediately</param>
        /// <param name="evaluator">The evaluator to run scripts on. If none is specified, a new evaluator will be created.</param>

        public ScriptFeeder(string initialScript = "", bool beginExecuting = false, CantusEvaluator evaluator = null)
        {
            Append(initialScript, beginExecuting);
            if (evaluator == null)
            {
                evaluator = new CantusEvaluator();
            }
            else
            {
                this._eval = evaluator;
            }

            if (beginExecuting)
            {
                BeginExecution();
            }
        }
    }

    /// <summary>
    /// Class for evaluating Cantus scripts and expressions within a scope, as well as storing user data
    /// </summary>
    public sealed partial class CantusEvaluator : IDisposable
    {
        #region "Enums"

        /// <summary>
        /// Represents a represenatation of an angle (degree/radian/gradian)
        /// </summary>
        public enum AngleRepresentation
        {
            Degree = 0,
            Radian,
            Gradian
        }

        /// <summary>
        /// Represents an output format
        /// </summary>
        public enum OutputFormat
        {
            /// <summary>
            /// Directly outputs as a decimal number (switches to scientific notation for very large/very small numbers)
            /// </summary>
            Raw = 0,
            /// <summary>
            /// Attempts to format output as a fraction, root, or multiple of pi
            /// </summary>
            Math,
            /// <summary>
            /// Formats output in scientific notation
            /// </summary>
            Scientific
        }
        #endregion

        #region "Types"
        /// <summary>
        /// Represents an user-defined function
        /// </summary>
        public sealed class UserFunction
        {
            /// <summary>
            /// The name of the function
            /// </summary>
            /// <returns></returns>
            public string Name { get; set; }

            /// <summary>
            /// The body of the function
            /// </summary>
            /// <returns></returns>
            public string Body { get; set; }

            /// <summary>
            /// Hashset of modifiers, such as private, applied to the function (NI)
            /// </summary>
            public HashSet<string> Modifiers { get; set; }

            /// <summary>
            ///  Return type of the function (NI)
            /// </summary>
            public string ReturnType { get; set; }

            /// <summary>
            /// Names of the function arguments
            /// </summary>
            public List<string> Args { get; set; }

            /// <summary>
            /// Get the minimum number of arguments required
            /// </summary>
            /// <returns></returns>
            public int RequiredArgsCount
            {
                get
                {
                    for (int i = 0; i <= Defaults.Count - 1; i++)
                    {
                        if (!(Defaults[i] == null || (Defaults[i] is double && double.IsNaN((double)(Defaults[i]))) || (Defaults[i] is BigDecimal && ((BigDecimal)Defaults[i]).IsUndefined)))
                        {
                            return i;
                        }
                    }
                    return Defaults.Count;
                }
            }

            /// <summary>
            /// Default values of the function arguments
            /// </summary>
            public List<object> Defaults { get; set; }

            /// <summary>
            /// Gets the scope in which this function was declared
            /// </summary>
            public string DeclaringScope { get; set; }

            /// <summary>
            /// Gets the full name of this function, including the scope
            /// </summary>
            /// <returns></returns>
            public string FullName
            {
                get { return DeclaringScope + SCOPE_SEP + Name; }
            }

            /// <summary>
            /// Create a new user function
            /// </summary>
            public UserFunction(string name, string body, List<string> args, string declaredScope, IEnumerable<string> modifiers = null, List<object> defaults = null, string returnType = "")
            {
                if (modifiers == null)
                {
                    this.Modifiers = new HashSet<string>();
                }
                else
                {
                    this.Modifiers = new HashSet<string>(modifiers);
                }
                this.Name = name;
                this.Body = body;
                this.Args = args;
                this.DeclaringScope = declaredScope;
                this.Defaults = defaults;
                if (this.Defaults == null)
                {
                    this.Defaults = new List<object>();
                    foreach (string a in this.Args)
                    {
                        this.Defaults.Add(double.NaN);
                    }
                }
                this.ReturnType = returnType;
            }

            /// <summary>
            /// Get the full definition of this function as a string
            /// </summary>
            public string ToString(string ignoreScope = CantusEvaluator.ROOT_NAMESPACE)
            {
                StringBuilder result = new StringBuilder();
                foreach (string m in Modifiers)
                {
                    result.Append(m).Append(" ");
                }
                result.Append("function ");

                string scope = RemoveRedundantScope(DeclaringScope, ignoreScope);
                result.Append(scope.Trim());
                if (!string.IsNullOrWhiteSpace(scope))
                    result.Append(SCOPE_SEP);

                result.Append(Name).Append("(").Append(string.Join(", ", Args)).AppendLine(")");
                result.Append(Body);
                return result.ToString();
            }
        }

        /// <summary>
        /// Represents an evaluator variable
        /// </summary>
        public sealed class Variable
        {
            /// <summary>
            /// The name of the variable
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets a reference to the value of the variable
            /// </summary>
            /// <returns></returns>
            public Reference Reference { get; set; }

            /// <summary>
            /// Gets or sets a hashset of modifiers for the variable
            /// </summary>
            /// <returns></returns>
            public HashSet<string> Modifiers { get; set; }

            /// <summary>
            /// Gets or sets the value of the variable without changing reference
            /// </summary>
            /// <returns></returns>
            public object Value
            {
                get { return Reference.Resolve(); }
                set { Reference.ResolveRef().SetValue(value); }
            }

            /// <summary>
            /// Gets the scope in which this variable was last assigned to
            /// </summary>
            /// <returns></returns>
            public string DeclaringScope { get; set; }

            /// <summary>
            /// Gets the full name of this variable, including the scope
            /// </summary>
            /// <returns></returns>
            public string FullName
            {
                get { return DeclaringScope + SCOPE_SEP + Name; }
            }

            /// <summary>
            /// Create a empty variable (internal)
            /// </summary>
            private Variable(string name, string declaringScope, IEnumerable<string> modifiers = null)
            {
                this.Name = name;
                this.DeclaringScope = declaringScope;
                if ((modifiers != null))
                {
                    this.Modifiers = new HashSet<string>(modifiers);
                }
                else
                {
                    this.Modifiers = new HashSet<string>();
                }
            }

            /// <summary>
            /// Create a new variable from a value
            /// </summary>
            public Variable(string name, object value, string declaringScope, IEnumerable<string> modifiers = null) : this(name, declaringScope, modifiers)
            {
                this.Reference = new Reference(value);
            }

            /// <summary>
            /// Create a new variable from an evaluator object
            /// </summary>
            public Variable(string name, EvalObjectBase value, string declaringScope, IEnumerable<string> modifiers = null) : this(name, declaringScope, modifiers)
            {
                this.Reference = new Reference(value);
            }

            /// <summary>
            /// Create a new variable from an existing reference
            /// </summary>
            public Variable(string name, Reference @ref, string declaringScope, IEnumerable<string> modifiers = null) : this(name, declaringScope, modifiers)
            {
                this.Reference = @ref;
            }

            /// <summary>
            /// Convert the variable to a (human-readable) string
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return this.FullName + " = " + this.Reference.ToString();
            }
        }

        /// <summary>
        /// Represents an user-defined class with OOP support
        /// </summary>
        public sealed class UserClass : IDisposable
        {


            private bool _disposed;
            /// <summary>
            /// The name of the class
            /// </summary>
            public string Name { get; set; } = "";

            /// <summary>
            /// Gets or sets a dictionary of fields in this class, in the format (name, reference)
            /// </summary>
            /// <returns></returns>
            public Dictionary<string, Variable> Fields { get; set; }

            /// <summary>
            /// Gets a dictionary of all fields in this class, including inherited ones.
            /// </summary>
            /// <returns></returns>
            public Dictionary<string, Variable> AllFields
            {
                get
                {
                    Dictionary<string, Variable> res = new Dictionary<string, Variable>();
                    foreach (var name in this.BaseClasses)
                    {
                        if (!this.Evaluator.UserClasses.ContainsKey(name))
                            continue;
                        UserClass b = this.Evaluator.UserClasses[name];
                        foreach (string f in b.AllFields.Keys)
                        {
                            res[f] = b.AllFields[f];
                        }
                    }
                    foreach (string f in this.Fields.Keys)
                    {
                        res[f] = this.Fields[f];
                    }
                    return res;
                }
            }

            /// <summary>
            /// Gets the constructor function for this class
            /// </summary>
            /// <returns></returns>
            public Lambda Constructor
            {
                get { return (Lambda)AllFields["init"].Reference.ResolveObj(); }
            }

            /// <summary>
            /// Gets or sets the body of the class declaration
            /// </summary>
            /// <returns></returns>
            public string Body { get; set; }

            /// <summary>
            /// Gets or sets a hashset of modifiers for the class
            /// </summary>
            /// <returns></returns>
            public HashSet<string> Modifiers { get; set; }

            /// <summary>
            /// Gets or sets a list of classes this class inherits from.
            /// </summary>
            /// <returns></returns>
            public IEnumerable<string> BaseClasses { get; set; }

            /// <summary>
            /// Get a list of all classes that this class inherits from, directly or indirectly
            /// </summary>
            /// <returns></returns>
            public IEnumerable<string> AllParentClasses
            {
                get
                {
                    List<string> lst = new List<string>();

                    foreach (string b in BaseClasses)
                    {
                        lst.Add(b);
                        if (!Evaluator.UserClasses.ContainsKey(b))
                            continue;
                        UserClass bc = Evaluator.UserClasses[b];
                        lst.AddRange(bc.AllParentClasses);
                    }

                    return lst;
                }
            }

            /// <summary>
            /// Gets the scope in which this variable was last assigned to
            /// </summary>
            /// <returns></returns>
            public string DeclaringScope { get; set; }

            private List<ClassInstance> _instances;
            /// <summary>
            /// List of instances of this class
            /// </summary>
            /// <returns></returns>
            public List<ClassInstance> Instances
            {
                get
                {
                    return _instances;
                }
            }

            private string _innerScope;
            /// <summary>
            /// The inner scope used to register functions, etc. of this class
            /// </summary>
            /// <returns></returns>
            public string InnerScope
            {
                get
                {
                    return _innerScope;
                }
            }

            /// <summary>
            /// Gets the full name of this variable, including the scope
            /// </summary>
            /// <returns></returns>
            public string FullName
            {
                get
                {
                    if (this._disposed)
                        return null;
                    return DeclaringScope + SCOPE_SEP + Name;
                }
            }

            /// <summary>
            /// Gets the shortest name of the class that can be directly used to access it in the evaluator
            /// </summary>
            /// <returns></returns>
            public string ShortestAccessibleName
            {
                get
                {
                    if (this._disposed)
                        return null;
                    return RemoveRedundantScope(this.FullName, Evaluator.Scope);
                }
            }

            /// <summary>
            /// The evaluator used with this user class
            /// </summary>
            public CantusEvaluator Evaluator { get; }

            /// <summary>
            /// Create a empty class with the specified name, definition, evaluator, scope, and modifiers
            /// </summary>
            /// <param name="declaringScope">Optional. If not specified, uses the scope of the specified evaluator.</param>

            public UserClass(string name, string def, CantusEvaluator eval, IEnumerable<string> modifiers = null, IEnumerable<string> inheritedClasses = null, string declaringScope = "")
            {
                this._disposed = false;
                this._instances = new List<ClassInstance>();

                if ((inheritedClasses != null))
                {
                    this.BaseClasses = new List<string>(inheritedClasses);
                }
                else
                {
                    this.BaseClasses = new List<string>();
                }

                this.Name = name;
                if (string.IsNullOrEmpty(declaringScope))
                {
                    this.DeclaringScope = eval.Scope;
                }
                else
                {
                    this.DeclaringScope = declaringScope;
                }

                this.Evaluator = eval;
                this.Body = def;

                this.Fields = new Dictionary<string, Variable>();
                if ((modifiers != null))
                {
                    this.Modifiers = new HashSet<string>(modifiers);
                }
                else
                {
                    this.Modifiers = new HashSet<string>();
                }

                string tmpScope = "__class_" + name + "_" + Guid.NewGuid().ToString().Replace("-", "") + System.DateTime.Now.Millisecond;

                CantusEvaluator tmpEval = eval.SubEvaluator(0, tmpScope);
                tmpEval.Variables.Clear();
                tmpEval.UserFunctions.Clear();

                tmpScope = tmpEval.Scope;
                string nsScope = eval.Scope + SCOPE_SEP + this.Name;
                this._innerScope = tmpScope;

                tmpEval.Eval(def, true);

                // add back newly declared variables
                foreach (Variable var in tmpEval.Variables.Values)
                {
                    if (var.DeclaringScope != tmpScope)
                        continue;
                    // static variables : declare in namespace with class name
                    if (var.Modifiers.Contains("static"))
                    {
                        var.DeclaringScope = nsScope;
                        var.Modifiers.Add("internal");
                        eval.Variables[var.FullName] = var;
                        this.Fields[var.Name] = new Variable(var.Name, var.Reference, tmpScope, var.Modifiers);
                    }
                    else
                    {
                        var.Modifiers.Add("internal");
                        this.Fields[var.Name] = new Variable(var.Name, var.Reference.GetDeepCopy(), tmpScope, var.Modifiers);
                    }
                }

                // add back newly declared functions
                foreach (UserFunction fn in tmpEval.UserFunctions.Values)
                {
                    if (fn.DeclaringScope != tmpScope)
                        continue;
                    // static functions
                    if (fn.Modifiers.Contains("static"))
                    {
                        fn.DeclaringScope = nsScope;
                        fn.Modifiers.Add("internal");
                        eval.UserFunctions[fn.FullName] = fn;
                        this.Fields[fn.Name] = new Variable(fn.Name, new Lambda(fn), tmpScope, fn.Modifiers);
                    }
                    else
                    {
                        fn.Modifiers.Add("internal");
                        this.Fields[fn.Name] = new Variable(fn.Name, new Lambda(fn, true), tmpScope, fn.Modifiers);
                    }
                }

                // add back newly declared classes
                foreach (UserClass uc in tmpEval.UserClasses.Values)
                {
                    if (uc.DeclaringScope != tmpScope)
                        continue;
                    uc.DeclaringScope = nsScope;
                    eval.UserClasses[uc.FullName] = uc;
                }

                // add empty constructor, if none exists
                if (!this.Fields.ContainsKey("init"))
                {
                    UserFunction fn = new UserFunction("init", "", new List<string>(), tmpScope);
                    fn.Modifiers.Add("internal");
                    this.Fields[fn.Name] = new Variable(fn.Name, new Lambda(fn, true), this.FullName, fn.Modifiers);
                }

                // add 'type' function
                UserFunction typeFn = new UserFunction("type", string.Format("return {0}{1}type(this)", ROOT_NAMESPACE, SCOPE_SEP), new List<string>(), tmpScope);
                typeFn.Modifiers.Add("internal");
                this.Fields[typeFn.Name] = new Variable(typeFn.Name, new Lambda(typeFn, true), this.FullName, typeFn.Modifiers);
            }

            /// <summary>
            /// Register an new instance of the class
            /// </summary>
            public void RegisterInstance(ClassInstance instance)
            {
                if (this._disposed)
                    return;
                if (instance.UserClass == this)
                {
                    this._instances.Add(instance);
                }
            }

            /// <summary>
            /// Convert the class to a (human-readable) string
            /// </summary>
            /// <returns></returns>
            public string ToString(string ignoreScope = "")
            {
                StringBuilder result = new StringBuilder();
                foreach (string m in Modifiers)
                {
                    result.Append(m).Append(" ");
                }
                result.Append("class ");

                string scope = RemoveRedundantScope(DeclaringScope, ignoreScope);
                result.Append(scope.Trim());
                if (!string.IsNullOrWhiteSpace(scope))
                    result.Append(SCOPE_SEP);
                result.Append(Name);
                if (!(BaseClasses.Count() == 0))
                {
                    result.Append(":");
                    bool first = true;
                    foreach (string b in BaseClasses)
                    {
                        if (!Evaluator.UserClasses.ContainsKey(b))
                            continue;
                        if (!first)
                            result.Append(",");
                        else
                            first = false;
                        result.Append(RemoveRedundantScope(Evaluator.UserClasses[b].FullName, ignoreScope));
                    }
                }
                result.AppendLine();

                result.Append(Body);
                return result.ToString();
            }

            public void Dispose()
            {
                if (this._disposed)
                    return;
                this._disposed = true;
                foreach (ClassInstance ins in this.Instances)
                {
                    try
                    {
                        ins.Dispose();
                    }
                    catch { }
                }

                foreach (string f in this.AllFields.Keys)
                {
                    Evaluator.SetVariable(this.InnerScope + SCOPE_SEP + f, double.NaN);
                }

                // prevent access
                this.Modifiers.Clear();
                this.Fields.Clear();
                this.Name = "";
            }
        }

        #region "TokenList"
        /// <summary>
        /// Represents a segment in the expression obtained after it is tokenized, consisting of 
        /// an object and an operator
        /// If either is unavailable they are set to null.
        /// </summary>
        private struct Token
        {
            public EvalObjectBase Object;
            public OperatorRegistar.Operator Operator;
            public Token(EvalObjectBase evalObject, OperatorRegistar.Operator operatorBefore)
            {
                this.Object = evalObject;
                this.Operator = operatorBefore;
            }
        }

        /// <summary>
        /// A special data structure used to store tokens
        /// Allows for indexing, removal, appending, lookup of tokens with a certain precedence
        /// </summary>
        private sealed class TokenList : IEnumerator, IEnumerable
        {
            /// <summary>
            /// A list of objects used with operators to store tokens
            /// </summary>
            private List<EvalObjectBase> _objects = new List<EvalObjectBase>();
            /// <summary>
            /// A list of operators used with objects to store tokens
            /// </summary>
            private List<OperatorRegistar.Operator> _operators = new List<OperatorRegistar.Operator>();
            /// <summary>
            /// A list of operator signs
            /// </summary>
            private List<string> _signs = new List<string>();
            /// <summary>
            /// A list of integers, one for each index in the list, indicating what it points to. Updated by Resolve.
            /// </summary>
            private List<int> _pointers = new List<int>();
            /// <summary>
            /// A list of sorted sets storing operators at each precedence level
            /// </summary>

            private List<SortedSet<int>> _opsByPrecedence = new List<SortedSet<int>>();
            /// <summary>
            /// Private variable storing the number of remaining tokens still pointing at themselves.
            /// </summary>

            private int _validCount = 0;
            /// <summary>
            /// The position of the enumerator
            /// </summary>

            private int _position = 0;
            /// <summary>
            /// Create a new TokenList for storing tokens.
            /// </summary>
            public TokenList()
            {
                foreach (int i in Enum.GetValues(typeof(OperatorRegistar.Precedence)))
                {
                    _opsByPrecedence.Add(new SortedSet<int>());
                }
            }

            public Token this[int index]
            {
                get { return new Token(_objects[Resolve(index)], _operators[Resolve(index)]); }
                set
                {
                    _objects[_pointers[index]] = value.Object;
                    _operators[Resolve(index)] = value.Operator;
                    _signs[Resolve(index)] = value.Operator.Signs[0];
                    if ((value.Operator != null))
                    {
                        _opsByPrecedence[(int)value.Operator.Precedence].Add(Resolve(index));
                    }
                }
            }

            public bool MoveNext()
            {
                _position += 1;
                return (_position < this.Count);
            }

            public void Reset()
            {
                _position = -1;
            }

            public object Current
            {
                get { return this[_position]; }
            }

            public IEnumerator GetEnumerator()
            {
                return (IEnumerator)this;
            }

            /// <summary>
            /// The number of TokenList objects not currently marked as removed (not pointing at another object)
            /// </summary>
            /// <returns></returns>
            public int ValidCount
            {
                get { return _validCount; }
            }

            /// <summary>
            /// The total number of indices in the TokenList
            /// </summary>
            /// <returns></returns>
            public int Count
            {
                get { return Math.Min(_operators.Count, _objects.Count); }
            }

            public int Capacity
            {
                get { return Count; }
            }

            /// <summary>
            /// Balance the operator and object lists
            /// </summary>
            public void BalanceLists()
            {
                while (_operators.Count > _objects.Count)
                {
                    _objects.Add(null);
                }
                while (_operators.Count < _objects.Count)
                {
                    _operators.Add(null);
                    _signs.Add(null);
                }
                while (_pointers.Count < _objects.Count)
                {
                    _pointers.Add(_pointers.Count);
                }
            }

            /// <summary>
            /// Remove the token at the specified index by pointing it at the previous index
            /// </summary>
            /// <param name="idx"></param>
            public void RemoveAt(int idx)
            {
                idx = Resolve(idx);
                _opsByPrecedence[(int)_operators[idx].Precedence].Remove(idx);
                _pointers[idx] = _pointers[idx - 1];
                _validCount -= 1;
            }

            /// <summary>
            /// The total number of operators in this TokenList
            /// </summary>
            /// <returns></returns>
            public int OperatorCount
            {
                get { return _operators.Count; }
            }

            /// <summary>
            /// The number of items in the object list
            /// </summary>
            /// <returns></returns>
            public int ObjectCount
            {
                get { return _objects.Count; }
            }

            /// <summary>
            /// Get the operator at the specified index
            /// </summary>
            /// <param name="idx"></param>
            /// <returns></returns>
            public OperatorRegistar.Operator OperatorAt(int idx)
            {
                return _operators[Resolve(idx)];
            }

            /// <summary>
            /// Get the sign of the operator at the specified index
            /// </summary>
            /// <param name="idx"></param>
            /// <returns></returns>
            public string OperatorSignAt(int idx)
            {
                return _signs[Resolve(idx)];
            }

            /// <summary>
            /// Get the number of token indices with the precedence specified
            /// </summary>
            /// <param name="prec">The precedence</param>
            /// <returns></returns>
            public int OperatorsWithPrecedenceCount(OperatorRegistar.Precedence prec)
            {
                return _opsByPrecedence[(int)prec].Count;
            }

            /// <summary>
            /// Get a list of token indecess with the precedence specified
            /// </summary>
            /// <param name="prec"></param>
            /// <returns></returns>
            public List<int> OperatorsWithPrecedence(OperatorRegistar.Precedence prec)
            {
                return new List<int>(_opsByPrecedence[(int)prec]);
            }

            /// <summary>
            /// Checks if the token is marked as removed (i.e. it points to another item other than itself)
            /// </summary>
            /// <param name="idx">The index</param>
            /// <returns></returns>
            public bool IsRemoved(int idx)
            {
                return _pointers[idx] != idx;
            }

            /// <summary>
            /// Lookup the read index of the item
            /// </summary>
            /// <param name="idx">The index</param>
            /// <returns></returns>
            public int Resolve(int idx)
            {
                List<int> updatelst = new List<int>();
                while ((_pointers[idx] != idx))
                {
                    updatelst.Add(idx);
                    idx = _pointers[idx];
                }
                foreach (int i in updatelst)
                {
                    _pointers[i] = idx;
                }
                return idx;
            }

            /// <summary>
            /// Add a new operator
            /// </summary>
            /// <param name="op">The operator</param>
            /// <param name="sign">The sign of the operator 
            /// (if not specified, the first one froom the operator object is used)</param>
            public void AddOperator(OperatorRegistar.Operator op, string sign = "")
            {
                if ((op != null))
                {
                    _opsByPrecedence[(int)op.Precedence].Add(_operators.Count);
                }
                _operators.Add(op);
                if (string.IsNullOrEmpty(sign))
                {
                    if ((op != null))
                    {
                        _signs.Add(op.Signs[0]);
                    }
                    else
                    {
                        _signs.Add(null);
                    }
                }
                else
                {
                    _signs.Add(sign);
                }
                while (_pointers.Count < _operators.Count)
                {
                    _pointers.Add(_pointers.Count);
                    _validCount += 1;
                }
            }
            /// <summary>
            /// Set the operator at the index specified
            /// </summary>
            /// <param name="idx">The index at which to change the operator</param>
            /// <param name="op">The operator</param>
            /// <param name="sign">The sign of the operator </param>
            public void SetOperator(int idx, OperatorRegistar.Operator op, string sign = "")
            {
                idx = Resolve(idx);

                // modify precedence lists
                if ((_operators[idx] != null))
                {
                    _opsByPrecedence[(int)_operators[idx].Precedence].Remove(idx);
                }

                _opsByPrecedence[(int)op.Precedence].Add(idx);
                _operators[idx] = op;

                if (string.IsNullOrEmpty(sign))
                {
                    if ((op != null))
                    {
                        _signs[idx] = op.Signs[0];
                    }
                    else
                    {
                        _signs[idx] = null;
                    }
                }
                else
                {
                    _signs[idx] = sign;
                }
            }

            /// <summary>
            /// Get the object at the specified index
            /// </summary>
            /// <param name="idx">The index</param>
            /// <returns></returns>
            public EvalObjectBase ObjectAt(int idx)
            {
                return _objects[Resolve(idx)];
            }

            /// <summary>
            /// Set the object at the specified index
            /// </summary>
            /// <param name="idx">The index at which to set the object</param>
            /// <param name="obj">The object</param>
            public void SetObject(int idx, EvalObjectBase obj)
            {
                _objects[Resolve(idx)] = obj;
            }

            /// <summary>
            /// Set the object at the specified index
            /// </summary>
            /// <param name="obj">The object</param>
            public void AddObject(EvalObjectBase obj)
            {
                _objects.Add(obj);
                while (_pointers.Count < _objects.Count)
                {
                    _pointers.Add(_pointers.Count);
                    _validCount += 1;
                }
            }
        }
        #endregion
        #region "Event Data"
        /// <summary>
        /// Event data for Cantus threading events
        /// </summary>
        public class ThreadEventArgs : EventArgs
        {
            public int ThreadId { get; }
            /// <summary>
            /// Create a new CantusThreadEventArgs class, containing the id of the thread that was started or terminated
            /// </summary>
            public ThreadEventArgs(int threadId)
            {
                this.ThreadId = threadId;
            }
        }

        /// <summary>
        /// Event data for Cantus IO events
        /// </summary>
        public sealed class IOEventArgs : EventArgs
        {
            public enum IOMessage
            {
                writeText = 0,
                readChar,
                readWord,
                readLine,
                confirm
            }

            /// <summary>
            /// The type of I/O operation
            /// </summary>
            public IOMessage Message { get; }

            /// <summary>
            /// The text to read or write
            /// </summary>
            public string Content { get; }

            /// <summary>
            /// Additional arguments, if available
            /// </summary>
            public IDictionary<string, object> Args { get; }

            /// <summary>
            /// Create a new I/O message
            /// </summary>
            public IOEventArgs(IOMessage message, string content, IDictionary<string, object> args = null)
            {
                this.Message = message;
                this.Content = content;
                if (args == null)
                {
                    this.Args = new Dictionary<string, object>();
                }
                else
                {
                    this.Args = args;
                }
            }
        }

        /// <summary>
        /// Data for EvalComplete event
        /// </summary>
        public sealed class AnswerEventArgs : EventArgs {

            private string _expression = null;
            /// <summary>
            /// The expression we evaluated
            /// </summary>
            public string Expression { get { return _expression; } }

            private bool _noSaveAns = false;
            /// <summary>
            /// False if this result was saved to answers
            /// </summary>
            public bool NoSaveAns { get { return _noSaveAns; } }

            private object _result = null;
            /// <summary>
            /// The result of the evaluation
            /// </summary>
            public object Result { get { return _result; } }

            /// <summary>
            /// The result as a double (or NaN if not convertible)
            /// </summary>
            public double ResultDouble {
                get
                {
                    if (Result is BigDecimal)
                        return (double)(BigDecimal)Result;
                    else if (Result is double)
                        return (double)Result;
                    else if (Result is int)
                        return (double)(int)Result;
                    else
                        return double.NaN;
                }
            }

            /// <summary>
            /// The result converted to a string
            /// </summary>
            public string ResultString {
                get
                {
                    if (Result is Exception) return ((Exception)Result).Message;
                    return _evaluator.Internals.O(Result);
                }
            }

            private CantusEvaluator _evaluator;
            /// <summary>
            /// The evaluator used to evaluate this expression
            /// </summary>
            public CantusEvaluator Evaluator { get { return _evaluator; } }

            /// <summary>
            /// Create a new object representing the result of an evaluation.
            /// </summary>
            public AnswerEventArgs(CantusEvaluator eval, object result, string expr, bool noSaveAns = true)
            {
                _evaluator = eval;
                _result = result;
                _expression = expr;
                _noSaveAns = noSaveAns;
            }

            public override string ToString()
            {
                return ResultString;
            }
        }

        #endregion
        #region "Thread management"
        public sealed class ThreadManager
        {

            /// <summary>
            /// Get the number of threads managed by this ThreadManager
            /// </summary>
            /// <returns></returns>
            public int ThreadCount
            {
                get { return _threadDict.Count; }
            }

            /// <summary>
            /// The max number of threads to spawn before killing old threads
            /// </summary>

            public int MaxThreads { get;  set; } = int.MaxValue;
            /// <summary>
            /// A dictionary containing threads managed by this ThreadManager
            /// </summary>
            private Dictionary<int, Thread> _threadDict { get; set; } = new Dictionary<int, Thread>();

            /// <summary>
            /// Counter for determining how many threads have been started since the last kill operation
            /// </summary>

            private int _threadCt = 0;
            /// <summary>
            /// Add a new thread to be managed
            /// </summary>
            /// <returns>The id of the new thread</returns>
            public int AddThread(Thread thread)
            {
                ManageThreads();
                _threadDict[thread.ManagedThreadId] = thread;
                return thread.ManagedThreadId;
            }

            /// <summary>
            /// Remove a thread with the specified id from the manager, without terminating it
            /// </summary>
            public void RemoveThreadById(int id)
            {
                _threadDict.Remove(id);
            }

            /// <summary>
            /// Get a thread with the specified id from the manager
            /// </summary>
            public Thread GetThreadById(int id)
            {
                if (!_threadDict.ContainsKey(id))
                    return null;
                return _threadDict[id];
            }

            /// <summary>
            /// Kill a thread with the specified id
            /// </summary>
            public void KillThreadWithId(int id)
            {
                try
                {
                    _threadDict[id].Abort();
                    _threadDict.Remove(id);
                }
                catch
                {
                    throw new EvaluatorException("Failed to kill thread");
                }
            }

            /// <summary>
            /// Kill all threads
            /// </summary>
            public void KillAll(int spareId)
            {
                Thread spared = null;
                foreach (Thread th in _threadDict.Values)
                {
                    try
                    {
                        if (th.ManagedThreadId == spareId)
                        {
                            spared = th;
                            continue;
                        }
                        th.Abort();
                    }
                    catch
                    {
                    }
                }
                _threadDict.Clear();
                if ((spared != null))
                {
                    _threadDict[spareId] = spared;
                }
            }

            /// <summary>
            /// Internal function for managing threads before each async operation
            /// </summary>
            private void ManageThreads()
            {
                if (this._threadDict.Count > 0)
                {
                    this._threadCt += 1;
                }
                else
                {
                    this._threadCt = 0;
                }

                if (this._threadCt > MaxThreads)
                {
                    try
                    {
                        foreach (Thread th in this._threadDict.Values)
                        {
                            if (Thread.CurrentThread.ManagedThreadId != th.ManagedThreadId)
                            {
                                try
                                {
                                    th.Abort();
                                    _threadCt -= 1;
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

            }
        }
        #endregion
        #endregion

        #region "Declarations"
        #region "Constants"
        /// <summary>
        /// Comment pattern: The (script) evaluator ignores all characters in each line after this pattern is found
        /// </summary>

        public const char COMMENT_START_PTN = '#';
        /// <summary>
        /// The default variable name used when none is specified (when using self-referring functions)
        /// </summary>

        public const string DEFAULT_VAR_NAME = "this";
        /// <summary>
        /// The root namespace
        /// </summary>

        public const string ROOT_NAMESPACE = "cantus";

        /// <summary>
        /// Our thread manager
        /// </summary>
        #endregion
        public ThreadManager ThreadController = new ThreadManager();
        #region "Variables"
        // modes
        /// <summary>
        /// The output mode of the evaluator
        /// </summary>
        /// <returns></returns>
        public OutputFormat OutputMode { get; set; }

        /// <summary>
        /// The angle representation mode of the evaluator (radians, degrees, gradians)
        /// </summary>
        /// <returns></returns>
        public AngleRepresentation AngleMode { get; set; }

        /// <summary>
        /// The number of spaces that would represent a tab. Default is 4.
        /// </summary>
        /// <returns></returns>
        public int SpacesPerTab { get; set; }

        /// <summary>
        /// If true, force explicit declaration of variables
        /// </summary>
        public bool ExplicitMode { get; set; }

        private bool _significantMode;
        /// <summary>
        /// If true, try to preserve sig figs whenever possible.
        /// </summary>
        public bool SignificantMode {
            get { return _significantMode; }
            set {
                _significantMode = value;
                if (_significantMode)
                {
                    foreach (string n in Variables.Keys.ToArray())
                    {
                        Reference r = Variables[n].Reference;
                        if (r.ResolveObj() is Number)
                            Variables[n].Value = new Number((((Number)r.ResolveObj()).BigDecValue()).FullDecimalRepr(), true).BigDecValue();
                    }
                }
                else
                {
                    foreach (string n in Variables.Keys.ToArray())
                    {
                        Reference r = Variables[n].Reference;
                        if (r.ResolveObj() is Number)
                        {
                            BigDecimal b = ((Number)r.ResolveObj()).BigDecValue();
                            b.SigFigs = int.MaxValue;
                            Variables[n].Value = new Number(b);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A list of previous answers (last item is the last answer)
        /// </summary>
        public List<EvalObjectBase> PrevAns { get; } = new List<EvalObjectBase>();

        // composition
        /// <summary>
        /// Object for registering and accessing operators (like +, -, *) usable in the evaluator
        /// </summary>
        internal OperatorRegistar OperatorRegistar { get; }

        /// <summary>
        /// Object for registering and accessing statements (like if, else, while, function) usable in the evaluator
        /// </summary>
        internal StatementRegistar StatementRegistar { get; }

        /// <summary>
        /// Object containing pre-defined functions usable in the evaluator
        /// </summary>
        internal InternalFunctions Internals { get; }

        /// <summary>
        /// Dictionary of user function definitions
        /// </summary>
        public Dictionary<string, UserFunction> UserFunctions { get; } = new Dictionary<string, UserFunction>();

        /// <summary>
        /// Dictionary for storing variables
        /// </summary>
        public Dictionary<string, Variable> Variables { get; } = new Dictionary<string, Variable>();

        /// <summary>
        /// Dictionary of user class definitions
        /// </summary>
        public Dictionary<string, UserClass> UserClasses { get; } = new Dictionary<string, UserClass>();

        /// <summary>
        /// Stores the names of imported scopes
        /// </summary>
        /// <returns></returns>
        public HashSet<string> Imported { get; } = new HashSet<string>();

        /// <summary>
        /// Stores the names of loaded scopes
        /// </summary>
        /// <returns></returns>
        public HashSet<string> Loaded { get; } = new HashSet<string>();

        private CantusEvaluator _parent = null;

        /// <summary>
        /// Get the parent of this evaluator
        /// </summary>
        public CantusEvaluator Parent
        {
            get
            {
                return _parent;
            }
        }

        /// <summary>
        /// Get the root evaluator of this evaluator
        /// </summary>
        public CantusEvaluator Root
        {
            get
            {
                CantusEvaluator cur = this;
                while (cur.Parent != null)
                {
                    cur = cur.Parent;
                }
                return cur;
            }
        }

        private string _scope;
        /// <summary>
        /// Records the current scope of this evaluator
        /// </summary>
        public string Scope
        {
            get
            {
                return _scope;
            }
            set
            {
                _scope = value;
            }
        }

        /// <summary>
        /// The path to the currently executing file on each process
        /// </summary>
        public Dictionary<int, string> ExecPath { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// The path to the currently executing directory on each process
        /// </summary>
        public Dictionary<int, string> ExecDir { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// Get the line number the evaluator started from, used for error reporting
        /// </summary>

        private int _baseLine;
        /// <summary>
        /// Get the line number the evaluator is currently processing, used for error reporting
        /// </summary>

        private int _curLine;
        /// <summary>
        /// The id used for the next unnamed scope created from this evaluator
        /// </summary>
        #endregion
        private int _anonymousScopeID = 0;

        #region "Events"
        /// <summary>
        /// Delegate for event raised when any async evaluation is complete.
        /// </summary>
        public delegate void EvalCompleteDelegate(object sender, AnswerEventArgs e);

        /// <summary>
        /// Event raised when any async evaluation is complete.
        /// </summary>
        /// <param name="sender">The evaluator that sent this result.</param>
        /// <param name="result">The value of the result</param>
        public event EvalCompleteDelegate EvalComplete;

        /// <summary>
        /// Delegate for thread events
        /// </summary>
        public delegate void ThreadDelegate(object sender, ThreadEventArgs e);

        /// <summary>
        /// Event raised when a new thread is started
        /// </summary>
        public event ThreadDelegate ThreadStarted;

        /// <summary>
        /// Raised when Cantus needs to read input input from the console. Handle to use I/O in GUI applications
        /// </summary>
        public event InternalFunctions.ReadInputDelegate ReadInput
        {
            add
            {
                Internals.ReadInput += value;
            }
            remove
            {
                Internals.ReadInput -= value;
            }

        }
        /// <summary>
        /// Raised when Cantus needs to read input input from the console. Handle to use I/O in GUI applications
        /// </summary>
        public event InternalFunctions.WriteOutputDelegate WriteOutput
        {
            add
            {
                Internals.WriteOutput += value;
            }
            remove
            {
                Internals.WriteOutput -= value;
            }

        }
        /// <summary>
        /// Raised when Cantus needs to clear the console
        /// </summary>
        public event InternalFunctions.ClearConsoleDelegate ClearConsole
        {
            add
            {
                Internals.RequestClearConsole += value;
            }
            remove
            {
                Internals.RequestClearConsole -= value;
            }
            #endregion
        }

        #region "Evaluator Constants"

        /// <summary>
        /// Accurate value of PI (300 DP), stored in a string
        /// </summary>
        private const string PI =  "3.1415926535897932384626433832795028841971693993751058209749445923078" +
                                   "164062862089986280348253421170679821480865132823066470938446095505822" + 
                                   "317253594081284811174502841027019385211055596446229489549303819644288" +
                                   "10975665933446128475648233786783165271201909145648566923460348610454326648213393607260249141273";
        /// <summary>
        /// Accurate value of E (300 DP), stored in a string
        /// </summary>
        private const string E = "2.718281828459045235360287471352662497757247093699959574966967627724076" +
                                 "63035354759457138217852516642742746639193200305992181741359662904357290" + 
                                 "03342952605956307381323286279434907632338298807531952510190115738341879" + 
                                 "30702154089149934884167509244761460668082264800168477411853742345442437107539077744992069";

        /// <summary>
        /// List of predefined constants, as a dictionary
        /// By default this includes some often used math, physics, and chemistry constants. 
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, object> _default { get; } = new Dictionary<string, object>{
            {"e", new Number(E).BigDecValue()},
            {"pi", new Number(PI).BigDecValue()},
            {"π", new Number(PI).BigDecValue()},
            {"phi", 1.61803398875},
            {"φ", 1.61803398875},
            {"avogadro", 6.0221409E+23},
            {"na", 6.0221409E+23},
            {"G", 0.0000000000667408},
            {"gravity", 0.0000000000667408},
            {"g", 9.807},
            {"i", System.Numerics.Complex.ImaginaryOne},
            {"imaginaryunit", System.Numerics.Complex.ImaginaryOne},
            {"c", 299792458.0},
            {"lightspeed", 299792458.0},
            {"h", 6.6260755E-34},
            {"planck", 6.6260755E-34},
            {"hbar", 1.05457266E-34},
            {"planckreduced", 1.05457266E-34},
            {"e0", 0.000000000008854187817},
            {"permittivity", 0.000000000008854187817},
            {"mu0", 4.0 * new Number(PI).BigDecValue() * 0.0000001},
            {"permeability", 4.0 * new Number(PI).BigDecValue() * 0.0000001},
            {"F", 96485.3329},
            {"faraday", 96485.3329},
            {"me", 9.10938356E-31},
            {"electronmass", 9.10938356E-31},
            {"mp", 1.6726219E-27},
            {"protonmass", 1.6726219E-27},
            {"q", 1.60217662E-19},
            {"elemcharge", 1.60217662E-19},
            {"soundspeed", 343.2},
            {"vs", 343.2},
            {"R", 8.3144598},
            {"gas", 8.3144598},
            {"cmperinch", 2.54},
            {"torrsperatm", 760.0},
            {"torrsperkpa", 7.50062},
            {"prime", 2 ^ 31 - 1}
        };


        /// <summary>
        /// List of reserved names not to be used in function or variable names
        /// </summary>
        /// <returns></returns>
        private static HashSet<string> _reserved { get; } = new HashSet<string>(
            new string[]{DEFAULT_VAR_NAME, "if", "else", "not", "and", "or", "xor",
            "while", "for", "in", "to", "step", "until", "repeat", "run", "import", "function", "let", "global",
            "undefined", "null", "switch", "case", "load", "prototype", "namespace"});

        /// <summary>
        /// Reload the default constants into variable storage (accessible via Reload() in execution)
        /// <param name="name">Optional: if specified, only reloads constant with name specified</param>
        /// </summary>
        public void ReloadDefault(string name = "")
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (_default.ContainsKey(name))
                {
                    SetVariable(name, _default[name], _scope);
                }
                else
                {
                    throw new EvaluatorException(name + " is not a valid default variable");
                }
            }
            else
            {
                foreach (KeyValuePair<string, object> kvp in _default)
                {
                    SetVariable(kvp.Key, kvp.Value, _scope);
                }
            }
        }

           
/// <summary>
/// Reload initialization files
/// </summary>
public void ReInitialize()
{
    string cantusPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;
	List<string> initScripts = new List<string>();
    if (Directory.Exists(cantusPath + "plugin/"))
    {
        initScripts.AddRange(Directory.GetFiles(cantusPath + "plugin/", "*.can", SearchOption.AllDirectories));
    }

	// initialization files: init.can and init/* ran in root scope on startup
	if (File.Exists(cantusPath + "init.can"))
		initScripts.Add(cantusPath + "init.can");
	if (Directory.Exists(cantusPath + "init/"))
		initScripts.AddRange(Directory.GetFiles(cantusPath + "init/", "*.can", SearchOption.AllDirectories));

	foreach (string file in initScripts) {
		try {
			// Evaluate each file. On error, ignore.
            if (file.StartsWith(cantusPath + "plugin/"))
            {
                AngleMode = AngleRepresentation.Radian;
                OutputMode = OutputFormat.Math;
                SignificantMode = false;
                ExplicitMode = false;
            }
			Load(file, file == cantusPath + "init.can" || file.ToLower().StartsWith(cantusPath + "init" + Path.DirectorySeparatorChar));
		} catch (Exception ex) {
			if (file == cantusPath + "init.can") {
                        throw new EvaluatorException("Error occurred while processing init.can.\nVariables and functions may not load.\n\nMessage:\n\n" + ex.Message);
			} else {
                        throw new EvaluatorException("Error occurred while loading \"" + file.Replace(Path.DirectorySeparatorChar, SCOPE_SEP).Remove(file.LastIndexOf(".")) + "\"\n" + ex.Message);
			}
		}
	}
}

        #endregion
        #endregion

        #region "Evaluator Logic"
        #region "Constructors"
        /// <summary>
        /// Not publicly accessible, for internal initialization only
        /// </summary>
        private CantusEvaluator()
        {
            this.Internals = new InternalFunctions(this);
            this.OperatorRegistar = new OperatorRegistar(this);
            this.StatementRegistar = new StatementRegistar(this);
            this._baseLine = 0;
            this._curLine = 0;
            this.ExecPath[Thread.CurrentThread.ManagedThreadId] = Internals.CantusPath();
            this.ExecDir[Thread.CurrentThread.ManagedThreadId] = Internals.CantusDir();
        }

        /// <summary>
        /// Create a new Evaluator for evaluating mathematical expressions &amp; .can scripts
        /// </summary>
        /// <param name="outputFormat">The output mode of this evaluator. E.g.: MathO: 0.5->1/2; SciO: 0.5->5 E -1; LineO: 0.5->0.500</param>
        /// <param name="angleRepr">The angle representation mode of this evaluator (Radians, Degrees, etc.)</param>
        /// <param name="spacesPerTab">The number of spaces per tab, default is 4</param>
        /// <param name="explicit">Whether to force explicit variable declarations in this evaluator</param>
        /// <param name="significant">Whether to keep track of sig figs in this evaluator</param>
        /// <param name="prevAns">List of previous answers</param>
        /// <param name="vars">Variable definitions to start</param>
        /// <param name="userFunctions">Dictionary of user function definitions</param>
        /// <param name="baseLine">The line number that this evaluator started at, used for error reporting</param>
        /// <param name="scope">The name of the scope of this evaluator</param>
        /// <param name="parent">The parent evaluator, if any</param>
        public CantusEvaluator(OutputFormat outputFormat = OutputFormat.Math,
            AngleRepresentation angleRepr = AngleRepresentation.Radian,
            int spacesPerTab = 4,
            bool @explicit = false,
            bool significant = false,
            List<EvalObjectBase> prevAns = null,
            Dictionary<string, Variable> vars = null,
            Dictionary<string, UserFunction> userFunctions = null,
            Dictionary<string, UserClass> userClasses = null,
            int baseLine = 0,
            string scope = ROOT_NAMESPACE,
            bool reloadDefault = true,
            CantusEvaluator parent = null) : this()
        {

            try
            {
                this.OutputMode = outputFormat;
                this.AngleMode = angleRepr;
                this.SpacesPerTab = spacesPerTab;
                this.SignificantMode = significant;
                this.ExplicitMode = @explicit;

                if ((prevAns != null))
                    this.PrevAns = prevAns;
                if ((vars != null))
                    this.Variables = vars;
                if ((userFunctions != null))
                    this.UserFunctions = userFunctions;

                this._baseLine = baseLine;
                this._curLine = baseLine;
                this._scope = scope;

                if (parent != null)
                {
                    this._parent = parent;
                    if (parent.Internals.RequestClearConsoleHandler != null) this.ClearConsole += parent.Internals.RequestClearConsoleHandler;
                    if (parent.Internals.ReadInputHandler != null) this.ReadInput += parent.Internals.ReadInputHandler;
                    if (parent.Internals.WriteOutputHandler != null) this.WriteOutput += parent.Internals.WriteOutputHandler;
                }

                Loaded.Add(ROOT_NAMESPACE);
                Loaded.Add("plugin");
                if (IsExternalScope(scope, ROOT_NAMESPACE))
                    this.Import(ROOT_NAMESPACE);

                this.Import("plugin");

                if (reloadDefault)
                {
                    // reload default variable values
                    this.ReloadDefault();
                    // reload initialization files
                    this.ReInitialize();
                }
            }
            catch (EvaluatorException ex)
            {
                throw ex;
            }
            catch (Exception) { // do nothing 
            }
        }
        
        #endregion

        /// <summary>
        /// Load all files within a directory, for internal uses only (handled by Load())
        /// </summary>
        private void LoadDir(string path, bool asInternal = false, bool import = false)
        {
            System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(path);
            foreach (FileInfo fi in dir.GetFiles("*.can", SearchOption.AllDirectories))
            {
                string curDir = Environment.CurrentDirectory;
                string fp = fi.FullName;
                if (fp.StartsWith(curDir))
                    fp = fp.Substring(curDir.Count() + 1);
                Load(fp, asInternal, false);
            }
            string newScope = path;
            if (newScope.EndsWith(".can"))
                newScope = newScope.Remove(newScope.Length - 4);
            if (newScope.StartsWith("include"))
                newScope = newScope.Substring("include".Length);
            if (newScope != System.IO.Path.GetFullPath(newScope))
            {
                newScope = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(newScope)) + SCOPE_SEP + System.IO.Path.GetFileName(newScope);
            }
            newScope = newScope.Replace('/', SCOPE_SEP).Replace('\\', SCOPE_SEP).Trim(new[] { SCOPE_SEP });
            if (import)
                this.Import(newScope.Trim());
        }

        /// <summary>
        /// Make available the specified package for use inside the evaluator (files in plugin/ and init/ are imported by default)
        /// Accepts: 
        /// 1. Absolute path to file (uses parent directory + file name as package name)
        /// 2. Relative path to directory/file from current directory
        /// 3. Relative path to directory/file from include/ 
        /// 4. Relative path from current directory or include, using SCOPE_SEP (usually ".") as separator
        /// The extension .can can be ignored
        /// </summary>
        /// <param name="path">Path of the script to evaluate</param>
        /// <param name="asInternal">If true, the script is executed in the current scope</param>
        /// <param name="import">If true, imports the package into the evaluator after loading</param>
        public void Load(string path, bool asInternal = false, bool import = false)
        {
            path = path.Trim();

            // if file does not exist, see if it is using SCOPE_SEP notation instead of absolute path
            if (!File.Exists(path))
            {
                //
                // it's a directory, so load entire directory and exit
                if (Directory.Exists(path))
                {
                    LoadDir(path, asInternal, import);
                    return;
                }

                path = path.Replace(SCOPE_SEP, System.IO.Path.DirectorySeparatorChar);
                if (path.EndsWith(System.IO.Path.DirectorySeparatorChar + "can"))
                    path = path.Remove(path.Length - 4);
                if (!path.EndsWith(".can"))
                    path += ".can";

                if (!File.Exists(path))
                {
                    // load entire directory, with SCORE_SEP notation
                    if (Directory.Exists(path))
                    {
                        LoadDir(path, asInternal, import);
                        return;
                    }
                    // if still not found look in the include directory
                    if (!path.StartsWith("include" + System.IO.Path.DirectorySeparatorChar))
                    {
                        path = "include" + Path.DirectorySeparatorChar + path;
                    }
                }
            }

            // load entire directory, with include
            if (Directory.Exists(path))
            {
                LoadDir(path, asInternal, import);
                return;
            }

            string newScope = _scope;
            if (!asInternal)
            {
                // get the scope name for the file
                newScope = GetFileScopeName(path);
            }

            CantusEvaluator tmpEval = DeepCopy(newScope);

            Exception except = null;
            string prevPath = ExecPath[Thread.CurrentThread.ManagedThreadId];
            string prevDir = ExecDir[Thread.CurrentThread.ManagedThreadId];
            try
            {
                ExecDir[Thread.CurrentThread.ManagedThreadId] = Path.GetDirectoryName(path);
                ExecPath[Thread.CurrentThread.ManagedThreadId] = path;
                tmpEval.EvalRaw(File.ReadAllText(path), noSaveAns: true);
            }
            catch (Exception ex)
            {
                except = ex;
                // first ensure all things we currently have are loaded. Throw the error in the end.
            }

            ExecPath[Thread.CurrentThread.ManagedThreadId] = prevPath;
            ExecDir[Thread.CurrentThread.ManagedThreadId] = prevDir;

            // load new user functions
            foreach (UserFunction uf in tmpEval.UserFunctions.Values)
            {
                if (uf.Modifiers.Contains("private") || uf.Modifiers.Contains("internal"))
                    continue;
                // ignore private functions
                UserFunctions[uf.FullName] = (uf);
            }

            // load new variables
            foreach (Variable var in tmpEval.Variables.Values)
            {
                if (var.Modifiers.Contains("private") || var.Modifiers.Contains("internal"))
                    continue;
                // ignore private variables
                Variables[var.FullName] = var;
            }

            // load new classes
            foreach (UserClass uc in tmpEval.UserClasses.Values)
            {
                if (uc.Modifiers.Contains("private") || uc.Modifiers.Contains("internal"))
                    continue;
                // ignore private variables
                UserClasses[uc.FullName] = uc;
            }

            if (asInternal)
            {
                OutputMode = tmpEval.OutputMode;
                AngleMode = tmpEval.AngleMode;
                SpacesPerTab = tmpEval.SpacesPerTab;
                ExplicitMode = tmpEval.ExplicitMode;
                SignificantMode = tmpEval.SignificantMode;
            }

            tmpEval.Dispose();

            Loaded.Add(newScope);

            // import the scope if we need to
            if (import)
                this.Import(newScope.Trim());

            while (newScope.Contains(SCOPE_SEP))
            {
                newScope = newScope.Remove(newScope.LastIndexOf(SCOPE_SEP));
                Loaded.Add(newScope);
            }

            // if there was an error, throw it now
            if ((except != null))
                throw except;
        }

        /// <summary>
        /// Import a scope so items declared in it may be accessed directly
        /// </summary>
        public void Import(string scope)
        {
            this.Imported.Add(scope);
        }

        /// <summary>
        /// Un-import a scope imported with import
        /// </summary>
        public void Unimport(string scope)
        {
            if (this.Imported.Contains(scope))
                this.Imported.Remove(scope);
        }

        /// <summary>
        /// Evauate a multi-line script and return the result as a string
        /// </summary>
        /// <param name="script">The script to evaluate</param>
        /// <param name="noSaveAns">If true, evaluates without saving answers</param>
        /// <param name="declarative">If true, disallows all expressions other than declarations</param>
        /// <param name="returnedOnly">If true, returns only if the result is returned from the script. Otherwise returns null.</param>
        /// <param name="feederScript">A ScriptFeeder object to get lines when no more lines are available</param>
        public string Eval(string script, bool noSaveAns = false, bool declarative = false,
            bool returnedOnly = false, ScriptFeeder feederScript = null)
        {
            object result = EvalRaw(script, noSaveAns, declarative, true, feederScript);
            if (returnedOnly)
            {

                if (((StatementResult)result).Code == StatementResult.ExecCode.@return)
                {
                    return Internals.O(((StatementResult)result).Value);
                }
                else if (((StatementResult)result).Code != StatementResult.ExecCode.resume)
                {
                    // else if the user tried to break or continue at the top level then we complain
                    throw new EvaluatorException("Invalid " + ((StatementResult)result).Code.ToString() + " statement");
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return Internals.O(((StatementResult)result).Value);
            }
        }

        /// <summary>
        /// Evaluate a multi-line script asynchronously and raises the EvalComplete event when done
        /// </summary>
        /// <param name="script">The script to evaluate</param>
        /// <param name="noSaveAns">If true, evaluates without saving answers</param>
        /// <param name="declarative">If true, disallows all expressions other than declarations</param>
        /// <param name="internal">If true, returns a internal StatementResult object with information on return code</param>
        public int EvalAsync(string script, bool noSaveAns = false, bool declarative = false, bool @internal = false)
        {
            Thread th = new Thread(() =>
            {
                try
                {
                    if (EvalComplete != null)
                    {
                        EvalComplete(this, new AnswerEventArgs(this, EvalRaw(script, noSaveAns, declarative, @internal), script, noSaveAns));
                    }
                    // do not return, do nothing
                }
                catch (ThreadAbortException)
                {
                }
                catch (Exception ex)
                {
                    if (EvalComplete != null)
                    {
                        EvalComplete(this, new AnswerEventArgs(this, ex, script, noSaveAns));
                    }
                }
                this.ThreadController.RemoveThreadById(Thread.CurrentThread.ManagedThreadId);
            });
            int id = this.ThreadController.AddThread(th);

            this.ExecPath[id] = Internals.CantusPath();
            this.ExecDir[id] = Internals.CantusDir();

            th.IsBackground = true;
            th.Start();
            if (ThreadStarted != null)
            {
                ThreadStarted(this, new ThreadEventArgs(th.ManagedThreadId));
            }
            return id;
        }

        /// <summary>
        /// Evauate a multi-line script and return the result as a system object
        /// </summary>
        /// <param name="script">The script to evaluate</param>
        /// <param name="noSaveAns">If true, evaluates without saving answers</param>
        /// <param name="declarative">If true, disallows all expressions other than declarations</param>
        /// <param name="internal">If true, returns a internal statement result object with information on return code</param>
        /// <param name="feederScript">A ScriptFeeder object to get lines when no more lines are available</param>
        public object EvalRaw(string script, bool noSaveAns = false, bool declarative = false, bool @internal = false, ScriptFeeder feederScript = null)
        {

            int lineNum = 0;
            string fullLine = "";

            try
            {
                List<string> lines = script.Replace(Environment.NewLine, "\n").Split(new[]{
                    '\r',
                    '\n'
                }).ToList();
                if (lines.Count == 0)
                    return double.NaN;

                // initial indentation level
                int rootIndentLevel = -1;

                Statement curSM = null;
                // current statement (if)
                Block curBlock = null;
                // current block within the statement (if, elif, elif, else)
                Block nextBlock = null;
                // next block in chain when 'then' chaining is used (run then while x<3000) 
                int curBlockBegin = 0;
                // the line at which the current block began at
                List<Block> curBlockLst = new List<Block>();
                // list of blocks for this statement
                StringBuilder curBlockInner = new StringBuilder();
                // the content of the block
                int curBlockIndent = rootIndentLevel;
                // the indentation level of this block
                object lastVal = double.NaN;
                // the last value we got in an evaluation, returned if no return statement is found

                // last line is an extra blank line to end blocks
                while (lineNum <= lines.Count)
                {
                    fullLine = "";

                    // feeder
                    if (lineNum == lines.Count && (feederScript != null) && !(curBlockLst.Count == 1 && curBlockLst[0].Keyword == "return"))
                    {
                        // wait for feed
                        if (!feederScript.IsBusy && feederScript.IsStarted)
                        {
                            feederScript.RaiseWaiting(lines[lines.Count - 1]);
                            feederScript.ResetEvent.Wait();
                        }
                        feederScript.ResetEvent.Reset();
                        if (!feederScript.IsStarted)
                        {
                            break;
                        }
                        lines.Add(feederScript.GetLine());
                    }

                    if (lineNum < lines.Count)
                    {
                        fullLine = lines[lineNum];

                        // remove comments
                        bool dqCount = true;
                        bool sqCount = true;
                        for (int i = 0; i <= fullLine.Count() - 1; i++)
                        {
                            char c = fullLine[i];
                            if (c == '\'')
                            {
                                dqCount = !dqCount;
                            }
                            else if (c == '\'')
                            {
                                sqCount = !sqCount;
                            }
                            else if (c == '\"')
                            {
                                sqCount = !dqCount;
                            }
                            else if (c == COMMENT_START_PTN && dqCount && sqCount)
                            {
                                fullLine = fullLine.Remove(i);
                                break;
                            }
                        }

                        // ignore blank lines
                        if (string.IsNullOrWhiteSpace(fullLine) && feederScript == null)
                        {
                            if ((curBlock != null))
                            {
                                curBlockInner.Append('\n');
                            }
                            lineNum += 1;
                            continue;
                        }

                        // while line ends with \ then keep joining the next line
                        while (fullLine.TrimEnd().EndsWith("\\"))
                        {
                            lineNum += 1;
                            if (lineNum >= lines.Count)
                            {
                                if (feederScript != null)
                                {
                                    if (!feederScript.IsBusy && feederScript.IsStarted)
                                    {
                                        feederScript.RaiseWaiting(lines[lines.Count - 1]);
                                        feederScript.ResetEvent.Wait();
                                    }
                                    feederScript.ResetEvent.Reset();
                                    if (!feederScript.IsStarted)
                                    {
                                        break;
                                    }
                                    lines.Add(feederScript.GetLine());
                                }
                                else
                                {
                                    break;
                                }
                            }
                            fullLine = fullLine.TrimEnd().TrimEnd('\\').TrimEnd() + lines[lineNum];
                        }

                        // multiline lambda
                        if (fullLine.TrimEnd().EndsWith("=>") || (fullLine.TrimEnd().EndsWith("`") && Internals.Count(fullLine, "`") % 2 == 1))
                        {
                            lineNum += 1;
                            while (lineNum < lines.Count)
                            {
                                fullLine = fullLine + Environment.NewLine + lines[lineNum];
                                lineNum += 1;
                            }
                        }

                        // multiline/triple quoted string
                        string tripleQuote = "\"\"\"";
                        if (fullLine.Contains(tripleQuote) && Internals.Count(fullLine, tripleQuote) % 2 == 1)
                        {
                            lineNum += 1;

                            while (lineNum < lines.Count)
                            {
                                fullLine = fullLine + Environment.NewLine + lines[lineNum];
                                if (lines[lineNum].Contains(tripleQuote))
                                    break;
                                lineNum += 1;
                            }
                        }

                        tripleQuote = "'''";
                        if (fullLine.Contains(tripleQuote) && Internals.Count(fullLine, tripleQuote) % 2 == 1)
                        {
                            lineNum += 1;

                            while (lineNum < lines.Count)
                            {
                                fullLine = fullLine + Environment.NewLine + lines[lineNum];
                                if (lines[lineNum].Contains(tripleQuote))
                                    break;
                                lineNum += 1;
                            }
                        }

                        // update global line number (only update for non-blank lines and does not update within blocks)
                        if (curBlock == null)
                            this._curLine = this._baseLine + lineNum - (feederScript != null ? 1 : 0);
                    }

                    int indent = LineIndentLevel(fullLine);
                    if (rootIndentLevel < 0)
                        rootIndentLevel = indent;

                    // allow inline expressions with ; (assume same indent level)
                    string[] spl = fullLine.Split(new[] { ';' });

                    for (int i = 0; i <= spl.Count() - 1; i++)
                    {
                        string l = spl[i];
                        if (string.IsNullOrWhiteSpace(l) && lineNum < lines.Count && i > 0)
                            continue;
                        // skip empty

                        if ((curSM != null) && (curBlock != null))
                        {
                            // if we are appending to a statement block
                            if (curBlockIndent == -1)
                            {
                                if (indent <= rootIndentLevel)
                                {
                                    // go back and cancel this block
                                    curBlockLst.Add(curBlock);
                                    lineNum -= 1;
                                    curBlock = null;
                                    continue;

                                    //If indent <= rootIndentLevel Then Throw New EvaluatorException(
                                    //"Higher indentation level expected")
                                }
                                else
                                {
                                    _curLine += 1;
                                    // increment current global line to first line of block 
                                }

                                // Set indentation level
                                curBlockIndent = indent;
                            }

                            // end of block found
                            if (indent < curBlockIndent)
                            {
                                curBlock.Content = curBlockInner.ToString().TrimEnd();

                                // 'Then' chaining
                                nextBlock = null;
                                if (curBlock.Argument.ToLowerInvariant().Contains(" then "))
                                {
                                    string right = curBlock.Argument.Substring(curBlock.Argument.ToLowerInvariant().IndexOf(" then ") + 6);

                                    curBlock.Argument = curBlock.Argument.Remove(curBlock.Argument.ToLowerInvariant().IndexOf(" then "));

                                    string newKwd = StatementRegistar.KeywordFromExpr(ref right);
                                    nextBlock = new Block(newKwd, right, "");
                                }

                                // Add this block to the list of blocks for this statement
                                curBlockLst.Add(curBlock);

                                // Clear to make new block
                                curBlock = null;

                                // just append the line and skip the rest
                            }
                            else
                            {
                                if (curBlockInner.Length > 0 && !(curBlockInner.Length == 1 && curBlockInner[0] == '\n'))
                                    curBlockInner.Append('\n');
                                // new line
                                // if we are not at the end append this line to the block
                                curBlockInner.Append(l);
                                continue;
                                // do not execute the rest of the code
                            }
                        }

                        if (indent > rootIndentLevel)
                            throw new SyntaxException("Invalid indentation (Check if tabs/spaces match: 1 tab=" + SpacesPerTab + " spaces (change with SpacesPerTab())");
                        string kwd = "";

                        // if we just finished executing the block, check if there is a continuation e.g. else for an if block
                        if ((curSM != null))
                        {
                            kwd = StatementRegistar.KeywordFromExpr(ref l);

                            // found end of statement
                            if (string.IsNullOrEmpty(kwd) || !curSM.AuxKeywords.Contains(kwd))
                            {
                                // don't execute if there are no blocks at all
                                if (curBlockLst.Count > 0)
                                {
                                    // inline operators: actually on the previous line
                                    if (!curSM.BlockLevel || lineNum <= curBlockBegin + 1)
                                        _curLine -= 1;
                                    StatementResult res = curSM.Execute(curBlockLst);
                                    // process the statement 

                                    if (@internal)
                                    {
                                        switch (res.Code)
                                        {
                                            case StatementResult.ExecCode.@break:
                                            case StatementResult.ExecCode.@continue:
                                            case StatementResult.ExecCode.@return:
                                                // return the code with the value

                                                return res;
                                            case StatementResult.ExecCode.breakLevel:
                                                // break level and resume

                                                return new StatementResult(res.Value, StatementResult.ExecCode.resume);
                                            default:
                                                // if Resume
                                                // continue executing otherwise 
                                                lastVal = res.Value;
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        // if we're at the top (non-internal) level and get the return code, directly return the value
                                        if (res.Code == StatementResult.ExecCode.@return)
                                        {
                                            return res.Value;
                                        }
                                        else if (!(res.Code == StatementResult.ExecCode.resume))
                                        {
                                            throw new SyntaxException("Continue and break statements may only be used within loops");
                                        }

                                        // other codes are invalid at this level
                                        lastVal = res.Value;
                                    }
                                }

                                curBlockLst.Clear();
                                curSM = StatementRegistar.StatementWithKeyword(kwd, true);

                                // then chaining
                                if ((nextBlock != null))
                                {
                                    // set block and go back
                                    curBlock = nextBlock;
                                    curSM = StatementRegistar.StatementWithKeyword(curBlock.Keyword, true);
                                    lineNum = curBlockBegin;
                                    curBlockInner.Clear();
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            // see if the current line matches any statement 
                            // note that this function also modifies l so that the keyword is removed (becomes the argument)
                            kwd = StatementRegistar.KeywordFromExpr(ref l);
                            curSM = StatementRegistar.StatementWithKeyword(kwd, true);
                        }

                        // if matches
                        if ((curSM != null) && !(declarative && !curSM.Declarative))
                        {

                            // check if we have an argument that we are not supposed to have
                            if (curSM.ArgumentExpected.ContainsKey(kwd) && !curSM.ArgumentExpected[kwd] && !string.IsNullOrWhiteSpace(l) && !l.TrimStart().ToLowerInvariant().StartsWith("then "))
                            {
                                throw new EvaluatorException("\"" + curSM.MainKeywords[0] + "\"" + " statement does not accept any arguments");
                            }

                            curBlock = new Block(kwd, l, "");
                            if (curSM.BlockLevel)
                            {
                                // make a new block object out of the keyword and the argument
                                // if this is a block level statement, start a new block
                                curBlockIndent = -1;
                                // expect a higher indentation level ( automatically set on next iteration )
                                curBlockInner = new StringBuilder();
                                curBlockBegin = lineNum;
                            }
                            else
                            {
                                // if this is an inline statement, we don't expect a block, so we use an empty one
                                curBlockLst.Add(curBlock);
                                curBlock = null;
                            }
                        }
                        else
                        {
                            if (StatementRegistar.HasKeyword(kwd))
                            {
                                string[] tmp = StatementRegistar.StatementWithKeyword(kwd).MainKeywords;
                                throw new SyntaxException("The \"" + kwd + "\" statement must be paired with " +
                                    (tmp.Length != 1 ? "one of: " + string.Join(",", tmp) : " the \"" + tmp[0] + "\" statement"));
                            }

                            if (string.IsNullOrWhiteSpace(l))
                                continue;
                            if (declarative)
                                throw new Exception("Declarative mode disallows non-declarative statements.");

                            object res = EvalExprRaw(l, true);
                            if (!(res is double && double.IsNaN((double)(res))) && !(res is BigDecimal && ((BigDecimal)res).IsUndefined))
                            {
                                lastVal = res;
                            }
                        }
                    }
                    lineNum += 1;
                }

                if (@internal)
                {
                    return new StatementResult(lastVal);
                }
                else
                {
                    // save answer
                    if (!noSaveAns)
                    {
                        // do not save if undefined
                        if ((!(lastVal is BigDecimal) || !((BigDecimal)lastVal).IsUndefined) && (!(lastVal is double) || !double.IsNaN((double)(lastVal))))
                        {
                            PrevAns.Add(DetectType(lastVal, true));
                        }
                    }
                    return lastVal;
                }

                // append line numbers to errors
            }
            catch (EvaluatorException ex)
            {
                if (ex.Line == 0)
                {
                    if (ex is MathException)
                    {
                        throw new MathException(ex.Message, _curLine + 1);
                    }
                    else if (ex is SyntaxException)
                    {
                        throw new SyntaxException(ex.Message, _curLine + 1);
                    }
                    else
                    {
                        throw new EvaluatorException(ex.Message, _curLine + 1);
                    }
                }
                else
                {
                    throw ex;
                }

                // do nothing
            }
            catch (ThreadAbortException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new EvaluatorException(ex.Message, _curLine + 1);
            }
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
                    res += SpacesPerTab;
                }
                else
                {
                    return res;
                }
            }
            return 0;
        }

        /// <summary>
        /// Evauate a mathematical expression and return the result as a processed string
        /// </summary>
        /// <param name="expr">The expression to evaluate</param>
        /// <param name="noSaveAns">If true, evaluates without saving answers</param>
        /// <param name="conditionMode">If true, the = operator is always used for comparison 
        /// (otherwise both assignment and comparison)</param>
        public string EvalExpr(string expr, bool noSaveAns = false, bool conditionMode = false)
        {
            return Internals.O(EvalExprRaw(expr, noSaveAns, conditionMode));
        }

        /// <summary>
        /// Evauate a mathematical expression asynchroneously and raises the EvalComplete event when done
        /// </summary>
        /// <param name="expr">The expression to evaluate</param>
        /// <param name="noSaveAns">If true, evaluates without saving answers</param>
        /// <param name="conditionMode">If true, the = operator is always used for comparison 
        /// (otherwise both assignment and comparison)</param>
        public int EvalExprAsync(string expr, bool noSaveAns = false, bool conditionMode = false)
        {
            Thread th = new Thread(() =>
            {
                try
                {
                    if (EvalComplete != null)
                    {
                        EvalComplete(this, new AnswerEventArgs(this,EvalExprRaw(expr, noSaveAns, conditionMode), expr, noSaveAns));
                    }
                    // do nothing
                }
                catch (ThreadAbortException)
                {
                }
                catch (Exception ex)
                {
                    if (EvalComplete != null)
                    {
                        EvalComplete(this, new AnswerEventArgs(this, ex, expr, noSaveAns));
                    }
                }
                this.ThreadController.RemoveThreadById(Thread.CurrentThread.ManagedThreadId);
            });
            int id = this.ThreadController.AddThread(th);

            this.ExecPath[id] = Internals.CantusPath();
            this.ExecDir[id] = Internals.CantusDir();

            th.IsBackground = true;
            th.Start();
            if (ThreadStarted != null)
            {
                ThreadStarted(this, new ThreadEventArgs(th.ManagedThreadId));
            }
            return id;
        }

        /// <summary>
        /// Evauate a mathematical expression and return the resulting object
        /// </summary>
        /// <param name="expr">The expression to evaluate</param>
        /// <param name="noSaveAns">If true, evaluates without saving answers</param>
        /// <param name="conditionMode">If true, the = operator is always used for comparison 
        /// (otherwise both assignment and comparison)</param>
        public object EvalExprRaw(string expr, bool noSaveAns = false, bool conditionMode = false)
        {
            bool oldmode = OperatorRegistar.ConditionMode;
            OperatorRegistar.ConditionMode = conditionMode;
            EvalObjectBase resultObj = ResolveOperators(Tokenize(expr));
            OperatorRegistar.ConditionMode = oldmode;

            object result = BigDecimal.Undefined;


            if ((resultObj != null))
            {
                if (resultObj is Reference && !(((Reference)resultObj).GetRefObject() is Reference))
                {
                    resultObj = ((Reference)resultObj).GetRefObject();
                }

                if (resultObj is Number)
                {
                    result = ((Number)resultObj).BigDecValue();
                }
                else if (resultObj is Reference)
                {
                    result = resultObj;
                }
                else
                {
                    result = resultObj.GetValue();
                }

                if (result == null)
                    result = BigDecimal.Undefined;
            }

            if (!noSaveAns)
            {
                // do not save if undefined
                if ((!(result is BigDecimal) || !((BigDecimal)result).IsUndefined) && (!(result is double) || !(double.IsNaN((double)(result)))))
                    PrevAns.Add(resultObj);
            }

            return result;
        }

        /// <summary>
        /// Goes through a list of tokens and evaluates all operators by precedence
        /// </summary>
        /// <param name="tokens">The list of tokens to evaluate</param>
        /// <returns></returns>
        private EvalObjectBase ResolveOperators(TokenList tokens)
        {
            // start from operators with highest precedence, skipping the brackets (already evaluated when tokenizing)
            for (int i = Enum.GetValues(typeof(OperatorRegistar.Precedence)).Length - 1; i >= 0; i += -1)
            {
                OperatorRegistar.Precedence cur_precedence = (OperatorRegistar.Precedence)i;

                int prevct = 0;
                // keep looping until all operators are done
                while (tokens.OperatorsWithPrecedenceCount(cur_precedence) > 0)
                {
                    List<int> preclst = tokens.OperatorsWithPrecedence(cur_precedence);
                    prevct = tokens.OperatorsWithPrecedenceCount(cur_precedence);

                    // RTL evaluation for assignment operators so you can chain them
                    if (cur_precedence == OperatorRegistar.Precedence.assignment)
                        preclst.Reverse();


                    foreach (int opid in preclst)
                    {
                        // check if the operator has not already been executed and is of the correct precedence
                        if (tokens.IsRemoved(opid) || tokens.OperatorAt(opid).Precedence != cur_precedence)
                            continue;

                        Token prevtoken = opid > 0 ? tokens[opid - 1] : new Token(null, null);
                        Token curtoken = tokens[opid];
                        EvalObjectBase result = default(EvalObjectBase);
                        // operators like x!
                        if (curtoken.Operator is OperatorRegistar.UnaryOperatorBefore)
                        {
                            OperatorRegistar.UnaryOperatorBefore op = (OperatorRegistar.UnaryOperatorBefore)curtoken.Operator;

                            // if we're not passing by reference then copy and "dereference" the references before passing
                            if (!op.ByReference && (prevtoken.Object != null))
                            {
                                prevtoken.Object = prevtoken.Object.GetDeepCopy();
                                if (prevtoken.Object is Reference)
                                {
                                    prevtoken.Object = ((Reference)prevtoken.Object).ResolveObj();
                                }
                            }

                            try
                            {
                                result = op.Execute(prevtoken.Object);
                                if (SystemMessage.IsType(result) &&
                                    ((SystemMessage)result).Type == SystemMessage.MessageType.defer) {
                                    continue;
                                }
                            }
                            catch (NullReferenceException)
                            {
                                throw new EvaluatorException("Operator " + op.Signs[0] + " disallows empty operands");
                            }

                            tokens.SetObject(opid - 1, result);
                            if (tokens.ObjectAt(opid) == null || (tokens.ObjectAt(opid).GetValue() is double && double.IsNaN((double)(tokens.ObjectAt(opid).GetValue()))))
                            {
                                tokens.RemoveAt(opid);
                            }
                            else
                            {
                                tokens.SetOperator(opid, OperatorRegistar.DefaultOperator);
                                // default to multiply
                            }

                            // operators like ~x
                        }
                        else if (curtoken.Operator is OperatorRegistar.UnaryOperatorAfter)
                        {
                            OperatorRegistar.UnaryOperatorAfter op = (OperatorRegistar.UnaryOperatorAfter)curtoken.Operator;
                            // allow for chaining of unary after operators with same precedence:

                            // skip evaluation if the target of execution Is null.
                            // and execute after the other unary after operator has filled in the target
                            if (curtoken.Object == null && opid < tokens.OperatorCount - 1)
                            {
                                if (tokens.OperatorAt(opid + 1).Precedence == cur_precedence)
                                    continue;
                            }
                            // if we're not passing by reference then "dereference" the references before passing
                            if (!op.ByReference && (prevtoken.Object != null))
                            {
                                prevtoken.Object = prevtoken.Object.GetDeepCopy();
                                if (prevtoken.Object is Reference)
                                {
                                    prevtoken.Object = ((Reference)prevtoken.Object).ResolveObj();
                                }
                            }

                            try
                            {
                                result = op.Execute(curtoken.Object);
                                if (SystemMessage.IsType(result) &&
                                    ((SystemMessage)result).Type == SystemMessage.MessageType.defer) {
                                    continue;
                                }
                            }
                            catch (NullReferenceException)
                            {
                                throw new EvaluatorException("Operator " + op.Signs[0] + " disallows empty operands");
                            }

                            tokens.SetObject(opid, result);

                            if (tokens.ObjectAt(opid - 1) == null || (tokens.ObjectAt(opid - 1).GetValue() is double && double.IsNaN((double)(tokens.ObjectAt(opid - 1).GetValue()))))
                            {
                                tokens.SetObject(opid - 1, result);
                                tokens.RemoveAt(opid);
                            }
                            else
                            {
                                tokens.SetOperator(opid, OperatorRegistar.DefaultOperator);
                                // default to multiply
                            }

                        }
                        else // if binary
                        {
                            OperatorRegistar.BinaryOperator op = (OperatorRegistar.BinaryOperator)curtoken.Operator;

                            if (curtoken.Object == null && opid < tokens.OperatorCount - 1)
                            {
                                // allow for chaining of binary operators with same precedence:
                                // defer evaluation until next pass if the right side Is null.
                                if (tokens[opid + 1].Operator.Precedence == cur_precedence)
                                {
                                    // for same precedence, just continue
                                    continue;
                                }
                                else
                                {
                                    // for different precedence, we'll have to evaluate separately and join
                                    tokens.SetObject(opid, DetectType(EvalExprRaw(tokens.OperatorAt(opid + 1).Signs[0].ToString() + tokens.ObjectAt(opid + 1).ToString(), true)));
                                    tokens.RemoveAt(opid + 1);
                                    prevct += 1;
                                    // cheat: continue even though we didn't make progress
                                    continue;
                                }
                            }

                            // if we're not passing by reference then "dereference" the references before passing
                            if (!op.ByReference)
                            {
                                if ((prevtoken.Object != null))
                                {
                                    if (prevtoken.Object is Reference)
                                    {
                                        prevtoken.Object = ((Reference)prevtoken.Object).ResolveObj();
                                    }
                                    prevtoken.Object = prevtoken.Object.GetDeepCopy();
                                }
                                if ((curtoken.Object != null))
                                {
                                    if (curtoken.Object is Reference)
                                    {
                                        curtoken.Object = ((Reference)curtoken.Object).ResolveObj();
                                    }
                                    curtoken.Object = curtoken.Object.GetDeepCopy();
                                }
                            }

                            try
                            {
                                result = op.Execute(prevtoken.Object, curtoken.Object);
                                if (SystemMessage.IsType(result) &&
                                    ((SystemMessage)result).Type == SystemMessage.MessageType.defer) {
                                    tokens.SetOperator(opid, NextOperator(op));
                                    continue;
                                }
                            }
                            catch (NullReferenceException)
                            {
                                throw new EvaluatorException("Operator " + op.Signs[0] + " disallows empty operands");
                            }

                            tokens.SetObject(opid - 1, result);
                            tokens.RemoveAt(opid);
                        }
                    }

                    // if we don't make any progress then stop trying
                    if (prevct <= tokens.OperatorsWithPrecedenceCount(cur_precedence))
                        break;
                }
            }

            return tokens.ObjectAt(tokens.ObjectCount - 1);
        }

        private OperatorRegistar.Operator NextOperator(OperatorRegistar.Operator op)
        {
            IEnumerable<OperatorRegistar.Operator> lst = OperatorRegistar.OperatorsWithSign(op.Signs[0]);
            for (int i=0; i<lst.Count()-1; ++i)
            {
                if (lst.ElementAt(i).Precedence == op.Precedence)
                {
                    return lst.ElementAt(i + 1);
                }
            }
            return null;
        }

        /// <summary>
        /// Parse the expression into a TokenList object, which can then be used to evaluate the expression
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        private TokenList Tokenize(string expr)
        {
            TokenList lst = new TokenList();

            // beginning has no operator
            lst.AddOperator(null);

            int idx = 0;

            for (int i = 0; i <= expr.Length - 1; i++)
            {
                for (int j = Math.Min(expr.Length, i + OperatorRegistar.MAX_OPERATOR_LENGTH); j >= i; j += -1)
                {
                    string valueL = expr.Substring(i, j - i).Replace("  ", " ").ToLowerInvariant();

                    if (OperatorRegistar.OperatorExists(valueL))
                    {
                        IEnumerable<OperatorRegistar.Operator> ops = OperatorRegistar.OperatorsWithSign(valueL);
                        string objstr = expr.Substring(idx, i - idx).Trim();
                        EvalObjectBase eo = null;

                        // if the object is not empty we try to detect its type
                        if (!string.IsNullOrEmpty(objstr))
                            eo = ObjectTypes.Parse(objstr, this, numberPreserveSigFigs: SignificantMode);
                        foreach (OperatorRegistar.Operator op in ops)
                        {
                            // if the object is not empty
                            if (!string.IsNullOrEmpty(objstr))
                            {

                                // we already detected the type of the object earlier

                                // if the object is an identifier, we try to resolve it.


                                if (ObjectTypes.Identifier.IsType(eo))
                                {
                                    List<EvalObjectBase> varlist = null;
                                    EvalObjectBase left = null;

                                    // this ends with a function, so try resolving the function
                                    if (valueL == "(")
                                    {

                                        string funcargs = expr.Substring(j);
                                        if (funcargs.Contains(")"))
                                        {
                                            int endIdx = ((OperatorRegistar.Bracket)OperatorRegistar.OperatorWithSign("(")).FindCloseBracket(funcargs, OperatorRegistar);
                                            if (endIdx < funcargs.Length)
                                            {
                                                funcargs = funcargs.Remove(endIdx);
                                            }
                                        }
                                        else
                                        {
                                            throw new EvaluatorException("(: No close bracket found");
                                        }

                                        if (lst.ObjectCount > 0 && lst.OperatorCount >= lst.ObjectCount && eo.ToString().Trim().StartsWith(SCOPE_SEP.ToString()))
                                        {
                                            left = lst.ObjectAt(lst.ObjectCount - 1);
                                        }
                                        varlist = ResolveFunctions(eo.ToString(), funcargs, ref left);

                                        // advance past this function
                                        idx = j + funcargs.Count() + 1;
                                        i = idx - 1;

                                        // this consists of variables only, so only resolve variables / function pointers
                                    }
                                    else
                                    {
                                        if (op.AssignmentOperator)
                                        {
                                            // for assignment operators, do not resolve the variables
                                            varlist = new List<EvalObjectBase>(new[] { GetVariableRef(eo.ToString()) });
                                        }
                                        else
                                        {
                                            // try resolving a function pointer

                                            varlist = new List<EvalObjectBase>();
                                            string fn = eo.ToString();
                                            if (HasUserFunction(fn))
                                            {
                                                varlist.Add(new Lambda(fn, GetUserFunction(fn).Args, true));
                                            }
                                            else if (HasFunction(fn))
                                            {
                                                varlist.Clear();
                                                if (fn.StartsWith(ROOT_NAMESPACE))
                                                    fn = fn.Remove(ROOT_NAMESPACE.Length).Trim(new[] { SCOPE_SEP });
                                                MethodInfo info = typeof(InternalFunctions).GetMethod(fn.ToLowerInvariant(), BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                                                varlist.Add(new Lambda(fn, (from param in info.GetParameters() select param.Name), true));
                                            }
                                            else
                                            {
                                                if (lst.ObjectCount > 0 && (lst.ObjectAt(lst.ObjectCount - 1) != null) && eo.ToString().StartsWith(SCOPE_SEP.ToString()))
                                                {
                                                    var obj = lst.ObjectAt(lst.ObjectCount - 1);
                                                    varlist = ResolveFunctions(eo.ToString(), "", ref obj);
                                                }
                                            }
                                            if (varlist.Count == 0)
                                            {
                                                varlist = ResolveVariables(eo.ToString());
                                            }
                                            else
                                            {
                                                // advance past this function
                                                idx = j + 1;
                                                i = idx - 1;
                                            }
                                        }
                                    }

                                    // good we found variables/functions, let's add them
                                    if (varlist.Count > 0)
                                    {

                                        if (left == null)
                                        {
                                            lst.AddObject(varlist[0]);
                                            // if this is a self-referring function call then we need to replace the previous value
                                        }
                                        else
                                        {
                                            lst.SetObject(lst.ObjectCount - 1, varlist[0]);
                                        }

                                        for (int k = 1; k <= varlist.Count - 1; k++)
                                        {
                                            // default operation is *; we add this operator between each variable

                                            lst.AddOperator(OperatorRegistar.DefaultOperator);
                                            //End If
                                            lst.AddObject(varlist[k]);
                                        }

                                        // we couldn't resolve this identifier
                                    }
                                    else
                                    {
                                        lst.AddObject(null);
                                    }
                                    // if it was a function then we don't continue since we're already
                                    // done adding And advancing the counters
                                    if (valueL == ("("))
                                        break;

                                    // if the object is not a identifier (if it is a number, etc.) we just add it
                                }
                                else
                                {
                                    lst.AddObject(eo);
                                }

                                // if the object is empty we just add the operator
                            }
                            else
                            {
                                // if the operator count is too high we will add a null object to balance it
                                if (lst.OperatorCount - lst.ObjectCount >= 1)
                                    lst.AddObject(null);
                                //'
                            }

                            if (!(op is OperatorRegistar.Bracket))
                                lst.AddOperator(op, valueL);

                            // if we find an operator with brackets type
                            // we evaluate the bracket and continue after it.
                            // If the value before is an identifier we recognize it as a function so we skip this

                            if (op is OperatorRegistar.Bracket && (op.Signs[0] != "( " || eo == null || !ObjectTypes.Identifier.IsType(eo)))
                            {
                                string inner = expr.Substring(j);
                                int endIdx = 0;

                                if (op.Signs.Count > 0)
                                {
                                    endIdx = ((OperatorRegistar.Bracket)op).FindCloseBracket(inner, OperatorRegistar);
                                }
                                else
                                {
                                    throw new EvaluatorException("Invalid bracket operator: must have at least 1 sign");
                                }

                                if (endIdx >= 0)
                                {
                                    if (endIdx < inner.Length)
                                        inner = inner.Remove(endIdx);

                                    OperatorRegistar.Bracket brkt = (OperatorRegistar.Bracket)op;
                                    EvalObjectBase left = null;
                                    EvalObjectBase orig = null;

                                    if (lst.ObjectCount > 0)
                                    {
                                        left = lst.ObjectAt(lst.ObjectCount - 1);
                                        // if we're not passing by reference then "dereference" the references before passing
                                        if (!brkt.ByReference && (left != null))
                                        {
                                            left = left.GetDeepCopy();
                                            if (left is Reference)
                                            {
                                                left = ((Reference)left).GetRefObject();
                                            }
                                        }
                                        orig = left;
                                    }
                                    EvalObjectBase result = brkt.Execute(inner, ref left);

                                    if (left == null)
                                    {
                                        lst.SetObject(lst.ObjectCount - 1, result);
                                    }
                                    else
                                    {
                                        if ((!object.ReferenceEquals(left, orig)))
                                        {
                                            lst.SetObject(lst.ObjectCount - 1, left);
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(valueL.Trim()))
                                                lst.AddOperator(OperatorRegistar.DefaultOperator);
                                            lst.AddObject(result);
                                            if (lst.ObjectCount > lst.OperatorCount)
                                                lst.AddOperator(OperatorRegistar.DefaultOperator);
                                        }
                                    }

                                    //advance the counters past the entire bracket set
                                    i += brkt.Signs[0].Length - 1 + inner.Length + (brkt.Signs.Count == 1 ? brkt.Signs[0].Length : brkt.Signs[1].Length);

                                    idx = i + 1;

                                    break;
                                }
                                else
                                {
                                    throw new EvaluatorException(op.Signs[0] + ": No close bracket found");
                                }
                            }

                            // advance the counters past this identifier
                            idx = j;
                            i = j - 1;
                            break;
                        }
                    }
                }
            }

            // add remaining bit at the end
            if (idx < expr.Length && !string.IsNullOrEmpty(expr.Substring(idx, expr.Length - idx).Trim()))
            {
                EvalObjectBase eo = ObjectTypes.Parse(expr.Substring(idx, expr.Length - idx).Trim(), numberPreserveSigFigs: SignificantMode);

                // if the object we get is an identifier, we try to break it into variables which are resolved using ResolveVariables
                if (ObjectTypes.Identifier.IsType(eo))
                {
                    List<EvalObjectBase> varlist = new List<EvalObjectBase>();

                    // try resolving a function pointer

                    varlist = new List<EvalObjectBase>();
                    string fn = eo.ToString();
                    if (HasUserFunction(fn))
                    {
                        varlist.Add(new Lambda(fn, GetUserFunction(fn).Args, true));
                    }
                    else if (HasFunction(fn))
                    {
                        varlist.Clear();
                        if (fn.StartsWith(ROOT_NAMESPACE))
                            fn = fn.Remove(ROOT_NAMESPACE.Length).Trim(new[] { SCOPE_SEP });
                        MethodInfo info = typeof(InternalFunctions).GetMethod(fn.ToLowerInvariant(), BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                        varlist.Add(new Lambda(fn, (from param in info.GetParameters() select param.Name), true));
                    }
                    else
                    {
                        if (lst.ObjectCount > 0 && (lst.ObjectAt(lst.ObjectCount - 1) != null) && eo.ToString().StartsWith(SCOPE_SEP.ToString()))
                        {
                            var obj = lst.ObjectAt(lst.ObjectCount - 1);
                            varlist = ResolveFunctions(eo.ToString(), "", ref obj);
                        }
                    }

                    if (varlist.Count == 0)
                    {
                        varlist = ResolveVariables(eo.ToString());
                    }
                    if (varlist.Count > 0)
                    {
                        lst.AddObject(varlist[0]);
                        for (int k = 1; k <= varlist.Count - 1; k++)
                        {
                            if (lst.OperatorCount <= lst.ObjectCount)
                            {
                                // default operation is *
                                lst.AddOperator(OperatorRegistar.DefaultOperator, "*");
                            }
                            lst.AddObject(varlist[k]);
                        }
                    }
                    else
                    {
                        lst.AddObject(null);
                    }
                    // otherwise we just add it
                }
                else
                {

                    lst.AddObject(eo);
                    while (lst.OperatorCount < lst.ObjectCount)
                    {
                        lst.AddOperator(OperatorRegistar.DefaultOperator, "*");
                    }
                }
            }

            lst.BalanceLists();

            return lst;
        }

        /// <summary>
        /// Tries to break a string into a function on the right and valid variables and constants on the left
        /// For example: xsin(x) should be x*sin(x), a variable times a function
        /// Note: Prioritizes longer functions and variables so if both hello() and he() llo() are defined hello() will be used
        /// </summary>
        /// <param name="str">The identifier string to parse</param>
        /// <param name="args">The string containing the function arguments</param>
        /// <param name="left">The object to the left of the function, needed to resolve self-referring function calls</param>
        /// <returns></returns>
        private List<EvalObjectBase> ResolveFunctions(string str, string args, ref EvalObjectBase left)
        {
            int min = 0;
            int max = str.Length - 1;

            EvalObjectBase baseObj = null;

            // deal with self-referring (.) notation
            if (str.Contains(SCOPE_SEP) && !HasFunction(str))
            {
                string baseTxt = str.Remove(str.IndexOf(SCOPE_SEP));
                if (max == min && baseTxt.Length + 1 != min)
                {
                    throw new EvaluatorException("Member function is undefined");
                }

                str = str.Substring(str.IndexOf(SCOPE_SEP) + 1);
                if (!string.IsNullOrEmpty(baseTxt))
                {
                    try
                    {
                        baseObj = GetVariableRef(baseTxt);
                    }
                    catch
                    {
                    }
                    Reference br = (Reference)baseObj;
                    try
                    {
                        if (baseObj == null || (br.Resolve() is double && double.IsNaN((double)(br.Resolve())) || br.Resolve() is BigDecimal && ((BigDecimal)br.Resolve()).IsUndefined))
                        {
                            baseObj = Parse(baseTxt, this, true, numberPreserveSigFigs: SignificantMode);
                            br = new Reference(baseObj);
                        }
                    }
                    catch
                    {
                    }

                    if (baseObj == null || (br.Resolve() is double && double.IsNaN((double)(br.Resolve())) || br.Resolve() is BigDecimal && ((BigDecimal)br.Resolve()).IsUndefined))
                    {
                        str = baseTxt + SCOPE_SEP + str;
                        // try full name
                        baseObj = null;
                    }
                    else
                    {
                        min = 0;
                        max = 0;
                    }
                }
                else
                {
                    if (left == null)
                    {
                        baseObj = GetDefaultVariableRef();
                        if (baseObj == null)
                        {
                            if (baseObj == null)
                                str = baseTxt + SCOPE_SEP + str;
                            // try full name
                        }
                        else
                        {
                            min = 0;
                            max = 0;
                        }
                    }
                    else
                    {
                        baseObj = left;
                    }
                }

            }
            else
            {
                left = null;
            }

            List<object> argLst = new List<object>();
            Dictionary<string, object> optDict = new Dictionary<string, object>();
            if ((baseObj != null))
            {
                // if a tuple is used, supplies multiple parameters
                if (baseObj is ObjectTypes.Tuple)
                {
                    foreach (Reference r in (Reference[])((ObjectTypes.Tuple)baseObj).GetValue())
                    {
                        if (r.GetRefObject() is Reference)
                        {
                            argLst.Add(r);
                        }
                        else if (r.GetRefObject() is Number)
                        {
                            argLst.Add(((Number)r.GetRefObject()).BigDecValue());
                        }
                        else
                        {
                            argLst.Add(r.GetValue());
                        }
                    }
                }
                else if (baseObj is Reference)
                {
                    if (((Reference)baseObj).GetRefObject() is Reference)
                    {
                        argLst.Add(baseObj);
                    }
                    else if (((Reference)baseObj).GetRefObject() is Number)
                    {
                        argLst.Add(((Number)((Reference)baseObj).GetRefObject()).BigDecValue());
                    }
                    else
                    {
                        argLst.Add(((Reference)baseObj).GetValue());
                    }
                }
                else if (baseObj is Number)
                {
                    argLst.Add(((Number)baseObj).BigDecValue());
                }
                else
                {
                    argLst.Add(baseObj.GetValue());
                }
            }

            int lastIdx = 0;
            if (!string.IsNullOrWhiteSpace(args))
            {
                List<Reference> tuple = new List<Reference>();

                for (int i = 0; i <= args.Length; i++)
                {
                    char c = ',';
                    if (i < args.Length)
                    {
                        c = args[i];
                        if (OperatorRegistar.OperatorExists(c.ToString()))
                        {
                            OperatorRegistar.Operator op = OperatorRegistar.OperatorWithSign(c.ToString());
                            if (op is OperatorRegistar.Bracket && ((OperatorRegistar.Bracket)op).OpenBracket == c.ToString())
                            {
                                i += ((OperatorRegistar.Bracket)op).FindCloseBracket(args.Substring(i + 1), OperatorRegistar) + ((OperatorRegistar.Bracket)op).CloseBracket.Length;
                                continue;
                            }
                        }
                    }

                    if (c == ',')
                    {
                        string lastSect = args.Substring(lastIdx, i - lastIdx);
                        lastIdx = i + 1;
                        string optVar = "";
                        if (lastSect.Contains(":=") && IsValidIdentifier(lastSect.Remove(lastSect.IndexOf(":="))))
                        {
                            optVar = lastSect.Remove(lastSect.IndexOf(":="));
                            if (lastSect.IndexOf(":=") + 1 == lastSect.Length)
                            {
                                lastSect = "";
                            }
                            else
                            {
                                lastSect = lastSect.Substring(lastSect.IndexOf(":=") + 1);
                            }
                        }

                        object resObj = EvalExprRaw(lastSect, true, true);
                        if (!string.IsNullOrEmpty(optVar))
                        {
                            optDict[optVar] = resObj;
                            // cannot have normal arguments after named ones
                        }
                        else if (optDict.Count > 0)
                        {
                            throw new SyntaxException("Unnamed parameters must precede all named parameters");
                        }
                        else
                        {
                            tuple.Add(new Reference(resObj));
                        }
                    }
                }

                foreach (Reference @ref in tuple)
                {
                    if (@ref == null)
                    {
                        argLst.Add(double.NaN);
                    }
                    else
                    {
                        if (@ref.GetRefObject() is Reference)
                        {
                            argLst.Add(@ref);
                        }
                        else if (@ref.GetRefObject() is Number)
                        {
                            argLst.Add(((Number)@ref.GetRefObject()).BigDecValue());
                        }
                        else
                        {
                            argLst.Add(@ref.GetValue());
                        }
                    }
                }
            }

            // loop through string from left to right and look for functions on right
            for (int i = min; i <= max; i++)
            {
                if (i >= str.Length)
                    break;
                string fn = str.Substring(i).Trim();
                string varstr = str.Remove(i);

                List<EvalObjectBase> lst = null;
                try
                {
                    lst = ResolveVariables(varstr);
                }
                catch
                {
                    break;
                }

                // for class instances, try looking for members
                if ((baseObj != null) && baseObj.GetValue() is ClassInstance)
                {
                    ClassInstance ci = (ClassInstance)baseObj.GetValue();
                    Reference @ref = ci.ResolveField(fn, Scope);

                    if (@ref.ResolveObj() is Lambda)
                    {
                        // remove the first item in the arguments, which is set to the lambda expression itself
                        argLst.RemoveAt(0);
                        Lambda lambda = (Lambda)@ref.ResolveObj();

                        if (lambda.Args.Count() != argLst.Count)
                        {
                            // incorrect parameter count
                            throw new EvaluatorException(ci.UserClass.Name + SCOPE_SEP + fn + ": " + lambda.Args.Count() + " parameter(s) expected");
                        }
                        else
                        {
                            // execute
                            CantusEvaluator tmpEvaluator = ci.UserClass.Evaluator.SubEvaluator();
                            tmpEvaluator.Scope = ci.InnerScope;
                            tmpEvaluator.SubScope();
                            tmpEvaluator.SetDefaultVariable(new Reference(ci));
                            lst.Add(DetectType(lambda.Execute(tmpEvaluator, argLst, tmpEvaluator.Scope), true));
                        }
                        return lst;
                    }
                    else
                    {
                        if (@ref.GetRefObject() is Reference)
                        {
                            lst.Add(@ref);
                        }
                        else
                        {
                            lst.Add(@ref.ResolveObj());
                        }
                        return lst;
                    }
                    // user class constructors
                }
                else if (HasUserClass(fn))
                {

                    UserClass uc = GetUserClass(fn);
                    if (uc.Constructor.Args.Count() > 0 && argLst.Count == 0)
                    {
                        lst.Add(new ClassInstance(uc));
                        // support creating empty objects without running constructor
                    }
                    else
                    {
                        lst.Add(new ClassInstance(uc, argLst));
                    }
                    return lst;

                    // user functions
                }
                else if (HasUserFunction(fn))
                {

                    object execResult = ExecUserFunction(fn, argLst, optDict);
                    lst.Add(DetectType(execResult, true));
                    return lst;

                }
                else if (HasVariable(fn) && Lambda.IsType(GetVariableRef(fn).ResolveObj()))
                {
                    // lambda expression/function pointer

                    Lambda lambda = (Lambda)GetVariableRef(fn).ResolveObj();
                    if (lambda.Args.Count() != argLst.Count())
                    {
                        throw new EvaluatorException(fn + ": " + lambda.Args.Count() + " parameter(s) expected" + ((baseObj != null) ? "(self-referring resolution on)" : ""));
                    }
                    else
                    {
                        lst.Add(DetectType(lambda.Execute(this, argLst), true));
                    }
                    return lst;

                    // internal functions defined in EvalFunctions
                }
                else if (HasFunction(fn))
                {
                    lst.Add(DetectType(ExecInternalFunction(fn, argLst), true));
                    return lst;
                }
            }

            throw new EvaluatorException("Function \"" + str.Trim() + "\" is undefined");
        }

        /// <summary>
        /// Tries to break the string into valid variables and constants
        /// i.e. the string abc can either represent the variable abc or a * b * c
        /// And then resolves the variables
        /// Note: Prioritizes longer variables so if both xy and x y are defined xy will be used
        /// </summary>
        /// <param name="str">The string to parse</param>
        /// <returns></returns>
        private List<EvalObjectBase> ResolveVariables(string str)
        {
            List<EvalObjectBase> ret = new List<EvalObjectBase>();

            int i = 0;

            while (i < str.Length)
            {
                bool done = false;
                for (int j = str.Length - i; j >= 1; j--)
                {
                    string cur = str.Substring(i, j);
                    if (Number.StrIsType(cur))
                    {
                        ret.Add(new Number(cur));
                        i += j - 1;
                        break;
                    }
                    else
                    {
                        try
                        {
                            ret.Add(GetVariableRef(cur, true));
                            i += j - 1;
                            break;
                        }
                        catch (Exception)
                        {
                            // really can't find anything
                            if (j == 1)
                            {
                                if (ExplicitMode)
                                {
                                    throw new EvaluatorException("Variable " + str + " is undefined. (Explicit mode disallows implicit declaration)");
                                }
                                else
                                {
                                    ret.Clear();
                                    SetVariable(str, double.NaN);
                                    ret.Add(GetVariableRef(str));
                                    done = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (done) break;
                i += 1;
            }
            return ret;
        }

        private HashSet<UserClass> _visClass;
        /// <summary>
        /// Serialize a user class in a way that preserves inheritance
        /// </summary>
        /// <returns></returns>
        private StringBuilder SerializeRelatedClasses(UserClass uc)
        {
            StringBuilder result = new StringBuilder();

            _visClass.Add(uc);

            foreach (string b in uc.BaseClasses)
            {
                if (!UserClasses.ContainsKey(b))
                    continue;
                UserClass baseUC = UserClasses[b];
                if (_visClass.Contains(baseUC))
                    continue;
                result.Append(SerializeRelatedClasses(baseUC));
            }

            result.AppendLine(uc.ToString(Scope));
            return result;
        }

        /// <summary>
        /// Convert the evaluator's user functions, variables, and configuration into a
        ///  script that can be ran again for storage
        /// </summary>
        public string ToScript()
        {
            try
            {
                StringBuilder serialized = new StringBuilder();
                serialized.AppendLine("# Cantus " + Internals.Ver() + " auto-generated initialization script");
                serialized.AppendLine("# Use caution when modifying manually").Append(Environment.NewLine);
                serialized.AppendLine("# Modes");

                serialized.Append("_output(").Append('\'').Append(OutputMode.ToString()).Append('\'').Append(")").Append(Environment.NewLine);
                serialized.Append("_anglerepr(").Append('\'').Append(AngleMode.ToString()).Append('\'').Append(")").Append(Environment.NewLine);
                serialized.Append("_spacespertab(").Append(SpacesPerTab.ToString()).Append(")").Append(Environment.NewLine);
                serialized.Append("_sigfigs(").Append(SignificantMode.ToString()).Append(")").Append(Environment.NewLine);

                serialized.AppendLine().AppendLine("# Class Definitions");
                _visClass = new HashSet<UserClass>();

                foreach (KeyValuePair<string, UserClass> uc in UserClasses)
                {
                    // Do not output if it is from an external scope -- it is already saved there somewhere
                    if (!_visClass.Contains(uc.Value) && !IsExternalScope(uc.Value.DeclaringScope, _scope) && !uc.Value.Modifiers.Contains("internal"))
                    {
                        serialized.Append(SerializeRelatedClasses(uc.Value));
                    }
                }

                serialized.AppendLine().AppendLine("# Function Definitions");
                foreach (KeyValuePair<string, UserFunction> func in UserFunctions)
                {
                    // Do not output if it is from an external scope -- it is already saved there somewhere.
                    // Also do not save 'internal' variables and functions - they are already saved with classes
                    if (!IsExternalScope(func.Value.DeclaringScope, _scope) && !func.Value.Modifiers.Contains("internal"))
                    {
                        serialized.AppendLine(func.Value.ToString(_scope));
                    }
                }

                serialized.AppendLine().AppendLine("# Variable Definitions");
                //
                foreach (KeyValuePair<string, Variable> var in Variables.ToArray())
                {
                    EvalObjectBase def = var.Value.Reference.ResolveObj();


                    if ((def != null) && (!(def is Number) ||
                        !double.IsNaN((double)(def.GetValue()))) && !(def is Reference) &&
                        !var.Value.Modifiers.Contains("internal") && !(var.Key == DEFAULT_VAR_NAME) &&
                        !(IsExternalScope(var.Value.DeclaringScope, _scope)))
                    {
                        string defs = null;

                        string fullName = RemoveRedundantScope(var.Key, _scope);

                        // special treatment for class instances
                        if (def is ClassInstance)
                        {
                            ClassInstance ci = (ClassInstance)def;
                            StringBuilder sb = new StringBuilder();
                            sb.Append(ci.UserClass.Name);
                            sb.AppendLine("()");
                            foreach (string f in ci.Fields.Keys)
                            {
                                sb.Append(fullName).Append(SCOPE_SEP).Append(f).Append(" = ").AppendLine(ci.Fields[f].ToString());
                            }
                            defs = sb.ToString();
                        }
                        else
                        {
                            defs = def.ToString();
                        }

                        serialized.Append(fullName).Append("=").AppendLine(defs);
                    }
                }


                if (ExplicitMode)
                {
                    serialized.AppendLine().AppendLine("# Explicit mode switch");
                    serialized.Append("_explicit(").Append(ExplicitMode.ToString()).Append(")").Append(Environment.NewLine);
                }

                serialized.AppendLine().AppendLine("# End of Cantus auto-generated initialization script. DO NOT modify this comment.");
                serialized.AppendLine("# You may write additional initialization code below this line.");

                return serialized.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: Failed to serial state as Cantus script. User data may be lost. Details:" + Environment.NewLine + ex.Message);
                return "";
            }
        }

        /// <summary>
        /// Given a system type, returns the name of the equivalent type used inside Cantus
        /// </summary>
        public static string GetEvaluatorTypeName(Type type)
        {
            if (type == typeof(string))
            {
                return "Text";
            }
            if (type == typeof(double))
            {
                return "Number";
            }
            if (type == typeof(BigDecimal))
            {
                return "Number";
            }
            if (type == typeof(SortedDictionary<Reference, Reference>))
            {
                return "Set";
            }
            if (type == typeof(Dictionary<Reference, Reference>))
            {
                return "HashSet";
            }
            if (type == typeof(List<Reference>) || type == typeof(IEnumerable<Reference>) || type == typeof(IList<Reference>))
            {
                return "Matrix";
            }
            if (type == typeof(LinkedList<Reference>))
            {
                return "LinkedList";
            }
            if (type == typeof(Reference[]))
            {
                return "Tuple";
            }
            if (type == typeof(Reference))
            {
                return "Reference";
            }
            if (type == typeof(Lambda))
            {

                return "Function";
            }
            if (type == typeof(ICollection<Reference>))
            {
                return "(Matrix, Set, HashSet, Tuple, LinkedList)";
            }
            if (type == typeof(IEnumerable<Reference>))
            {
            }
            if (type == typeof(IList<Reference>))
            {
                return "(Matrix, LinkedList)";
            }
            if (type == typeof(IDictionary<Reference, Reference>))
            {

                return "(Set, HashSet)";
            }
            if (type == typeof(object))
            {
                return "(Variable)";
            }
            else
            {
                return type.Name;
            }
        }

        /// <summary>
        /// Create a copy of the evaluator containing the same user functions, variables,
        /// and functions starting at the current line, at a subscope below the current evaluator
        /// </summary>
        /// <returns></returns>
        internal CantusEvaluator SubEvaluator(int lineNumber = -1, string subScopeName = "")
        {
            if (lineNumber < 0)
                lineNumber = _curLine;

            Dictionary<string, Variable> varsCopy = new Dictionary<string, Variable>();

            KeyValuePair<string, Variable>[] tmp = this.Variables.ToArray();
            for (int i = 0; i <= tmp.Count() - 1; i++)
            {
                if (tmp[i].Value.Reference.Resolve() is double && double.IsNaN((double)(tmp[i].Value.Reference.Resolve())))
                    continue;
                // skip undefined vars
                varsCopy[tmp[i].Key] = tmp[i].Value;
            }

            Dictionary<string, UserFunction> funcCopy = new Dictionary<string, UserFunction>(this.UserFunctions);
            Dictionary<string, UserClass> classesCopy = new Dictionary<string, UserClass>(this.UserClasses);

            // if no scope name is given then give it the next anonymous scope
            if (string.IsNullOrEmpty(subScopeName))
                subScopeName = GetAnonymousSubscope();

            CantusEvaluator res =
                new CantusEvaluator(this.OutputMode, this.AngleMode, this.SpacesPerTab,
                this.ExplicitMode, this.SignificantMode, this.PrevAns, varsCopy, funcCopy, classesCopy,
                lineNumber,
                _scope + SCOPE_SEP + subScopeName, false, this);
            foreach (string import in GetAllAccessibleScopes())
            {
                res.Import(import);
            }

            return res;
        }

        /// <summary>
        /// Create an identical copy of the evaluator containing deep copies of the same user functions, variables, and functions
        /// </summary>
        /// <returns></returns>
        public CantusEvaluator DeepCopy(string scopeName = "")
        {
            Dictionary<string, Variable> varsCopy = new Dictionary<string, Variable>();
            try
            {
                foreach (KeyValuePair<string, Variable> k in Variables.ToArray())
                {
                    if (k.Value.Reference.Resolve() is double && double.IsNaN((double)(k.Value.Reference.Resolve())))
                        continue;
                    // skip undefined vars
                    varsCopy.Add(k.Key, new Variable(k.Value.Name, (Reference)k.Value.Reference.GetDeepCopy(), k.Value.DeclaringScope));
                }
            }
            catch (Exception)
            {
            }

            Dictionary<string, UserFunction> funcCopy = new Dictionary<string, UserFunction>(this.UserFunctions);
            Dictionary<string, UserClass> classesCopy = new Dictionary<string, UserClass>(this.UserClasses);

            CantusEvaluator res = new CantusEvaluator(this.OutputMode, this.AngleMode, this.SpacesPerTab, this.ExplicitMode,
                this.SignificantMode, this.PrevAns, varsCopy, funcCopy, classesCopy, this._baseLine,
                string.IsNullOrEmpty(scopeName) ? this._scope : scopeName, false);

            if (Internals.RequestClearConsoleHandler != null) res.ClearConsole += Internals.RequestClearConsoleHandler;
            if (Internals.ReadInputHandler != null) res.ReadInput += Internals.ReadInputHandler;
            if (Internals.WriteOutputHandler != null) res.WriteOutput += Internals.WriteOutputHandler;

            foreach (string import in GetAllAccessibleScopes())
            {
                res.Import(import);
            }
            return res;
        }

        /// <summary>
        /// Create an identical copy of the evaluator containing references to the same user functions, variables, and functions
        /// </summary>
        /// <returns></returns>
        internal CantusEvaluator ShallowCopy(string scopeName = "")
        {
            Dictionary<string, Variable> varsCopy = new Dictionary<string, Variable>();
            try
            {
                foreach (KeyValuePair<string, Variable> k in Variables.ToArray())
                {
                    if (k.Value.Reference.Resolve() is double && double.IsNaN((double)(k.Value.Reference.Resolve())))
                        continue;
                    // skip undefined vars
                    varsCopy.Add(k.Key, new Variable(k.Value.Name, k.Value.Reference, k.Value.DeclaringScope));
                }
            }
            catch
            {
            }

            Dictionary<string, UserFunction> funcCopy = new Dictionary<string, UserFunction>(this.UserFunctions);
            Dictionary<string, UserClass> classesCopy = new Dictionary<string, UserClass>(this.UserClasses);

            CantusEvaluator res = new CantusEvaluator(this.OutputMode, this.AngleMode, this.SpacesPerTab, this.ExplicitMode, this.SignificantMode, this.PrevAns, varsCopy, funcCopy, classesCopy, this._baseLine,
            string.IsNullOrEmpty(scopeName) ? this._scope : scopeName, false);
            foreach (string import in GetAllAccessibleScopes())
            {
                res.Import(import);
            }
            return res;
        }

        /// <summary>
        /// Stop all threads, optionally sparing the thread marked as haveMercy
        /// </summary>
        public void StopAll(int haveMercy = -1)
        {
            this.StatementRegistar.StopAll();
            this.ThreadController.KillAll(haveMercy);
        }

        /// <summary>
        /// Cleans up threads spawned by this evaluator. Unneeded if no threads spawned.
        /// </summary>
        public void Dispose()
        {
            try
            {
                StopAll();
                this.StatementRegistar.Dispose();
            }
            catch { }
        }
        #endregion

        #region "Scoping"

        /// <summary>
        /// Move this evaluator down into a subscope of the current scope. 
        /// If no subscope is specified, this will use an anonymous subscope name.
        /// </summary>
        internal void SubScope(string subScopeName = "")
        {
            if (string.IsNullOrEmpty(subScopeName))
                subScopeName = GetAnonymousSubscope();
            _scope += SCOPE_SEP + subScopeName;
        }

        /// <summary>
        /// Move this evaluator up to the parent scope
        /// </summary>
        internal void ParentScope()
        {
            if (_scope.Contains(SCOPE_SEP))
                _scope = _scope.Remove(_scope.LastIndexOf(SCOPE_SEP));
        }

        /// <summary>
        /// Get an anonymous subscope name
        /// </summary>
        private string GetAnonymousSubscope()
        {
            _anonymousScopeID += 1;

            if (_anonymousScopeID == Int32.MaxValue)
                _anonymousScopeID = 0;
            return "__anonymous_" + (_anonymousScopeID - 1).ToString();
        }
        /// <summary>
        /// Get a list of scopes accessible from the current scope
        /// </summary>
        /// <returns></returns>
        internal List<string> GetAllAccessibleScopes()
        {
            List<string> checkScopeLst = new List<string>();
            checkScopeLst.Add(Scope);
            checkScopeLst.AddRange(GetParentScopes(Scope).ToArray());
            checkScopeLst.AddRange(Imported.ToArray());
            return checkScopeLst;
        }

        #endregion

        #region "User Data: Evaluator Variables, Functions, Classes, Past Answers"
        /// <summary>
        /// Get the value of the variable with the name specified as an IEvalObject
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="explicit">If true, simulates explicit mode even when not set on the evaluator</param>
        /// <returns></returns>
        internal Reference GetVariableRef(string name, bool @explicit = false)
        {
            if (name == "ans")
                return new Reference(GetLastAns());
            string scope = _scope;
            name = RemoveRedundantScope(name, scope);

            if (Variables.ContainsKey(name))
                return Variables[name].Reference;

            foreach (string sc in GetAllAccessibleScopes())
            {
                string s = sc;
                string temp = name;
                while (temp.Contains(SCOPE_SEP))
                {
                    temp = temp.Remove(temp.LastIndexOf(SCOPE_SEP));
                    if (HasVariable(s + SCOPE_SEP + temp))
                    {
                        Reference v = Variables[s + SCOPE_SEP + temp].Reference;
                        if (v.ResolveObj() is ClassInstance)
                        {
                            return ((ClassInstance)v.ResolveObj()).ResolveField(name.Substring(temp.Length + 1), scope);
                        }
                    }
                }

                temp = name;
                NormalizeScope(ref temp, ref s);

                if (Variables.ContainsKey(s + SCOPE_SEP + temp))
                {
                    // ignore if private
                    if (!IsParentScopeOf(s, scope) && Variables[s + SCOPE_SEP + temp].Modifiers.Contains("private"))
                        continue;
                    if (scope != s && IsParentScopeOf(s, scope))
                    {
                        SetVariable(temp, Variables[s + SCOPE_SEP + temp].Reference);
                        return Variables[scope + SCOPE_SEP + temp].Reference;
                    }
                    else
                    {
                        return Variables[s + SCOPE_SEP + temp].Reference;
                    }
                }
            }

            // variable not found, implicit declaration?

            // explicit mode: disallow
            if (ExplicitMode || @explicit)
                throw new EvaluatorException("Variable " + name + " is undefined. (Explicit mode disallows implicit declaration)");

            NormalizeScope(ref name, ref scope);

            // classes: disallow any declarations within a class scope (unless specified in the class)
            string tmp = scope;

            while (true)
            {
                if (HasUserClass(tmp))
                {
                    UserClass uc = GetUserClass(scope);
                    string subName = name;
                    if (subName.Contains(SCOPE_SEP))
                        subName = subName.Remove(subName.IndexOf(SCOPE_SEP));
                    if (!uc.AllFields.ContainsKey(subName))
                    {
                        throw new EvaluatorException("Cannot declare variable " + name + " inside class " + UserClasses[scope].Name);
                    }
                }

                if (!tmp.Contains(SCOPE_SEP))
                    break;
                tmp = tmp.Remove(tmp.LastIndexOf(SCOPE_SEP));
            }

            Variable var = new Variable(name, new Reference(double.NaN), scope);
            Variables[var.FullName] = var;

            return Variables[scope + SCOPE_SEP + name].Reference;
        }

        /// <summary>
        /// Get the string representation of the variable with the name specified
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="explicit">If true, simulates explicit mode even when not set on the evaluator</param>

        public string GetVariableRepr(string name, bool @explicit = false)
        {
            return GetVariableRef(name).ToString();
        }


        /// <summary>
        /// Get the value of the variable with the name specified
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="explicit">If true, simulates explicit mode even when not set on the evaluator</param>
        public object GetVariable(string name, bool @explicit = false)
        {
            if (name == "ans")
                return GetLastAns();
            string scope = _scope;
            name = RemoveRedundantScope(name, scope);

            if (Variables.ContainsKey(name))
                return Variables[name].Reference;

            foreach (string sc in GetAllAccessibleScopes())
            {
                string s = sc;
                string temp = name;
                while (temp.Contains(SCOPE_SEP))
                {
                    temp = temp.Remove(temp.LastIndexOf(SCOPE_SEP));
                    if (HasVariable(scope + SCOPE_SEP + temp))
                    {
                        // ignore if private
                        if (!IsParentScopeOf(s, scope) && Variables[s + SCOPE_SEP + name].Modifiers.Contains("private"))
                            continue;
                        Reference v = Variables[scope + SCOPE_SEP + temp].Reference;
                        if (v.ResolveObj() is ClassInstance)
                        {
                            return ((ClassInstance)v.ResolveObj()).ResolveField(name.Substring(temp.Length + 1), scope).Resolve();
                        }
                    }
                }
                temp = name;
                NormalizeScope(ref temp, ref s);
                if (Variables.ContainsKey(s + SCOPE_SEP + temp))
                {
                    // ignore if private
                    if (!IsParentScopeOf(s, scope) && Variables[s + SCOPE_SEP + temp].Modifiers.Contains("private"))
                        continue;
                    if (scope != s && IsParentScopeOf(s, scope))
                    {
                        SetVariable(temp, Variables[s + SCOPE_SEP + temp].Reference);
                        return Variables[scope + SCOPE_SEP + temp].Reference.Resolve();
                    }
                    else
                    {
                        return Variables[s + SCOPE_SEP + temp].Reference.Resolve();
                    }
                }
            }

            // variable not found, implicit declaration?

            // explicit mode: disallow
            if (ExplicitMode || @explicit)
                throw new EvaluatorException("Variable " + name + " is undefined. (Explicit mode disallows implicit declaration)");

            NormalizeScope(ref name, ref scope);

            // classes: disallow any declarations within a class scope (unless specified in the class)
            string tmp = scope;

            while (true)
            {
                if (HasUserClass(tmp))
                {
                    UserClass uc = GetUserClass(scope);
                    string subName = name;
                    if (subName.Contains(SCOPE_SEP))
                        subName = subName.Remove(subName.IndexOf(SCOPE_SEP));
                    if (!uc.AllFields.ContainsKey(subName))
                    {
                        throw new EvaluatorException("Cannot declare variable " + name + " inside class " + UserClasses[scope].Name);
                    }
                }
                if (!tmp.Contains(SCOPE_SEP))
                    break;
                tmp = tmp.Remove(tmp.LastIndexOf(SCOPE_SEP));
            }

            Variables[scope + SCOPE_SEP + name] = new Variable(name, new Reference(double.NaN), scope);
            return Variables[scope + SCOPE_SEP + name].Value;
        }

        /// <summary>
        /// Set the value of the variable with the name specified to the object referenced
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="ref">Value of the variable as a Reference</param>
        internal void SetVariable(string name, Reference @ref, string scope = "", IEnumerable<string> modifiers = null)
        {
            // set declaring scope
            if (string.IsNullOrWhiteSpace(name))
                throw new EvaluatorException("Variable name cannot be empty");
            if (_reserved.Contains(name.Trim().ToLower()))
            {
                throw new EvaluatorException("Variable name \"" + name + "\" is reserved by Cantus and may not be assigned to");
            }
            if (!IsValidIdentifier(name))
                throw new EvaluatorException("Invalid Variable Name: " + name);

            if (string.IsNullOrWhiteSpace(scope))
                scope = _scope;

            NormalizeScope(ref name, ref scope);
            Variable var = new Variable(name, @ref, scope, modifiers);
            Variables[var.FullName] = var;
        }

        /// <summary>
        /// Set the value of the variable with the name specified
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="value">Value of the variable as an IEvalObject</param>
        public void SetVariable(string name, EvalObjectBase value, string scope = "", IEnumerable<string> modifiers = null)
        {
            NormalizeScope(ref name, ref scope);

            if (string.IsNullOrWhiteSpace(name))
                throw new EvaluatorException("Variable name cannot be empty");

            if (_reserved.Contains(name.Trim().ToLower()))
            {
                throw new EvaluatorException("Variable name \"" + name + "\" is reserved by Cantus and may not be assigned to");
            }
            if (!IsValidIdentifier(name))
                throw new EvaluatorException("Invalid Variable Name: " + name);
            if (string.IsNullOrEmpty(scope))
                scope = _scope;

            Variable var = new Variable(name, value, scope, modifiers);
            Variables[var.FullName] = var;
        }

        /// <summary>
        /// Set the value of the variable with the name specified
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="value">Value of the variable as a system object</param>
        public void SetVariable(string name, object value, string scope = "", IEnumerable<string> modifiers = null)
        {
            SetVariable(name, new Reference(value), scope, modifiers);
        }

        /// <summary>
        /// Set the value of the default variable (i.e. this) used when no name is specified in a self-referring function call (.xxyy())
        /// </summary>
        /// <param name="ref">Value of the variable as a Reference</param>
        internal void SetDefaultVariable(Reference @ref)
        {
            Variables[CombineScope(Scope, DEFAULT_VAR_NAME)] = new Variable(DEFAULT_VAR_NAME, @ref, _scope);
        }

        /// <summary>
        /// Get the value of the default variable (i.e. this) used when no name is specified in a self-referring function call (.xxyy())
        /// </summary>
        internal Reference GetDefaultVariableRef()
        {
            if (Variables.ContainsKey(CombineScope(Scope, DEFAULT_VAR_NAME)))
            {
                return Variables[CombineScope(Scope, DEFAULT_VAR_NAME)].Reference;
            }
            else
            {
                // default variable not set, we'll complain about the variable name
                throw new EvaluatorException("Variable name \"" + DEFAULT_VAR_NAME + "\" is reserved by Cantus and may not be assigned to");
            }
        }

        /// <summary>
        /// Returns true if the variable with the specified name is defined
        /// </summary>
        public bool HasVariable(string name)
        {
            if (name == "ans")
                return true;
            if (Variables.ContainsKey(name))
                return true;
            foreach (string scope in GetAllAccessibleScopes())
            {
                // do not return if private
                if (Variables.ContainsKey(scope + SCOPE_SEP + name) && (IsParentScopeOf(_scope, scope) || !Variables[scope + SCOPE_SEP + name].Modifiers.Contains("private")))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the name given is a valid identifier (variable/function/class/namespace) name 
        /// (i.e. is not empty, does not contain any of &amp;+-*/{}[]()';^$@#!%=&lt;&gt;,:|\` and does not start with a number)
        /// </summary>
        public static bool IsValidIdentifier(string name)
        {
            try
            {
                return ObjectTypes.Identifier.StrIsType(name);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clear all variables defined on this evaluator
        /// </summary>
        public void ClearVariables()
        {
            Variables.Clear();
        }

        /// <summary>
        /// Clears all variables, user functions, and previous answers on this evaluator
        /// </summary>
        public void ClearEverything()
        {
            this._scope = ROOT_NAMESPACE;
            ClearVariables();
            UserFunctions.Clear();
            UserClasses.Clear();
            PrevAns.Clear();
            Imported.Clear();
            Loaded.Clear();
            Loaded.Add(ROOT_NAMESPACE);
            if (IsExternalScope(Scope, ROOT_NAMESPACE))
                this.Import(ROOT_NAMESPACE);
        }

        /// <summary>
        /// Get the last answer
        /// </summary>
        /// <returns></returns>
        public object GetLastAns()
        {
            if (PrevAns.Count > 0)
                return PrevAns[PrevAns.Count - 1].GetValue();
            return 0;
        }

        /// <summary>
        /// Add or set a user function
        /// </summary>
        /// <param name="name">The name of the function</param>
        /// <param name="args">A list of argument names</param>
        /// <param name="def">The function definition</param>

        private void InternalDefineUserFunction(string name, List<string> args, string def, IEnumerable<string> modifiers = null)
        {
            string scope = _scope;
            NormalizeScope(ref name, ref scope);

            if (name.Length == 0 || !IsValidIdentifier(name[0].ToString()))
            {
                throw new EvaluatorException("Error: Invalid Function Name");
            }

            List<object> defaults = new List<object>();

            for (int i = 0; i <= args.Count - 1; i++)
            {
                args[i] = args[i].Trim();
                // default
                if (args[i].Contains(":="))
                {
                    defaults.Add(EvalExprRaw(args[i].Substring(args[i].IndexOf(":=") + 1).Trim(), true, true));
                    args[i] = args[i].Remove(args[i].IndexOf(":=")).Trim();
                }
                else
                {
                    defaults.Add(double.NaN);
                }
                if (!IsValidIdentifier(args[i]))
                    throw new EvaluatorException("Invalid Argument Name: " + args[i]);
            }

            if (string.IsNullOrWhiteSpace(def))
                RemUserFunction(name);

            UserFunctions[scope + SCOPE_SEP + name] = new UserFunction(name, def, args, scope, modifiers, defaults);
        }

        /// <summary>
        /// Add/set a user function
        /// </summary>
        /// <param name="fmtstr">The function in function notation e.g. name(a,b)</param>
        /// <param name="def">The function definition</param>
        public bool DefineUserFunction(string fmtstr, string def, IEnumerable<string> modifiers = null)
        {

            int openBracket = 0;
            int closeBracket = 0;
            string name = null;
            if (fmtstr.Contains("("))
            {
                openBracket = fmtstr.IndexOf("(");
                if (!fmtstr.Contains(")"))
                    throw new SyntaxException("No close bracket found");
                closeBracket = fmtstr.IndexOf(")");
                name = fmtstr.Remove(openBracket).Trim();
            }
            else
            {
                openBracket = fmtstr.Length - 1;
                closeBracket = fmtstr.Length;
                name = fmtstr.Trim();
            }

            List<string> l = new List<string>(fmtstr.Substring(openBracket + 1, closeBracket - openBracket - 1).Split(','));

            if (l.Count == 1 && string.IsNullOrWhiteSpace(l[0]))
                l.Clear();
            InternalDefineUserFunction(name, l, def);

            return true;
        }

        /// <summary>
        /// Remove the user function with the name
        /// </summary>
        /// <param name="name"></param>
        public void RemUserFunction(string name)
        {
            if (UserFunctions.ContainsKey(name))
            {
                UserFunctions.Remove(name);
            }
            else
            {
                name = CombineScope(_scope, name);
                if ((UserFunctions.ContainsKey(name)))
                    UserFunctions.Remove(name);
            }
        }

        /// <summary>
        /// Return true if a user function with the given name exists
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool HasUserFunction(string name)
        {
            if (UserFunctions.ContainsKey(name))
                return true;
            name = RemoveRedundantScope(name, _scope);
            foreach (string s in this.GetAllAccessibleScopes())
            {
                if (UserFunctions.ContainsKey(s + SCOPE_SEP + name))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Return true if an internal or user function with the given name exists
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool HasFunction(string name)
        {
            if (HasUserFunction(name)) return true;

            name = RemoveRedundantScope(name, ROOT_NAMESPACE);
            MethodInfo info = typeof(InternalFunctions).GetMethod(name.ToLowerInvariant(), 
                BindingFlags.IgnoreCase | BindingFlags.Public | 
                BindingFlags.Instance | BindingFlags.DeclaredOnly);

            return (info != null && ! info.IsSpecialName);
        }

        /// <summary>
        /// Get the function with the name as a UserFunction object
        /// </summary>
        /// <param name="name"></param>
        public UserFunction GetUserFunction(string name)
        {
            string scope = _scope;
            name = RemoveRedundantScope(name, scope);
            if (HasUserFunction(name))
            {
                if (UserFunctions.ContainsKey(name))
                {
                    return UserFunctions[name];
                }
                else
                {
                    foreach (string s in this.GetAllAccessibleScopes())
                    {
                        if (UserFunctions.ContainsKey(s + SCOPE_SEP + name))
                        {
                            return UserFunctions[s + SCOPE_SEP + name];
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Execute the function with the given arguments
        /// </summary>
        /// <param name="optionalArgs">A dictionary containing values for optional arguments</param>
        public object ExecUserFunction(string name, IEnumerable<object> args, Dictionary<string, object> optionalArgs = null)
        {
            string scope = _scope;
            name = RemoveRedundantScope(name, scope);

            if (HasUserFunction(name))
            {
                UserFunction uf = null;
                if (UserFunctions.ContainsKey(name))
                {
                    uf = UserFunctions[name];
                }
                else
                {
                    foreach (string s in this.GetAllAccessibleScopes())
                    {
                        if (UserFunctions.ContainsKey(s + SCOPE_SEP + name))
                        {
                            uf = UserFunctions[s + SCOPE_SEP + name];
                            break;
                        }
                    }
                }

                CantusEvaluator tmpEval = null;

                // use a scoped evaluator for function call
                tmpEval = SubEvaluator(0);
                tmpEval.Scope = uf.DeclaringScope;

                List<string> argnames = uf.Args;

                if (args.Count() <= argnames.Count && (args.Count() == argnames.Count || args.Count() >= uf.RequiredArgsCount && (optionalArgs != null)))
                {
                    var arglst = args.ToList();
                    for (int i = 0; i < args.Count(); i++)
                    {
                        tmpEval.SetVariable(argnames[i], arglst[i]);
                    }

                    // named/optional args
                    for (int i = args.Count(); i <= argnames.Count - 1; i++)
                    {
                        if (optionalArgs.ContainsKey(argnames[i]))
                        {
                            tmpEval.SetVariable(argnames[i], optionalArgs[argnames[i]]);
                            // use provided value
                        }
                        else
                        {
                            tmpEval.SetVariable(argnames[i], uf.Defaults[i]);
                            // use default value
                        }
                    }
                }
                else
                {
                    throw new EvaluatorException(name + " : " + argnames.Count + " parameter(s) expected");
                }

                // execute the function in a new scope
                try
                {
                    return tmpEval.EvalRaw(uf.Body, noSaveAns: true);
                }
                catch (EvaluatorException ex)
                {
                    // append current function name & internal to exception 'stack trace'
                    string newMsg = ex.Message + " [In function " + name + " (" + scope + "), line " +
                        (ex.Line+1) + "]" + Environment.NewLine;
                    if (ex is MathException)
                    {
                        throw new MathException(newMsg, ex.Line+1);
                    }
                    else if (ex is SyntaxException)
                    {
                        throw new SyntaxException(newMsg, ex.Line+1);
                    }
                    else
                    {
                        throw new EvaluatorException(newMsg, ex.Line+1);
                    }
                }
            }
            else
            {
                throw new EvaluatorException("Function " + name + " is undefined");
            }
        }

        /// <summary>
        /// Execute an internal function with the given name and arguments
        /// </summary>
        /// <returns></returns>
        public object ExecInternalFunction(string name, List<object> args)
        {
            MethodInfo info = null;

            name = RemoveRedundantScope(name, ROOT_NAMESPACE);
            info = typeof(InternalFunctions).GetMethod(name.ToLowerInvariant(), BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if ((info != null))
            {
                int minParamCt = 0;
                int maxParamCt = 0;
                bool parameterMismatch = false;

                int outputSigFigs = int.MaxValue;

                foreach (ParameterInfo paraminfo in info.GetParameters())
                {
                    if (!paraminfo.IsOptional)
                        minParamCt += 1;
                    if (maxParamCt >= args.Count)
                    {
                        if (paraminfo.IsOptional)
                        {
                            args.Add(paraminfo.DefaultValue);
                        }
                        else
                        {
                            parameterMismatch = true;
                        }
                    }
                    else
                    {
                        // maintain support for legacy functions where BigDecimals are not supported
                        // list of exceptions
                        string[] exceptions = { "Log", "Print", "PrintLine", "ReadLine", "Read", "ReadChar", "Max", "Min", "Abs" };
                        if (!(paraminfo.ParameterType == typeof(BigDecimal)) &&
                            args[maxParamCt].GetType() == typeof(BigDecimal) && !exceptions.Contains(info.Name))
                        {
                            outputSigFigs = ((BigDecimal)args[maxParamCt]).SigFigs;
                            args[maxParamCt] = (double)((BigDecimal)args[maxParamCt]);
                        }
                        else if (args[maxParamCt].GetType() == typeof(BigDecimal))
                        {
                            maxParamCt += 1;
                            continue;
                        }

                        if (!paraminfo.ParameterType.IsAssignableFrom(args[maxParamCt].GetType()))
                        {
                            string paramTypeName = GetEvaluatorTypeName(paraminfo.ParameterType);

                            if (paramTypeName.Contains("`"))
                                paramTypeName = paramTypeName.Remove(paramTypeName.IndexOf("`"));

                            throw new EvaluatorException("In " + name.ToLowerInvariant() + ": Parameter " + (maxParamCt + 1) + " \"" + paramTypeName + "\" Type Expected");
                        }
                    }
                    maxParamCt += 1;
                }

                if (parameterMismatch || args.Count > maxParamCt)
                {
                    throw new EvaluatorException(name.ToLowerInvariant() + ": " + (minParamCt == maxParamCt ? minParamCt.ToString() : minParamCt + " to " + maxParamCt) + " parameter(s) expected ");
                }
                try
                {
                    // execute the internal function
                    object execResult = info.Invoke(Internals, args.ToArray());
                    // if is null then we should return NaN
                    if (execResult == null)
                    {
                        return double.NaN;
                    }
                    else if (execResult is double)
                    {
                        return new BigDecimal((double)(execResult), outputSigFigs);
                        // legacy function support
                    }
                    else
                    {
                        return execResult;
                    }
                }
                catch (EvaluatorException ex)
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException is EvaluatorException)
                    {
                        throw new EvaluatorException("In " + name.ToLowerInvariant() + ": " + ex.InnerException.Message, _curLine);
                    }
                    else
                    {
                        throw new EvaluatorException("In " + name.ToLowerInvariant() + ": Unknown error", _curLine);
                    }
                }
            }
            else
            {
                throw new EvaluatorException("Function " + name.ToLowerInvariant() + " is undefined", _curLine);
            }
        }

        /// <summary>
        /// Return true if a user class with the given name exists
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool HasUserClass(string name)
        {
            if (UserClasses.ContainsKey(name))
                return true;
            foreach (string s in this.GetAllAccessibleScopes())
            {
                if (UserClasses.ContainsKey(s + SCOPE_SEP + name))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the UserClass with the name specified
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public UserClass GetUserClass(string name)
        {
            if (UserClasses.ContainsKey(name))
                return UserClasses[name];
            foreach (string s in this.GetAllAccessibleScopes())
            {
                if (UserClasses.ContainsKey(s + SCOPE_SEP + name))
                    return UserClasses[s + SCOPE_SEP + name];
            }
            return null;
        }

        /// <summary>
        /// Define a UserClass with the name specified and the definition specified, 
        /// inheriting from the classes specified
        /// </summary>
        public void DefineUserClass(string name, string def, IEnumerable<string> inherit = null, IEnumerable<string> modifiers = null)
        {
            name = name.Trim();
            if (!IsValidIdentifier(name))
                throw new SyntaxException("\"" + name + "\" is not a valid class name.");

            string key = CombineScope(this.Scope, name);

            try
            {
                if (this.UserClasses.ContainsKey(key))
                    this.UserClasses[key].Dispose();

                List<string> baseClasses = new List<string>();

                if ((inherit != null))
                {
                    foreach (string str in inherit)
                    {
                        string inh = str.Trim();
                        if (inh == name)
                            throw new EvaluatorException(inh + " may not inherit itself");
                        if (!HasUserClass(inh))
                            throw new EvaluatorException(inh + " is not a valid base class name");
                        UserClass baseClass = GetUserClass(inh);
                        if (baseClass.AllParentClasses.Contains(key))
                            throw new EvaluatorException(inh + ": circular inheritance detected");
                        baseClasses.Add(baseClass.FullName);
                    }
                }

                UserClass uc = new UserClass(name, def, this, modifiers, baseClasses);
                this.UserClasses[key] = uc;
            }
            catch (Exception ex)
            {
                if (this.UserClasses.ContainsKey(key))
                    this.UserClasses.Remove(key);
                throw ex;
            }
        }
        #endregion
    }

    /// <summary>
    /// Defines common scoping operations like joining two scopes and extracting the parent scopes
    /// </summary>
    public static class Scoping
    {
        /// <summary>
        /// Character separating namespaces, etc.; For example, '.' in 'cantus.abc'
        /// </summary>

        public const char SCOPE_SEP = '.';

        /// <summary>
        /// Gets the base scope of the current scope: for cantus.foo.bar
        /// that would be cantus
        /// </summary>
        static public string GetScopeBase(string scope)
        {
            if (scope.Contains(SCOPE_SEP))
            {
                return scope.Remove(scope.IndexOf(SCOPE_SEP)).Trim();
            }
            else
            {
                return scope.Trim();
            }
        }

        /// <summary>
        /// If scope1 is a parent scope of scope2, returns true
        /// that would be cantus
        /// </summary>
        static public bool IsParentScopeOf(string scope1, string scope2, string baseScope = "")
        {
            scope1 = RemoveRedundantScope(scope1, baseScope);
            scope2 = RemoveRedundantScope(scope2, baseScope);
            return scope2.StartsWith(scope1 + SCOPE_SEP) || scope1 == scope2;
        }

        /// <summary>
        /// Get a list of parent scopes of the scope specified, from closest to furthest
        /// that would be cantus
        /// </summary>
        static public List<string> GetParentScopes(string scope)
        {
            List<string> scopes = new List<string>();

            while (scope.Contains(SCOPE_SEP))
            {
                scope = scope.Remove(scope.LastIndexOf(SCOPE_SEP));
                scopes.Add(scope);
            }

            return scopes;
        }

        /// <summary>
        /// Moves all namespaces to the scope and leaves only the name of the variable or function as name
        /// e.g. name=a.b scope=cantus -> name=b scope=cantus.a
        /// </summary>
        static internal void NormalizeScope(ref string name, ref string scope)
        {
            if (name.Contains(SCOPE_SEP))
            {
                string varscope = name.Remove(name.LastIndexOf(SCOPE_SEP));
                // remove duplicate scope: name=cantus.a.b.c scope=cantus.a -> name=c scope=cantus.a.b
                if (varscope.StartsWith(scope))
                    varscope = varscope.Substring(scope.Length).Trim(new[] { SCOPE_SEP });
                scope += SCOPE_SEP + varscope;
                scope = scope.Trim(new[] { SCOPE_SEP });
                name = name.Substring(name.LastIndexOf(SCOPE_SEP) + 1);
            }
        }

        /// <summary>
        /// Removes redundant scope on a name relative to a scope: cantus.abc -> abc
        /// </summary>
        static public string RemoveRedundantScope(string name, string scope)
        {
            if (scope == name)
                return "";
            // same scope, none required
            if (name.StartsWith(scope + SCOPE_SEP))
                name = name.Substring(scope.Length + 1);
            return name;
        }

        /// <summary>
        /// Checks if the first scope is 'external' (that is, 
        /// does not have the same base scope) in relation to the second scope.
        /// </summary>
        public static bool IsExternalScope(string scope1, string scope2)
        {
            return GetScopeBase(scope1) != GetScopeBase(scope2);
        }

        /// <summary>
        /// Combine a scope and a name, removing redundancies
        /// </summary>
        public static string CombineScope(string scope, string name)
        {
            return scope + SCOPE_SEP + RemoveRedundantScope(name, scope);
        }

        /// <summary>
        /// Get the internal scope name of a file
        /// </summary>
        public static string GetFileScopeName(string path)
        {
            path = Path.GetFullPath(path);
            string newScope = "";
            string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (path.StartsWith(appDir))
            {
                newScope = path.Substring(appDir.Length + 1).Replace('/', SCOPE_SEP).Replace('\\', SCOPE_SEP);
                // do not include 'include'
                if (newScope.StartsWith("include."))
                    newScope = newScope.Substring("include.".Count());
            }
            else
            {
                newScope = Path.GetFileName(Path.GetDirectoryName(path)) + SCOPE_SEP + Path.GetFileName(path);
            }
            if (newScope.EndsWith(".can"))
                newScope = newScope.Remove(newScope.Length - 4);
            newScope = newScope.Trim(SCOPE_SEP);
            return newScope;
        }
    }
}
