using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Cantus.Core.CommonTypes;
using System.Text;
using Cantus.Core.Exceptions;
using System.Collections.Specialized;
using System.Net;
using System.Security.Cryptography;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Linq;

using static Cantus.Core.CantusEvaluator.ObjectTypes;
using static Cantus.Core.CantusEvaluator.IOEventArgs;
using System.Reflection;

namespace Cantus.Core
{

    public sealed partial class CantusEvaluator
    {
        /// <summary>
        /// Contains definitions for all built-in functions accessible from Cantus expressions
        /// </summary>
        public sealed class InternalFunctions
        {

            /// <summary>
            /// A collection of publicly available internal functions
            /// </summary>
            public static readonly IEnumerable<System.Reflection.MethodInfo> Methods = typeof(InternalFunctions).GetMethods(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.DeclaredOnly).Where((System.Reflection.MethodInfo inf) => !inf.IsSpecialName);

            // Define functions here (name is case insensitive)
            // All public functions are directly accessible when evaluating an expression` (though may be overridden by user functions)
            // All private and friend functions are hidden

            private CantusEvaluator _eval;
            /// <summary>
            /// Raised when Cantus needs to read input input from the console. Handle to use I/O in GUI applications
            /// </summary>
            public delegate void ReadInputDelegate(object sender, IOEventArgs e, out object @return);
            /// <summary>
            /// Raised when Cantus needs to read input input from the console. Handle to use I/O in GUI applications
            /// </summary>
            public event ReadInputDelegate ReadInput;
            
            /// <summary>
            /// The event handler for the ReadInput event
            /// </summary>
            internal ReadInputDelegate ReadInputHandler
            {
                    get{
                        return this.ReadInput;
                    }
            }

            /// <summary>
            /// Raised when Cantus needs to write output to the console. Handle to use I/O in GUI applications
            /// </summary>
            public delegate void WriteOutputDelegate(object sender, IOEventArgs e);
            /// <summary>
            /// Raised when Cantus needs to read input input from the console. Handle to use I/O in GUI applications
            /// </summary>
            public event WriteOutputDelegate WriteOutput;

            /// <summary>
            /// The event handler for the WriteOutput event
            /// </summary>
            internal WriteOutputDelegate WriteOutputHandler
            {
                get{
                    return this.WriteOutput;
                }
            }


            public delegate void ClearConsoleDelegate(object sender, EventArgs e);
            /// <summary>
            /// Raised when Cantus is required to clear the console
            /// </summary>
            public event ClearConsoleDelegate RequestClearConsole;

            public InternalFunctions(CantusEvaluator parent)
            {
                this._eval = parent;
            }

            /// <summary>
            /// The event handler for the RequestClearConsole event
            /// </summary>
            internal ClearConsoleDelegate RequestClearConsoleHandler
            {
                    get{
                        return this.RequestClearConsole;
                    }
            }

            // evaluator management

            /// <summary>
            /// Exit the evaluator
            /// </summary>
            public void _Exit()
            {
                Environment.Exit(0);
            }

            /// <summary>
            /// Kill all sub-threads (except this one) spawned from the evaluator
            /// </summary>
            public void _StopAll()
            {
                _eval.StopAll(Thread.CurrentThread.ManagedThreadId);
            }

            /// <summary>
            /// Reload all constants, clears all variables and UserFunctions, and clears imports
            /// </summary>
            public void _AllClear()
            {
                _eval.ClearEverything();
                _eval.ReloadDefault();
            }

            /// <summary>
            /// Reload default constants. if name is specified, reloads constant with that name only.
            /// </summary>
            public void _ReloadConst(string name = "")
            {
                _eval.ReloadDefault(name);
            }

            /// <summary>
            /// Reload all constants and init scripts
            /// </summary>
            public void _Reload()
            {
                _eval.ReloadDefault();
                _eval.ReInitialize();
            }

            /// <summary>
            /// Get the previous nth answer
            /// </summary>
            public object _PrevAns(double index)
            {
                return _eval.PrevAns[Int(_eval.PrevAns.Count - index - 1)];
            }

            // modes
            /// <summary>
            /// Get or set the output format of this evaluator
            /// </summary>
            public string _Output(string val = "")
            {
                if (!string.IsNullOrEmpty(val))
                {
                    try
                    {
                        val = val.ToLower();
                        // shorthands
                        if (val == "sci")
                        {
                            val = "Scientific";
                        }
                        // do not allow numbers
                        double ___tmp;
                        if (double.TryParse(val, out ___tmp))
                            throw new EvaluatorException();
                        _eval.OutputMode = (OutputFormat)Enum.Parse(typeof(OutputFormat), val, true);
                    }
                    catch
                    {
                        throw new EvaluatorException(val + " is not a valid output mode. Choices are: Raw, Math, Scientific (Sci)");
                    }
                }
                return _eval.OutputMode.ToString();
            }

            /// <summary>
            /// Get or set the angle representation mode of this evaluator
            /// </summary>
            public string _AngleRepr(string val = "")
            {
                if (!string.IsNullOrEmpty(val))
                {
                    val = val.ToLower();
                    // shorthands
                    if (val == "deg")
                    {
                        val = "Degree";
                    }
                    else if (val == "rad")
                    {
                        val = "Radian";
                    }
                    else if (val == "grad")
                    {
                        val = "Gradian";
                    }
                    try
                    {
                        _eval.AngleMode = (AngleRepresentation)Enum.Parse(typeof(AngleRepresentation), val, true);
                    }
                    catch
                    {
                        throw new EvaluatorException(val + " is not a valid angle representation mode." + "Choices are: Radian (Rad), Degree (Deg), Gradian (Grad)");
                    }
                }
                return _eval.AngleMode.ToString();
            }

            /// <summary>
            /// Get or set the number of spaces per tab of this evaluator
            /// </summary>
            public double _SpacesPerTab(double val = double.NaN)
            {
                if (val >= 0)
                {
                    _eval.SpacesPerTab = Int(val);
                }
                else if (val < 0 && !double.IsNaN(val))
                {
                    throw new EvaluatorException("Number of spaces per tab may not be negative.");
                }
                return _eval.SpacesPerTab;
            }

            /// <summary>
            /// Get or set the explicit mode
            /// </summary>
            public bool _Explicit(bool? val = null)
            {
                if ((val != null))
                {
                    _eval.ExplicitMode = Convert.ToBoolean(val);
                }
                return _eval.ExplicitMode;
            }

            /// <summary>
            /// Get or set the significant mode of the evaluator
            /// </summary>
            public bool _SigFigs(bool? val = null)
            {
                if ((val != null))
                {
                    _eval.SignificantMode = Convert.ToBoolean(val);
                }
                return _eval.SignificantMode;
            }

            /// <summary>
            /// Get the scope
            /// </summary>
            public string _Scope()
            {
                return _eval.Scope;
            }

            // reflection

            /// <summary>
            /// Read the definition of a User Functions
            /// </summary>
            public string _FunctionDef(string fullName)
            {
                try
                {
                    return _eval.UserFunctions[fullName].ToString(_eval.Scope);
                }
                catch
                {
                    if (fullName.StartsWith(ROOT_NAMESPACE + Scoping.SCOPE_SEP) && _eval.HasFunction(fullName))
                        return "(Internal Code)";
                    else throw new EvaluatorException("Function \"" + fullName + "\" is undefined");
                }
            }

            /// <summary>
            /// Get the parameters of the specified function
            /// </summary>
            public IEnumerable<Reference> _FunctionParams(string fullName)
            {
                if (_eval.UserFunctions.ContainsKey(fullName))
                {
                    return (_eval.UserFunctions[fullName].Args.Select(
                        new Func<string, Reference>((x) => new Reference(x)))).ToList();
                } 
                else if (fullName.StartsWith(ROOT_NAMESPACE + Scoping.SCOPE_SEP) && _eval.HasFunction(fullName))
                {
                    MethodInfo mi = typeof(InternalFunctions).GetMethod(
                        Scoping.RemoveRedundantScope(fullName, ROOT_NAMESPACE),
                        BindingFlags.IgnoreCase | BindingFlags.Public | 
                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    return (from x in mi.GetParameters() select
                         new Reference(new Text(x.Name))).ToList();
                }
                else
                {
                    throw new EvaluatorException("Function " + fullName + " is undefined.");
                }
            }



            /// <summary>
            /// Get a pointer to the function with the specified name
            /// </summary>
            public Lambda _FunctionPtr(string fullName)
            {
                if (_eval.UserFunctions.ContainsKey(fullName))
                {
                    return new Lambda(_eval.UserFunctions[fullName]);
                } 
                else if (fullName.StartsWith(ROOT_NAMESPACE + Scoping.SCOPE_SEP) && _eval.HasFunction(fullName))
                {
                    MethodInfo mi = typeof(InternalFunctions).GetMethod(
                        Scoping.RemoveRedundantScope(fullName, ROOT_NAMESPACE),
                        BindingFlags.IgnoreCase | BindingFlags.Public | 
                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    return new Lambda(fullName,
                        mi.GetParameters().Select(new Func<ParameterInfo, string>((x)=>x.Name)),  true);
                }
                else
                {
                    throw new EvaluatorException("Function " + fullName + " is undefined.");
                }
            }

            /// <summary>
            /// Execute a function by full name
            /// </summary>
            public object _FunctionExec(string fullName, IList<Reference> args=null,
                IDictionary<Reference,Reference> kwargs = null)
            {
                if (args == null) args = new List<Reference>();
                if (_eval.UserFunctions.ContainsKey(fullName))
                {
                    if (kwargs != null)
                    {
                        Dictionary<string, object> skwargs = new Dictionary<string, object>();
                        foreach (KeyValuePair<Reference, Reference> p in kwargs)
                        {
                            skwargs[p.Key.ToString()] = p.Value.Resolve();
                        }

                        return _eval.ExecUserFunction(fullName,
                            args.Select(new Func<Reference, object>((x) => x.Resolve())),
                            skwargs);
                    }
                    else
                    {
                        return _eval.ExecUserFunction(fullName,
                            args.Select(new Func<Reference, object>((x) => x.Resolve())));
                    }
                } 
                else if (fullName.StartsWith(ROOT_NAMESPACE + Scoping.SCOPE_SEP) && _eval.HasFunction(fullName))
                {
                   return _eval.ExecInternalFunction(fullName,
                        args.Select(new Func<Reference, object>((x) => x.Resolve())).ToList());
                }
                else
                {
                    throw new EvaluatorException("Function " + fullName + " is undefined.");
                }
            }

            /// <summary>
            /// Returns true if function with specified name exists
            /// </summary>
            public bool _FunctionDefined(string fullName)
            {
                if (_eval.UserFunctions.ContainsKey(fullName)) return true;
                if (fullName.StartsWith(ROOT_NAMESPACE + Scoping.SCOPE_SEP) && _eval.HasFunction(fullName))
                    return true;
                return false;
            }

            /// <summary>
            /// List functions in the scope. By default, lists all in current scope.
            /// </summary>
            public List<Reference> _FunctionList(string scope = "")
            {
                if (scope == "") scope = _eval.Scope;
                List<Reference> lst = new List<Reference>();
                foreach (UserFunction fn in _eval.UserFunctions.Values)
                {
                    if (fn.FullName.StartsWith(scope) )
                    {
                        lst.Add(new Reference(fn.FullName));
                    }
                }
                if (scope == ROOT_NAMESPACE)
                {
                    MethodInfo[] infoset = typeof(InternalFunctions).GetMethods(
                        BindingFlags.IgnoreCase | BindingFlags.Public | 
                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (MethodInfo mi in infoset)
                    {
                        lst.Add(new Reference(Scoping.CombineScope(ROOT_NAMESPACE, mi.Name)));
                    }
                }
                return lst;
            }

            /// <summary>
            /// Get the value of the variable with the name
            /// </summary>
            public object _VariableValue(string fullName)
            {
                if (!_VariableDefined(fullName)) throw new EvaluatorException("Variable " + fullName + " is undefined.");
                return _eval.Variables[fullName].Value;
            }

            /// <summary>
            /// Get a reference to the variable with the name
            /// </summary>
            public Reference _VariableRef(string fullName)
            {
                if (!_VariableDefined(fullName)) throw new EvaluatorException("Variable " + fullName + " is undefined.");
                return new Reference(_eval.Variables[fullName].Reference);
            }

            /// <summary>
            /// Returns true if the variable with the given name is defined
            /// </summary>
            public bool _VariableDefined(string fullName) { return _eval.Variables.ContainsKey(fullName);  }

            /// <summary>
            /// List variables in the scope. By default, lists all in current scope.
            /// </summary>
            public List<Reference> _VariableList(string scope = "")
            {
                if (scope == "") scope = _eval.Scope;
                List<Reference> lst = new List<Reference>();
                foreach (Variable var in _eval.Variables.Values)
                {
                    if (var.FullName.StartsWith(scope))
                    {
                        lst.Add(new Reference(var.FullName));
                    }
                }
                return lst;
            }

            /// <summary>
            /// Returns true if the class with the given name is defined
            /// </summary>
            public bool _ClassDefined(string fullName) { return _eval.UserClasses.ContainsKey(fullName);  }

            /// <summary>
            /// Returns true if the class with the given name is defined
            /// </summary>
            public ClassInstance _ClassInit(string fullName, IList<Reference> args=null, IDictionary<Reference,Reference> kwargs=null) {
                if (!_ClassDefined(fullName)) throw new EvaluatorException("Class " + fullName + " is undefined.");
                    if (args == null) args = new List<Reference>();
                    UserClass uc = _eval.UserClasses[fullName];
                    if (uc.Constructor.Args.Count() > 0 && args.Count == 0)
                    {
                        return new ClassInstance(uc);
                    }
                    else
                    {
                        return new ClassInstance(uc, args);
                    }
 
            }

            /// <summary>
            /// List classes in the scope. By default, lists all in current scope.
            /// </summary>
            public List<Reference> _ClassList(string scope = "")
            {
                if (scope == "") scope = _eval.Scope;
                List<Reference> lst = new List<Reference>();
                foreach (UserClass cls in _eval.UserClasses.Values)
                {
                    if (cls.FullName.StartsWith(scope))
                    {
                        lst.Add(new Reference(cls.FullName));
                    }
                }
                return lst;
            }

            /// <summary>
            /// Set or get the clipboard object
            /// </summary>
            public object Clip(object obj = null)
            {
                if (obj == null)
                {
                    object result = double.NaN;
                    Thread th = new Thread(() =>
                    {
                        try
                        {
                            result = ObjectTypes.Parse(Clipboard.GetText(), _eval, true, false, _eval.SignificantMode).GetValue();
                        }
                        catch
                        {
                        }
                    });
                    th.SetApartmentState(ApartmentState.STA);
                    th.Start();
                    th.Join();
                    return result;
                }
                else
                {
                    Thread th = new Thread(() =>
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(obj.ToString()))
                            {
                                Clipboard.Clear();
                            }
                            else
                            {
                                Clipboard.SetText(obj.ToString());
                            }
                        }
                        catch
                        {
                        }
                    });
                    th.SetApartmentState(ApartmentState.STA);
                    th.Start();

                    return obj.ToString() + ": copied to clipboard";
                }
            }

            // convert degree, radians, gradians
            public double DToR(double x)
            {
                return x * Math.PI / 180;
            }
            public double RToD(double x)
            {
                return x / Math.PI * 180;
            }
            public double RToG(double x)
            {
                return x / Math.PI * 200;
            }
            public double DToG(double x)
            {
                return x / 9 * 10;
            }
            public double GToD(double x)
            {
                return x / 10 * 9;
            }
            public double GToR(double x)
            {
                return x / 200 * Math.PI;
            }

            // trig functions
            public object Sin(object x)
            {
                if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return SinD((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return SinR((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return SinG((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Sin((System.Numerics.Complex)x);
                }
                else
                {
                    return double.NaN;
                }
            }
            public object Cos(object x)
            {
                if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return CosD((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return CosR((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return CosG((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Cos((System.Numerics.Complex)x);
                }
                else
                {
                    return double.NaN;
                }
            }
            public object Tan(object x)
            {
                if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return TanD((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return TanR((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return TanG((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Tan((System.Numerics.Complex)x);
                }
                else
                {
                    return double.NaN;
                }
            }
            public object Cot(object x)
            {
                if (x is double)
                {
                    return 1 / (double)(Tan((double)(x)));
                }
                else if (x is System.Numerics.Complex)
                {
                    return 1 / (double)(Tan((System.Numerics.Complex)x));
                }
                else
                {
                    return double.NaN;
                }
            }
            public object Sec(object x)
            {
                if (x is double)
                {
                    return 1 / (double)(Cos((double)(x)));
                }
                else if (x is System.Numerics.Complex)
                {
                    return 1 / (double)(Cos((System.Numerics.Complex)x));
                }
                else
                {
                    return double.NaN;
                }
            }
            public object Csc(object x)
            {
                if (x is double)
                {
                    return 1 / (double)(Sin((double)(x)));
                }
                else if (x is System.Numerics.Complex)
                {
                    return 1 / (double)(Sin((System.Numerics.Complex)x));
                }
                else
                {
                    return double.NaN;
                }
            }

            // specific trig functions
            public double SinD(double x)
            {
                double deg = DToR(x);
                deg = Math.Sin(deg);
                return Math.Round(deg, 11);
            }
            public double CosD(double x)
            {
                double deg = DToR(x);
                deg = Math.Cos(deg);
                return Math.Round(deg, 11);
            }
            public double TanD(double x)
            {
                double deg = DToR(x);
                deg = Math.Tan(deg);
                return Math.Round(deg, 11);
            }
            public double CotD(double x)
            {
                return 1 / TanD(x);
            }
            public double SecD(double x)
            {
                return 1 / CosD(x);
            }
            public double CscD(double x)
            {
                return 1 / SinD(x);
            }
            public double SinR(double x)
            {
                return Math.Round(Math.Sin(x), 9);
            }
            public double CosR(double x)
            {
                return Math.Round(Math.Cos(x), 9);
            }
            public double TanR(double x)
            {
                return Math.Round(Math.Tan(x), 9);
            }
            public double CotR(double x)
            {
                return 1 / TanR(x);
            }
            public double SecR(double x)
            {
                return 1 / CosR(x);
            }
            public double CscR(double x)
            {
                return 1 / SinR(x);
            }
            public double SinG(double x)
            {
                return Math.Round(Math.Sin(GToR(x)), 11);
            }
            public double CosG(double x)
            {
                return Math.Round(Math.Cos(GToR(x)), 11);
            }
            public double TanG(double x)
            {
                return Math.Round(Math.Tan(GToR(x)), 11);
            }
            public double CotG(double x)
            {
                return 1 / TanG(x);
            }
            public double SecG(double x)
            {
                return 1 / CosG(x);
            }
            public double CscG(double x)
            {
                return 1 / SinG(x);
            }

            public object Asin(object x)
            {
                if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Asin((System.Numerics.Complex)x);
                }
                else if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return Asind((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return Asinr((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return Asing((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else
                {
                    return double.NaN;
                }
            }

            public object Acos(object x)
            {
                if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Acos((System.Numerics.Complex)x);
                }
                else if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return Acosd((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return Acosr((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return Acosg((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Asind(double x)
            {
                double deg = Math.Asin(x) / Math.PI * 180;
                return Math.Round(deg, 11);
            }
            public double Acosd(double x)
            {
                double deg = Math.Acos(x) / Math.PI * 180;
                return Math.Round(deg, 11);
            }
            public double Asinr(double x)
            {
                return Math.Round(Math.Asin(x), 11);
            }
            public double Acosr(double x)
            {
                return Math.Round(Math.Acos(x), 11);
            }
            public double Asing(double x)
            {
                return Math.Round(RToG(Math.Asin(x)), 11);
            }
            public double Acosg(double x)
            {
                return Math.Round(RToG(Math.Acos(x)), 11);
            }

            public object Atan(object x)
            {
                if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Atan((System.Numerics.Complex)x);
                }
                else if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return Atand((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return Atanr((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return Atang((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Atand(double x)
            {
                return Math.Atan(x) / Math.PI * 180;
            }
            public double Atanr(double x)
            {
                return Math.Atan(x);
            }
            public double Atang(double x)
            {
                return RToG(Math.Atan(x));
            }

            public object Sinh(object x)
            {
                if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Sinh((System.Numerics.Complex)x);
                }
                else if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return SinhD((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return SinhR((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return SinhG((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else
                {
                    return double.NaN;
                }
            }

            public object Tanh(object x)
            {
                if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Tanh((System.Numerics.Complex)x);
                }
                else if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return TanhD((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return TanhR((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return TanhG((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else
                {
                    return double.NaN;
                }
            }

            public object Cosh(object x)
            {
                if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Cosh((System.Numerics.Complex)x);
                }
                else if (x is double)
                {
                    switch (_eval.AngleMode)
                    {
                        case CantusEvaluator.AngleRepresentation.Degree:
                            return CoshD((double)(x));
                        case CantusEvaluator.AngleRepresentation.Radian:
                            return CoshR((double)(x));
                        case CantusEvaluator.AngleRepresentation.Gradian:
                            return CoshG((double)(x));
                        default:
                            return double.NaN;
                    }
                }
                else
                {
                    return double.NaN;
                }
            }
            public double SinhD(double x)
            {
                return Math.Sinh(DToR(x));
            }
            public double TanhD(double x)
            {
                return Math.Tanh(DToR(x));
            }
            public double CoshD(double x)
            {
                return Math.Cosh(DToR(x));
            }
            public double SinhR(double x)
            {
                return Math.Sinh(x);
            }
            public double TanhR(double x)
            {
                return Math.Tanh(x);
            }
            public double CoshR(double x)
            {
                return Math.Cosh(x);
            }
            public double SinhG(double x)
            {
                return Math.Sinh(GToR(x));
            }
            public double TanhG(double x)
            {
                return Math.Tanh(GToR(x));
            }
            public double CoshG(double x)
            {
                return Math.Cosh(GToR(x));
            }

            /// <summary>
            /// Raise e to the given power
            /// </summary>
            public object Exp(object power)
            {
                if (power is double)
                {
                    return Math.Exp((double)(power));
                }
                else if (power is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Exp((System.Numerics.Complex)power);
                }
                else
                {
                    throw new EvaluatorException("Invalid types ofr exp");
                }
            }

            /// <summary>
            /// Get the first number raised to the second
            /// </summary>
            public object Pow(object @base, object power)
            {
                if (@base is double) @base = (BigDecimal)(double)@base;
                if (@base is int) @base = (BigDecimal)(int)@base;
                if (power is double) power = (BigDecimal)(double)power;
                if (power is int) power = (BigDecimal)(int)power;
                if (@base is BigDecimal && power is BigDecimal)
                {
                    return BigDecimal.Pow((BigDecimal)@base, (BigDecimal)power);

                }
                else if (@base is System.Numerics.Complex)
                {
                    if (power is BigDecimal || power is double)
                        power = new System.Numerics.Complex((double)(power), 0);
                    return System.Numerics.Complex.Pow((System.Numerics.Complex)@base, (System.Numerics.Complex)power);

                }
                else if (@base is IEnumerable<Reference> && power is double || power is BigDecimal)
                {
                    return new Matrix((IEnumerable<Reference>)@base).Expo(Int((double)(power))).GetValue();

                }
                else
                {
                    throw new EvaluatorException("Invalid pow");
                }
            }

            /// <summary>
            /// Compute the square root of a number
            /// </summary>
            public object Sqrt(object x)
            {
                if (x is double)
                {
                    if ((double)(x) >= 0)
                    {
                        return BigDecimal.Pow((BigDecimal)(double)x, 0.5);
                    }
                    else
                    {
                        return new System.Numerics.Complex(0, System.Numerics.Complex.Sqrt((double)(x)).Imaginary);
                    }
                }
                else if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Sqrt((System.Numerics.Complex)x);
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Compute the cube root of a number
            /// </summary>
            public object Cbrt(object x)
            {
                if (x is double)
                {
                    return ((BigDecimal)(double)(x) < 0.0 ? -1.0 : 1.0) * BigDecimal.Pow((BigDecimal)Abs((BigDecimal)(double)(x)), 1.0 / 3);
                }
                else if (x is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Pow((System.Numerics.Complex)x, 1 / 3);
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Get the nth root of the value
            /// </summary>
            public object Root(object value, object n)
            {
                if (value is double && n is double)
                {
                    // handle negative odd roots which are otherwise undefined
                    if (CmpDbl((double)(n) % 2, 0) == 1)
                    {
                        return (double)(value) < 0 ? -1 : 1 * Math.Pow(Math.Abs((double)(value)), 1 / (double)(n));
                    }
                    else
                    {
                        return Math.Pow((double)(value), 1 / (double)(n));
                    }
                }
                else if (value is System.Numerics.Complex & n is double)
                {
                    return System.Numerics.Complex.Pow((System.Numerics.Complex)value, 1 / (double)(n));
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Calculates the factorial of a number within the double range
            /// using an approximation of the gamma function
            /// </summary>
            public double Factorial(double value)
            {
                return (double)RoundSF((BigDecimal)Gamma(value + 1), 3);
            }

            /// <summary>
            /// Calculates the precise integer factorial of any number
            /// </summary>
            public BigDecimal FactBigInt(BigDecimal value)
            {
                BigDecimal total = value.TruncateInt();
                total.Normalize();
                for (BigDecimal i=value-1; i > 1; i = i - 1)
                {
                    total = total * i;
                    total.Normalize();
                    total = total.Truncate();
                }
                total.Normalize();
                total = total.Truncate();
                return total;
            }

            /// Gamma function (Use Lanczos approximation)
            /// From wikipedia "Lanczos approximation" page
            /// Originally in Python, translated by author to VB.NET
            public double Gamma(object val)
            {
                System.Numerics.Complex z;
                if (val is System.Numerics.Complex) z = (System.Numerics.Complex)val;
                else if (val is BigDecimal) z = (double)(BigDecimal)val;
                else if (val is double) z = (double)val;
                else return double.NaN;

                try
                {
                    double epsi = 1E-07;

                    double[] p = {
                        676.520368121885,
                        -1259.1392167224,
                        771.323428777653,
                        -176.615029162141,
                        12.5073432786869,
                        -0.13857109526572,
                        9.98436957801957E-06,
                        1.50563273514931E-07
                    };
                    System.Numerics.Complex result = new System.Numerics.Complex();
                    if (z.Real < 0.5)
                    {
                        result = Math.PI / (System.Numerics.Complex.Sin(Math.PI * z) * Gamma(1 - z));
                    }
                    else
                    {
                        z -= 1;
                        System.Numerics.Complex x = new System.Numerics.Complex(0.99999999999981, 0);

                        for (int i = 0; i <= p.Length - 1; i++)
                        {
                            x += p[i] / (z + i + 1);
                        }

                        System.Numerics.Complex t = z + p.Length - 0.5;
                        result = Math.Sqrt(2 * Math.PI) * System.Numerics.Complex.Pow(t, (z + 0.5)) *
                            System.Numerics.Complex.Exp(-t) * x;
                    }
                    if (CmpDbl(result.Imaginary, 0, epsi) == 0)
                    {
                        return result.Real;
                    }
                }
                catch
                {
                    return double.NaN;
                }
                return double.NaN;
            }

            /// <summary>
            /// Check if a number is prime. Deterministic for small numbers under 10^16
            /// </summary>
            /// <param name="n"></param>
            /// <returns></returns>
            public bool IsPrime(double n)
            {
                // First test trivial cases
                if (double.IsNaN(n) || double.IsInfinity(n) || n <= 1 || n % 2 == 0)
                    return false;

                try
                {
                    // Then try using the Miller-Rabin algorithm
                    if (!MillerRabin(Convert.ToInt64(Math.Floor(n)), 20))
                        return false;
                }
                catch
                {
                }

                // Try brute forcing 
                int max = 100000001;
                if (max > Math.Sqrt(n))
                    max = Int(Math.Sqrt(n));
                for (int i = 3; i <= max; i++)
                {
                    if (n % i == 0)
                        return false;
                }

                return true;
            }

            /// <summary>
            /// Miller-Rabin primality test, from RosettaCode
            /// </summary>
            /// <param name="n"></param>
            /// <param name="k"></param>
            /// <returns></returns>
            private bool MillerRabin(long n, int k)
            {
                if (n < 2)
                {
                    return false;
                }
                if (n != 2 && n % 2 == 0)
                {
                    return false;
                }
                int s = Int(n - 1);
                while (s % 2 == 0)
                {
                    s >>= 1;
                }
                Random r = new Random();
                for (int i = 0; i <= k - 1; i++)
                {
                    double a = r.Next(Int(n - 1)) + 1;
                    int temp = s;
                    long modulo = Convert.ToInt64(Math.Pow(a, temp)) % n;
                    while (temp != n - 1 && modulo != 1 && modulo != n - 1)
                    {
                        modulo = (modulo * modulo) % n;
                        temp = temp * 2;
                    }
                    if (modulo != n - 1 && temp % 2 == 0)
                    {
                        return false;
                    }
                }
                return true;
            }

            // combinatorics
            /// <summary>
            /// Compute combinations / binomial coefficients
            /// </summary>
            public double Comb(double n, double r)
            {
                if (CmpDbl(n, 0) == 0 && r < 1 && r >= 0)
                    return 1;
                // (0 0) = 1

                double sum = 1;
                for (double i = 0; i <= r - 1; i++)
                {
                    sum *= n - i;
                    sum /= i + 1;
                }

                return sum;
            }

            /// <summary>
            /// Compute combinations / binomial coefficients
            /// </summary>
            public double Choose(double n, double k)
            {
                return Comb(n, k);
            }

            /// <summary>
            /// Compute combinations
            /// </summary>
            public double NCr(double n, double k)
            {
                return Comb(n, k);
            }

            /// <summary>
            /// Compute permutations
            /// </summary>
            public double Perm(double n, double k)
            {
                double sum = 0;
                n = Math.Truncate(n);
                if (CmpDbl(n, 0) == 0 && k < 1 && k >= 0)
                    return 1;
                // (0 0) = 1
                for (long i = 0; i <= Convert.ToInt64(Math.Truncate(k - 1)); i++)
                {
                    sum += Math.Log10(n - i);
                }
                return Math.Round(Math.Pow(10, sum));
            }

            /// <summary>
            /// Compute permutations
            /// </summary>
            public double nPr(double n, double k)
            {
                return Perm(n, k);
            }
            public double GCF(double v1, double v2)
            {
                try
                {
                    //convert to integers
                    ulong i1 = Convert.ToUInt64(Abs(v1));
                    ulong i2 = Convert.ToUInt64(Abs(v2));
                    ulong r = 0;
                    //euclid's algorithm
                    do
                    {
                        r = i1 % i2;
                        i1 = i2;
                        i2 = r;
                    } while (r > 0);
                    return i1;
                }
                catch (Exception)
                {
                    return double.NaN;
                }
            }

            public double GCD(double v1, double v2)
            {
                //alternate name
                return GCF(v1, v2);
            }
            public double LCM(double v1, double v2)
            {
                try
                {
                    int i1 = Int(v1);
                    int i2 = Int(v2);
                    return i1 * i2 / GCF(i1, i2);
                }
                catch
                {
                    return double.NaN;
                }
            }

            // rounding
            /// <summary>
            /// Round the number to the nearest integer (or to the specified number of digits)
            /// </summary>
            public BigDecimal Round(BigDecimal value, BigDecimal? digits = null)
            {
                try {
                    int dgts;
                    if (digits == null) dgts = 0;
                    else dgts = Int((double)(BigDecimal)digits);

                    return (value * new BigDecimal(1, exponent: dgts)).Round() / new BigDecimal(1, exponent: dgts);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return 0.0;
                }
            }

            /// <summary>
            /// Round the number to the integer above
            /// </summary>
            public BigDecimal Ceil(BigDecimal value)
            {
                int modify = 0;
                if (value > 0) modify = 1;
                BigDecimal trunc = Truncate(value);
                return trunc + modify;
            }

            /// <summary>
            /// Round the number to the integer below
            /// </summary>
            public BigDecimal Floor(BigDecimal value)
            {
                int modify = 0;
                if (value < 0) modify = 1;
                BigDecimal trunc = Truncate(value);
                return trunc + modify;
            }

            /// <summary>
            /// Round the number to the adjacent integer nearest to 0
            /// </summary>
            public BigDecimal Truncate(BigDecimal value)
            {
                return value.TruncateInt();
            }

            /// <summary>
            /// Set the significance of the specified number to the speciied number of significant figures, without rounding it
            /// It will be rounded automatically on output in sigfig mode.
            /// </summary>
            public BigDecimal SF(BigDecimal value, double sigfigs = 1)
            {
                value.SigFigs = Int(sigfigs);
                return value;
            }

            /// <summary>
            /// Make the specified number "infinitely precise" when calculating with sig figs
            /// </summary>
            public BigDecimal NoSF(BigDecimal value)
            {
                value.SigFigs = int.MaxValue;
                return value;
            }

            /// <summary>
            /// Round the number to the specified number of significant figures
            /// </summary>
            public BigDecimal RoundSF(BigDecimal value, BigDecimal sigfigs)
            {
                value.SigFigs = (int)sigfigs;
                value.Normalize();
                return value.Truncate(Int(sigfigs));
            }

            /// <summary>
            /// Get the precomputed number of sig figs in a number
            /// </summary>
            public double GetSF(BigDecimal value)
            {
                int sf = value.SigFigs;
                if (sf == int.MaxValue)
                    return double.PositiveInfinity;
                return sf;
            }

            /// <summary>
            /// Get the lowest sig figs in a number, using the precomputed sig fig number
            /// </summary>
            public double GetLeastSF(BigDecimal value)
            {
                return value.LeastSigFig;
            }

            /// <summary>
            /// Count the number of sig figs in a number
            /// Important note: 0.0 is seen as 2 sig figs
            /// </summary>
            public double CountSigFigs(string textRepr)
            {
                string sep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                if (!textRepr.Contains(sep) && !textRepr.EndsWith("0"))
                    textRepr += sep;

                int sigCt = 0;
                int trailingZeroCt = 0;
                bool metDP = false;
                bool started = false;
                int zeroCt = 0;

                for (int i = 0; i < textRepr.Count(); i += 1)
                {
                    char c = textRepr[i];
                    if (char.IsDigit(c))
                    {
                        if (started)
                        {
                            if (c == '0' && !metDP)
                            {
                                trailingZeroCt++;
                            }
                            else
                            {
                                sigCt += trailingZeroCt;
                                trailingZeroCt = 0;
                                sigCt++;
                            }
                        }
                        else if (c != '0')
                        {
                            started = true;
                            sigCt = 1;
                        }
                        else
                        {
                            zeroCt += 1;
                        }
                    }
                    else if (c.ToString() == sep)
                    {
                        sigCt += trailingZeroCt;
                        trailingZeroCt = 0;
                        metDP = true;
                    }
                }

                if (sigCt == 0) return zeroCt;
                return sigCt;
            }

            /// <summary>
            /// Get the sign of a number
            /// </summary>
            public BigDecimal Sgn(BigDecimal value)
            {
                if (value > 0) return 1;
                else if (value < 0) return -1;
                else return 0;
            }

            /// <summary>
            /// Get the absolute value of a number, determinant of a matrix, magnitude of a vector
            /// </summary>
            public object Abs(object value)
            {
                if (value is double)
                {
                    return Math.Abs((double)(value));
                }
                else if (value is BigDecimal)
                {
                    BigDecimal bdec = (BigDecimal)value;
                    if (bdec >= 0)
                    {
                        return bdec;
                    }
                    else
                    {
                        return -bdec;
                    }
                }
                else if (value is System.Numerics.Complex)
                {
                    return ((System.Numerics.Complex)value).Magnitude;
                }
                else if (value is IEnumerable<Reference>)
                {
                    // Find the determinant. If we cannot, try getting the magnitude. If that still fails, get the length of the list
                    try
                    {
                        return Det((IEnumerable<Reference>)value);
                    }
                    catch
                    {
                        try
                        {
                            return Magnitude((IEnumerable<Reference>)value);
                        }
                        catch
                        {
                            return ((IEnumerable<Reference>)value).Count();
                        }
                    }
                }
                else if (value is IDictionary<Reference, Reference>)
                {
                    return ((IDictionary<Reference, Reference>)value).Count;
                }
                else if (value is string)
                {
                    return Convert.ToString(value).Length;
                }
                else
                {
                    return 0;
                }
            }

            /// <summary>
            /// Get the natural logarithm of a number
            /// </summary>
            public object Ln(object value)
            {
                if (value is double)
                {
                    return Math.Log((double)(value));
                }
                else if (value is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Log((System.Numerics.Complex)value);
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Get the base [base] logarithm of a number. If base is not specified, defaults to 10.
            /// </summary>
            public object Log(object value, object @base = null)
            {
                if (@base == null) @base = 10;

                if (@base is BigDecimal) @base = (double)((BigDecimal)@base);
                if (@base is int) @base = (double)((int)@base);

                if (value is double)
                {
                    return Math.Log((double)(value), (double)(@base));
                }
                else if (value is BigDecimal)
                {
                    BigDecimal orig = (BigDecimal)value;
                    if (orig < 0)
                        throw new MathException("Logarithms for non-positive numbers are undefined.");
                    if ((double)(@base) == 10 && _eval.SignificantMode)
                    {
                        BigDecimal bn = new BigDecimal(Math.Round(Math.Log((double)((BigDecimal)value), (double)(@base)), orig.SigFigs));
                        bn.SigFigs = bn.HighestDigit() + orig.SigFigs + 1;
                        return bn;
                    }
                    else
                    {
                        return Math.Log((double)(orig), (double)(@base));
                    }
                }
                else if (value is System.Numerics.Complex)
                {
                    return System.Numerics.Complex.Log((System.Numerics.Complex)value, (double)(@base));
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Get the base 2 logarithm of a number
            /// </summary>
            public object Log2(object value)
            {
                return this.Log(value, 2);
            }

            /// <summary>
            /// Get the base 2 logarithm of a number
            /// </summary>
            public object Lg(object value)
            {
                return this.Log2(value);
            }

            /// <summary>
            /// Get the modulo of two numbers
            /// </summary>
            public BigDecimal Modulo(BigDecimal value1, BigDecimal value2)
            {
                return (value1 % value2 + value2) % value2;
            }

            /// <summary>
            /// Swap two references to objects
            /// </summary>
            public Reference[] Swap(Reference value1, Reference value2)
            {
                object t = value1.GetValue();
                value1.SetValue(value2.GetValue());
                value2.SetValue(t);
                return new Reference[] { value1, value2 };
            }

            /// <summary>
            /// Returns the mean of a set
            /// </summary>
            /// <param name="value1"></param>
            /// <returns></returns>
            public BigDecimal Average(object value1)
            {
                int ct = 1;
                BigDecimal res = RecursiveComputeLst(new Reference[] { new Reference(value1) },
                    new Func<BigDecimal, BigDecimal, BigDecimal>((BigDecimal a, BigDecimal b) =>
                 {
                     ct += 1;
                     return a + b;
                 }));
                return res / ct;
            }

            /// <summary>
            /// Returns the mean of a set (alias for average)
            /// </summary>
            /// <param name="value1"></param>
            /// <returns></returns>
            public BigDecimal Mean(object value1)
            {
                return Average(value1);
            }

            /// <summary>
            /// Returns the sum of all items in a set/matrix (real only)
            /// </summary>
            /// <param name="value1"></param>
            /// <returns></returns>
            public BigDecimal Sum(object value1)
            {
                BigDecimal res = RecursiveComputeLst(new Reference[] { new Reference(value1) },
                    new Func<BigDecimal, BigDecimal, BigDecimal>((BigDecimal a, BigDecimal b) =>
                 {
                     return a + b;
                 }));

                return res;
            }

            /// <summary>
            /// Returns the median value of a list. If the number of elements is even, returns the average of the middle two elements.
            /// </summary>
            /// <param name="value1"></param>
            /// <returns></returns>
            public double Median(object value1)
            {
                List<double> lst = new List<double>();
                if (value1 is IEnumerable<Reference>)
                {
                    IEnumerable<Reference> tmp = (IEnumerable<Reference>)value1;
                    foreach (Reference r in tmp)
                    {
                        lst.Add((double)(r.Resolve()));
                    }
                }
                else if (value1 is Reference[])
                {
                    IEnumerable<Reference> tmp = ((Reference[])value1).ToList();
                    foreach (Reference r in tmp)
                    {
                        lst.Add((double)(r.Resolve()));
                    }
                }
                else if (value1 is BigDecimal || value1 is double)
                {
                    return (double)(value1);
                }
                else
                {
                    return double.NaN;
                }
                lst.Sort();
                if (lst.Count % 2 == 1)
                {
                    return lst[Int(lst.Count / 2)];
                }
                else
                {
                    return lst[Int(lst.Count / 2)] / 2 + lst[Int(lst.Count / 2 - 1)] / 2;
                }
            }

            /// <summary>
            /// Returns the mode of the list. If there are multiple modes (aka. no mode), returns undefined (NaN)
            /// </summary>
            /// <param name="value1"></param>
            /// <returns></returns>
            public double Mode(object value1)
            {
                List<double> lst = new List<double>();
                Dictionary<double, int> count = new Dictionary<double, int>();
                Dictionary<int, int> countfreq = new Dictionary<int, int>();

                if (value1 is IEnumerable<Reference>)
                {
                    IEnumerable<Reference> tmp = (IEnumerable<Reference>)value1;
                    foreach (Reference r in tmp)
                    {
                        lst.Add((double)(r.Resolve()));
                    }
                }
                else if (value1 is Reference[])
                {
                    IEnumerable<Reference> tmp = ((Reference[])value1).ToList();
                    foreach (Reference r in tmp)
                    {
                        lst.Add((double)(r.Resolve()));
                    }
                }
                else if (value1 is BigDecimal || value1 is double)
                {
                    return (double)(value1);
                }
                else
                {
                    return double.NaN;
                }

                if (lst.Count == 0)
                    return double.NaN;

                count[0] = 0;
                double highCount = 0;
                foreach (double item in lst)
                {
                    double x = Math.Round(item, 10);
                    if (!count.ContainsKey(x))
                        count[x] = 0;
                    if (!countfreq.ContainsKey(count[x]))
                        countfreq[count[x]] = 0;
                    countfreq[count[x]] -= 1;
                    count[x] += 1;
                    if (!countfreq.ContainsKey(count[x]))
                        countfreq[count[x]] = 0;
                    countfreq[count[x]] += 1;
                    if (count[x] > count[highCount])
                    {
                        highCount = x;
                    }
                }

                if (countfreq[count[highCount]] > 1)
                {
                    return double.NaN;
                    // nore than one mode
                }
                else
                {
                    return highCount;
                    // found!
                }
            }

            public BigDecimal Min(object value1, BigDecimal? value2 = null)
            {
                if (value1 == null) value1 = BigDecimal.Undefined;
                if (value2 == null) value2 = BigDecimal.Undefined;
                return RecursiveComputeLst(new Reference[]{
                new Reference(value1),
                new Reference((BigDecimal)value2)
            }, (BigDecimal a, BigDecimal b) => {
                if (a.IsUndefined) return b;
                if (b.IsUndefined) return a;
                return a < b ? a : b;
            });
            }

            public BigDecimal Max(object value1, BigDecimal? value2 = null)
            {
                    if (value1 == null) value1 = BigDecimal.Undefined;
                    if (value2 == null) value2 = BigDecimal.Undefined;
                    return RecursiveComputeLst(new Reference[]{
                   new Reference( value1),
                   new Reference((BigDecimal)value2)
                }, (BigDecimal a, BigDecimal b) =>
                {
                    if (a.IsUndefined) return b;
                    if (b.IsUndefined) return a;
                    return a > b ? a : b;
                }
                );
            }

            private BigDecimal RecursiveComputeLst(Reference[] list, Func<BigDecimal, BigDecimal, BigDecimal> func)
            {
                try {
                    if (list.Length == 0)
                        return double.NaN;

                    object first = list[0].Resolve();
                    if (first is IEnumerable<Reference> || first is IList<Reference> || first is List<Reference>)
                    {
                        first = RecursiveComputeLst(new List<Reference>((IEnumerable<Reference>)first).ToArray(), func);
                    }
                    else if (first is Reference[])
                    {
                        first = RecursiveComputeLst(new List<Reference>((Reference[])first).ToArray(), func);
                    }
                    while (first is Reference) first = ((Reference)first).GetValue();

                    if (!(first is double || first is BigDecimal))
                        return double.NaN;

                    BigDecimal result;
                    if (first is double)
                        result = (double)(first);
                    else
                        result = (BigDecimal)(first);
                    for (int i = 1; i <= list.Length - 1; i++)
                    {
                        object obj = list[i].Resolve();

                        if (obj is IEnumerable<Reference> || obj is IList<Reference>)
                        {
                            obj = RecursiveComputeLst(((IEnumerable<Reference>)obj).ToArray(), func);
                        }
                        else if (obj is Reference[])
                        {
                            obj = RecursiveComputeLst((Reference[])obj, func);
                        }

                        while (obj is Reference) obj = ((Reference)obj).GetValue();

                        if (obj is BigDecimal)
                        {
                            if (((BigDecimal)obj).IsUndefined) continue;
                            result = func(result, (BigDecimal)(obj));
                        }
                        else if (obj is double)
                        {
                            if (double.IsNaN((double)obj))
                                continue;
                            result = func(result, (double)(obj));
                        }
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return 1;
                }
            }

            // calculus
            public double Dydx(Lambda func, double x = double.NaN)
            {
                if (func.Args.Count() != 1)
                    throw new SyntaxException("Differentiated function must have one parameter");
                if (double.IsNaN(x))
                    x = (double)(_eval.GetVariableRef(func.Args.ElementAt(0)).Resolve());
                return Derivative(func, x);
            }
            public double DNydxN(Lambda func, double n, double x = double.NaN)
            {
                if (func.Args.Count() != 1)
                    throw new SyntaxException("Differentiated function must have one parameter");
                if (double.IsNaN(x))
                    x = (double)(_eval.GetVariableRef(func.Args.ElementAt(0)).Resolve());
                if (n > 0)
                {
                    if (CmpDbl(n, 1) > 0)
                    {
                        for (int i = 1; i <= Int(Math.Floor(n)) - 1; i++)
                        {
                            string newFn = func.ToString();
                            if (newFn.StartsWith("`"))
                            {
                                newFn = newFn.Trim('`');
                            }
                            else
                            {
                                newFn = newFn + "()";
                            }
                            func = new Lambda("`derivative(" + '\'' + newFn + '\'' + ")`");
                        }
                    }
                    return Derivative(func, x);
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Take the derivative of a function at x
            /// </summary>
            public double Derivative(Lambda func, double x = double.NaN)
            {
                if (func.Args.Count() != 1)
                    throw new SyntaxException("Differentiated function must have one parameter");
                if (double.IsNaN(x))
                    x = (double)(_eval.GetVariableRef(func.Args.ElementAt(0)).Resolve());
                BigDecimal l = (BigDecimal)func.Execute(_eval, new object[] { x - 0.0001 });
                BigDecimal r = (BigDecimal)func.Execute(_eval, new object[] { x + 0.0001 });
                return Math.Round((double)(r - l) / 0.0002, 5);
            }

            // integration
            /// <summary>
            /// Takes the definite integral of a function between a and b
            /// </summary>
            public double Integral(Lambda func, double a, double b = double.NaN)
            {
                if (func.Args.Count() != 1)
                    throw new SyntaxException("Integrated function must have one parameter");
                // use simpson's rule by default
                return IntegralSimpson(func, a, b);
            }

            /// <summary>
            ///  Integral estimation with simpson's rule
            /// </summary>
            public double IntegralSimpson(Lambda func, double a, double b = double.NaN)
            {
                if (func.Args.Count() != 1)
                    throw new SyntaxException("Integrated function must have one parameter");
                if (double.IsNaN(b))
                    b = (double)(_eval.GetVariableRef(func.Args.ElementAt(0)).Resolve());

                if (CmpDbl(a, b) == 0)
                    return 0;
                decimal stepx = Convert.ToDecimal(b - a) / 2500;
                decimal res = 0;
                decimal sw = 1;
                for (decimal cx = Convert.ToDecimal(a); cx <= Convert.ToDecimal(b) - stepx; cx += stepx)
                {
                    res += sw * Convert.ToDecimal(func.Execute(_eval, new object[] { cx }));
                    if (sw == 2 || sw == 1)
                    {
                        sw = 4;
                    }
                    else
                    {
                        sw = 2;
                    }
                }
                res += Convert.ToDecimal(func.Execute(_eval, new object[] { b }));
                return Math.Round((double)(res / 3 * stepx), 5);
            }

            /// <summary>
            ///  Integral estimation with trapezoid sums
            /// </summary>
            public double IntegralTrapezoid(Lambda func, double a, double b = double.NaN)
            {
                if (func.Args.Count() != 1)
                    throw new SyntaxException("Integrated function must have one parameter");
                if (double.IsNaN(b))
                    b = (double)(_eval.GetVariableRef(func.Args.ElementAt(0)).Resolve());

                if (CmpDbl(a, b) == 0)
                    return 0;
                decimal stepx = Convert.ToDecimal(b - a) / 25000;
                decimal res = 0;
                decimal py = Convert.ToDecimal(func.Execute(_eval, new object[] { a }));
                for (decimal cx = Convert.ToDecimal(a) + stepx; cx <= Convert.ToDecimal(b); cx += stepx)
                {
                    decimal cy = Convert.ToDecimal(func.Execute(_eval, new object[] { cx }));
                    res += (py + (cy - py) / 2) * stepx;
                    py = cy;
                }
                return Math.Round((double)res, 5);
            }

            /// <summary>
            ///  Integral estimation with midpoint sums
            /// </summary>
            public double IntegralMidpoint(Lambda func, double a, double b = double.NaN)
            {
                return IntegralRiemann(func, a, b, 0);
            }

            /// <summary>
            /// integral estimation with left riemann sums
            /// </summary>
            public double IntegralLeft(Lambda func, double a, double b = double.NaN)
            {
                return IntegralRiemann(func, a, b, -1, 50000);
            }

            /// <summary>
            /// integral estimation with right riemann sums
            /// </summary>
            public double IntegralRight(Lambda func, double a, double b = double.NaN)
            {
                return IntegralRiemann(func, a, b, 1, 50000);
            }

            /// <summary>
            /// helper function for integral estimations with riemann sums
            /// </summary>
            private double IntegralRiemann(Lambda func, double a, double b, int offset, int intervals = 10000)
            {
                if (double.IsNaN(b))
                    b = (double)(_eval.GetVariableRef(func.Args.ElementAt(0)).Resolve());

                if (CmpDbl(a, b) == 0)
                    return 0;
                decimal stepx = Convert.ToDecimal(b - a) / intervals;
                decimal res = 0;
                for (decimal cx = Convert.ToDecimal(a) + stepx / 2 * (offset + 1); cx <= Convert.ToDecimal(b) + stepx / 2 * (offset - 1); cx += stepx)
                {
                    res += stepx * Convert.ToDecimal(func.Execute(_eval, new object[] { cx }));
                }
                return Math.Round((double)res, 5);
            }
            // end calculus

            /// <summary>
            /// Summation: takes the sum of expression over the range between a and b, inclusive
            /// </summary>
            public BigDecimal Sigma(Lambda func, double a, double b, double step = 1)
            {

                try
                {
                    // step cannot be 0 or in the reverse direction
                    if (CmpDbl(step, 0) == 0 || (b - a) / step < 0 || double.IsInfinity(step) || double.IsNaN(step))
                        return double.NaN;
                    if (b < a)
                        step = -1;
                    BigDecimal sum = 0;
                    for (double i = a; i <= b; i += step)
                    {
                        sum += (BigDecimal)func.Execute(_eval, new object[] { i });
                    }
                    return sum;
                }
                catch
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Product: takes the product of expression over the range between l and r, inclusive
            /// Alias for sigma()
            /// </summary>
            public BigDecimal Product(Lambda func, double a, double b, double step = 1)
            {
                try
                {
                    // step cannot be 0 or in the reverse direction
                    if (CmpDbl(step, 0) == 0 || (b - a) / step < 0 || double.IsInfinity(step) || double.IsNaN(step))
                        return double.NaN;
                    if (b < a)
                        step = -1;
                    BigDecimal prod = 1;
                    for (double i = a; i <= b; i += step)
                    {
                        prod *= (BigDecimal)func.Execute(_eval, new object[] { i });
                    }
                    return prod;
                }
                catch
                {
                    return double.NaN;
                }
            }

            // encryption (just for fun)
            // function for creating caesar ciphers
            public string Caesar(string value, double shift)
            {
                value = value.ToUpper();
                StringBuilder ret = new StringBuilder();
                foreach (char c in value)
                {
                    if (c == ' ')
                    {
                        ret.Append(" ");
                    }
                    else
                    {
                        int cid = (int)c + Int(shift);
                        if (cid > (int)'Z')
                            cid -= 26;
                        if (cid < (int)'A')
                            cid += 26;
                        ret.Append((char)cid);
                    }
                }
                return ret.ToString();
            }

            /// <summary>
            /// Encodes a string into Xecryption (very bad encryption)
            /// </summary>
            public string EncodeXecryption(string value, double pwd = 0)
            {
                string res = "";
                Random rand = new Random();
                foreach (char c in value)
                {
                    int val = (int)(c) + Int(Math.Truncate(pwd));
                    double x = rand.NextDouble();
                    double y = rand.NextDouble();
                    double z = rand.NextDouble();
                    double sum = x + y + z;
                    int xi = Int(val * (x / sum));
                    int yi = Int(val * (y / sum));
                    int zi = Int(val * (z / sum));
                    if (xi + yi + zi != val)
                    {
                        int modify = rand.Next(0, 3);
                        if (modify == 0)
                        {
                            xi += val - xi - yi - zi;
                        }
                        else if (modify == 1)
                        {
                            yi += val - xi - yi - zi;
                        }
                        else
                        {
                            zi += val - xi - yi - zi;
                        }
                    }
                    res += string.Format(".{0}.{1}.{2}", xi, yi, zi);
                }
                return res;
            }

            /// <summary>
            /// Decodes a string from Xecryption (very bad encryption)
            /// </summary>
            public string DecodeXecryption(string value, double pwd = -1)
            {
                string res = "";
                Random rand = new Random();
                string[] spl = value.Trim(new[]{
                '.',
                '\r',
                '\n',
                ' '
            }).Split('.');
                if (spl.Length < 3)
                    return "";
                if (pwd == -1)
                {
                    int totalini = int.Parse(spl[0]) + int.Parse(spl[1]) + int.Parse(spl[2]);
                    string subopt = "";
                    for (int t = totalini - 126; t <= totalini - 10; t++)
                    {
                        string text = DecodeXecryption(value, t);
                        for (int i = 0; i <= text.Length - 1; i++)
                        {
                            int ascii = (int)(text[i]);
                            if (ascii >= (int)('{') || (ascii < (int)(' ') && ascii != (int)('\n') && ascii != (int)('\r')))
                            {
                                text = "";
                                break;
                            }
                        }
                        if (string.IsNullOrEmpty(text))
                            continue;
                        for (int i = 0; i <= text.Length - 1; i++)
                        {
                            int ascii = (int)(text[i]);
                            if (((ascii >= (int)(' ') && ascii < (int)('#')) || (ascii >= (int)(',') && ascii <= (int)('.')) || (ascii >= (int)(':') && ascii < (int)('<')) || ascii == (int)('?')))
                            {
                                if (string.IsNullOrEmpty(subopt))
                                    subopt = text + Environment.NewLine;
                                text = "";
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(text))
                        {
                            return text;
                        }
                    }
                    if (!string.IsNullOrEmpty(subopt))
                        return subopt;
                    return "XEcryption: Auto Decryption Failed";
                }
                for (int i = 2; i <= spl.Length - 1; i += 3)
                {
                    int total = int.Parse(spl[i - 2]) + int.Parse(spl[i - 1]) + int.Parse(spl[i]) - Int(Math.Truncate(pwd));
                    res += (char)(total);
                }
                return res;
            }

            // encryption (actual)

            /// <summary>
            /// Encode a texting into base 64
            /// </summary>
            /// <param name="value"></param>
            /// <returns></returns>
            public string EncodeBase64(string value)
            {
                return Convert.ToBase64String(Encoding.ASCII.GetBytes(value));
            }
            public string Eb64(string value)
            {
                return EncodeBase64(value);
            }

            /// <summary>
            /// Decode a texting from base 64
            /// </summary>
            /// <param name="value"></param>
            /// <returns></returns>
            public string DecodeBase64(string value)
            {
                return Encoding.ASCII.GetString(Convert.FromBase64String(value));
            }
            public string Db64(string value)
            {
                return DecodeBase64(value);
            }

            /// <summary>
            /// Encrypts a texting using the AES/Rijndael symmetric key algorithm using the specified password
            /// generates a random hash and IV and appends them before the actual cipher, so they can be used when decrypting
            /// modified from http://www.obviex.com/samples/encryption.aspx
            /// </summary>
            /// <param name="value">The value to encrypt. Automatically converted to a texting.</param>
            /// <param name="pwd">The password</param>
            /// <param name="keySize">The size of the key (default is 256, please use a power of 2)</param>
            /// <returns></returns>
            public string EncryptAES(object value, string pwd, double keySize = 256)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value.ToString());
                RijndaelManaged symmetricKey = null;
                symmetricKey = new RijndaelManaged();
                symmetricKey.Mode = CipherMode.CBC;

                byte[] saltBytes = new byte[33];
                RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
                rand.GetNonZeroBytes(saltBytes);

                Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(pwd, saltBytes, 2);

                byte[] keyBytes = null;
                keyBytes = password.GetBytes(Int(keySize / 8));

                symmetricKey.GenerateIV();
                ICryptoTransform encryptor = null;
                encryptor = symmetricKey.CreateEncryptor(keyBytes, symmetricKey.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        // Start encrypting.
                        cryptoStream.Write(bytes, 0, bytes.Length);
                        cryptoStream.FlushFinalBlock();

                        byte[] cipherTextBytes = memoryStream.ToArray();
                        string ivTxt = Convert.ToBase64String(symmetricKey.IV);

                        ivTxt = ivTxt.Remove(ivTxt.Count() - 2);
                        // save two bytes
                        return Convert.ToBase64String(saltBytes) + ivTxt + Convert.ToBase64String(cipherTextBytes);
                    }
                }
            }

            /// <summary>
            /// Decode a AES/Rijndael-encrypted texting using the specified password
            /// modified from http://www.obviex.com/samples/encryption.aspx
            /// </summary>
            /// <param name="value">The encrypted texting</param>
            /// <param name="pwd">The password</param>
            /// <param name="keySize">The size of the key (default is 256, please use a power of 2)</param>
            /// <returns></returns>
            public string DecryptAES(string value, string pwd, double keySize = 256)
            {

                string salt = value.Remove(44);
                string iv = value.Remove(66).Substring(44) + "==";
                string cipher = value.Substring(66);

                byte[] saltBytes = Convert.FromBase64String(salt);
                byte[] ivBytes = Convert.FromBase64String(iv);
                byte[] cipherTextBytes = Convert.FromBase64String(cipher);

                Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(pwd, saltBytes, 2);

                byte[] keyBytes = null;
                keyBytes = password.GetBytes(Int(keySize / 8));

                RijndaelManaged symmetricKey = null;
                symmetricKey = new RijndaelManaged();

                symmetricKey.Mode = CipherMode.CBC;

                ICryptoTransform decryptor = null;
                decryptor = symmetricKey.CreateDecryptor(keyBytes, ivBytes);

                using (MemoryStream MemoryStream = new MemoryStream(cipherTextBytes))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(MemoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        byte[] plainTextBytes = null;
                        plainTextBytes = new byte[cipherTextBytes.Length + 1];

                        int decryptedByteCount = 0;
                        decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);

                        return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                    }
                }
            }

            public string Hash(object value)
            {
                return SHA2Hash(value);
            }

            public double IntHash(object value)
            {
                return value.GetHashCode();
            }

            public string MD5Hash(object value)
            {
                MD5 md5Crypt = System.Security.Cryptography.MD5.Create();
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(ObjectTypes.DetectType(value.ToString(), true).ToString());
                byte[] hash = md5Crypt.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i <= hash.Length - 1; i++)
                {
                    sb.Append(hash[i].ToString("X2"));
                }
                return sb.ToString();
            }

            public string SHA1Hash(object value)
            {
                SHA1 sha1Crypt = System.Security.Cryptography.SHA1.Create();
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(ObjectTypes.DetectType(value.ToString(), true).ToString());
                byte[] hash = sha1Crypt.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i <= hash.Length - 1; i++)
                {
                    sb.Append(hash[i].ToString("X2"));
                }
                return sb.ToString();
            }

            public string SHA2Hash(object value)
            {
                SHA256 sha256Crypt = System.Security.Cryptography.SHA256.Create();
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(ObjectTypes.DetectType(value.ToString(), true).ToString());
                byte[] hash = sha256Crypt.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i <= hash.Length - 1; i++)
                {
                    sb.Append(hash[i].ToString("X2"));
                }
                return sb.ToString();
            }

            public string RandomPassword(double length = 8, bool allowSymbols = false)
            {
                StringBuilder sb = new StringBuilder();
                Random rand = new Random();
                List<char> ltrs = new List<char>();
                if (allowSymbols)
                {
                    ltrs.AddRange(new[]{
                    '=',
                    '+',
                    '$',
                    '#',
                    '@',
                    '!',
                    '%',
                    '^',
                    '&'
                });
                }
                for (int i = (int)('0'); i <= (int)('9'); i++)
                {
                    ltrs.Add((char)(i));
                }
                for (int i = (int)('a'); i <= (int)('z'); i++)
                {
                    ltrs.Add((char)(i));
                }
                for (int i = (int)('A'); i <= (int)('Z'); i++)
                {
                    ltrs.Add((char)(i));
                }

                for (int i = 1; i <= Int(length); i++)
                {
                    sb.Append(ltrs[rand.Next(0, ltrs.Count)]);
                }

                return sb.ToString();
            }

            // base conversion
            public string CBase(string value, double frombase, double @base)
            {
                string ret = "";
                double rm = 0;
                double qo = 0;
                for (int i = 0; i <= value.Length - 1; i++)
                {
                    char dgtc = value[value.Length - 1 - i];
                    double dgt = 0;
                    if (char.IsNumber(dgtc))
                    {
                        dgt = double.Parse(dgtc.ToString());
                    }
                    else if (char.IsUpper(dgtc))
                    {
                        dgt = (int)(dgtc) - (int)('A') + 10;
                    }
                    else if (char.IsLower(dgtc))
                    {
                        dgt = (int)(dgtc) - (int)('a') + 36;
                    }
                    else
                    {
                        dgt = 0;
                    }
                    if (dgt >= frombase)
                        return "Undefined";
                    qo += dgt * Math.Pow(frombase, i);
                }
                while (CmpDbl(qo, 0) != 0)
                {
                    rm = qo % @base;
                    qo = Math.Floor(qo / @base);
                    if (rm >= 36)
                    {
                        ret = (char)(Int(rm) + (int)('a') - 36) + ret;
                    }
                    else if (rm >= 10)
                    {
                        ret = (char)(Int(rm) + (int)('A') - 10) + ret;
                    }
                    else
                    {
                        ret = Int(rm) + ret;
                    }
                }
                if (string.IsNullOrEmpty(ret))
                {
                    if (@base == 64)
                    {
                        return "A";
                    }
                    else
                    {
                        return "0";
                    }
                }
                return ret;
            }

            // binary repr   
            public string Bin(double value)
            {
                return Convert.ToString(Int(Math.Truncate(value)), 2);
            }
            // octal repr
            public string Oct(double value)
            {
                return Convert.ToString(Int(Math.Truncate(value)), 7);
            }
            // hex repr   
            public string Hex(double value)
            {
                return HexU(value);
            }
            public string HexL(double value)
            {
                return Convert.ToString(Int(Math.Truncate(value)), 16);
            }
            public string HexU(double value)
            {
                return Convert.ToString(Int(Math.Truncate(value)), 16).ToUpper();
            }

            public bool IsUndefined(double val)
            {
                return double.IsNaN(val);
            }

            // dates
            public object DateTime(object first, double month = 0, double day = 1, double hour = 0, double minute = 0, double second = 0, double ms = 0)
            {
                if (first is string)
                {
                    return new ObjectTypes.DateTime(first.ToString()).GetValue();
                }
                else if (first is double || first is BigDecimal || first is int)
                {
                    if (month == 0)
                    {
                        TimeSpan ts = new TimeSpan(Convert.ToInt64(first));
                        if (ts.Days > ObjectTypes.DateTime.TIMESPAN_DIVIDER)
                        {
                            return ObjectTypes.DateTime.BASE_DATE.Add(ts);
                        }
                        else
                        {
                            return ts;
                        }
                    }
                    else
                    {
                        if (Int((double)(first)) * 365 + Int(month) * 30 + Int(day) <= ObjectTypes.DateTime.TIMESPAN_DIVIDER)
                        {
                            return new TimeSpan(Int((double)(first)) * 365 + Int(month) * 30 + Int(day), Int(hour), Int(minute), Int(second), Int(ms));
                        }
                        else
                        {
                            return new System.DateTime(Int((double)(first)), Int(month), Int(day), Int(hour), Int(minute), Int(second), Int(ms));
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
            public System.DateTime Date(double year, double month = 1, double day = 1)
            {
                return new System.DateTime(Int(year), Int(month), Int(day));
            }
            public TimeSpan Time(double hour = 0, double minute = 0, double second = 0, double millisecond = 0)
            {
                return new TimeSpan(Int(hour), Int(minute), Int(second), Int(millisecond));
            }

            public System.DateTime Now()
            {
                return System.DateTime.Now;
            }
            public System.DateTime UTCNow()
            {
                return System.DateTime.UtcNow;
            }
            public System.DateTime Today()
            {
                return System.DateTime.Today;
            }

            // datetime modification
            public double Year(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).Year;
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Days / 365;
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Years(object dt)
            {
                return Year(dt);
            }

            public double Month(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).Month;
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Days / 30;
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Months(object dt)
            {
                return Month(dt);
            }

            public double Day(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).Day;
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Days;
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Days(object dt)
            {
                return Day(dt);
            }

            public string DayOfWeekName(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).DayOfWeek.ToString();
                }
                return "Unknown";
            }
            public double DayOfWeek(object dt)
            {
                if (dt is System.DateTime)
                {
                    return Int((double)((System.DateTime)dt).DayOfWeek);
                }
                return double.NaN;
            }

            public double Hour(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).Hour;
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Hours;
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Hours(object dt)
            {
                return Hour(dt);
            }

            public double Minute(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).Minute;
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Minutes;
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Minutes(object dt)
            {
                return Minute(dt);
            }

            public double Second(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).Second;
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Seconds;
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Seconds(object dt)
            {
                return Second(dt);
            }

            public double Millisecond(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).Millisecond;
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Seconds;
                }
                else
                {
                    return double.NaN;
                }
            }
            public double Milliseconds(object dt)
            {
                return Second(dt);
            }

            public double Ticks(object dt)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).Ticks;
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Ticks;
                }
                else
                {
                    return double.NaN;
                }
            }

            public object AddYears(object dt, double years)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddYears(Int(years));
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(Int(years) * 365, 0, 0, 0));
                }
                else
                {
                    return null;
                }
            }
            public object AddMonths(object dt, double months)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddMonths(Int(months));
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(Int(months) * 30, 0, 0, 0));
                }
                else
                {
                    return null;
                }
            }
            public object AddWeeks(object dt, double days)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddDays(Int(days) * 7);
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(Int(days) * 7, 0, 0, 0));
                }
                else
                {
                    return null;
                }
            }
            public object AddDays(object dt, double days)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddDays(Int(days));
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(Int(days), 0, 0, 0));
                }
                else
                {
                    return null;
                }
            }
            public object AddHours(object dt, double hours)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddHours(Int(hours));
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(Int(hours), 0, 0));
                }
                else
                {
                    return null;
                }
            }
            public object AddMinutes(object dt, double minutes)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddMinutes(Int(minutes));
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(0, Int(minutes), 0));
                }
                else
                {
                    return null;
                }
            }
            public object AddSeconds(object dt, double seconds)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddSeconds(Int(seconds));
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(0, 0, Int(seconds)));
                }
                else
                {
                    return null;
                }
            }
            public object AddMilliseconds(object dt, double ms)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddMilliseconds(Int(ms));
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(0, 0, 0, 0, Int(ms)));
                }
                else
                {
                    return null;
                }
            }
            public object AddTicks(object dt, double ticks)
            {
                if (dt is System.DateTime)
                {
                    return ((System.DateTime)dt).AddTicks(Int(ticks));
                }
                else if (dt is System.TimeSpan)
                {
                    return ((System.TimeSpan)dt).Add(new TimeSpan(Int(ticks)));
                }
                else
                {
                    return null;
                }
            }

            public double Rand(double min = 0, double max = 1)
            {
                Random r = new Random();
                return r.NextDouble() * (max - min) + min;
            }

            public double RandInt(double min, double max)
            {
                Random rand = new Random();
                return rand.Next(Int(min), Int(max));
            }

            public string GUID()
            {
                return System.Guid.NewGuid().ToString();
            }

            public string UUID()
            {
                return GUID();
            }

            /// <summary>
            /// Compare two double values with the precision
            /// </summary>
            internal int CmpDbl(double v1, double v2, double epsi = 1E-12)
            {
                double diff = Math.Abs(v1 - v2);
                if (diff < epsi)
                {
                    return 0;
                }
                else if (v1 > v2)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }

            /// <summary>
            /// Compare two double values with the precision
            /// </summary>
            internal int CmpDbl(BigDecimal v1, BigDecimal v2, BigDecimal? epsi = null)
            {
                if (epsi == null) epsi = 1e-12;
                BigDecimal diff = (BigDecimal)Abs(v1 - v2);
                if (diff < epsi)
                {
                    return 0;
                }
                else if (v1 > v2)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }

            /// <summary>
            /// Get the length of any collection or string
            /// </summary>
            public double Len(object obj)
            {
                if (obj is string)
                {
                    return Convert.ToString(obj).Length;
                }
                else if (obj is IEnumerable<Reference>)
                {
                    return ((IEnumerable<Reference>)obj).Count();
                }
                else if (obj is Reference[])
                {
                    return ((Reference[])obj).Length;
                }
                else if (obj is Dictionary<object, object>)
                {
                    return ((Dictionary<object, object>)obj).Count();
                }
                else
                {
                    if (obj == null)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }

            /// <summary>
            /// Get the length of a piece of text or a collection
            /// </summary>
            public double Size(object obj)
            {
                return Len(obj);
            }

            /// <summary>
            /// Get the length of a piece of text or a collection
            /// </summary>
            public double Length(object lst)
            {
                return Len(lst);
            }

            /// <summary>
            /// Get the type of the object
            /// </summary>
            public string Type(object obj)
            {
                if (obj == null || (obj is double && double.IsNaN((double)(obj))) || obj is System.DBNull)
                {
                    return "Undefined";
                }
                else if (obj is LinkedList<Reference>)
                {
                    return "LinkedList";
                }
                else if (obj is IEnumerable<Reference>)
                {
                    return "Matrix";
                }
                else if (obj is Reference[])
                {
                    return "Tuple";
                }
                else if (obj is SortedDictionary<Reference, Reference>)
                {
                    return "Set";
                }
                else if (obj is Dictionary<Reference, Reference>)
                {
                    return "HashSet";
                }
                else if (obj is double || obj is float || obj is decimal)
                {
                    return "Number";
                }
                else if (obj is string)
                {
                    return "Text";
                }
                else if (obj is System.DateTime)
                {
                    return "Date";
                }
                else if (obj is bool)
                {
                    return "Boolean";
                }
                else if (obj is ICollection)
                {
                    return "(Matrix/Set/Tuple)";
                }
                else if (obj is Reference)
                {
                    return "Reference of " + Type(((Reference)obj).GetValue());
                }
                else if (obj is Lambda)
                {
                    return "Function";
                }
                else if (obj is ClassInstance)
                {
                    return ((ClassInstance)obj).UserClass.FullName;
                }
                else
                {
                    return obj.GetType().Name;
                }
            }
            public string GetType(object obj)
            {
                return Type(obj);
            }

            /// <summary>
            /// Automatically detect the type of an object and convert it to a char array
            /// </summary>
            /// <param name="chars"></param>
            /// <returns></returns>
            private char[] ToCharArray(object chars)
            {
                if (chars is string)
                {
                    return Convert.ToString(chars).ToCharArray();
                }
                else
                {
                    List<char> lst = new List<char>();
                    IEnumerable<Reference> refLst = null;
                    if (chars is IEnumerable<Reference>)
                    {
                        refLst = (IEnumerable<Reference>)chars;
                    }
                    else if (chars is IDictionary<Reference, Reference>)
                    {
                        refLst = ((IDictionary<Reference, Reference>)chars).Keys;
                    }
                    else
                    {
                        throw new EvaluatorException("Invalid types: expecting list of characters");
                    }
                    foreach (Reference r in refLst)
                    {
                        object res = r.Resolve();
                        if (res is string)
                        {
                            if (Convert.ToString(res).Length != 1)
                                throw new EvaluatorException("Character expected");
                            lst.Add(Convert.ToString(res)[0]);
                        }
                    }
                    return lst.ToArray();
                }
            }

            /// <summary>
            /// Remove all instances of each character from both ends of the string
            /// </summary>
            public string Strip(string text, object chars = null)
            {
                if (chars == null)
                {
                    return text.Trim();
                }
                else
                {
                    return text.Trim(ToCharArray(chars));
                }
            }

            /// <summary>
            /// Remove all instances of each character from the start of the string
            /// </summary>
            public string StripStart(string text, object chars = null)
            {
                if (chars == null)
                {
                    return text.TrimStart();
                }
                else
                {
                    return text.TrimStart(ToCharArray(chars));
                }
            }

            /// <summary>
            /// Remove all instances of each character from the end of the string
            /// </summary>
            public string StripEnd(string text, object chars = null)
            {
                if (chars == null)
                {
                    return text.TrimEnd();
                }
                else
                {
                    return text.TrimEnd(ToCharArray(chars));
                }
            }

            /// <summary>
            /// Replace format {0} in text with 
            /// </summary>
            /// <returns></returns>
            public string Format(object text, object formatPattern)
            {
                return string.Format(text.ToString(), formatPattern);
            }

            /// <summary>
            /// Truncate and convert to integer
            /// </summary>
            internal int Int(double value)
            {
                return Convert.ToInt32(Math.Truncate(value));
            }

            /// <summary>
            /// Truncate and convert to integer
            /// </summary>
            internal int Int(BigDecimal value)
            {
                return Convert.ToInt32((BigDecimal)Truncate(value));
            }

            /// <summary>
            /// Solve a quadratic equation with coefficients a, b, and c
            /// </summary>
            public IEnumerable<Reference> Quadratic(double a, double b, double c)
            {
                double tort = Math.Pow(b, 2) - 4 * a * c;
                HashSet<Reference> resultLst = new HashSet<Reference>();
                if (tort >= 0)
                {
                    resultLst.Add(new Reference((-b + Math.Sqrt(tort)) / (2 * a)));
                    resultLst.Add(new Reference((-b - Math.Sqrt(tort)) / (2 * a)));
                }
                else
                {
                    resultLst.Add(new Reference((-b + System.Numerics.Complex.Sqrt(tort)) / (2 * a)));
                    resultLst.Add(new Reference((-b - System.Numerics.Complex.Sqrt(tort)) / (2 * a)));
                }
                return new List<Reference>(resultLst);
            }

            /// <summary>
            /// Solve a quadratic equation with coefficients a, b, and c (shorthand for quadratic())
            /// </summary>
            public IEnumerable<Reference> Qdtc(double a, double b, double c)
            {
                //shorthand for quadratic
                return Quadratic(a, b, c);
            }

            /// <summary>
            /// Simplify a radical with radicand d and index ind
            /// </summary>
            private string SimpRadical(BigDecimal rdc, BigDecimal ind)
            {
                string sign = "";
                if (rdc < 0)
                {
                    sign = "-";
                }
                long[] rad = SRadical((BigDecimal)Abs(rdc), Int(ind));
                string textbefore = "[" + ind.ToString();
                string textafter = "]";
                if (ind < 3)
                {
                    textbefore = "";
                    textafter = "";
                }
                if (rad[0] == 1)
                {
                    return sign + textbefore + "√" + textafter + rad[1];
                }
                else if (rad[1] == 1)
                {
                    return sign + rad[0];
                }
                else if (rad[1] == 0)
                {
                    return "0";
                }
                else
                {
                    return sign + rad[0] + textbefore + "√" + textafter + rad[1];
                }
            }

            /// <summary>
            /// Internal helper for simplifying radicals
            /// </summary>
            private long[] SRadical(BigDecimal rdc, int ind)
            {
                // The new index
                long newInd = 1;
                // The new radical
                long newRdc = (long)Round(rdc);
                for (int i = 2; i <= 12; i++)
                {
                    for (int j = 12; j >= ind; j += -ind)
                    {
                        long pow = (long)Math.Pow(i, j);
                        long modu = newRdc % pow;

                        if (modu == 0)
                        {
                            newInd *= (long)i * j / ind;
                            newRdc /= pow;
                        }
                    }
                }
                return new[]{
                    newInd,
                    newRdc
                };
            }

            /// <summary>
            /// Convert a double value to a fraction
            /// </summary>
            private string ToFrac(BigDecimal d)
            {
                int sign = Int(Sgn(d));
                BigDecimal[] res = CFrac((BigDecimal)Abs(d), Min(1E-9 * (BigDecimal)Pow(10, Round((BigDecimal)Pow((BigDecimal)Abs(d), 0.1))), 0.001));
                string lft = (sign * res[0]).ToString();
                if (res[1] == 1)
                {
                    return lft;
                }
                else if (res[1] == 0)
                {
                    throw new Exception("Fraction resulted in 0 denominator");
                }
                else if (res[1] < 50000)
                {
                    if (lft.Contains(' ')) lft = '(' + lft + ')';
                    return lft + "/" + res[1];
                }
                else
                {
                    return d.ToString();
                }
            }

            /// <summary>
            /// Internal function for converting a double value to a fraction
            /// </summary>
            private BigDecimal[] CFrac(BigDecimal d, BigDecimal? epsi = null)
            {
                if (epsi == null) epsi = 1E-16;
                BigDecimal n = Floor(d);
                d -= n;
                if (d < epsi)
                {
                    return new[]{
                    n,
                    1
                };
                }
                else if (1 - epsi < d)
                {
                    return new[]{
                    n + 1,
                    1
                };
                }
                BigDecimal lower_n = 0;
                BigDecimal lower_d = 1;

                BigDecimal upper_n = 1;
                BigDecimal upper_d = 1;

                BigDecimal middle_n = 0;
                BigDecimal middle_d = 0;

                int runtimes = 0;
                while (runtimes < 1000000)
                {
                    middle_n = lower_n + upper_n;
                    middle_d = lower_d + upper_d;
                    if (middle_d * (d + epsi) < middle_n)
                    {
                        upper_n = middle_n;
                        upper_d = middle_d;
                    }
                    else if (middle_n < (d - epsi) * middle_d)
                    {
                        lower_n = middle_n;
                        lower_d = middle_d;
                    }
                    else
                    {
                        return new[]{
                        n * middle_d + middle_n,
                        middle_d
                    };
                    }
                    runtimes += 1;
                }
                return new[]{
                d,
                1
            };
            }

            /// <summary>
            /// Tests if the double value is an interger
            /// </summary>
            private bool IsInteger(double d)
            {
                return CmpDbl(d, Math.Round(d), 1E-08) == 0;
            }

            /// <summary>
            /// Tests if the double value is an interger
            /// </summary>
            public bool IsInteger(BigDecimal d)
            {
                return CmpDbl(d, Round(d), 1E-08) == 0;
            }

            /// <summary>
            /// Trinomial factoring Function (needs improvement)
            /// </summary>
            public string TriFact(double a, double b, double c)
            {
                double fact = GCF(GCF(a, b), c);
                if (a < 0)
                {
                    a = -a;
                    b = -b;
                    c = -c;
                    fact = -fact;
                }
                a /= fact;
                b /= fact;
                c /= fact;
                double ac = a * c;
                int stp = 1;
                int fa1 = -1;
                int fa2 = -1;
                bool found = false;
                if ((b < 0))
                    stp = -1;
                for (int i = -Int(b); i <= Int(b); i += stp)
                {
                    //trifact(2,9,-5)
                    if (i * Int(b - i) == Int(ac))
                    {
                        fa1 = i;
                        fa2 = Int(b - i);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return "Not Factorable";
                }
                else
                {
                    int ff1 = Int(GCF(fa1, a));
                    int ff2 = Int(GCF(fa2, a));
                    int fm1 = Int(a / ff1);
                    int fm2 = Int(a / ff2);
                    fa1 /= ff1;
                    fa2 /= ff2;

                    string facttxt = null;
                    if (fact == 1)
                    {
                        facttxt = "(";
                    }
                    else if (fact == -1)
                    {
                        facttxt = "-(";
                    }
                    else
                    {
                        facttxt = fact + "(";
                    }

                    string fm1txt = SignText(fm1);
                    string fm2txt = SignText(fm2);
                    string fa1sgn = fa1 > 0 ? "+" : "";
                    string fa2sgn = fa2 > 0 ? "+" : "";
                    if (fm2txt + fa2sgn + fa2 == fm1txt + fa1sgn + fa1)
                    {
                        return facttxt + fm1txt + "x" + fa1sgn + fa1 + ")^2";
                    }
                    else
                    {
                        return facttxt + fm1txt + "x" + fa1sgn + fa1 + ")(" + fm2txt + "x" + fa2sgn + fa2 + ")";
                    }
                }
            }

            /// <summary>
            /// Convert to scientific notation
            /// </summary>
            /// <param name="value">The double to use</param>
            /// <returns>Scientific notation representation</returns>
            private string Sci(double value)
            {
                if (value == 0)
                    return "0";
                if (double.IsNaN(value) || double.IsInfinity(value))
                    return value.ToString();
                int expo = Int((double)(Log(Math.Abs(value))));
                return value / Math.Pow(10, expo) + " x 10^" + expo;
            }

            /// <summary>
            /// Convert a value to scientific notation
            /// </summary>
            private string Sci(BigDecimal value)
            {
                return value.ToScientific();
            }

            // output modes
            /// <summary>
            /// Output as scientific notation
            /// </summary>
            private string SciO(object value)
            {
                if (value is double)
                {
                    return Sci((double)(value));
                }
                else if (value is BigDecimal)
                {
                    return Sci((BigDecimal)value);
                }
                else
                {
                    return value.ToString();
                }
            }

            /// <summary>
            /// Output directly
            /// </summary>
            private string LineO(object value)
            {
                // use this to see results in linear fashion when in mathio mode
                if (value is double)
                {
                    if (Math.Abs((double)(value)) < 0.0001 || Math.Abs((double)(value)) >= 1E+15)
                    {
                        // switch to scientific for extreme values
                        return SciO(value);
                    }
                    else
                    {
                        return Convert.ToDecimal(value).ToString("F4");
                    }
                }
                else if (value is BigDecimal)
                {
                    return ((BigDecimal)value).ToString();
                }
                else
                {
                    return value.ToString();
                }
            }

            /// <summary>
            /// Output as mathematical notation
            /// </summary>
            private string MathO(object o)
           {
                // use this to see results in mathio fashion when in lineio mode
                if (o is BigDecimal || o is double)
                {
                    BigDecimal value = 0;
                    if (o is BigDecimal)
                    {
                        if (((BigDecimal)o).IsOutsideDispRange())
                        {
                            return ((BigDecimal)o).ToString();
                        }
                        else
                        {
                            value = (BigDecimal)o;
                        }
                    }
                    else
                    {
                        value = (BigDecimal)(double)(o);
                    }
                    value = value.Truncate(12);

                    // eliminate integer and undefined/non-real values.
                    if (value.IsUndefined || IsInteger(value) || value.IsOutsideDispRange())
                    {
                        return value.ToString();
                    }

                    // multiples of PI
                    for (int j = -50; j <= 50; j++)
                    {
                        if (j == 0)
                            continue;
                        for (int k = -20; k <= 20; k++)
                        {
                            string s = SimpStr(value, Math.PI, j, "π", "+", k);
                            if (!string.IsNullOrEmpty(s))
                                return s;
                        }
                    }
                    try
                    {
                        // radicals
                        for (int i = 2; i <= 3; i++)
                        {
                            BigDecimal sq = (BigDecimal)Pow(value, (BigDecimal)i);

                            // ignore excessively large roots
                            if (sq > 10000)
                                break;
                            if (i % 2 == 0)
                                sq *= Sgn(value);
                            if (IsInteger(sq))
                            {
                                return SimpRadical(sq, i);
                            }
                        }
                        for (int i = 2; i <= 3; i++)
                        {
                            for (int j = 1; j <= 40; j++)
                            {
                                BigDecimal sq = (BigDecimal)Pow((value - j), i);
                                if (sq > 15000)
                                    break;
                                // ignore excessively large roots
                                if (i % 2 == 0)
                                    sq *= Sgn(value - j);
                                if (IsInteger(sq))
                                {
                                    return (j + " + " + SimpRadical(sq, i)).Replace("+ -", "- ");
                                }
                                sq = (BigDecimal)Pow((value + j), i);
                                if (sq > 15000)
                                    break;
                                // ignore excessively large roots
                                if (i % 2 == 0)
                                    sq *= Sgn(value + j);
                                if (IsInteger(sq))
                                {
                                    return SimpRadical(sq, i) + " - " + j;
                                }
                            }
                        }
                        for (int i = 2; i <= 3; i++)
                        {
                            for (int j = 0; j <= 40; j++)
                            {
                                for (int k = 2; k <= 40; k++)
                                {
                                    BigDecimal ba = (value * k - j);
                                    if (IsInteger(ba))
                                        continue;
                                    BigDecimal sq = (BigDecimal)Pow(ba, i);
                                    if (sq > 15000)
                                        break;
                                    // ignore excessively large roots
                                    if (i % 2 == 0)
                                        sq *= Sgn(ba);
                                    if (IsInteger(sq))
                                    {
                                        if (j == 0)
                                        {
                                            return (SimpRadical(sq, i)).Replace("+ -", "- ") + "/" + k;
                                        }
                                        else
                                        {
                                            return ("(" + j + " + " + SimpRadical(sq, i)).Replace("+ -", "- ") + ")/" + k;
                                        }
                                    }
                                    sq = (value * k + j);
                                    if (IsInteger(sq))
                                        continue;
                                    //ignore integer base 
                                    sq = (BigDecimal)Pow(sq, i);
                                    if (sq > 15000)
                                        break;
                                    // ignore excessively large roots
                                    if (i % 2 == 0)
                                        sq *= Sgn(value + j);
                                    if (IsInteger(sq))
                                    {
                                        if (j == 0)
                                        {
                                            return SimpRadical(sq, i) + "/" + k;
                                        }
                                        else
                                        {
                                            return "(" + SimpRadical(sq, i) + " - " + j + ")/" + k;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception) { }

                    // fractions of PI
                    for (int j = 1; j <= 100; j++)
                    {
                        for (int k = -20; k <= 20; k++)
                        {
                            string s = SimpStr(value, Math.PI, 1 / j, "π", "+", k);
                            if (!string.IsNullOrEmpty(s))
                                return s;
                            s = SimpStr(value, Math.PI, -1 / j, "π", "+", k);
                            if (!string.IsNullOrEmpty(s))
                                return s;
                        }
                    }

                    // try converting to fraction

                    return ToFrac(value);

                    // return original text
                    //Return value.ToString
                }
                else
                {
                    return o.ToString();
                    //not double, don't simplify
                }
            }

            /// <summary>
            /// Crazy helper function that checks for equality and then returns a symbol to use if equal
            /// </summary>
            private string SimpStr(BigDecimal val, double num, double mul, string symb, string addop = "", int addval = 0, string divop = "", double divval = 1)
            {


                if (CmpDbl(val, (num * mul + addval) / divval) == 0)
                {
                    string addtext = "";
                    string divtext = "";
                    if (!string.IsNullOrEmpty(addop) & addval != 0)
                        addtext = addval + " " + addop + " ";
                    if (divval != 1)
                    {
                        divtext = divop + divval;
                        if (addval != 0 | mul > 1)
                        {
                            addtext = "(" + addtext;
                            divtext = ")" + divtext;
                        }
                    }
                    if ((mul == 1))
                    {
                        return (addtext + symb + divtext).Replace("+ -", "- ");
                    }
                    else if (mul == -1)
                    {
                        return (addtext + "-" + symb + divtext).Replace("+ -", "- ");
                    }
                    else
                    {
                        return (addtext + ToFrac(mul) + symb + divtext).Replace("+ -", "- ");
                    }
                }
                return "";
            }

            /// <summary>
            /// Output using the current output mode. Also deals with data structures.
            /// </summary>
            internal string O(object value)
            {
                // use Undefined instead of NaN
                if (value == null || (value is BigDecimal && ((BigDecimal)value).IsUndefined) || (value is double && double.IsNaN((double)(value))))
                    return "Undefined";

                // reference: display ampersand
                if (value is Reference)
                {
                    return "&" + ((Reference)value).ToString();

                    // lambda: display ampersand if function pointer
                }
                else if (value is Lambda)
                {
                    string result = ((Lambda)value).ToString();
                    if (result.StartsWith("`"))
                        return result;
                    return "&" + result;

                    // text: put quotes
                }
                else if (value is string)
                {
                    return '\'' + Convert.ToString(value) + '\'';
                    // put quotes around textings

                    // numbers: process (detect fractions, etc.)
                }
                else if (value is double || value is BigDecimal)
                {
                    string ret = null;
                    if (_eval.OutputMode == CantusEvaluator.OutputFormat.Math)
                    {
                        ret = MathO(value);
                    }
                    else if (_eval.OutputMode == CantusEvaluator.OutputFormat.Scientific)
                    {
                        ret = SciO(value);
                    }
                    else
                    {
                        ret = LineO(value);
                    }
                    return ret;

                }
                else
                {
                    return DetectType(value).ToString();
                    // other stuff like sets, lists: use type-specific serialization
                }
            }

            /// <summary>
            /// Evaluate an expression
            /// </summary>
            public object Eval(object value)
            {
                object ans = _eval.EvalExprRaw(value.ToString(), true);
                return ans;
            }

            /// <summary>
            /// Loop over a collection and apply an operation
            /// </summary>
            public object Each(object lst, Lambda operation)
            {
                List<Reference> loopLst = new List<Reference>();
                if (lst is IEnumerable<Reference>)
                {
                    loopLst.AddRange((IEnumerable<Reference>)lst);
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    loopLst.AddRange(ToList((IDictionary<Reference, Reference>)lst));
                }
                foreach (Reference r in loopLst)
                {
                    object x = r.GetRefObject();
                    _eval.SetDefaultVariable(new Reference(x));
                    if (x is Reference[])
                    {
                        operation.Execute(_eval, (Reference[])x);
                    }
                    else if (x is Reference)
                    {
                        operation.Execute(_eval, new[] { (Reference)x });
                    }
                    else
                    {
                        operation.Execute(_eval, new[] { new Reference(x) });
                    }
                }
                return lst;
            }

            /// <summary>
            /// Loop over a collection, apply an operation, and replace the original values with the returned values 
            /// </summary>
            public object Select(object lst, Lambda function)
            {
                List<Reference> loopLst = new List<Reference>();
                if (lst is IList<Reference>)
                {
                    IList<Reference> list = (IList<Reference>)lst;
                    for (int i=0; i<list.Count(); i++)
                    {
                        object x = list[i].ResolveObj();
                        _eval.SetDefaultVariable(new Reference(x));
                        object res;
                        if (x is ObjectTypes.Tuple)
                        {
                             res = function.Execute(_eval,
                                 ((Reference[])(((ObjectTypes.Tuple)x).GetValue())).
                                 Select(new Func<Reference, object>(r => r.Resolve())));
                        }
                        else if (x is Matrix)
                        {
                             res = Select(((Matrix)x).GetValue(), function);
                        }
                        else
                        {
                           res = function.Execute(_eval, new[] { x });
                        }
                        if (res is Reference)
                        {
                            list[i] = (Reference)res;
                        }
                        else
                        {
                            list[i] = new Reference(res);
                        }
                    }
                    return list;
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    IDictionary<Reference, Reference> dict = (IDictionary<Reference, Reference>)lst;
                    IDictionary<Reference, Reference> newDict = new SortedDictionary<Reference, Reference>();

                    foreach (Reference k in dict.Keys)
                    {
                        object x = k.GetRefObject();
                        _eval.SetDefaultVariable(new Reference(x));
                        object res;
                        if (x is Reference[])
                        {
                             res= function.Execute(_eval, (Reference[])x);
                        }
                        else if (x is Reference)
                        {
                            res = function.Execute(_eval, new[] { (Reference)x });
                        }
                        else
                        {
                           res = function.Execute(_eval, new[] { new Reference(x) });
                        }
                        if (res is Reference)
                        {
                            newDict[(Reference)res] = dict[k];
                        }
                        else
                        {
                            newDict[new Reference(res)] = dict[k];
                        }
                    }
                    return newDict;
                }
                else
                {
                    return lst;
                }
            }


            /// <summary>
            /// Flatten the matrix to a list
            /// </summary>
            public List<Reference> Flatten(IList<Reference> lst)
            {
                List<Reference> newLst = new List<Reference>();
                for (int i=0; i < lst.Count; ++i)
                {
                    if (lst[i].GetRefObject() is Matrix)
                    {
                        foreach (Reference r in Flatten((List<Reference>)lst[i].GetValue()))
                        {
                            newLst.Add(r);
                        }
                    }
                    else
                    {
                        newLst.Add(lst[i]);
                    }
                }
                return newLst;
            }

            /// <summary>
            /// Loop over a collection and select items that pass a predicate
            /// </summary>
            public List<Reference> Filter(object lst, Lambda predicate)
            {
                List<Reference> loopLst = new List<Reference>();
                if (lst is IEnumerable<Reference>)
                {
                    loopLst.AddRange((IEnumerable<Reference>)lst);
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    loopLst.AddRange(ToList((IDictionary<Reference, Reference>)lst));
                }

                List<Reference> ret = new List<Reference>();
                foreach (Reference r in loopLst)
                {
                    object x = r.GetRefObject();
                    _eval.SetDefaultVariable(new Reference(x));
                    if (x is Reference)
                    {
                        if (IsTrue(predicate.Execute(_eval, new[] { (Reference)x })))
                        {
                            ret.Add(r);
                        }
                    }
                    else
                    {
                        if (IsTrue(predicate.Execute(_eval, new[] { new Reference(x) })))
                        {
                            ret.Add(new Reference(x));
                        }
                    }
                }
                return ret;
            }

            public List<Reference> FilterIndex(object lst, Lambda predicate)
            {
                List<Reference> loopLst = new List<Reference>();
                if (lst is IEnumerable<Reference>)
                {
                    loopLst.AddRange((IEnumerable<Reference>)lst);
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    loopLst.AddRange(ToList((IDictionary<Reference, Reference>)lst));
                }

                List<Reference> ret = new List<Reference>();
                for (int i = 0; i< loopLst.Count; i++)
                {
                    object x = loopLst[i].GetRefObject();
                    _eval.SetDefaultVariable(new Reference(x));
                    if (IsTrue(predicate.Execute(_eval, new[] { new Reference(i) })))
                    {
                        ret.Add(loopLst[i]);
                    }
                }
                return ret;
            }

            /// <summary>
            /// Loop over a collection and select items that do not pass a predicate
            /// </summary>
            public List<Reference> Exclude(object lst, Lambda predicate)
            {
                List<Reference> loopLst = new List<Reference>();
                if (lst is IEnumerable<Reference>)
                {
                    loopLst.AddRange((IEnumerable<Reference>)lst);
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    loopLst.AddRange(ToList((IDictionary<Reference, Reference>)lst));
                }

                List<Reference> ret = new List<Reference>();
                foreach (Reference r in loopLst)
                {
                    object x = r.GetRefObject();
                    _eval.SetDefaultVariable(new Reference(x));
                    if (x is Reference)
                    {
                        if (!IsTrue(predicate.Execute(_eval, new[] { (Reference)x })))
                        {
                            ret.Add((Reference)x);
                        }
                    }
                    else
                    {
                        if (!IsTrue(predicate.Execute(_eval, new[] { new Reference(x) })))
                        {
                            ret.Add(new Reference(x));
                        }
                    }
                }
                return ret;
            }

            /// <summary>
            /// Loop over a collection and return the first item that passes a predicate, or undefined if nothing does.
            /// </summary>
            public object Get(object lst, Lambda predicate)
            {
                List<Reference> loopLst = new List<Reference>();
                if (lst is IEnumerable<Reference>)
                {
                    loopLst.AddRange((IEnumerable<Reference>)lst);
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    loopLst.AddRange(ToList((IDictionary<Reference, Reference>)lst));
                }

                foreach (Reference r in loopLst)
                {
                    object x = r.GetRefObject();
                    _eval.SetDefaultVariable(new Reference(x));
                    if (x is Reference)
                    {
                        if (IsTrue(predicate.Execute(_eval, new[] { (Reference)x })))
                        {
                            return x;
                        }
                    }
                    else
                    {
                        if (IsTrue(predicate.Execute(_eval, new[] { new Reference(x) })))
                        {
                            return x;
                        }
                    }
                }
                return double.NaN;
            }

            /// <summary>
            /// Loop over a list, starting at index 0 and incrementing the index by interval each time. Adds the item at
            /// each index to a new list and returns the new list when done.
            /// </summary>
            public object Every(List<Reference> lst, BigDecimal interval)
            {
                List<Reference> newLst = new List<Reference>();
                for (BigDecimal i=0; i<lst.Count; i+=interval)
                {
                    newLst.Add(lst[Int((double)i)]);
                }
                return newLst;
            }

            /// <summary>
            /// Ternary operator: if(condition, [true], [false])
            /// </summary>
            public object If(object condition, object a, object b)
            {
                if (IsTrue(Eval(condition)))
                    return a;
                else
                    return b;
            }

            // lines and points for graphing
            public double LineSeg(double x1, double y1, double x2, double y2)
            {
                double var = (double)(_eval.GetVariable("x"));
                if ((var < x1 && var < x2) || (var > x1 && var > x2))
                {
                    return double.NaN;
                }
                return (y2 - y1) * (var - x1) / (x2 - x1) + y1;
            }

            public double Line(double x1, double y1, double x2, double y2)
            {
                double var = (double)(_eval.GetVariable("x"));
                return (y2 - y1) * (var - x1) / (x2 - x1) + y1;
            }

            public double RayFrom(double x1, double y1, double x2, double y2)
            {
                double var = (double)(_eval.GetVariable("x"));
                if ((x2 - x1) * (x1 - var) < 0)
                {
                    return double.NaN;
                }
                return (y2 - y1) * (var - x1) / (x2 - x1) + y1;
            }

            public double RayTo(double x1, double y1, double x2, double y2)
            {
                double var = (double)(_eval.GetVariable("x"));
                if ((x1 - x2) * (x2 - var) < 0)
                {
                    return double.NaN;
                }
                return (y2 - y1) * (var - x1) / (x2 - x1) + y1;
            }

            private string SignText(int coefficient, bool showpositive = false)
            {
                //helper
                if (coefficient == 1)
                {
                    if (showpositive)
                    {
                        return "+";
                    }
                    else
                    {
                        return "";
                    }
                }
                else if (coefficient == -1)
                {
                    return "-1";
                }
                else
                {
                    return coefficient.ToString();
                }
            }

            public string InstanceId(ClassInstance instance) {
                return instance.InnerScope;
            }

            public string Text(object obj)
            {
                if (obj is Reference)
                    obj = ((Reference)obj).Resolve();
                return O(obj);
            }

            public bool Boolean(object obj)
            {
                return IsTrue(obj);
            }

            public string Char(double id)
            {
                return ((char)(Int(id))).ToString();
            }

            public int Ascii(string c)
            {
                return (int)(c[0]);
            }

            public string ToString(object obj)
            {
                return obj.ToString();
            }
            public object Parse(string text)
            {
                try
                {
                    return ObjectTypes.Parse(text, eval : _eval , identifierAsText : true, primitiveOnly : false).GetValue();
                }
                catch
                {
                    return text;
                }
            }

            public double ParseNumber(string text)
            {
                return (double)(new Number(text).GetValue());
            }

            public object ParseDate(string text)
            {
                return new ObjectTypes.DateTime(text).GetValue();
            }

            // texting operations
            /// <summary>
            /// Concatenate (join) two text objects
            /// </summary>
            public object Concat(object a, object b)
            {
                if (a is IList<Reference> && b is IList<Reference>)
                {
                    IList<Reference> ac = (IList<Reference>)new Matrix((IList<Reference>)a).GetDeepCopy().GetValue();
                    return Append(ac, (IList<Reference>)b);
                }
                else
                {
                    return a.ToString() + b.ToString();
                }
            }

            /// <summary>
            /// Convert the entire texting to lower case
            /// </summary>
            public string ToLower(string text)
            {
                return text.ToLowerInvariant();
            }

            /// <summary>
            /// Convert the entire texting to upper case
            /// </summary>
            public string ToUpper(string text)
            {
                return text.ToUpperInvariant();
            }

            /// <summary>
            /// Checks if the given string consists of only upper case letters and non-letter characters
            /// </summary>
            public bool IsUpper(string text)
            {
                foreach (char c in text)
                {
                    if (char.IsLower(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// Checks if the given string consists of only lower case letters and non-letter characters
            /// </summary>
            public bool IsLower(string text)
            {
                foreach (char c in text)
                {
                    if (char.IsUpper(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// Checks if the given string consists of only letters
            /// </summary>
            public bool IsLetter(string text)
            {
                foreach (char c in text)
                {
                    if (!char.IsLetter(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// Checks if the given string consists of only digits
            /// </summary>
            public bool IsDigit(string text)
            {
                foreach (char c in text)
                {
                    if (!char.IsDigit(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// Checks if the given string consists of only digits and letters
            /// </summary>
            public bool IsLetterOrDigit(string text)
            {
                foreach (char c in text)
                {
                    if (!char.IsLetterOrDigit(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// Checks if the given string consists of only punctuation marks
            /// </summary>
            public bool IsPunctuation(string text)
            {
                foreach (char c in text)
                {
                    if (!char.IsPunctuation(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// Checks if the given string consists of only separator marks
            /// </summary>
            public bool IsSeparator(string text)
            {
                foreach (char c in text)
                {
                    if (!char.IsSeparator(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// Checks if the given string consists of only symbol marks
            /// </summary>
            public bool IsSymbol(string text)
            {
                foreach (char c in text)
                {
                    if (!char.IsSymbol(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// Make the first letter of the text capitalized, if necessary
            /// </summary>
            public string Capitalize(string text)
            {
                if (text.Length <= 0)
                    return text;
                text = text.ToLowerInvariant();
                char c = char.ToUpperInvariant(text[0]);
                if (text.Length <= 1)
                    return c.ToString();
                text = c + text.Substring(1);
                return text;
            }

            /// <summary>
            /// Get the subtexting of the texting starting at the point specified with the length specified
            /// </summary>
            public string Substring(string text, double fi, double len = -1)
            {
                if (len < 0)
                {
                    return text.Substring(Int(Math.Truncate(fi)));
                }
                else
                {
                    return text.Substring(Int(Math.Truncate(fi)), Int(Math.Truncate(len)));
                }
            }

            /// <summary>
            /// Get the subtexting of the texting starting at the point specified with the length specified (alias for Substring)
            /// </summary>
            public string Subtext(string text, double fi, double len = -1)
            {
                return Substring(text, fi, len);
            }

            /// <summary>
            /// Returns true if the given texting is empty
            /// </summary>
            public bool IsEmpty(object obj)
            {
                if (obj is string)
                {
                    return string.IsNullOrEmpty(Convert.ToString(obj));
                }
                else if (obj is IEnumerable<Reference>)
                {
                    return ((IEnumerable<Reference>)obj).Count() == 0;
                }
                else if (obj is IDictionary<Reference, Reference>)
                {
                    return ((IDictionary<Reference, Reference>)obj).Count == 0;
                }
                else if (obj is Reference[])
                {
                    return ((Reference[])obj).Length == 0;
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// Returns true if the given texting is empty or white space
            /// </summary>
            /// <param name="text"></param>
            /// <returns></returns>
            public bool IsEmptyOrSpace(string text)
            {
                return string.IsNullOrWhiteSpace(text) | string.IsNullOrEmpty(text);
            }

            /// <summary>
            /// Returns true if the given texting starts with the pattern (regex enabled)
            /// </summary>
            public bool StartsWith(string text, string pattern)
            {
                if (!pattern.StartsWith("^"))
                    pattern = "^" + pattern;
                return RegexMatch(text, pattern).Count() > 0;
            }

            /// <summary>
            /// Returns true if the given texting ends with the pattern (regex enabled)
            /// </summary>
            public bool EndsWith(string text, string pattern)
            {
                if (!pattern.EndsWith("$"))
                    pattern += "$";
                return RegexMatch(text, pattern).Count() > 0;
            }

            /// <summary>
            /// Join the matrix, set, or tuple with the specified separator
            /// </summary>
            /// <returns></returns>
            public string Join(string sep, object lst, bool ignoreEmpty = false)
            {
                StringBuilder res = new StringBuilder();

                IEnumerable<Reference> lstc = null;
                if (lst is IEnumerable<Reference>)
                {
                    lstc = (IEnumerable<Reference>)lst;
                }
                else if (lst is Reference[])
                {
                    lstc = (Reference[])lst;
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    lstc = ((IDictionary<Reference, Reference>)lst).Keys;
                }
                else
                {
                    return "";
                }

                bool init = true;

                foreach (Reference r in lstc)
                {
                    string cur = null;
                    if (r.Resolve() is string)
                    {
                        cur = Convert.ToString(r.Resolve());
                    }
                    else
                    {
                        cur = r.ToString();
                    }
                    if (!ignoreEmpty || !string.IsNullOrWhiteSpace(cur))
                    {
                        if (!init)
                            res.Append(sep);
                        else
                            init = false;
                        res.Append(cur);
                    }
                }
                return res.ToString();
            }

            // regex, wildcards
            /// <summary>
            /// Checks if the current text matches the regex pattern
            /// </summary>
            /// <param name="text"></param>
            /// <param name="pattern"></param>
            /// <returns></returns>
            public bool RegexIsMatch(string text, string pattern)
            {
                Regex regex = new Regex(pattern);
                return regex.IsMatch(text);
            }

            /// <summary>
            /// Find the first occurrence of the pattern in the texting using regex
            /// </summary>
            /// <param name="text"></param>
            /// <param name="pattern"></param>
            /// <returns></returns>
            public IEnumerable<Reference> RegexMatch(string text, string pattern)
            {
                Regex regex = new Regex(pattern);
                List<Reference> result = new List<Reference>();
                Match match = regex.Match(text);
                if (!match.Success)
                    return result;
                // if failed, return empty list
                if (match.Groups.Count > 1)
                {
                    for (int i = 1; i <= match.Groups.Count - 1; i++)
                    {
                        result.Add(new Reference(match.Groups[i].Value));
                    }
                }
                else
                {
                    result.Add(new Reference(match.Value));
                }
                return result;
            }

            /// <summary>
            /// Find all occurrences of the pattern in the texting using regex
            /// </summary>
            /// <param name="text"></param>
            /// <param name="pattern"></param>
            /// <returns></returns>
            public IEnumerable<Reference> RegexMatchAll(string text, string pattern)
            {
                Regex regex = new Regex(pattern);
                List<Reference> result = new List<Reference>();
                foreach (Match match in regex.Matches(text))
                {
                    if (!match.Success)
                        continue;
                    // if failed, skip
                    if (match.Groups.Count > 1)
                    {
                        SortedDictionary<Reference, Reference> curlst = new SortedDictionary<Reference, Reference>();
                        for (int i = 1; i <= match.Groups.Count - 1; i++)
                        {
                            if (!match.Groups[i].Success)
                                continue;
                            // if failed, skip
                            curlst[new Reference(regex.GroupNameFromNumber(i))] = (new Reference(match.Groups[i].Value));
                        }
                        result.Add(new Reference(new Set(curlst)));
                    }
                    else
                    {
                        result.Add(new Reference(match.Value));
                    }
                }
                return result;
            }

            public bool WildCardMatch(string text, string pattern)
            {
                string regexPtn = "^" + Regex.Escape(pattern)
                          .Replace(@"\*", ".*")
                          .Replace(@"\?", ".")
                   + "$";

                return RegexIsMatch(text, regexPtn);
            }


            // matrix/list stuff

            /// <summary>
            /// Get the item at the index, wrapping around if out of range
            /// </summary>
            public object Index(object lst, object val)
            {
                if (lst is IList<Reference>)
                {
                    if ((val is double || val is BigDecimal || val is int) && (Len(lst) > (double)(val) && -Len(lst) <= (double)(val)))
                    {
                        if ((double)(val) < 0)
                            val = ((IEnumerable<Reference>)lst).Count() + (double)(val);
                        return ((IEnumerable<Reference>)lst).ElementAt(Int((double)(val)));
                    }
                    else
                    {
                        return "Index Is Out Of Range";
                    }
                    // WARNING: inefficient
                }
                else if (lst is LinkedList<Reference>)
                {
                    if ((val is double || val is BigDecimal || val is int) && (Len(lst) > (double)(val) && -Len(lst) <= (double)(val)))
                    {
                        if ((double)(val) < 0)
                            val = ((LinkedList<Reference>)lst).Count + (double)(val);
                        LinkedListNode<Reference> first = ((LinkedList<Reference>)lst).First;
                        for (int i = 1; i <= Int((double)(val)); i++)
                        {
                            first = first.Next;
                        }
                        return new Reference(first);
                    }
                    else
                    {
                        return "Index Is Out Of Range";
                    }
                }
                else if (lst is Reference[])
                {
                    if ((val is double || val is BigDecimal || val is int) && (Len(lst) > (double)(val) && -Len(lst) <= (double)(val)))
                    {
                        if ((double)(val) < 0)
                            val = ((Reference[])lst).Length + (double)(val);
                        return ((Reference[])lst)[Int((double)(val))];
                    }
                    else
                    {
                        return "Index Is Out Of Range";
                    }
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    try
                    {
                        object res = ((IDictionary<Reference, Reference>)lst)[new Reference(ObjectTypes.DetectType(val))];
                        if (res == null)
                            return double.NaN;
                        return res;
                    }
                    catch
                    {
                        return double.NaN;
                    }
                }
                else if (lst is string)
                {
                    if ((val is double || val is BigDecimal || val is int) && (Len(lst) > (double)(val) && -Len(lst) <= (double)(val)))
                    {
                        if ((double)(val) < 0)
                            val = ((Reference[])lst).Length + (double)(val);
                        return ((string)lst)[Int((double)(val))].ToString();
                    }
                    else
                    {
                        return "Index Is Out Of Range";
                    }
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Get the item at the index, wrapping around if out of range
            /// </summary>
            public object IndexCircular(object lst, object val)
            {
                if (lst is IList<Reference>)
                {
                    if ((val is double || val is BigDecimal || val is int))
                    {
                        val = Modulo(Int((double)(val)), Len(lst));
                        return ((IEnumerable<Reference>)lst).ElementAt(Int((double)(val)));
                    }
                    else
                    {
                        return "Index Must Be A Number";
                    }
                    // WARNING: inefficient
                }
                else if (lst is LinkedList<Reference>)
                {
                    if ((val is double || val is BigDecimal || val is int))
                    {
                        val = Modulo(Int((double)(val)), Len(lst));
                        LinkedListNode<Reference> first = ((LinkedList<Reference>)lst).First;
                        for (int i = 1; i <= Int((double)(val)); i++)
                        {
                            first = first.Next;
                        }
                        return new Reference(first);
                    }
                    else
                    {
                        return "Index Is Out Of Range";
                    }
                }
                else if (lst is Reference[])
                {
                    if ((val is double || val is BigDecimal || val is int))
                    {
                        val = Modulo(Int((double)(val)), Len(lst));
                        return ((Reference[])lst)[Int((double)(val))];
                    }
                    else
                    {
                        return "Index Must Be A Number";
                    }
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    try
                    {
                        object res = ((IDictionary<Reference, Reference>)lst)[new Reference(ObjectTypes.DetectType(val))];
                        if (res == null)
                            return double.NaN;
                        return res;
                    }
                    catch
                    {
                        return double.NaN;
                    }
                }
                else if (lst is string)
                {
                    if ((val is double || val is BigDecimal || val is int))
                    {
                        val = Modulo(Int((double)(val)), Len(lst));
                        return ((string)lst)[Int((double)(val))].ToString();
                    }
                    else
                    {
                        return "Index Must Be A Number";
                    }
                }
                else
                {
                    return double.NaN;
                }
            }

            public object At(object lst, object val)
            {
                return IndexCircular(lst, val);
            }

            public object SetAt(object lst, object idx, object val)
            {
                if (lst is IList<Reference>)
                {
                    if (idx is double && (Len(lst) > (double)(idx) && (double)(idx) >= 0))
                    {
                        ((IList<Reference>)lst)[Int((double)(idx))] = new Reference(ObjectTypes.DetectType(val));
                    }
                }
                else if (lst is Reference[])
                {
                    if (idx is double && (Len(lst) > (double)(idx) && (double)(idx) >= 0))
                    {
                        ((Reference[])lst)[Int((double)(idx))] = new Reference(ObjectTypes.DetectType(val));
                    }
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    ((IDictionary<Reference, Reference>)lst)[new Reference(ObjectTypes.DetectType(idx))] = new Reference(ObjectTypes.DetectType(val));
                }
                else
                {
                    return "Index Out Of Range";
                }
                return lst;
            }

            public object SetAtCircular(object lst, object idx, object val)
            {
                if (lst is IList<Reference>)
                {
                    if (idx is double)
                    {
                        idx = Modulo((double)(idx), ((IList<Reference>)lst).Count);
                        ((IList<Reference>)lst)[Int((double)(idx))] = new Reference(ObjectTypes.DetectType(val));
                    }
                }
                else if (lst is Reference[])
                {
                    if (idx is double && (Len(lst) > (double)(idx) && (double)(idx) >= 0))
                    {
                        ((Reference[])lst)[Int((double)(idx))] = new Reference(ObjectTypes.DetectType(val));
                    }
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    ((IDictionary<Reference, Reference>)lst)[new Reference(ObjectTypes.DetectType(idx))] = new Reference(ObjectTypes.DetectType(val));
                }
                else
                {
                    return "Index Out Of Range";
                }
                return lst;
            }

            public object Slice(object lst, double a = double.NaN, double b = double.NaN)
            {
                bool reverse = false;
                if (double.IsNaN(b))
                    b = Len(lst);
                if (double.IsNaN(a))
                    a = 0;

                a = Math.Truncate(a);
                b = Math.Truncate(b);

                if (b < 0)
                    b = Len(lst) + b;
                if (a < 0)
                    a = Len(lst) + a;

                if (b < a)
                {
                    reverse = true;
                    double t = a;
                    a = b;
                    b = t;
                }

                // allow out of range
                if (b > Len(lst))
                    b = Len(lst);
                if (a < 0)
                    a = 0;

                if (lst is IEnumerable<Reference>)
                {
                    List<Reference> rlst = new List<Reference>((IEnumerable<Reference>)lst);
                    rlst.RemoveRange(Int(b), Int(Len(rlst) - b));
                    rlst.RemoveRange(0, Int(a));
                    if (reverse)
                        rlst.Reverse();
                    return rlst;
                }
                else if (lst is Reference[])
                {
                    List<Reference> lst2 = new List<Reference>((Reference[])lst);
                    lst2.RemoveRange(Int(b), Int(Len(lst2) - b));
                    lst2.RemoveRange(0, Int(a));
                    if (reverse)
                        lst2.Reverse();
                    return lst2;
                }
                else if (lst is string)
                {
                    string text = null;
                    if (b == Convert.ToString(lst).Length)
                    {
                        text = Convert.ToString(lst).Substring(Int(a));
                    }
                    else
                    {
                        text = Convert.ToString(lst).Remove(Int(b)).Substring(Int(a));
                    }
                    if (reverse)
                        return this.Reverse(text);
                    return text;
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Add an item to a list-matrix or setionary-set
            /// </summary>
            public object Add(object lst, object val)
            {
                if (lst is IList<Reference>)
                {
                    ((IList<Reference>)lst).Add(new Reference(ObjectTypes.DetectType(val)));
                }
                else if (lst is LinkedList<Reference>)
                {
                    ((LinkedList<Reference>)lst).AddLast(new Reference(ObjectTypes.DetectType(val)));
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    if (val is IEnumerable<Reference> && ((IEnumerable<Reference>)val).Count() == 2)
                    {
                        IEnumerable<Reference> valLst = (IEnumerable<Reference>)val;
                        ((IDictionary<Reference, Reference>)lst).Add(valLst.ElementAt(0), valLst.ElementAt(1));
                    }
                    else if (val is IDictionary<Reference, Reference>)
                    {
                        lst = Union((IDictionary<Reference, Reference>)lst, (IDictionary<Reference, Reference>)val);
                    }
                    else
                    {
                        ((IDictionary<Reference, Reference>)lst)[new Reference(ObjectTypes.DetectType(val))] = null;
                    }
                }
                else
                {
                    return null;
                }
                return lst;
            }

            /// <summary>
            /// Add a vector as a row in the matrix. If no vector is specified, adds a blank row.
            /// </summary>
            public IEnumerable<Reference> AddRow(IEnumerable<Reference> lst, IEnumerable<Reference> vec = null)
            {
                Matrix mat = new Matrix(lst);
                mat.Resize(mat.Height + 1, mat.Width);

                if ((vec != null))
                {
                    for (int i = 0; i <= mat.Width - 1; i++)
                    {
                        if (vec.Count() > i)
                            mat.SetCoord(mat.Height - 1, i, vec.ElementAt(i));
                    }
                }

                return (IEnumerable<Reference>)mat.GetValue();
            }

            /// <summary>
            /// Add a vector as a column in the matrix. If no vector is specified, adds a blank column.
            /// </summary>
            public IEnumerable<Reference> AddCol(IEnumerable<Reference> lst, IEnumerable<Reference> vec = null)
            {
                Matrix mat = new Matrix(lst);
                mat.Resize(mat.Height, mat.Width + 1);

                if ((vec != null))
                {
                    for (int i = 0; i <= mat.Height - 1; i++)
                    {
                        if (vec.Count() > i)
                            mat.SetCoord(i, mat.Width - 1, vec.ElementAt(i));
                    }
                }

                return (IEnumerable<Reference>)mat.GetValue();
            }

            /// <summary>
            /// Append the second list onto the first list
            /// </summary>
            public IEnumerable<Reference> Append(IEnumerable<Reference> lst, IEnumerable<Reference> val)
            {
                if (lst is List<Reference>)
                {
                    ((List<Reference>)lst).AddRange(val.ToArray());
                }
                else if (lst is LinkedList<Reference>)
                {
                    foreach (Reference r in val)
                    {
                        ((LinkedList<Reference>)lst).AddLast(r);
                    }
                }
                else
                {
                    return null;
                    // not supported
                }
                return lst;
            }

            /// <summary>
            /// Remove the first object matching the specified object within the list
            /// </summary>
            public object Remove(object lst, object val = null)
            {
                if (lst is IList<Reference>)
                {
                    int i = 0;
                    IList<Reference> tmp = (IList<Reference>)lst;
                    while (i < tmp.Count)
                    {
                        if (CommonTypes.ObjectComparer.CompareObjs(tmp[i], val) == 0)
                        {
                            tmp.RemoveAt(i);
                            break;
                        }
                        i += 1;
                    }
                    return tmp;
                }
                else if (lst is LinkedList<Reference>)
                {
                    LinkedListNode<Reference> tmp = ((LinkedList<Reference>)lst).First;
                    while ((tmp != null))
                    {
                        if (ObjectComparer.CompareObjs(tmp.Value, val) == 0)
                        {
                            tmp.List.Remove(tmp);
                            break;
                        }
                        tmp = tmp.Next;
                    }
                    return tmp.List;
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    IDictionary<Reference, Reference> tmp = (IDictionary<Reference, Reference>)lst;
                    tmp.Remove(new Reference(ObjectTypes.DetectType(val)));
                    return tmp;
                }
                else if (lst is string)
                {
                    if (Convert.ToString(lst).Contains(val.ToString()))
                    {
                        lst = Convert.ToString(lst).Remove(Convert.ToString(lst).IndexOf(val.ToString())) + Convert.ToString(lst).Substring(Convert.ToString(lst).IndexOf(val.ToString()) + val.ToString().Length);
                    }
                    return lst;
                }
                else if (lst is Reference)
                {
                    ((Reference)lst).NodeRemove();
                    return (Reference)lst;
                }
                else
                {
                    return null;
                }
            }

            /// <summary>
            /// Remove all objects matching the specified object within the list
            /// </summary>
            public object RemoveAll(object lst, object val)
            {
                if (lst is IList<Reference>)
                {
                    int i = 0;
                    IList<Reference> tmp = (IList<Reference>)lst;
                    while (i < tmp.Count)
                    {
                        if (ObjectComparer.CompareObjs(tmp[i], val) == 0)
                        {
                            tmp.RemoveAt(i);
                            continue;
                        }
                        i += 1;
                    }
                }
                else if (lst is LinkedList<Reference>)
                {
                    LinkedListNode<Reference> tmp = ((LinkedList<Reference>)lst).First;
                    while ((tmp != null))
                    {
                        if (ObjectComparer.CompareObjs(tmp.Value, val) == 0)
                        {
                            LinkedListNode<Reference> nxt = tmp.Next;
                            tmp.List.Remove(tmp);
                            tmp = nxt;
                        }
                        tmp = tmp.Next;
                    }
                    return tmp.List;
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    return Remove(lst, val);
                }
                else if (lst is string)
                {
                    while (Convert.ToString(lst).Contains(val.ToString()))
                    {
                        lst = Convert.ToString(lst).Remove(Convert.ToString(lst).IndexOf(val.ToString())) + Convert.ToString(lst).Substring(Convert.ToString(lst).IndexOf(val.ToString()) + 1);
                    }
                }
                else
                {
                    return null;
                }
                return lst;
            }

            /// <summary>
            /// Reverse the entire dimension of the matrix
            /// </summary>
            /// <returns></returns>
            public object Reverse(object lst)
            {
                if (lst is IEnumerable<Reference>)
                {
                    ((IEnumerable<Reference>)lst).Reverse();
                }
                else if (lst is string)
                {
                    IEnumerable<char> slst = Convert.ToString(lst).ToList();
                    slst.Reverse();
                    StringBuilder s = new StringBuilder();
                    foreach (char c in slst)
                    {
                        s.Append(c);
                    }
                    return s.ToString();
                }
                else
                {
                    return null;
                }
                return lst;
            }

            /// <summary>
            /// Return a reference to the first item in the linked list
            /// </summary>
            public Reference First(LinkedList<Reference> lst)
            {
                return new Reference(((LinkedList<Reference>)lst).First);
            }

            /// <summary>
            /// Return a reference to the last item in the linked list
            /// </summary>
            public Reference Last(LinkedList<Reference> lst)
            {
                return new Reference(((LinkedList<Reference>)lst).Last);
            }

            /// <summary>
            /// Tries to move the reference to the next place on the linkedlist the specified number of times
            /// </summary>
            public Reference Next(Reference @ref, double times = 1)
            {
                for (double i = 0; i <= times - 1; i++)
                {
                    @ref.NodeNext();
                }
                return @ref;
            }

            /// <summary>
            /// Tries to move the reference to the previous place on the linkedlist the specified number of times
            /// </summary>
            public Reference Prev(Reference @ref, double times = 1)
            {
                for (double i = 0; i <= times - 1; i++)
                {
                    @ref.NodePrevious();
                }
                return @ref;
            }

            /// <summary>
            /// Tries to add an item after the one pointed to by the reference on the linked list 
            /// </summary>
            public Reference AddAfter(Reference @ref, object val)
            {
                @ref.NodeAddAfter(new Reference(val));
                return @ref;
            }

            /// <summary>
            /// Tries to add an item before the one pointed to by the reference on the linked list 
            /// </summary>
            public Reference AddBefore(Reference @ref, object val)
            {
                @ref.NodeAddBefore(new Reference(val));
                return @ref;
            }

            /// <summary>
            /// Take an item from the end of the list, remove it and return it.
            /// </summary>
            public object Pop(object lst)
            {
                if (lst is IList<Reference>)
                {
                    IList<Reference> lr = (IList<Reference>)lst;
                    if (lr.Count == 0)
                        return double.NaN;
                    object last = lr[lr.Count - 1];
                    lr.RemoveAt(lr.Count - 1);
                    return last;
                }
                else if (lst is LinkedList<Reference>)
                {
                    LinkedList<Reference> lr = (LinkedList<Reference>)lst;
                    if (lr.Count == 0)
                        return double.NaN;
                    object last = lr.Last.Value.Resolve();
                    lr.RemoveLast();
                    return last;
                }
                else if (lst is string)
                {
                    if (Convert.ToString(lst).Length == 0)
                        return double.NaN;
                    string last = Convert.ToString(lst).Substring(Convert.ToString(lst).Length - 1);
                    lst = Convert.ToString(lst).Remove(Convert.ToString(lst).Length - 1);
                    return last;
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Take an item from the end of the list, remove it and return it.
            /// </summary>
            public object PopLast(object lst)
            {
                return Pop(lst);
            }

            /// <summary>
            /// Take an item from the start of the list, remove it and return it.
            /// </summary>
            public object PopFirst(object lst)
            {
                if (lst is IList<Reference>)
                {
                    IList<Reference> lr = (IList<Reference>)lst;
                    if (lr.Count == 0)
                        return double.NaN;
                    object first = lr[0];
                    lr.RemoveAt(0);
                    return first;
                }
                else if (lst is LinkedList<Reference>)
                {
                    LinkedList<Reference> lr = (LinkedList<Reference>)lst;
                    if (lr.Count == 0)
                        return double.NaN;
                    object first = lr.First.Value.Resolve();
                    lr.RemoveFirst();
                    return first;
                }
                else if (lst is string)
                {
                    if (Convert.ToString(lst).Length == 0)
                        return double.NaN;
                    string first = Convert.ToString(lst).Remove(1);
                    lst = Convert.ToString(lst).Substring(1);
                    return first;
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Push an item to the end of the list
            /// </summary>
            public object Push(object lst, object obj)
            {
                if (lst is IList<Reference>)
                {
                    IList<Reference> lr = (IList<Reference>)lst;
                    lr.Add(new Reference(obj));
                    return lr;
                }
                else if (lst is LinkedList<Reference>)
                {
                    LinkedList<Reference> lr = (LinkedList<Reference>)lst;
                    lr.AddLast(new Reference(obj));
                    return lr;
                }
                else if (lst is string)
                {
                    lst = Convert.ToString(lst) + obj.ToString();
                    return lst;
                }
                else
                {
                    return null;
                }
            }

            /// <summary>
            /// Push an item to the end of the list
            /// </summary>
            public object PushLast(object lst, object obj)
            {
                return Push(lst, obj);
            }

            /// <summary>
            /// Push an item to the beginning of the list
            /// </summary>
            public object PushFirst(object lst, object obj)
            {
                if (lst is IList<Reference>)
                {
                    IList<Reference> lr = (IList<Reference>)lst;
                    lr.Insert(0, new Reference(obj));
                    return lr;
                }
                else if (lst is LinkedList<Reference>)
                {
                    LinkedList<Reference> lr = (LinkedList<Reference>)lst;
                    lr.AddFirst(new Reference(obj));
                    return lr;
                }
                else if (lst is string)
                {
                    lst = obj.ToString() + Convert.ToString(lst);
                    return lst;
                }
                else
                {
                    return null;
                }
            }

            /// <summary>
            /// Cycle the list forwards: [1,2,3]->[3,1,2]
            /// </summary>
            public object Cycle(object lst, double times = 1)
            {
                bool reverse = false;
                if (times < 0)
                {
                    reverse = true;
                    times = -times;
                }
                for (int i = 0; i <= Int(times) - 1; i++)
                {
                    if (reverse)
                    {
                        Push(lst, PopFirst(lst));
                    }
                    else
                    {
                        PushFirst(lst, Pop(lst));
                    }
                }
                return lst;
            }

            /// <summary>
            /// Cycle the list backwards: [1,2,3]->[2,3,1]
            /// </summary>
            public object CycleReverse(object lst, double times = 1)
            {
                return Cycle(lst, -times);
            }

            /// <summary>
            /// Remove at the index in the matrix-list
            /// </summary>
            public object RemoveAt(object lst, double val)
            {
                if (lst is IList<Reference>)
                {
                    ((IList<Reference>)lst).RemoveAt(Int(val));
                }
                else if (lst is LinkedList<Reference>)
                {
                    LinkedListNode<Reference> tmp = ((LinkedList<Reference>)lst).First;
                    for (int i = 1; i <= Int(val); i++)
                    {
                        tmp = tmp.Next;
                    }
                    tmp.List.Remove(tmp);
                }
                else if (lst is string)
                {
                    int idx = Convert.ToString(lst).IndexOf(val.ToString());
                    lst = Convert.ToString(lst).Remove(idx) + Convert.ToString(lst).Substring(idx + 1);
                }
                else
                {
                    return null;
                }
                return lst;
            }

            /// <summary>
            /// Count the number of times the given value occurs within the matrix, set, or texting.
            /// Note: for sets, if the set is in setionary form (new[]{a:b,c:d}), 
            /// then this counts the number of times the value, not key occurs, in the set (i.e. b and d are checked)
            /// Otherwise, this simply returns one because elements of the set are unique.
            /// Note2: Regex enabled for textings
            /// </summary>
            public double Count(object lst, object val)
            {
                int ct = 0;
                if (lst is IEnumerable<Reference>)
                {
                    foreach (object i in (IEnumerable)lst)
                    {
                        if (O(i) == O(val))
                        {
                            ct += 1;
                        }
                    }
                }
                else if (lst is LinkedList<Reference>)
                {
                    foreach (object i in (LinkedList<Reference>)lst)
                    {
                        if (O(i) == O(val))
                        {
                            ct += 1;
                        }
                    }
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    int notNullCt = 0;
                    foreach (KeyValuePair<Reference, Reference> i in (IDictionary<Reference, Reference>)lst)
                    {
                        if (i.Value == null)
                            continue;
                        if (O(i.Value) == O(val))
                            ct += 1;
                        notNullCt += 1;
                    }
                    if (notNullCt <= 0)
                        return 1;
                }
                else if (lst is string)
                {
                    for (int i = 0; i <= Convert.ToString(lst).Length; i++)
                    {
                        if (StartsWith(Convert.ToString(lst).Substring(i), val.ToString()))
                            ct += 1;
                    }
                }
                else
                {
                    return 1;
                }
                return ct;
            }

            /// <summary>
            /// Clear the matrix or set
            /// </summary>
            public object Clear(object lst)
            {
                if (lst is IList<Reference>)
                {
                    ((IList<Reference>)lst).Clear();
                }
                else if (lst is LinkedList<Reference>)
                {
                    ((LinkedList<Reference>)lst).Clear();
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    ((IDictionary<Reference, Reference>)lst).Clear();
                }
                else
                {
                    return null;
                }
                return lst;
            }

            /// <summary>
            /// Returns true if the specified matrix, text, or set contains the value. (regex enabled for texting)
            /// </summary>
            public bool Contains(object lst, object val)
            {
                if (lst is IEnumerable<Reference>)
                {
                    return ((IEnumerable<Reference>)lst).Contains(new Reference(ObjectTypes.DetectType(val)));
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    return ContainsKey((IDictionary<Reference, Reference>)lst, val);
                }
                else if (lst is string)
                {
                    return !(RegexMatch(Convert.ToString(lst), val.ToString()).Count() == 0);
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// find the specified object or subtexting within the array or texting from the beginning (regex enabled for textings)
            /// </summary>
            /// <param name="lst"></param>
            /// <param name="val"></param>
            /// <returns></returns>
            public double Find(object lst, object val)
            {
                double res = 0;
                if (lst is IList<Reference>)
                {
                    res = ((IList<Reference>)lst).IndexOf(new Reference(ObjectTypes.DetectType(val)));
                }
                else if (lst is string)
                {
                    Regex regex = new Regex(val.ToString());
                    res = regex.Match(Convert.ToString(lst)).Index;
                    if (res < 0)
                        res = double.NaN;
                }
                else
                {
                    return double.NaN;
                }
                if (res < 0)
                    return double.NaN;
                return res;
            }

            /// <summary>
            /// find the specified object or subtexting within the array or texting from the end (regex enabled for textings)
            /// </summary>
            /// <param name="lst"></param>
            /// <param name="val"></param>
            /// <returns></returns>
            public double FindEnd(object lst, object val)
            {
                double res = 0;
                if (lst is List<Reference>)
                {
                    res = ((List<Reference>)lst).LastIndexOf(new Reference(ObjectTypes.DetectType(val)));
                }
                else if (lst is string)
                {
                    Regex regex = new Regex(val.ToString(), RegexOptions.RightToLeft);
                    res = regex.Match(Convert.ToString(lst)).Index;
                    if (res < 0)
                        res = double.NaN;
                }
                else
                {
                    return double.NaN;
                }
                if (res < 0)
                    return double.NaN;
                return res;
            }

            /// <summary>
            /// replace first instance of the value with in the texting with the new value
            /// </summary>
            /// <returns></returns>
            public string ReplaceFirst(string text, string oldVal, string newVal)
            {
                Regex regex = new Regex(oldVal.ToString());
                Match m = regex.Match(text);
                StringBuilder sb = new StringBuilder(text);
                if (m.Groups.Count > 1)
                {
                    // replace captures
                    for (int j = m.Groups.Count - 1; j >= 1; j += -1)
                    {
                        sb.Remove(m.Groups[j].Index, m.Groups[j].Length);
                        sb.Insert(m.Groups[j].Index, newVal);
                    }
                }
                else
                {
                    //replace everything
                    sb.Remove(m.Index, m.Length);
                    sb.Insert(m.Index, newVal);
                }
                return sb.ToString();
            }

            /// <summary>
            /// replace last instance of the value with in the texting with the new value
            /// </summary>
            /// <returns></returns>
            public string ReplaceLast(string text, string oldVal, string newVal)
            {
                Regex regex = new Regex(oldVal.ToString(), RegexOptions.RightToLeft);
                Match m = regex.Match(text);
                StringBuilder sb = new StringBuilder(text);
                if (m.Groups.Count > 1)
                {
                    // replace captures
                    for (int j = m.Groups.Count - 1; j >= 1; j += -1)
                    {
                        sb.Remove(m.Groups[j].Index, m.Groups[j].Length);
                        sb.Insert(m.Groups[j].Index, newVal);
                    }
                }
                else
                {
                    //replace everything
                    sb.Remove(m.Index, m.Length);
                    sb.Insert(m.Index, newVal);
                }
                return sb.ToString();
            }

            /// <summary>
            /// replace all visible instances of the value with in the texting with the new value
            /// </summary>
            /// <returns></returns>
            public string Replace(string text, string oldVal, string newVal)
            {
                Regex regex = new Regex(oldVal.ToString());
                MatchCollection res = regex.Matches(text);
                StringBuilder sb = new StringBuilder(text);
                for (int i = res.Count - 1; i >= 0; i += -1)
                {
                    Match m = res[i];
                    if (m.Groups.Count > 1)
                    {
                        // replace captures
                        for (int j = m.Groups.Count - 1; j >= 1; j += -1)
                        {
                            sb.Remove(m.Groups[j].Index, m.Groups[j].Length);
                            sb.Insert(m.Groups[j].Index, newVal);
                        }
                    }
                    else
                    {
                        //replace everything
                        sb.Remove(m.Index, m.Length);
                        sb.Insert(m.Index, newVal);
                    }
                }
                return sb.ToString();
            }

            /// <summary>
            /// replace all instances of the value with in the texting with the new value
            /// repeats until the value no longer exists at all
            /// e.g. replace("1233333","123","312") will return 3123333 while this will return 33333`12
            /// </summary>
            /// <returns></returns>
            public string ReplaceAll(string text, string oldVal, string newVal)
            {
                while (Contains(text, oldVal))
                {
                    text = Replace(text, oldVal, newVal);
                }
                return text;
            }

            /// <summary>
            /// add to the left side of the texting or matrix/list until it is at least the specified length.
            /// </summary>
            /// <param name="lst"></param>
            /// <param name="length"></param>
            /// <returns></returns>
            public object Pad(object lst, double length, object item = null)
            {
                if (lst is IList<Reference>)
                {
                    if (item == null)
                        item = 0.0;
                    IList<Reference> rlst = (IList<Reference>)lst;
                    while (Len(rlst) < Int(length))
                    {
                        rlst.Insert(0, new Reference(item));
                    }
                    return rlst;
                }
                else if (lst is string)
                {
                    if (item == null)
                        item = " ";
                    return Convert.ToString(lst).PadLeft(Int(length), item.ToString()[0]);
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// add to the right side of the texting or matrix/list until it is at least the specified length.
            /// </summary>
            /// <param name="lst"></param>
            /// <param name="length"></param>
            /// <returns></returns>
            public object PadEnd(object lst, double length, object item = null)
            {
                if (lst is IList<Reference>)
                {
                    if (item == null)
                        item = 0.0;
                    IList<Reference> rlst = (IList<Reference>)lst;
                    while (Len(rlst) < Int(length))
                    {
                        rlst.Add(new Reference(item));
                    }
                    return rlst;
                }
                else if (lst is string)
                {
                    if (item == null)
                        item = " ";
                    return Convert.ToString(lst).PadRight(Int(length), item.ToString()[0]);
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Get the levenshtein edit distance between two strings, optionally with custom weights
            /// </summary>
            /// <returns></returns>
            public double EditDist(string a, string b, double costAdd = 1, double costRem = 1, double costChange = 1)
            {

                costAdd = Math.Min(costAdd, costRem);

                double[,] dp = new double[a.Length + 2, b.Length + 2];

                // initialize to 0
                for (int i = 0; i <= a.Length; i++)
                {
                    dp[i, 0] = i * costAdd;
                }
                for (int i = 1; i <= b.Length; i++)
                {
                    dp[0, i] = i * costAdd;
                }

                for (int i = 1; i <= a.Length; i++)
                {
                    for (int j = 1; j <= b.Length; j++)
                    {
                        if (a[i - 1] == b[j - 1])
                        {
                            dp[i, j] = dp[i - 1, j - 1];
                        }
                        else
                        {
                            dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + costAdd, dp[i, j - 1] + costAdd), dp[i - 1, j - 1] + costChange);
                        }
                    }
                }

                return dp[a.Length, b.Length];
            }

            /// <summary>
            /// Swap two items in a list without creating a matrix, used internally by qsort
            /// </summary>
            private void InplaceSwap(IList<Reference> list, int a, int b)
            {
                Reference tmp = list[a];
                list[a] = list[b];
                list[b] = tmp;
            }

            /// <summary>
            /// Internal function using quicksort to sort a list in-place
            /// </summary>
            /// <param name="lst">List to sort</param>
            /// <param name="l">Left limit of range to sort (inclusive)</param>
            /// <param name="r">Right limit of range to sort (exclusive)</param>
            private void QSort(IList<Reference> lst, int l, int r, Lambda comparer = null)
            {
                if (l + 1 >= r)
                    return;

                //MsgBox(String.Join(",", lst))
                int pivot = Int(l + (r - l) / 2);
                // choose middle element as pivot
                InplaceSwap(lst, pivot, r - 1);
                // move pivot to end

                pivot = r - 1;
                // new location for pivot, set for convenience

                int mid = l;
                // the index we are moving things less than the pivot to
                // do not loop onto pivot
                for (int i = l; i <= pivot - 1; i++)
                {
                    // if less than pivot, swap
                    if ((comparer == null && ObjectComparer.CompareObjs(lst[i], lst[pivot]) < 0) || ((comparer != null) && (double)(comparer.Execute(_eval, new[]{
                    lst[i],
                    lst[pivot]
                })) < 0))
                    {
                        InplaceSwap(lst, i, mid);
                        mid += 1;
                        // this index is full, go to next
                    }
                }

                InplaceSwap(lst, mid, pivot);
                // swap pivot back to where it should belong

                // divide and conquer
                QSort(lst, l, mid);
                QSort(lst, mid + 1, r);
            }

            /// <summary>
            /// Sort a list using the generic comparer, returning true on success
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public IList<Reference> Sort(IList<Reference> lst, Lambda comparer = null)
            {
                QSort(lst, 0, lst.Count, comparer);
                return lst;
            }

            /// <summary>
            /// Randomly shuffle a list
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public IList<Reference> Shuffle(IList<Reference> lst)
            {
                Random rnd = new Random();
                for (int i = 0; i <= lst.Count - 1; i++)
                {
                    int r = rnd.Next(i, lst.Count);
                    InplaceSwap(lst, i, r);
                }
                return lst;
            }

            /// <summary>
            /// Get the height of the matrix
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public double Height(IEnumerable<Reference> lst)
            {
                return new Matrix(lst).Height;
            }

            /// <summary>
            /// Alias for matrix height
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public double Rows(IEnumerable<Reference> lst)
            {
                return Height(lst);
            }

            /// <summary>
            /// Get the width of the matrix
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public double Width(IEnumerable<Reference> lst)
            {
                return new Matrix(lst).Width;
            }

            /// <summary>
            /// Alias for matrix width
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public double Cols(IEnumerable<Reference> lst)
            {
                return Width(lst);
            }

            /// <summary>
            /// Give the matrix a standard width and height
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public IEnumerable<Reference> Normalize(IEnumerable<Reference> lst)
            {
                return (IEnumerable<Reference>)new Matrix(lst).GetValue();
            }

            /// <summary>
            /// Get a row of the matrix as a 1-column matrix/vector
            /// </summary>
            /// <returns></returns>
            public IEnumerable<Reference> Row(IEnumerable<Reference> lst, double id)
            {
                return (IEnumerable<Reference>)new Matrix(lst).Row(Int(id)).GetValue();
            }

            /// <summary>
            /// Get a column of the matrix as a 1-column matrix/vector
            /// </summary>
            /// <returns></returns>
            public IEnumerable<Reference> Col(IEnumerable<Reference> lst, double id)
            {
                return (IEnumerable<Reference>)new Matrix(lst).Col(Int(id)).GetValue();
            }

            /// <summary>
            /// Resize a matrix
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public IEnumerable<Reference> Resize(List<Reference> lst, double height, double width = double.NaN)
            {
                Matrix mat = new Matrix(lst);
                if (width == double.NaN)
                    width = mat.Width;
                mat.Resize(Int(height), Int(width));
                return (IEnumerable<Reference>)mat.GetValue();
            }

            /// <summary>
            /// Multiply two matrices. If we're unable to do so, we try to take the inner product.
            /// </summary>
            /// <returns></returns>
            public object Multiply(List<Reference> A, List<Reference> B)
            {
                Matrix ma = new Matrix(A);
                Matrix mb = new Matrix(B);
                try
                {
                    return ma.Multiply(mb).GetValue();
                }
                catch (MathException ex)
                {
                    try {
                        return Inner(A, B);
                    }
                    catch (MathException)
                    {
                        throw ex;
                    }
                }
            }

            /// <summary>
            /// Compute the dot product of two column vectors
            /// </summary>
            /// <returns></returns>
            public object Dot(List<Reference> a, List<Reference> b)
            {
                return new Matrix(a).Dot(new Matrix(b));
            }

            /// <summary>
            /// Compute the inner product of two column vectors
            /// </summary>
            /// <returns></returns>
            public List<Reference> Inner(List<Reference> a, List<Reference> b)
            {
                return (List<Reference>)new Matrix(a).Inner(new Matrix(b)).GetValue();
            }

            /// <summary>
            /// Compute the cross product of two column vectors
            /// </summary>
            /// <returns></returns>
            public List<Reference> Cross(List<Reference> a, List<Reference> b)
            {
                return (List<Reference>)new Matrix(a).Cross(new Matrix(b)).GetValue();
            }

            /// <summary>
            /// Multiply a matrix and a scalar
            /// </summary>
            /// <returns></returns>
            public List<Reference> Scale(List<Reference> a, object b)
            {
                return (List<Reference>)new Matrix(a).MultiplyScalar(b).GetValue();
            }

            /// <summary>
            /// Swap two rows in a matrix
            /// </summary>
            /// <returns></returns>
            public IList<Reference> SwapRows(IList<Reference> mat, double a, double b)
            {
                return (IList<Reference>)new Matrix(mat).SwapRows(Int(a), Int(b)).GetValue();
            }

            /// <summary>
            /// Swap two columns in a matrix
            /// </summary>
            /// <returns></returns>
            public IList<Reference> SwapCols(IList<Reference> mat, double a, double b)
            {
                return (IList<Reference>)new Matrix(mat).SwapCols(Int(a), Int(b)).GetValue();
            }

            /// <summary>
            /// Find the determinant of a matrix
            /// </summary>
            /// <returns></returns>
            public object Det(IEnumerable<Reference> A)
            {
                return new Matrix(A).Determinant();
            }

            /// <summary>
            /// Find the reduced row echelon form of a matrix, optionally specifying an augmented matrix
            /// </summary>
            /// <returns></returns>
            public IEnumerable<Reference> Rref(IEnumerable<Reference> mat, List<Reference> aug = null)
            {
                if ((aug != null))
                {
                    return (IEnumerable<Reference>)new Matrix(mat).Rref(new Matrix(aug)).GetValue();
                }
                else
                {
                    return (IEnumerable<Reference>)new Matrix(mat).Rref().GetValue();
                }
            }

            /// <summary>
            /// Find the norm of a column vector (gives the square of the magnitude)
            /// </summary>
            /// <returns></returns>
            public object Norm(IEnumerable<Reference> A)
            {
                return new Matrix(A).Norm();
            }

            /// <summary>
            /// Find the transpose of a matrix
            /// </summary>
            /// <returns></returns>
            public IEnumerable<Reference> Transpose(IEnumerable<Reference> A)
            {
                return (IEnumerable<Reference>)new Matrix(A).Transpose().GetValue();
            }

            /// <summary>
            /// Find the inverse of a matrix
            /// </summary>
            /// <returns></returns>
            public IEnumerable<Reference> Inverse(IEnumerable<Reference> A)
            {
                return (IEnumerable<Reference>)new Matrix(A).Inverse().GetValue();
            }

            /// <summary>
            /// Get the identity matrix (filled with zeros except the primary diagonal which is filled with ones) 
            /// with the specified number of rows and cols
            /// </summary>
            /// <returns></returns>
            public IEnumerable<Reference> IdentityMatrix(double rows, double cols = -1)
            {
                return (IEnumerable<Reference>)ObjectTypes.Matrix.IdentityMatrix(Int(rows), Int(cols)).GetValue();
            }

            /// <summary>
            /// Checks if the given matrix is an identity matrix
            /// </summary>
            /// <returns></returns>
            public bool IsIdentityMatrix(List<object> A)
            {
                return new Matrix(A).IsIdentityMatrix();
            }

            /// <summary>
            /// Get the identity matrix (filled with zeros except the primary diagonal which is filled with ones) 
            /// with the specified number of rows and cols
            /// (Alias for IdentityMatrix(,))
            /// </summary>
            public IEnumerable<Reference> IMat(double rows, double cols = -1)
            {
                return IdentityMatrix(rows, cols);
            }

            /// <summary>
            /// Get the upper triangular part of a matrix
            /// </summary>
            public List<Reference> TriUpper(List<Reference> A)
            {
                Matrix mat = (Matrix)new Matrix(A).GetDeepCopy();
                for (int i = 0; i <= mat.Width - 2; i++)
                {
                    for (int j = i + 1; j <= mat.Height - 1; j++)
                    {
                        mat.SetCoord(j, i, 0);
                    }
                }
                return (List<Reference>)mat.GetValue();
            }

            /// <summary>
            /// Get the lower triangular part of a matrix
            /// </summary>
            public List<Reference> TriLower(List<Reference> A)
            {
                Matrix mat = (Matrix)new Matrix(A).GetDeepCopy();
                for (int i = 1; i <= mat.Width - 1; i++)
                {
                    for (int j = 0; j <= i - 1; j++)
                    {
                        mat.SetCoord(j, i, 0);
                    }
                }
                return (List<Reference>)mat.GetValue();
            }

            /// <summary>
            /// Get a new matrix, replacing all instances of val with the item with the same coordinates in B
            /// </summary>
            public List<Reference> Mask(List<Reference> A, List<Reference> B, object value = null)
            {
                if (value == null) value = 0.0;

                if (value is double)
                    value = (BigDecimal)(double)(value);

                Matrix matA = (Matrix)new Matrix(A).GetDeepCopy();
                Matrix matB = new Matrix(B);
                for (int i = 0; i <= matA.Width - 1; i++)
                {
                    for (int j = 0; j <= matA.Height - 1; j++)
                    {
                        object val = matA.GetCoord(j, i);
                        bool match = false;

                        if (val is double)
                        {
                            if (CmpDbl((double)(val), (double)((BigDecimal)value)) == 0)
                                match = true;
                        }
                        else if (val is BigDecimal)
                        {
                            if (((BigDecimal)val).Truncate(10) == (BigDecimal)value)
                                match = true;
                        }
                        else if (val is System.Numerics.Complex && value is System.Numerics.Complex)
                        {
                            if (CmpDbl(((System.Numerics.Complex)val).Magnitude, ((System.Numerics.Complex)value).Magnitude) == 0)
                                match = true;
                        }

                        if (match && j < matB.Height && i < matB.Width)
                        {
                            matA.SetCoord(j, i, matB.GetCoordRef(j, i));
                        }
                    }
                }

                return (List<Reference>)matA.GetValue();
            }


            /// <summary>
            /// Checks if a matrix is symmetric
            /// </summary>
            public bool IsSymmetric(List<Reference> A)
            {
                Matrix mat = (Matrix)new Matrix(A).GetDeepCopy();
                for (int i = 1; i <= mat.Width - 1; i++)
                {
                    for (int j = 0; j <= i - 1; j++)
                    {
                        object val = mat.GetCoord(j, i);
                        if (val is double)
                        {
                            if (CmpDbl((double)(val), 0) != 0)
                                return false;
                        }
                        else if (val is BigDecimal)
                        {
                            if (((BigDecimal)val).Truncate(10) != 0)
                                return false;
                        }
                        else if (val is System.Numerics.Complex)
                        {
                            if (CmpDbl(((System.Numerics.Complex)val).Magnitude, 0) != 0)
                                return false;
                        }
                    }
                }
                return true;
            }

            /// <summary>
            /// Checks if a matrix is upper trangular
            /// </summary>
            public bool IsUpperTri(List<Reference> A)
            {
                Matrix mat = (Matrix)new Matrix(A).GetDeepCopy();
                for (int i = 0; i <= mat.Width - 2; i++)
                {
                    for (int j = i + 1; j <= mat.Height - 1; j++)
                    {
                        object val = mat.GetCoord(j, i);
                        if (val is double)
                        {
                            if (CmpDbl((double)(val), 0) != 0)
                                return false;
                        }
                        else if (val is BigDecimal)
                        {
                            if (((BigDecimal)val).Truncate(10) != 0)
                                return false;
                        }
                        else if (val is System.Numerics.Complex)
                        {
                            if (CmpDbl(((System.Numerics.Complex)val).Magnitude, 0) != 0)
                                return false;
                        }
                    }
                }
                return true;
            }

            /// <summary>
            /// Checks if a matrix is lower trangular
            /// </summary>
            public bool IsLowerTri(List<Reference> A)
            {
                Matrix mat = (Matrix)new Matrix(A).GetDeepCopy();
                for (int i = 1; i <= mat.Width - 1; i++)
                {
                    for (int j = 0; j <= i - 1; j++)
                    {
                        object vall = mat.GetCoord(j, i);
                        object valu = mat.GetCoord(mat.Height - j - 1, mat.Width - i - 1);

                        if (vall is double)
                        {
                            if (CmpDbl((double)(vall), (double)(valu)) != 0)
                                return false;
                        }
                        else if (vall is BigDecimal)
                        {
                            if (((BigDecimal)vall).Truncate(10) != ((BigDecimal)valu).Truncate(10))
                                return false;
                        }
                        else if (vall is System.Numerics.Complex)
                        {
                            if (CmpDbl(((System.Numerics.Complex)vall).Magnitude, ((System.Numerics.Complex)valu).Magnitude) != 0)
                                return false;
                        }
                    }
                }
                return true;
            }

            /// <summary>
            /// Get a basis for the null space of a matrix, obtained with rref
            /// </summary>
            public List<Reference> NullSpace(List<Reference> lst)
            {
                Matrix mat = new Matrix(lst);
                mat = mat.Rref();

                Matrix ker = new Matrix(mat.Width, mat.Width - mat.Height + 1);

                for (int i = 0; i <= ker.Width - 1; i++)
                {
                    for (int j = 0; j <= ker.Width - 1; j++)
                    {
                        object val = mat.GetCoord(i, j + ker.Width);
                        if (val is double)
                        {
                            ker.SetCoord(i, j, -(double)(val));
                        }
                        else if (val is BigDecimal)
                        {
                            ker.SetCoord(i, j, -(BigDecimal)val);
                        }
                        else if (val is System.Numerics.Complex)
                        {
                            ker.SetCoord(i, j, -(System.Numerics.Complex)val);
                        }
                    }
                }

                for (int i = 0; i <= ker.Width - 1; i++)
                {
                    ker.SetCoord(ker.Width + i, i, 1);
                }

                return (List<Reference>)ker.GetValue();
            }

            /// <summary>
            /// Get the main diagonal of a matrix
            /// </summary>
            public List<Reference> Diag(List<Reference> lst)
            {
                Matrix mat = new Matrix(lst);
                List<Reference> diagonal = new List<Reference>(Math.Min(mat.Width, mat.Height));
                for (int i = 0; i <= Math.Min(mat.Width - 1, mat.Height - 1); i++)
                {
                    diagonal.Add(mat.GetCoordRef(i, i));
                }
                return diagonal;
            }

            /// <summary>
            /// Get a square matrix with the specified vector as its diagonal
            /// </summary>
            public List<Reference> AsDiag(List<Reference> lst)
            {
                Matrix vec = new Matrix(lst);
                if (vec.Width != 1)
                    throw new EvaluatorException("Matrix is not a column vector");

                Matrix mat = new Matrix(vec.Height, vec.Height);
                for (int i = 0; i <= vec.Height - 1; i++)
                {
                    mat.SetCoord(i, i, vec.GetCoordRef(i, 0));
                }
                return (List<Reference>)mat.GetValue();
            }

            /// <summary>
            /// Fill a collection with 0's
            /// </summary>
            public object Fill(object lst, object val = null)
            {
                if (val == null) val = 0.0;
                if (lst is IEnumerable<Reference>)
                {
                    foreach (Reference r in (IEnumerable<Reference>)lst)
                    {
                        Fill(r, val);
                    }
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    foreach (Reference r in ((IDictionary<Reference, Reference>)lst).Keys)
                    {
                        Fill(r, val);
                    }

                }
                else if (lst is Reference)
                {
                    ((Reference)lst).SetValue(val);
                }
                else
                {
                    lst = val;
                }

                return lst;
            }

            /// <summary>
            /// Create a new vector with the specified length, filled with the specified number (default 0)
            /// </summary>
            /// <param name="len"></param>
            /// <param name="fill"></param>
            /// <returns></returns>
            public IEnumerable<Reference> Vector(double len, object fill = null)
            {
                if (fill == null) fill = 0.0;
                List<Reference> vec = new List<Reference>(Int(len));
                for (int i = 1; i <= Int(len); i++)
                {
                    vec.Add(new Reference(fill));
                }
                return vec;
            }

            /// <summary>
            /// Get a column vector with the specified length filled with ones.
            /// </summary>
            public IEnumerable<Reference> Ones(double len)
            {
                return Vector(len, 1);
            }

            /// <summary>
            /// Get a column vector with the specified length filled with zeros.
            /// </summary>
            public IEnumerable<Reference> Zeros(double len)
            {
                return Vector(len);
            }

            /// <summary>
            /// Convert to set
            /// </summary>
            public SortedDictionary<Reference, Reference> ToSet(object lst)
            {
                Set tmp = default(Set);
                if (lst is IEnumerable<Reference>)
                {
                    tmp = new Set((IEnumerable<Reference>)lst);
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    tmp = new Set((IDictionary<Reference, Reference>)lst);
                }
                else
                {
                    tmp = new Set(new[] { new Reference(lst) }.ToList());
                }
                return (SortedDictionary<Reference, Reference>)tmp.GetValue();
            }

            /// <summary>
            /// Create a new set from any object
            /// </summary>
            public SortedDictionary<Reference, Reference> Set(object lst = null)
            {
                if (lst == null)
                    return new SortedDictionary<Reference, Reference>();
                return ToSet(lst);
            }

            /// <summary>
            /// Convert to hashset
            /// </summary>
            public Dictionary<Reference, Reference> ToHashSet(object lst)
            {
                HashSet tmp = default(HashSet);
                if (lst is IEnumerable<Reference>)
                {
                    tmp = new HashSet((IEnumerable<Reference>)lst);
                }
                else if (lst is IDictionary<Reference, Reference>)
                {
                    tmp = new HashSet((IDictionary<Reference, Reference>)lst);
                }
                else
                {
                    tmp = new HashSet(new[] { new Reference(lst) }.ToList());
                }
                return (Dictionary<Reference, Reference>)tmp.GetValue();
            }

            /// <summary>
            /// Create a new hashset from any object
            /// </summary>
            public Dictionary<Reference, Reference> HashSet(object lst = null)
            {
                if (lst == null)
                    return new Dictionary<Reference, Reference>();
                return ToHashSet(lst);
            }

            // setionary/set

            /// <summary>
            /// Computes the union of two sets
            /// </summary>
            public IDictionary<Reference, Reference> Union(IDictionary<Reference, Reference> set1, IDictionary<Reference, Reference> set2)
            {
                foreach (KeyValuePair<Reference, Reference> kv in set2)
                {
                    set1[kv.Key] = kv.Value;
                }
                return set1;
            }

            /// <summary>
            /// Computes the intersection of two sets
            /// </summary>
            public IDictionary<Reference, Reference> Intersect(IDictionary<Reference, Reference> set1, IDictionary<Reference, Reference> set2)
            {
                List<Reference> toRem = new List<Reference>();
                foreach (KeyValuePair<Reference, Reference> kv in set1)
                {
                    if (!set2.ContainsKey(kv.Key))
                        toRem.Add(kv.Key);
                }
                foreach (Reference r in toRem)
                {
                    set1.Remove(r);
                }
                return set1;
            }

            /// <summary>
            /// Computes the difference of two sets
            /// </summary>
            public IDictionary<Reference, Reference> Difference(IDictionary<Reference, Reference> set1, IDictionary<Reference, Reference> set2)
            {
                foreach (KeyValuePair<Reference, Reference> kv in set2)
                {
                    if (set1.ContainsKey(kv.Key))
                        set1.Remove(kv.Key);
                }
                return set1;
            }

            /// <summary>
            /// Computes the symmetric difference of two sets
            /// </summary>
            public IDictionary<Reference, Reference> DifferenceSymmetric(IDictionary<Reference, Reference> set1, IDictionary<Reference, Reference> set2)
            {
                foreach (KeyValuePair<Reference, Reference> kv in set2)
                {
                    if (set1.ContainsKey(kv.Key))
                    {
                        set1.Remove(kv.Key);
                    }
                    else
                    {
                        set1[kv.Key] = kv.Value;
                    }
                }
                return set1;
            }

            /// <summary>
            /// Returns true if the specified setionary (set) contains the key
            /// </summary>
            public bool ContainsKey(IDictionary<Reference, Reference> @set, object val)
            {
                return @set.ContainsKey(new Reference(ObjectTypes.DetectType(val)));
            }

            /// <summary>
            /// Returns true if the specified setionary (set) contains the value
            /// </summary>
            public bool ContainsValue(IDictionary<Reference, Reference> @set, object val)
            {
                Reference valRef = new Reference(ObjectTypes.DetectType(val));
                foreach (Reference r in @set.Values)
                {
                    if (ObjectComparer.CompareObjs(r, valRef) == 0)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Convert to a matrix
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public List<Reference> ToMatrix(object lst)
            {
                List<Reference> ret = new List<Reference>();
                if (lst is IDictionary)
                {
                    foreach (KeyValuePair<Reference, Reference> k in (IDictionary<Reference, Reference>)lst)
                    {
                        if (k.Value == null)
                        {
                            ret.Add(k.Key);
                        }
                        else
                        {
                            ret.Add(new Reference(new[]{
                            k.Key,
                            k.Value
                        }));
                        }
                    }
                }
                else if (lst is IEnumerable<Reference>)
                {
                    ret.AddRange((IEnumerable<Reference>)lst);
                }
                else
                {
                    ret.Add(new Reference(lst));
                }
                return ret;
            }

            /// <summary>
            /// Alias for ToMatrix(lst); Creates a matrix from another collection or object
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public List<Reference> ToList(object lst)
            {
                return ToMatrix(lst);
            }

            /// <summary>
            /// Convert to a matrix or create a new matrix with a rows and b columns filled with 0
            /// </summary>
            /// <param name="lstOrRows">either a collection or the number of rows</param>
            /// <param name="cols">number of columns</param>
            /// <returns></returns>
            public List<Reference> Matrix(object lstOrRows = null, object cols = null)
            {
                if (lstOrRows == null)
                    return new List<Reference>();
                if (cols == null)
                {
                    return ToMatrix(lstOrRows);
                }
                else
                {
                    return (List<Reference>)new Matrix(Int((double)(lstOrRows)), Int((double)(cols))).GetValue();
                }
            }

            /// <summary>
            /// Convert to a linked list
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public LinkedList<Reference> ToLinkedList(object lst)
            {
                LinkedList<Reference> ret = new LinkedList<Reference>();
                if (lst is IDictionary)
                {
                    foreach (KeyValuePair<Reference, Reference> k in (IDictionary<Reference, Reference>)lst)
                    {
                        if (k.Value == null)
                        {
                            ret.AddLast(k.Key);
                        }
                        else
                        {
                            ret.AddLast(new Reference(ToLinkedList(new[]{
                            k.Key,
                            k.Value
                        })));
                        }
                    }
                }
                else if (lst is IEnumerable<Reference>)
                {
                    foreach (Reference r in (IEnumerable<Reference>)lst)
                    {
                        ret.AddLast(r);
                    }
                }
                else
                {
                    ret.AddLast(new Reference(lst));
                }
                return ret;
            }

            /// <summary>
            /// Alias for ToLinkedList(lst); Creates a new linked list 
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public LinkedList<Reference> LinkedList(object lst = null)
            {
                if (lst == null)
                    return new LinkedList<Reference>();
                return ToLinkedList(lst);
            }

            /// <summary>
            /// Initialize an array with the specified number of dimensions, with only one item at 0.
            /// </summary>
            /// <returns></returns>
            public List<Reference> Array(double dimensions)
            {
                int d = Int(dimensions);
                if (d < 0)
                    throw new EvaluatorException("Array dimensions cannot be negative");
                if (d > 25)
                    throw new EvaluatorException("Array dimensions too large: please keep under 15 dimensions");
                if (d == 0)
                    return new List<Reference>();
                // empty

                List<Reference> lst = new List<Reference>(new[] { new Reference(new Number(0.0)) });
                for (int i = 2; i <= d; i++)
                {
                    lst = new List<Reference>(new[] { new Reference(new Matrix(lst)) });
                }
                return lst;
            }
            
            public List<Reference> Range(BigDecimal a, BigDecimal b, double step = 1.0)
            {
                List<Reference> lst = new List<Reference>();
                for (BigDecimal d = a; d < b; d += step)
                {
                    lst.Add(new Reference(d));
                }
                return lst;
            }

            /// <summary>
            /// Convert to a tuple
            /// </summary>
            /// <param name="lst"></param>
            /// <returns></returns>
            public Reference[] ToTuple(object lst)
            {
                List<Reference> ret = new List<Reference>();
                if (lst is IDictionary)
                {
                    try
                    {
                        foreach (KeyValuePair<Reference, Reference> k in (IDictionary<Reference, Reference>)lst)
                        {
                            if (k.Value == null)
                            {
                                ret.Add(k.Key);
                            }
                            else
                            {
                                ret.Add(new Reference(new[]{
                                k.Key,
                                k.Value
                            }));
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                else if (lst is IEnumerable<Reference>)
                {
                    ret.AddRange((IEnumerable<Reference>)lst);
                }
                else
                {
                    ret.Add(new Reference(lst));
                }
                return ret.ToArray();
            }

            /// <summary>
            /// Convert to a tuple; Alias for ToTuple(lst)
            /// </summary>
            /// <returns></returns>
            public Reference[] Tuple(object lst = null)
            {
                if (lst == null)
                    return new Reference[] { };
                return ToTuple(lst);
            }

            // complex number stuff
            /// <summary>
            /// Create a new complex number from real and imaginary parts
            /// </summary>
            /// <returns></returns>
            public System.Numerics.Complex ToComplex(double real, double imag = 0)
            {
                return new System.Numerics.Complex(real, imag);
            }

            /// <summary>
            /// Create a new complex number from real and imaginary parts
            /// </summary>
            /// <returns></returns>
            public System.Numerics.Complex Complex(double real = 0, double imag = 0)
            {
                return ToComplex(real, imag);
            }

            /// <summary>
            /// Get the conjugate of the complex number
            /// </summary>
            public System.Numerics.Complex Conjugate(System.Numerics.Complex val)
            {
                return System.Numerics.Complex.Conjugate(val);
            }

            /// <summary>
            /// Get the reciprocal of the complex number
            /// </summary>
            public System.Numerics.Complex Reciprocal(System.Numerics.Complex val)
            {
                return System.Numerics.Complex.Reciprocal(val);
            }

            /// <summary>
            /// Get the real part of the complex number
            /// </summary>
            public double Real(System.Numerics.Complex val)
            {
                return val.Real;
            }

            /// <summary>
            /// Get the imaginary part of the complex number
            /// </summary>
            public double Imag(System.Numerics.Complex val)
            {
                return val.Imaginary;
            }

            /// <summary>
            /// Get the magnitude of a complex number or a column vector
            /// </summary>
            public object Magnitude(object val)
            {
                if (val is System.Numerics.Complex)
                {
                    return ((System.Numerics.Complex)val).Magnitude;
                }
                else if (val is IList<Reference>)
                {
                    return new Matrix((IList<Reference>)val).Magnitude();
                }
                else
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Get the phase of a complex number 
            /// </summary>
            public double Phase(System.Numerics.Complex val)
            {
                return val.Phase;
            }

            /// <summary>
            /// Create a new complex number from polar coordinates.
            /// </summary>
            public System.Numerics.Complex FromPolar(double magnitude, double phase)
            {
                return System.Numerics.Complex.FromPolarCoordinates(magnitude, phase);
            }

            /// <summary>
            /// find out If the Object Is 'truthy' (e.g. for numbers, all numbers other than 0 are considered truthy), for use with conditions
            /// </summary>
            public bool IsTrue(object obj)
            {
                if (obj is bool)
                {
                    return Convert.ToBoolean(obj) == true;
                }
                else if (obj is int || obj is Int32)
                {
                    return Int((double)(obj)) != 0;
                }
                else if (obj is double || obj is float)
                {
                    return ((double)(obj) - 0) > 1E-08;
                }
                else if (obj is BigDecimal)
                {
                    return ((BigDecimal)obj - 0) > 1E-08;
                }
                else if (obj is string)
                {
                    return obj.ToString().ToLower() == "true";
                }
                else if (obj is System.DateTime || obj is System.DateTime)
                {
                    return Convert.ToDateTime(obj) == System.DateTime.Now;
                }
                else
                {
                    return false;
                }
            }

            // limits for functions
            /// <summary>
            /// If the variable specified (default x) is between l and r, then evaluates func and returns the value
            /// Otherwise returns undefined.
            /// </summary>
            public object SliceFunction(object text, double left, double right, string var = "x")
            {
                try
                {
                    double varval = (double)(_eval.GetVariable(var));
                    if (varval >= left && varval <= right)
                    {
                        double ans = (double)(_eval.EvalExprRaw(text.ToString()));
                        return ans;
                    }
                    else
                    {
                        return double.NaN;
                    }
                }
                catch
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// If dom evaluates to a 'truthy' value, then evaluates func and returns the value
            /// Otherwise returns undefined.
            /// </summary>
            public object Domain(object func, string dom)
            {
                try
                {
                    if (IsTrue(_eval.EvalExprRaw(dom)))
                    {
                        double ans = (double)(_eval.EvalExprRaw(func.ToString()));
                        return ans;
                    }
                    else
                    {
                        return double.NaN;
                    }
                }
                catch
                {
                    return double.NaN;
                }
            }

            // input / output
            // command line
            /// <summary>
            /// Prints to the console
            /// </summary>
            public void Print(object text = null)
            {
                if (text == null) text = "";
                if ((WriteOutput != null))
                {
                    if (WriteOutput != null)
                    {
                        WriteOutput(_eval, new IOEventArgs(IOMessage.writeText, text.ToString()));
                    }
                }
                else
                {
                    Console.Write(text.ToString());
                }
            }

            /// <summary>
            /// Prints the text to the console, followed immediately by a line break
            /// </summary>
            public void PrintLine(object text = null)
            {
                if (text == null) text = "";
                if ((WriteOutput != null))
                {
                    if (WriteOutput != null)
                    {
                        WriteOutput(_eval, new IOEventArgs(IOMessage.writeText, text.ToString() + Environment.NewLine));
                    }
                }
                else
                {
                    Console.WriteLine(text.ToString());
                }
            }

            /// <summary>
            /// Read a line from the console
            /// </summary>
            public string ReadLine(string message = "")
            {
                if (!string.IsNullOrWhiteSpace(message))
                    PrintLine(message);
                if (ReadInput != null)
                {
                    object userInput = "";
                    if (ReadInput != null)
                    {
                        ReadInput(_eval, new IOEventArgs(IOMessage.readLine, message), out userInput);
                    }
                    if ((userInput != null))
                    {
                        return userInput.ToString();
                    }
                }
                return Console.ReadLine();
            }

            /// <summary>
            /// Read a word from the console
            /// </summary>
            public string Read(string message = "")
            {
                if (!string.IsNullOrWhiteSpace(message))
                    PrintLine(message);
                if (ReadInput != null)
                {
                    object userInput = "";
                    ReadInput(_eval, new IOEventArgs(IOMessage.readWord, message), out userInput);
                    userInput = userInput.ToString().TrimStart();
                    if ((userInput != null))
                    {
                        for (int i = 0; i <= userInput.ToString().Count() - 1; i++)
                        {
                            char cI = userInput.ToString()[i];
                            if ((int)(cI) <= (int)(' '))
                                return userInput.ToString().Remove(i);
                        }
                        return userInput.ToString();
                    }
                }

                StringBuilder ret = new StringBuilder();
                while (true)
                {
                    int c = Convert.ToChar(Console.ReadKey().KeyChar);
                    if (c <= (int)(' '))
                        break;
                    ret.Append((char)(c));
                }

                Console.Write("/" + ret.ToString() + "/");
                return ret.ToString();
            }

            /// <summary>
            /// Read a character from the console, if available
            /// </summary>
            public string ReadChar(string message = "")
            {
                if (!string.IsNullOrWhiteSpace(message))
                    PrintLine(message);
                if ((ReadInput != null))
                {
                    object userInput = "";
                    if (ReadInput != null)
                    {
                        ReadInput(_eval, new IOEventArgs(IOMessage.readChar, message), out userInput);
                    }
                    if ((userInput != null) && userInput.ToString().Length > 0)
                    {
                        return userInput.ToString()[0].ToString();
                    }
                }
                return ((char)(Console.Read())).ToString();
            }

            /// <summary>
            /// Generic method for prompting for confirmation
            /// </summary>
            private bool InternalConfirm(object message = null, string yesMessage = "ok", string noMessage = "cancel")
            {
                if (message == null) message = "";
                if (!string.IsNullOrWhiteSpace(message.ToString()))
                    PrintLine(message);
                if ((ReadInput != null))
                {
                    object userInput = false;
                    if (ReadInput != null)
                    {
                        ReadInput(_eval, new IOEventArgs(IOMessage.confirm, message.ToString(), new Dictionary<string, object> {
                        {
                            "yes",
                            yesMessage
                        },
                        {
                            "no",
                            noMessage
                        }
                    }), out userInput);
                    }
                    if (!(userInput is bool))
                    {
                        throw new EvaluatorException("Invalid type returned to confirmation request: boolean expected.");
                    }
                    if ((userInput != null))
                        return Convert.ToBoolean(userInput);
                }

                if (!string.IsNullOrWhiteSpace(message.ToString()))
                    PrintLine(message);
                string result = "";
                yesMessage = yesMessage.ToLowerInvariant();
                noMessage = noMessage.ToLowerInvariant();
                while (true)
                {
                    PrintLine(string.Format("Please enter \"{0}\" or \"{1}\":", yesMessage, noMessage));
                    result = Console.ReadLine().ToLowerInvariant();
                    if (result == yesMessage || result == "Y")
                    {
                        return true;
                    }
                    else if (result == noMessage || result == "N")
                    {
                        return false;
                    }
                    else
                    {
                        PrintLine("Invalid response.");
                    }
                }
            }

            /// <summary>
            /// Ask the user for confirmation in the form of yes and no
            /// </summary>
            public bool Confirm(object message = null)
            {
                return InternalConfirm(message, "ok", "cancel");
            }

            /// <summary>
            /// Ask the user for confirmation in the form of yes and no
            /// </summary>
            public bool YesNo(object message  = null)
            {
                return InternalConfirm(message, "yes", "no");
            }

            /// <summary>
            /// Clear the console
            /// </summary>
            public void ClearConsole()
            {
                try {
                    if (RequestClearConsole != null)
                    {
                        RequestClearConsole(_eval, new EventArgs());
                    }
                    else
                    {
                        Console.Clear();
                    }
                }
                catch 
                {
                    throw new EvaluatorException("No console available");
                }
            }

            // file IO
            /// <summary>
            /// Read text from a file
            /// </summary>
            /// <param name="path"></param>
            /// <returns></returns>
            public string ReadFileText(string path)
            {
                try
                {
                    return System.IO.File.ReadAllText(path);
                    //ex As Exception
                }
                catch
                {
                    return "";
                }
            }

            /// <summary>
            /// Read a file as a series of numbers representing bytes
            /// </summary>
            /// <param name="path"></param>
            /// <returns></returns>
            public List<Reference> ReadFile(string path)
            {
                try
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    List<Reference> lst = new List<Reference>();
                    foreach (byte b in bytes)
                    {
                        lst.Add(new Reference((double)(Convert.ToInt32(b))));
                    }
                    return lst;
                    //ex As Exception
                }
                catch
                {
                    return null;
                }
            }

            /// <summary>
            /// Read the specified line of text from a file
            /// </summary>
            /// <param name="path"></param>
            /// <param name="line"></param>
            /// <returns></returns>
            public object ReadFileLine(string path, double line)
            {
                try
                {
                    return System.IO.File.ReadLines(path).Skip(Int(line) - 1).Take(1).First();
                    //ex As Exception
                }
                catch
                {
                    return double.NaN;
                }
            }

            /// <summary>
            /// Overwrite a file with bytes; or, if append is true, appends to the file
            /// </summary>
            public void WriteFile(string path, List<Reference> content, bool append = false)
            {
                try
                {
                    List<byte> bytes = new List<byte>();

                    foreach (Reference r in content)
                    {
                        object obj = r.Resolve();
                        if (obj is double)
                        {
                            bytes.Add(Convert.ToByte(Int((double)(obj))));
                        }
                        else if (obj is BigDecimal)
                        {
                            bytes.Add(Convert.ToByte(Int((double)((BigDecimal)obj))));
                        }
                    }

                    if (append)
                    {
                        using (FileStream stream = new FileStream(path, FileMode.Append))
                        {
                            stream.Write(bytes.ToArray(), 0, bytes.Count);
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(path, bytes.ToArray());
                    }
                    //ex As Exception
                }
                catch
                {
                    throw new EvaluatorException("Failed to write to file: " + path);
                }
            }

            /// <summary>
            /// Overwrite a file with text; or, if append is true, appends to the file
            /// </summary>
            public void WriteFileText(string path, object content, bool append = false)
            {
                try
                {
                    if (append)
                    {
                        System.IO.File.AppendAllText(path, content.ToString());
                    }
                    else
                    {
                        System.IO.File.WriteAllText(path, content.ToString());
                    }
                    //ex As Exception
                }
                catch
                {
                    throw new EvaluatorException("Failed to write to file: " + path);
                }
            }

            /// <summary>
            /// Write to the specified line in the file
            /// </summary>
            public void WriteFileLine(string path, int line, object content)
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(path);
                    lines[Int(line - 1)] = content.ToString();
                    File.WriteAllText(path, string.Join(Environment.NewLine, lines));
                    //ex As Exception
                }
                catch
                {
                    throw new EvaluatorException("Failed to write to file: " + path);
                }
            }

            /// <summary>
            /// Append to file, equal to Write(path, content, true)
            /// </summary>
            public void AppendFile(string path, object content)
            {
                WriteFileText(path, content, true);
            }

            /// <summary>
            /// Move a file
            /// </summary>
            public void MoveFile(string path, string path2)
            {
                try
                {
                    File.Move(path, path2);
                }
                catch
                {
                    throw new EvaluatorException("Failed move file: " + path);
                }
            }

            /// <summary>
            /// Rename a file
            /// </summary>
            public void RenameFile(string path, string newname)
            {
                try
                {
                    File.Move(path, Path.Combine(Path.GetDirectoryName(path), newname));
                }
                catch
                {
                    throw new EvaluatorException("Failed to rename file: " + path);
                }
            }

            /// <summary>
            /// Delete a file
            /// </summary>
            public void DeleteFile(string path)
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    throw new EvaluatorException("Failed delete file: " + path);
                }
            }

            /// <summary>
            /// Move a directory
            /// </summary>
            public void MoveDirectory(string path, string newPath)
            {
                try
                {
                    Directory.Move(path, newPath);
                }
                catch
                {
                    throw new EvaluatorException("Failed move directory: " + path);
                }
            }

            /// <summary>
            /// Rename a directory
            /// </summary>
            public void RenameDirectory(string path, string newName)
            {
                try
                {
                    Directory.Move(path, Path.Combine(Path.GetDirectoryName(path), newName));
                }
                catch
                {
                    throw new EvaluatorException("Failed rename directory: " + path);
                }
            }

            /// <summary>
            /// Delete a directory
            /// </summary>
            public bool DeleteDir(string path)
            {
                Directory.Delete(path, true);
                return true;
            }

            /// <summary>
            /// List the files and directories at the specified file system path
            /// </summary>
            public IEnumerable<Reference> ListDir(string path)
            {
                return (IEnumerable<Reference>)new Matrix(Directory.EnumerateFileSystemEntries(path).ToList()).GetValue();
            }

            /// <summary>
            /// List the files at the specified file system path
            /// </summary>
            public IEnumerable<Reference> ListFiles(string path)
            {
                return (IEnumerable<Reference>)new Matrix(Directory.EnumerateFiles(path).ToList()).GetValue();
            }

            /// <summary>
            /// List the directories at the specified file system path
            /// </summary>
            public IEnumerable<Reference> ListDirs(string path)
            {
                return (IEnumerable<Reference>)new Matrix(Directory.EnumerateDirectories(path).ToList()).GetValue();
            }

            /// <summary>
            /// Checks if the specified file exists in the filesystem
            /// </summary>
            public bool FileExists(string path)
            {
                return File.Exists(path);
            }

            /// <summary>
            /// Checks if the specified file exists in the filesystem
            /// </summary>
            public bool DirExists(string path)
            {
                return Directory.Exists(path);
            }

            /// <summary>
            /// Given the full path, gets the file name 
            /// </summary>
            public string GetFileName(string path)
            {
                return Path.GetFileName(path);
            }

            /// <summary>
            /// Given the full path, gets the file extension 
            /// </summary>
            public string GetFileExt(string path)
            {
                return Path.GetExtension(path);
            }

            /// <summary>
            /// Given the full path of a file and a new extension, 
            /// returns a file name with the new extension
            /// </summary>
            public string ChangeFileExt(string path, string extension)
            {
                return Path.ChangeExtension(path, extension);
            }

            /// <summary>
            /// Given the full path, gets the file name without the extension 
            /// </summary>
            public string GetFileNameNoExt(string path)
            {
                return Path.GetFileNameWithoutExtension(path);
            }


            /// <summary>
            /// Given the partial path of a file, returns the absolute path
            /// </summary>
            public string GetFullPath(string path)
            {
                return Path.GetFullPath(path);
            }

            /// <summary>
            /// Get a random file name
            /// </summary>
            public string GetRandomFileName()
            {
                return Path.GetRandomFileName();
            }

            /// <summary>
            /// Given the full path, gets the directory name of the file
            /// </summary>
            public string GetDirPath(string path)
            {
                return Path.GetDirectoryName(path);
            }

            /// <summary>
            /// Combines two paths into one
            /// </summary>
            public string JoinPath(string path1, string path2)
            {
                return Path.Combine(path1, path2);
            }

            /// <summary>
            /// Get the base directory
            /// </summary>
            public string CurrentDir()
            {
                return Environment.CurrentDirectory;
            }

            /// <summary>
            /// Get the desktop path
            /// </summary>
            public string DesktopDir()
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            /// <summary>
            /// Get the desktop path
            /// </summary>
            public string PublicDesktopDir()
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            }

            /// <summary>
            /// Get the program files directory
            /// </summary>
            public string ProgramFilesPath()
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            }

            /// <summary>
            /// Get the program files (x86) directory
            /// </summary>
            public string ProgramFilesX86Path()
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            }

            /// <summary>
            /// Get the temp directory
            /// </summary>
            public string TempDir()
            {
                return Path.GetTempPath();
            }

            /// <summary>
            /// Get the appdata directory
            /// </summary>
            public string AppDataDir()
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            /// <summary>
            /// Get the appdata directory for all users
            /// </summary>
            public string PublicAppDataDir()
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            }


            /// <summary>
            /// Get the path of the executing file
            /// </summary>
            public string ExecPath()
            {
                return _eval.ExecPath[Thread.CurrentThread.ManagedThreadId];
            }

            /// <summary>
            /// Get the directory of the executing file
            /// </summary>
            public string ExecDir()
            {
                return _eval.ExecDir[Thread.CurrentThread.ManagedThreadId];
            }

            /// <summary>
            /// Get the path of the Cantus core dll
            /// </summary>
            public string CantusPath()
            {
                return Assembly.GetExecutingAssembly().Location;
            }

            /// <summary>
            /// Get the directory where the Cantus executable resides in
            /// </summary>
            public string CantusDir()
            {
                return Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location);
            }

            /// <summary>
            /// Get the system's path separator (/ for linux, \ for windows)
            /// </summary>
            public string GetPathSeparator()
            {
                return Path.DirectorySeparatorChar.ToString();
            }

            // threading 

            /// <summary>
            /// Dictionary of threads created by async operations.
            /// </summary>

            private Dictionary<int, Thread> asyncThreads = new Dictionary<int, Thread>();
            /// <summary>
            /// Start an asynchronous task.
            /// </summary>
            /// <returns>The id of the thread started</returns>
            public int Async(Lambda func, List<Reference> args = null, Lambda callback = null)
            {
                try
                {
                    if (args == null)
                        args = new List<Reference>();
                    return func.ExecuteAsync(_eval, args, callback);
                }
                catch
                {
                    return -1;
                }
            }

            /// <summary>
            /// Cause the current thread to wait the specified number of seconds
            /// </summary>
            /// <param name="seconds"></param>
            /// <returns></returns>
            public bool Wait(double seconds)
            {
                Thread.Sleep(Int(seconds * 1000));
                return true;
            }

            /// <summary>
            /// Get a thread with the given id
            /// </summary>
            private Thread GetThread(int id)
            {
                return _eval.ThreadController.GetThreadById(id);
            }

            /// <summary>
            /// Killed the thread with the specified id
            /// </summary>
            public void KillThread(double id)
            {
                try
                {
                    _eval.ThreadController.KillThreadWithId(Int(id));
                }
                catch (ThreadAbortException)
                {
                }
                catch (NullReferenceException)
                {
                    throw new EvaluatorException(string.Format("No thread with id {0} found.", id));
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            /// <summary>
            /// Wait until the thread with the specified id is done
            /// </summary>
            public void JoinThread(double id)
            {
                try
                {
                    GetThread(Int(id)).Join();
                }
                catch (ThreadAbortException)
                {
                }
                catch (NullReferenceException)
                {
                    throw new EvaluatorException(string.Format("No thread with id {0} found.", id));
                }
                catch (Exception)
                {
                    throw new EvaluatorException("Failed to join thread");
                }
            }

            /// <summary>
            /// Alias for jointhread
            /// </summary>
            public void WaitUntilDone(double id)
            {
                JoinThread(id);
            }

            /// <summary>
            /// Start a process from the specified filesystem path, wait for completion, and get the return value
            /// </summary>
            public string StartWait(string path, string args = "")
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    throw new EvaluatorException("Error: file does not exist");
                Process p = new Process();
                ProcessStartInfo si = new ProcessStartInfo(path, args);
                si.UseShellExecute = true;
                si.RedirectStandardOutput = true;
                p.StartInfo = si;
                p.Start();

                string ret = null;
                using (System.IO.StreamReader oStreamReader = p.StandardOutput)
                {
                    ret = oStreamReader.ReadToEnd();
                }
                return ret;
            }

            /// <summary>
            /// Start a process from the specified filesystem path without waiting for completion
            /// </summary>
            public void Start(string path, string args = "")
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    throw new EvaluatorException("Error: file does not exist");
                Process p = new Process();
                ProcessStartInfo si = new ProcessStartInfo(path, args);
                si.UseShellExecute = true;
                si.RedirectStandardOutput = true;
                p.StartInfo = si;
                p.Start();
            }

            /// <summary>
            /// Execute a script at the specified path, saves the result into var and executes runAfter
            /// </summary>
            public void Run(string path, Lambda callback = null)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    throw new EvaluatorException("Error: script does not exist");
                CantusEvaluator tmp = _eval.DeepCopy();
                tmp.EvalComplete += (object sender, AnswerEventArgs e) => { callback.Execute(_eval, new[] { e.Result }); };
                string prevDir = _eval.ExecDir[Thread.CurrentThread.ManagedThreadId];
                string prevPath = _eval.ExecPath[Thread.CurrentThread.ManagedThreadId];
                try {
                    _eval.ExecPath[Thread.CurrentThread.ManagedThreadId] = path;
                    _eval.ExecDir[Thread.CurrentThread.ManagedThreadId] = Path.GetDirectoryName(path);
                    tmp.EvalAsync(File.ReadAllText(path));
                }
                finally
                {
                    _eval.ExecDir[Thread.CurrentThread.ManagedThreadId] = prevDir;
                    _eval.ExecPath[Thread.CurrentThread.ManagedThreadId] = prevPath;
                }
            }

            /// <summary>
            /// Execute the script at the specified path, wait, and return the result
            /// </summary>
            public object RunWait(string path)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    throw new EvaluatorException("Error: script does not exist");
                return _eval.EvalRaw(File.ReadAllText(path), noSaveAns: true);
            }

            /// <summary>
            /// Download from the specified url to the specified path and wait for completion
            /// </summary>
            public bool DownloadWait(string url, string path)
            {
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(new Uri(url), path);
                }
                return System.IO.File.Exists(path);
            }

            /// <summary>
            /// Download from the specified url to the specified path without waiting for completion
            /// </summary>
            public void Download(string url, string path)
            {
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFileAsync(new Uri(url), path);
                }
            }

            public bool UploadWait(string url, string path)
            {
                using (WebClient wc = new WebClient())
                {
                    wc.UploadFile(new Uri(url), path);
                }
                return true;
            }

            public void Upload(string url, string path)
            {
                using (WebClient wc = new WebClient())
                {
                    wc.UploadFileAsync(new Uri(url), path);
                }
            }

            public string DownloadText(string url)
            {
                using (WebClient wc = new WebClient())
                {
                    return wc.DownloadString(new Uri(url));
                }
            }

            public string DownloadSource(string url)
            {
                return DownloadText(url);
            }

            public string WebGet(string url, IDictionary<Reference, Reference> @params = null)
            {
                if (@params != null)
                {
                    url += "?";
                    foreach (KeyValuePair<Reference, Reference> k in @params)
                    {
                        if (url != "?")
                            url += "&";
                        url += k.Key.ToString() + "=" + k.Value.ToString();
                    }
                }
                return DownloadText(url);
            }

            public string WebPost(string url, IDictionary<Reference, Reference> @params = null)
            {
                using (WebClient wc = new WebClient())
                {
                    if (@params == null)
                        @params = new SortedDictionary<Reference, Reference>(new ObjectComparer());
                    NameValueCollection nvc = new NameValueCollection();
                    foreach (KeyValuePair<Reference, Reference> k in @params)
                    {
                        nvc.Add(k.Key.ToString(), k.Value.ToString());
                    }
                    byte[] response = wc.UploadValues(url, nvc);
                    return System.Text.Encoding.UTF8.GetString(response);
                }
            }

            public string UserGroup()
            {
                if (Environment.UserName.Contains("\\"))
                {
                    return Environment.UserName.Remove(Environment.UserName.IndexOf("\\"));
                }
                else
                {
                    return "";
                }
            }

            public string Username()
            {
                if (Environment.UserName.Contains("\\"))
                {
                    return Environment.UserName.Substring(Environment.UserName.IndexOf("\\") + 1);
                }
                else
                {
                    return Environment.UserName;
                }
            }

            public string Ver()
            {
                return Assembly.GetAssembly(typeof(InternalFunctions)).GetName().Version.ToString();
            }

            private string HKLM_GetString(string path, string key)
            {
                try
                {
                    Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                    if (rk == null)
                    {
                        return "";
                    }
                    return (string)rk.GetValue(key);
                }
                catch
                {
                    return "";
                }
            }


            public string OsName()
            {
                string ProductName = HKLM_GetString("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "ProductName");
                string CSDVersion = HKLM_GetString("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "CSDVersion");
                if (!string.IsNullOrEmpty(ProductName))
                {
                    return Convert.ToString(ProductName + (!string.IsNullOrEmpty(CSDVersion) ? Convert.ToString(" ") + CSDVersion : ""));
                }
                return "Unknown";
            }

            public string OsVer()
            {
                return Environment.OSVersion.ToString();
            }

            // removed functions, kept to prevent upgrade errors. Will be deleted eventually
            public string OMode(string val = "")
            {
                // older names to maintain compatibility
                switch (val)
                {
                    case "MathO":
                        val = "Math";
                        break;
                    case "SciO":
                        val = "Scientific";
                        break;
                    case "LineO":
                        val = "Raw";
                        break;
                }
                _Output(val);
                return "Removed in version 2.1. Please use _Output() instead";
            }

            public string AngleRep(string val = "")
            {
                _AngleRepr(val);
                return "Removed in version 2.1. Please use _AngleRepr() instead";
            }

            public string SpacesPerTab(double val = double.NaN)
            {
                _SpacesPerTab(val);
                return "Deprecated in version 2.1. Please use _SpacesPerTab() instead";
            }
        }
    }
}
