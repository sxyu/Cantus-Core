using System;
namespace Cantus.Core.Exceptions
{
    /// <summary>
    /// Represents any exception that occurs during evaluation
    /// </summary>
    public class EvaluatorException : Exception
    {
        public int Line { get; }
        public EvaluatorException() : base()
        {
        }
        public EvaluatorException(string message, int line = 0) : base(new CantusEvaluator(reloadDefault: false).Internals.Replace(message, " \\[Line [0-9]*\\]", "") + (line > 0 ? " [Line " + line + "]" : ""))
        {
            this.Line = line;
        }
    }

    /// <summary>
    /// Represents any exception caused by incorrect operator, statement, or statement syntax
    /// </summary>
    public class SyntaxException : EvaluatorException
    {
        public SyntaxException() : base("Syntax Error")
        {
        }
        public SyntaxException(string message, int line = 0) : base(!message.StartsWith("Syntax Error") ? "Syntax Error: " + message : message, line)
        {
        }
    }

    /// <summary>
    /// Represents any exception caused by invalid math operations 
    /// </summary>
    public class MathException : EvaluatorException
    {
        public MathException() : base("Math Error")
        {
        }
        public MathException(string message, int line = 0) : base(!message.StartsWith("Math Error") ? "Math Error: " + message : message, line)
        {
        }
    }
}