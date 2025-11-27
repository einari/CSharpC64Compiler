using RoslynC64Compiler;

// RoslynC64Compiler - A C# to Commodore 64 Compiler
// Uses Roslyn as the frontend to compile C# code to 6502 machine code

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     RoslynC64Compiler - C# to Commodore 64 Compiler        ║");
Console.WriteLine("║     Using Roslyn as frontend, targeting 6502 CPU           ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

if (args.Length == 0)
{
    PrintUsage();
    return 0;
}

var options = ParseArguments(args);

if (string.IsNullOrEmpty(options.InputFile))
{
    Console.WriteLine("Error: No input file specified");
    PrintUsage();
    return 1;
}

var compiler = new C64Compiler(options);
var result = compiler.Compile();

if (result.Success)
{
    Console.WriteLine();
    Console.WriteLine("Compilation successful!");
    Console.WriteLine($"  Output: {result.OutputPath}");
    Console.WriteLine($"  Code size: {result.CodeSize} bytes");
    Console.WriteLine($"  Time: {result.CompilationTime.TotalMilliseconds:F2}ms");
    
    if (result.ListingPath != null)
        Console.WriteLine($"  Listing: {result.ListingPath}");
    if (result.D64Path != null)
        Console.WriteLine($"  D64: {result.D64Path}");
        
    Console.WriteLine();
    Console.WriteLine("To run in VICE emulator:");
    Console.WriteLine($"  x64sc {result.OutputPath}");
    
    return 0;
}
else
{
    Console.WriteLine();
    Console.WriteLine("Compilation failed!");
    
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  ERROR: {error}");
    }
    
    foreach (var warning in result.Warnings)
    {
        Console.WriteLine($"  WARNING: {warning}");
    }
    
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage: RoslynC64Compiler <source.cs> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -o <file>     Output file path (default: <source>.prg)");
    Console.WriteLine("  -d64          Also generate a D64 disk image");
    Console.WriteLine("  -name <name>  Program name for D64 (default: PROGRAM)");
    Console.WriteLine("  -v            Verbose output");
    Console.WriteLine("  -ir           Dump intermediate representation");
    Console.WriteLine("  -no-listing   Don't generate assembly listing");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  RoslynC64Compiler hello.cs -d64 -v");
    Console.WriteLine();
    Console.WriteLine("Supported C# features:");
    Console.WriteLine("  - byte, sbyte, short, ushort, bool types");
    Console.WriteLine("  - Variables (global and local)");
    Console.WriteLine("  - Arithmetic operators (+, -, *, /)");
    Console.WriteLine("  - Bitwise operators (&, |, ^, ~, <<, >>)");
    Console.WriteLine("  - Comparison operators (==, !=, <, >, <=, >=)");
    Console.WriteLine("  - if/else, while, for loops");
    Console.WriteLine("  - Function calls");
    Console.WriteLine("  - Console.WriteLine() -> KERNAL print");
    Console.WriteLine();
    Console.WriteLine("C64 Intrinsics (use with 'C64.' prefix or as standalone):");
    Console.WriteLine("  - C64.Print(string)           Print text");
    Console.WriteLine("  - C64.PrintLine(string)       Print text with newline");
    Console.WriteLine("  - C64.PrintChar(byte)         Print single character");
    Console.WriteLine("  - C64.GetKey()                Wait for keypress");
    Console.WriteLine("  - C64.ClearScreen()           Clear the screen");
    Console.WriteLine("  - C64.SetBorderColor(byte)    Set border color (0-15)");
    Console.WriteLine("  - C64.SetBackgroundColor(byte) Set background color (0-15)");
    Console.WriteLine("  - C64.Poke(ushort, byte)      Write byte to memory");
    Console.WriteLine("  - C64.Peek(ushort)            Read byte from memory");
}

static CompilerOptions ParseArguments(string[] args)
{
    var options = new CompilerOptions();
    
    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        
        if (arg.StartsWith("-"))
        {
            switch (arg.ToLowerInvariant())
            {
                case "-o":
                    if (i + 1 < args.Length)
                        options.OutputFile = args[++i];
                    break;
                case "-d64":
                    options.GenerateD64 = true;
                    break;
                case "-name":
                    if (i + 1 < args.Length)
                        options.ProgramName = args[++i];
                    break;
                case "-v":
                case "-verbose":
                    options.Verbose = true;
                    break;
                case "-ir":
                    options.DumpIr = true;
                    break;
                case "-no-listing":
                    options.GenerateListing = false;
                    break;
            }
        }
        else if (string.IsNullOrEmpty(options.InputFile))
        {
            options.InputFile = arg;
        }
    }
    
    return options;
}
