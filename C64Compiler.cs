using System.Diagnostics;
using RoslynC64Compiler.CodeGeneration;
using RoslynC64Compiler.Frontend;
using RoslynC64Compiler.IR;

namespace RoslynC64Compiler;

/// <summary>
/// C64 Compiler options
/// </summary>
public class CompilerOptions
{
    public string InputFile { get; set; } = "";
    public string? OutputFile { get; set; }
    public bool GenerateD64 { get; set; }
    public bool GenerateListing { get; set; } = true;
    public bool Verbose { get; set; }
    public bool DumpIr { get; set; }
    public string ProgramName { get; set; } = "PROGRAM";
}

/// <summary>
/// Result of compilation
/// </summary>
public class CompilationResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? ListingPath { get; set; }
    public string? D64Path { get; set; }
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public TimeSpan CompilationTime { get; set; }
    public int CodeSize { get; set; }
}

/// <summary>
/// Main C64 compiler that orchestrates the compilation pipeline
/// </summary>
public class C64Compiler
{
    private readonly CompilerOptions _options;

    public C64Compiler(CompilerOptions options)
    {
        _options = options;
    }

    public CompilationResult Compile()
    {
        var result = new CompilationResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Read source file
            if (!File.Exists(_options.InputFile))
            {
                result.Errors.Add($"Source file not found: {_options.InputFile}");
                return result;
            }

            var sourceCode = File.ReadAllText(_options.InputFile);
            
            if (_options.Verbose)
            {
                Console.WriteLine($"Compiling: {_options.InputFile}");
                Console.WriteLine($"Source size: {sourceCode.Length} characters");
            }

            // Phase 1: Parse C# and convert to IR
            if (_options.Verbose) Console.WriteLine("Phase 1: Parsing C# source...");
            
            var frontend = new CSharpToIrCompiler();
            var program = frontend.Compile(sourceCode, _options.InputFile);

            if (program == null || frontend.Diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
            {
                foreach (var diag in frontend.Diagnostics)
                {
                    var location = diag.Location.GetLineSpan();
                    var message = $"{location.Path}({location.StartLinePosition.Line + 1},{location.StartLinePosition.Character + 1}): {diag.Severity}: {diag.GetMessage()}";
                    
                    if (diag.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        result.Errors.Add(message);
                    else
                        result.Warnings.Add(message);
                }
                
                if (program == null)
                {
                    result.Errors.Add("Compilation failed: could not parse source file");
                    return result;
                }
            }

            if (_options.Verbose)
            {
                Console.WriteLine($"  Found {program.Functions.Count} function(s)");
                Console.WriteLine($"  Found {program.Globals.Count} global variable(s)");
                Console.WriteLine($"  Found {program.StringConstants.Count} string constant(s)");
            }

            // Dump IR if requested
            if (_options.DumpIr)
            {
                var irPrinter = new IrPrinter();
                var irDump = irPrinter.Print(program);
                var irPath = Path.ChangeExtension(_options.InputFile, ".ir");
                File.WriteAllText(irPath, irDump);
                Console.WriteLine($"IR dumped to: {irPath}");
            }

            // Phase 2: Generate 6502 machine code
            if (_options.Verbose) Console.WriteLine("Phase 2: Generating 6502 code...");
            
            var codeGen = new CodeGenerator6502();
            var (machineCode, listing) = codeGen.Generate(program);

            if (_options.Verbose)
            {
                Console.WriteLine($"  Generated {machineCode.Length} bytes of machine code");
            }

            result.CodeSize = machineCode.Length;

            // Phase 3: Generate output files
            if (_options.Verbose) Console.WriteLine("Phase 3: Generating output files...");

            var outputPath = _options.OutputFile ?? Path.ChangeExtension(_options.InputFile, ".prg");
            
            var prgGen = new PrgGenerator();
            prgGen.Save(outputPath, machineCode);
            result.OutputPath = outputPath;

            if (_options.Verbose)
            {
                Console.WriteLine($"  PRG file: {outputPath} ({new FileInfo(outputPath).Length} bytes)");
            }

            // Generate listing file
            if (_options.GenerateListing)
            {
                var listingPath = Path.ChangeExtension(outputPath, ".lst");
                File.WriteAllText(listingPath, listing);
                result.ListingPath = listingPath;
                
                if (_options.Verbose)
                {
                    Console.WriteLine($"  Listing file: {listingPath}");
                }
            }

            // Generate D64 if requested
            if (_options.GenerateD64)
            {
                var d64Path = Path.ChangeExtension(outputPath, ".d64");
                var prgData = File.ReadAllBytes(outputPath);
                var d64Data = prgGen.GenerateD64(prgData, _options.ProgramName);
                File.WriteAllBytes(d64Path, d64Data);
                result.D64Path = d64Path;
                
                if (_options.Verbose)
                {
                    Console.WriteLine($"  D64 file: {d64Path}");
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Internal compiler error: {ex.Message}");
            if (_options.Verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        finally
        {
            stopwatch.Stop();
            result.CompilationTime = stopwatch.Elapsed;
        }

        return result;
    }
}
