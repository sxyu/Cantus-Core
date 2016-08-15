# Cantus Core 2.4
*Cantus* is a lightweight yet powerful mathematical language.
This repo (Cantus Core) contains a free and open-source .NET library for running expressions and scripts written in the Cantus language.

This is designed to be **directly usable as a mathematical expression evaluator library** and to be usable with minimal set up.

For the full Cantus suite including an editor and a console, download and install the installer: 
[From here](https://github.com/sxyu/Cantus-Core/blob/master/setup/setup.exe)

### Quickstart
First, add `cantus.core.dll` as a reference in your project.

Then, import the Cantus.Core namespace for convenience:
```cs
using Cantus.Core;
```

Now you can add this to your main method (or wherever you want to use Cantus):
```cs
CantusEvaluator eval = new CantusEvaluator();

// Set evaluation modes.
// These specific settings (Radian, Math, etc.) are already the defaults, 
// so you don't really need to set them, but I'm writing them here for demo purposes

// Angle representation: Radian, Degree, or Gradian
eval.AngleMode = CantusEvaluator.eAngleRepresentation.Radian; 

// Math (auto-detect fractions and roots), Scientific (scientific notation), 
// or Raw (raw number, uses scientific for very large/very small values)
eval.OutputFormat = CantusEvaluator.eOutputFormat.Math; 

// If true, prevents implicit declarations of variables in scripts
eval.ExplicitMode = false;

// If true, automatically rounds according to significant figures when appropriate
eval.SignificantMode = false;

// An arbitrary script we'll try executing
string myScript = @"
    x = sin(pi/6)
    if x > 0
        return x / 4
";

// Execute scripts
Console.WriteLine(eval.Eval(myScript)); // output: 1/8 
Console.WriteLine(eval.EvalRaw(myScript)); // output: 0.125

// Evaluate one-line mathematical expressions
Console.WriteLine(eval.EvalExpr("3.5!")); // output: 58/5
Console.WriteLine(eval.EvalExprRaw("3.5!")); // output: 11.6
```

All of the above methods have asynchronous alternate versions.
For example, you can use `EvalAsync()` instead of `Eval()` to evaluate a full script asynchronously.
These methods raise the EvalComplete event when done.

## IO Handling
By default the CantusEvaluator will try to print to the standard output and read from the standard input.
However, this can sometimes be undesirable, and when multi-threaded, these operations can become unstable.

To handle IO yourself, you may handle the `ReadInput` and `WriteOutput` events. The `ReadInput` event has several message types that you should handle separately. Return the result by setting the `@return` parameter.

There is also a `ClearConsole` event that you can handle, normally raised when the console needs clearing.

## ScriptFeeder
This library also includes a ScriptFeeder class for running scripts line-by-line in real time.
The ScriptFeeder class is always associated with a CantusEvaluator. You may specify the evaluator
when constructing a ScriptFeeder. If you don't specify one, a new CantusEvaluator will be created.

### Basic Usage
```cs
// Another random script
string myScript = @"
    print("Hello ");
    print("World! ");
";
ScriptFeeder foo = new ScriptFeeder();
foo.Append(myScript); // add to the current script
foo.BeginExecution(); // starts the script

foo.SendNextLine(); // evaluate a line
// Console says: Hello 
foo.SendAllLines();
// Console says: Hello World!

string myScript2 = @"
    print("Today is ");
    print(today().dayofweekname());
";
foo.Append(myScript2, True); // true as the second argument will directly execute the newly appended script.
// Console says: Hello World! Today is Sunday (or whatever day it is)

foo.EndAfterQueueDone(); // Safely stops execution when everything is done
```

## Customization
You are welcome to customize/recompile it for any purposes. Credits would be greatly appreciated.

### Tips on Customization
If you want to add your own internal functions, you can easily do so by adding new public methods inside `InternalFunctions.cs`.

You can also add new operators inside `OperatorRegistar.cs` or new statements inside `StatementRegistar.cs`.

You can even modify `ObjectTypes.cs` to define extra types, though you'll then need to add methods inside
`InternalFunctions.cs` to create the type.

Note: The setup file and some info files have been included here to ease deployment (yes, I use some very lazy deployment methods). They are not strictly part of the source code.

The following section is adapted from the README of the editor project, with irrelevant information removed.

# Cantus Language
*Cantus* is a lightweight yet powerful mathematical language. 

### What you can do with Cantus:
  - Create and do calculations with **matrices**, **sets**, **dictionaries** and **tuples**
  - Perform operations on **complex numbers**
  - **Arbitrary precision** decimals for basic operations (+-*/^)
  - Use various built-in functions and operators
  - Use **flow control statements** like in a procedural language (If, For, While, Until, Repeat, Switch, etc.)
  - Use variable and function **scoping**
  - Automatically track significant figures in calculations
  - Customize
    - Declare and use **variables**
    - Declare **functions**
    - Run other .can scripts
    - Add initialization scripts to customize the calculatOrElse   - Graph **cartesian**, **inverse**, **polar**, or **parametric** functions as well as **slope fields**

### Basic Usage

* Basic settings of the evaluator:
    *  AngleRepr: Angle Representation: 
        *  Deg, Rad, Grad
    *  Output format:
        *  Math: Output fractions, roots, and multiples of PI
        *  Scientific: Output scientic notation
        *  Raw: Output numbers
    * Explicit: Force explicit variable declarations
    * SigFigs/Significant: Track sigfigs in numbers during computations
        * Use Raw or Scientific mode with SigFigs mode to see output rounded to the correct significant figure.
        * Rounding on 5's: up if odd, down if even.
        * Warning: sig fig tracking may behave unexpectedly with 0, 0.0, due to the fact that 0 has no sig figs.
    * You can also use functions to change these modes (discuss that later)
    * Click the version number to see the update log
    * To change settings, click the gear button the on the right or press `Alt`+`S`

#### Creating things
* `1.5` Define a number
* `1.2 E 1000` Define a number in scientific notation
* `true` Define  boolean value
* `"abc"` or `'abc'` To define strings
    *  \ to escape: \n, \t, \" etc.
* `datetime(year, month, day, hour,...)` To create a datetime value
* `[1,2,3]`  Create a matrix
* `{1,2,3}`  Create a set
* `{1:"a",2:"b",3:"c"}`  Create a dictionary
* `(1,2,3)` or `1,2,3`  Create a tuple
* To define a complex number, use one of
    *  `(2 - 1i)` Depends on the definition of i (i is defined as sqrt(-1) by default)
    *  `complex(real, imag)`  From real, imaginary parts
    *  `frompolar(magnitude, phase)` From polar coordinates

#### Saving something to a variable
> **A note on the 'partial' case sensitivity in Cantus:**  
> Variables and user-defined functions in Cantus are **case sensitive**  
> However, internal functions like`sin(v)` and statements like `if` are **case insensitive**    
> **Why the strange case sensitivity?**  
> This design lets the user define more functions and variables and differentiates between the upper case and lower case symbols (such as G and g in physics) that is often useful, while minimizing the chance of geting syntax errors by pressing shift accidentally

**There are three ways to make a variable:**

---- `foo = ...`  
This is the easiest (and usually best) way, but if a variable is already declared, this will assign to it in its original scope as opposed to declaring a new one.

```python
    let foo = 1
    run
        let foo = 2
        run
            foo = 3
        return foo
```
This will return 3.

----  `let foo = ...`  
This creates a new variable in the current scope. So for the abovecode, if instead of using `a = 1` on the third line we use a let statement, the result will be of the evaluation will be 2.

---- `global foo = ...`   
This works in the same way as the other declaraions, but the variable is declared in the global (highest) scope and the current scope. Intermediate scopes that declared variables with let are not affected.

---- 
**Now try:** `let myMatrix = [[1,2,3],[4,5,6],[7,8,9]]`

*Note: To unset a variable, simply do: foo = undefined*

> **Variable Resolution**  
> If you enter any identifier, say `abc`, *Cantus* tries to resolve variables from it by placing multiplication signs where appropriate. It first checks abc, then `ab*c`, then `a*bc`, and finally `a*b*c` (as a rule of thumb, it checks left before right, longest before shortest). If it cannot resolve any of these, it will give undefined. To force it to resolve a certain way, simply add `*` in between


#### Operators
There are all sorts of operators in *Cantus*. If you don't specify any operators, multiplication is used by default. 

Most operators work like you would expect them to. Operators for sets may not be as obvious at first: `*` specifies intersection, `+` union, `-` difference, and `^^` symmetric difference.

For matrices, `*` specified matrix or scalar multiplication or vector dot product, `+` is for addition (or appending), `/` scalar division, `^` for exponentiation, `^-1` for inverse, etc. 

If you want to duplicate a matrix like `[1,2]**3 = [1,2,1,2,1,2]`, use `**`. `**` is also the operator for vector cross product in R<sup>3</sup>. Note that matrices of only one column are seen as vectors.

`+` can be used for appending a single element, but if you want to append a another matrix/vector, you will need to use `append(lst,item)` (a function)

**Assignment operators:**   
As shown earlier, the basic assignment operator is simply `=`  
`=` actually functions both as an equality and as an assignment operator. It functions as an assignment operator only when the left side is something you can assign to, like a variable, and vice versa.   

To force an assignment, use `:=`. On the other hand, use `==` to force a comparison.  

You can use [operator]= (e.g. `+=` `*=`) for most basic operators as a shorthand for performing the operator before assigning. `++` and `--` are decrement operators (for the value before only).

*Chain assignment:* You can assign variables in a chain like `a=b=c=3`, since  assignment operators are evaluated right to left.

*Tuple assignment:* You can assign variables in tuples like `a,b=b,a` or `a,b,c,d=0`.

**Comparison operators:**   
Comparison operators `=` and `==` were discussed in the above section. Other comparison operators include `<`, `>`, `<=` (less than or equal to), `>=` (greater than or equal to), `<>`, and `!=` (not equal to)

**Notes on (Sort of) Unusual Operators:**  
* `!` is the factorial operatOrElse     * The logical not operator is just `not` 
    * The bitwise not operator is `~` 
* `|` is the absolute value operatOrElse    * The logical or operator is just `or`  
    * The bitwise or operator is `||`  
* `&` is the string concatenation operator (try `123 & "456"`)
    * The logical and operator is just `and`  
    * The bitwise and operator is `&&`  
* `^` is the exponentiation operatOrElse    * The logical xor operator is just `xor`  
    * The bitwise xor operator is `^^`  
* `%` is the percentage operator, and the modulo operator is just `mod`

**Indexing and Slicing**

Index using `[]`, as in `[1,2,3,4][0] = 1`. *We use zero based indexing.*
For dictionaries, just put the key: `dict[key]`  

Negative indices are counted from the end of the string, with the -1<sup>st</sup> index being the last item.

Python-like slicing is also supported: `[1,2,3,4,5,6][1:4]` or `"abc"[1:]` etc.

#### Functions

In all, Cantus has over 350 internal functions available for use, including aome for networking and filesystem access. Functions may be called by writing   
`functionname(param 1, param2, ...)`. Functions with no parameters may be called like `foo()` or `foo()`

**'Insert Function' Window**

You can explore all the functions by clicking the `f(x)` button to the left of the `ans` button on the right on-screen keyboard. Type away in this window to filter (regex enabled) functions. This window is used for finding and inserting functions. We have already discussed some of them earlier.

**Self-Referring Functions Calls**  
You can refer to functions in a different way:  
Instead of `append(lst,lst2)` `shuffle(lst)`
you can write `lst.append(lst2)` `lst.shuffle()`   
This notation feels like the normal member access function, but what it really does is set the first parameter to the calling object on the left.

Note that you can also do things like `(a,b).min()`. By using a tuple as the calling object, all the items in the tuple are used as parameters in order.

**Sorting**  
Sorting is done with the `sort(lst)` function. You can use `reverse(lst)` after to reverse the sorting. When sorting multiple lists in a list (matrix), lists are compared from the first item to the last item. Note that different object types are allowed in the same list, and the result be separated by type.

You can pass a function to the sort function as the second parameter. This function should accept two arguments and return -1 if the first item should come before (is smaller), 1 if it should come after (is larger), or 0 if the items are equal.

**Regular Expressions**     
`contains()` `startswith()` `endswith()` `replace()` `find()` `findend()` `regexismatch()` `regexmatch()` `regexmatchall()`     
All use regular expressions to match text.

If you are not familiar with Regex, [here's a good Tutorial](http://code.tutsplus.com/tutorials/you-dont-know-anything-about-regular-expressions-a-complete-guide--net-7869)

**Define A Function**  
A very standard basic function declaration:
```python
    function DestroyThisUniverse(param1, param2)
        if param2 = 0:
            return param1 / param2
        else:
            param2 = 0 # sorry
            return param1 / param2
```
(See the section below about blocks for more details on how the `if` `else` `return` etc. work.)

Now you can use `myFunction()` in the evaluator. This function should also appear at the top of the explore window mentioned above.

#### Basic functional programming
**Lambda Functions and Function Pointers**
Another way to define a function is using backticks like this:
`foo=\`1\`` or `foo=\`x=>x+1\``
This is called a lambda expression. With this you can use functions like sigma:
`sigma(`i=>i^2`,1,5)`

You can write a multiline lambda expression like this:
```python
    myFunction=\`x=>
    if x>0
        return x+1
    \`
```

All normal functions can also be used in this way. When no arguments are supplied,
they act as function pointers and can be assigned.

For example: 
```python
   b=sind(x)
   return b(30) # returns 0.5
```
*Tip: You can also do cool things like dydx(sin). Try drawing this in the graphing window.*

**Iteration and modification of collections using functional programming**
* `.each(func)` performs an action to each item in a collection, where the item is passed as the first argument
* `.filter(func)` returns a new collection with the items for which the function given returns true, where the item is passed as the first argument
* `.exclude(func)` returns a new collection with the items for which the function given returns false, where the item is passed as the first argument
* `.get(func)` returns the first item for which the function given returns true, where the item is passed as the first argument
* `.filterindex(func)` returns a new collection with the items for which the function given returns true, where the **index** is passed as the first argument
* `.every(interval)` loops through the collection, starting from index 0, incrementing by the specified interval and adding the item to the new collection each time. 

Example usage:
```python
   printline([1,2,3,4,5].filter(`x=>x mod 2 = 1`));
```
This will print [1, 3, 5].

#### Writing a Full Script: Statements and Blocks
You have seen an example of a script with blocks in the variable declaration section. The function declaration above is also really a block.

As in Python, blocks are formatted using indentation. However, unlike Python, blocks do not require ':' on the starting line. This is designed to minimize typing, decrease risk of error, and maximize readability.

**Example Block Structure:**
```python
    let a=1
    while true
        if a > 15
            break
        a += 1
    return a # returns 16
```

**List of Blocks**  
*For the next two lists:* Items in `<>` are variables/conditions that you will need to fill in. Items in `[]` are optional.
* `if <condition>` Runs if the condition is true.  
(may be followed by `elif <condition>` and `else`)
* `while <condition>` Runs while the condition is true
* `until <condition>` Runs until the condition is true
* `for <var>[, <var2>...] in <lst>` Runs once for each item in lst
* `for var=init to end [step step]` Loops until the variable reaches the end, incrementing by step each time. Step is 1 by default for end &gt; init and -1 for end &lt; init.
* `repeat number` Runs the block a certain number of times
* `run` Simply runs the block. Used to scope variables or in a `then` chain (see below)
* `try` Attempts to run the block. If an error occurs stops and runs `catch`, if available, setting a variable to the error message.
(may be followed by `catch <var>` and `else`)
* `with <var>` Allows you to refer var in the block by the name `this`. Also allows you to run self-referring functions without a variable name, implying var: `.removeat(3)`.
* `switch <var>` May be followed by many blocks of `case <value>`. Finds the first block where var=value and runs it, and skips the rest of the blocks. You may write statements outside the case blocks at the end to run 

**List of Inline Statements**  
* `return <value>` Stops all blocks and returns value to the top
* `continue` Stops all blocks up to the loop above and trolls it.
* `break` Break out of the loop above

**Infinite Loops**
* Try not to write loops like
```python
    while 1=1
        1=1
```
* This will create a infinite loop. Try it (trust me, your computer won't explode). No answer will be displayed.
* Fortunately for you Cantus runs these expressions on separate threads so the main program won't crash. However, this will take a lot of CPU resources for nothing and also if you do this several time the program may end up getting very slow / freezing / failing to close.
* Use the `_stopall()` Function` to stop these threads and recover resouces. You may (rarely, but occasionally) need to call this more than once if some threads aren't responding.

#### Running Scripts
After writing a script, save it as a .can (Cantus Script) file.

To run the script later, you can do one of the following:
* Go in command prompt (cmd) and type (without quotes or angle brackets)    
*"can &lt;filename&gt;.can"* (result written to console)
* Double click the file and select to open with the Cantus program (result not shown)
* Press `F5` in the editor ([/sxyu/Cantus-2]) and select the file (result written to label)
* Use the run(path) function (async) or runwait(path) function (single threaded)

**Run another program**     
The `start(path)` and `startwait(path)` functions facilitate adding new functionality by allowing you to call other programs from within Cantus. For `start(path)`, the output from the program is saved to the variable called "result" by default. For `startwait(path)`, the output is returned.

### Object-Oriented Programming

Cantus also supports basic OOP: you can create classes and use inheritance.
Example of some classes:
```python
class pet
    name = ""
    function init(name)
        this.name = name
    function text()
        return "Pet name: " + name

class cat : pet
    function init(name)
        this.name = name
    function speak()
        return "Meow!"
    function text()
        return "Cat name: " + name

myCat = cat("Alex")
print(myCat) # prints Cat name: Alex
print(myCat.speak()) # prints Meow!
```

### License

**MIT License**  
Cantus is now licensed under the [MIT License](https://tldrlegal.com/license/mit-license).

See LICENCE in the repository for the full license.


### Want to Help Out?  
Your help will be greatly appreciated! There are quite a few things I am working on for this project:
#### General Testing and Debugging
This is an early release and many aspects are still kind of buggy. Finding and getting rid of bugs is certainly a priority. (even if you're not a programmer, you can help find and report errant behaviour in the program).
#### Efficiency Improvement
The efficiency of this program needs some improvement as evaluation is quite slow.

#### Upgrade to C#
This will take some work, but I am hoping to upgrade this project to C# from VB .NET in the future. VB .NET is not really considered a mainstream language anymore, and is sometimes overly verbose. I started the evaluator out a long time ago in VB and kind of stuck to it.

#### Implementation in C or C++
Finally, on the long term, this project should optimally be ported to a lower level language. Obviously, there won't be a feature-to-feature match but the language certainly can be implemented.
