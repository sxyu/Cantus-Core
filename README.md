# Cantus Core 2.0
*Cantus Core* is a free and open-source .NET library for running expressions and scripts written in the Cantus language.
It is designed to be very easy to set up and to be **directly usable as a mathematical expression evaluator**.

### Quickstart
First, add `cantus.core.dll` as a reference in your project.

Then, in your imports:
```cs
using Cantus.Core;
```

Inside your main method (or where you want to do evaluations):
```cs
CantusEvaluator eval = new CantusEvaluator();

// Set evaluation modes: These settings (Radian, Math, etc.) are the defaults, 
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

// Evaluate one-line expressions
Console.WriteLine(eval.EvalExpr("3.5!")); // output: 58/5
Console.WriteLine(eval.EvalExprRaw("3.5!")); // output: 11.6
```
All of the above methods also have asynchroneous counterparts. 
For example, you can use `EvalAsync()` instead of `Eval()` to evaluate a full script asynchronously.

## Customization
Cantus is licenced under the MIT licence, and you are welcome to customize/recompile it for any purposes.
I will not force you but I would greatly appreciate it if you can give credit to this project.

### Tips:
If you want to add your own internal functions, you can easily do so by adding new public methods inside `InternalFunctions.cs`.

You can also add new operators inside `OperatorRegistar.cs` or new statements inside `StatementRegistar.cs`.

You can even modify `ObjectTypes.cs` to define extra types, though you'll then need to add methods inside
`InternalFunctions.cs` to create the type.

# Cantus Language
*Cantus* is a lightweight yet powerful mathematical language.

### What you can do with Cantus:
  - Create and do calculations with **matrices**, **sets**, **dictionaries** and **tuples**
  - Perform operations on **complex numbers**
  - **Arbitrary precision** decimals for basic operations (+-*/^)
  - Use various built-in functions and operators
  - Use **flow control statements** like in a procedural language (If, For, While, Until, Repeat, Switch, etc.)
  - Use variable and function **scoping**
  - Customize
    - Declare and use **variables**
    - Declare **functions**
    - Run other .can scripts
    - Add initialization scripts to customize the calculatOrElse   - Graph **cartesian**, **inverse**, **polar**, or **parametric** functions as well as **slope fields**

### Installation
**Cantus needs .Net Framework 4.0 or above to work.**  
.Net framework is already included in the latest versions of Windows such as Windows 10 and 8.1.  

You can download .Net [Here](https://www.microsoft.com/en-us/download/confirmation.aspx?id=17851). If you aren't sure if you have it installed, it is fine to try downloading Cantus and see if it runs first before trying to install.

You can run Cantus on Mac or Linux with Wine and Mono.
Note that Cantus as a language can really be implemented elsewhere, but since this is done using .Net unfortunately cross-platform support is poor at this time.

**Now go ahead and download *Cantus***  
[**Download Cantus Here**](https://drive.google.com/uc?export=download&id=0B314tJw3ioySQnhfUjdVTVZ1RWs)  
No real installation required. Just run, read over the update log if you wish, and start using the evaluator.	

**Important: I have not yet obtained a codes signing certificate to sign the executable, so your computer will probably either block it or display a warning. If smartscreen blocks it, please click "more" and "run anyway".**	
These certificates just cost [so much](https://www.symantec.com/popup.jsp?popupid=csc_ms_authenticode_buy&footer=0&inid=us_symc_buy_pdp_next-steps_cod-sign-msft) (Symantec lists $499/yr per person)!

### Basic Usage

Run the downloaded program, and type in the expression in the main textbox to see the result. Play around as much as you like.  
*Sidenote: The evaluation is done asynchroneously, so you can keep working while it evaluates*

* To change settings, click the gear button the on the right
    *  AngleRepr: Angle Representation (`Ctrl`+`Alt`+`P`): 
    	*  Deg (`Ctrl`+`Alt`+`D`), Rad (`Ctrl`+`Alt`+`R`), Grad (`Ctrl`+`Alt`+`G`)
    *  Output format: (`Ctrl`+`Alt`+`O`)
        *  MathO (`Ctrl`+`Alt`+`M`): Output fractions, roots, and multiples of PI
        *  LineO (`Ctrl`+`Alt`+`L`): Output numbers
        *  SciO (`Ctrl`+`Alt`+`S`): Output scientic notation
	*  Explicit (`Ctrl`+`Alt`+`T`): Force explicit variable declarations
    * You can also use functions to change these modes (discuss that later)
    * Click the version number to see the update log
    * To change settings, click the gear button the on the right or press `Alt`+`S`
* To graph functions, click the graph button (below the gear button) on the right or press `Alt` + `G`
* To change mode

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
* `!` is the factorial operatOrElse 	* The logical not operator is just `not` 
	* The bitwise not operator is `~` 
* `|` is the absolute value operatOrElse 	* The logical or operator is just `or`  
	* The bitwise or operator is `||`  
* `&` is the string concatenation operator (try `123 & "456"`)
	* The logical and operator is just `and`  
	* The bitwise and operator is `&&`  
* `^` is the exponentiation operatOrElse 	* The logical xor operator is just `xor`  
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

**Lambda Functions and Function Pointers**
Another way to define a function is using backticks like this:
`foo=\`1\`` or `foo=\`x=>x+1\``
This is called a lambda expression. With this you can use functions like sigma:
`sigma(`i=>i^2`,1,5)`

You can write a multiline lambda expression like this:
```myFunction=\`x=>
	if x>0
		return x+1
	\`
```

All normal functions can also be used in this way. When no arguments are supplied,
they act as function pointers and can be assigned.

For example: 
```b=sind(x)
   return b(30) # returns 0.5
```
*Tip: You can also do cool things like dydx(sin). Try drawing this in the graphing window.*

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
After writing a script, save it as a .can (Cantus Script) file. You can do this by pressing `Ctrl` + `S` in the editor.

To run the script later, you can do one of the following:
* Go in command prompt (cmd) and type (without quotes or angle brackets) 	
*"cantus &lt;filename&gt;.can"* (result written to console)
* Double click the file and select to open with the Cantus program (result not shown)
* Press `F5` in the editor and select the file (result written to label)
* Use the run(path) function (async) or runwait(path) function (single threaded)

To open the script for editing later, press `Ctrl` + `O` in the editor and select the file. Line endings CR LF, CR, etc. will automatically standardized based on the platform.

#### Extending Cantus

**The Main Initialization Script**	
The main initialization script is located at "init.can" at the base directory (the directory where the cantus program is located).

Normally, this file contains some auto-generated code storing the variables, functions, and configuration that you defined as well as the default constants like e and pi. You can add your own startup configuration (function definitions, variables/constants, etc.) below where the comment says you can.

Be careful **never** to change the end comment (that might mess up the file when the editor re-generates the settings).

**The plugin Directory**	
The plugin/ directory is used for storing plugins, as the name implies.
All .can files under the init directory (and all subdirectories) under the base directory will be loaded at startup, but
are not imported.

**Importing and loading**
Loading a file with the `load` statement makes it available to the evaluator. Functions and variables declared in the scope will become
visible their original namespace.

Importing a file with the `import` statement makes the contents of the file directly accessible in the evaluatOrElse 
Both of these statements can either be supplied with a full path, a relative path from the executing directory 
(using . as separator), or a path inside the include directory.

**The include Directory**	
Files in this directory are not loaded on startup. However, all .can files in the include directory may be
easily imported into the evaluator with an import statement.

For example,
`import a.b` will import the file at include/a/b.can if available

**The init Directory**	
The init directory is for placing additional initialization scripts. They are run after the main initialization file and override
items declared in the main evaluation file.

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
