# CSharpC64Compiler

A C# to Commodore 64 compiler that uses Roslyn as the frontend to compile C# code down to executable 6502 machine code for the Commodore 64.

## Features

- **Roslyn Frontend**: Leverages the Microsoft.CodeAnalysis (Roslyn) library for C# parsing and semantic analysis
- **6502 Code Generation**: Generates native 6502 machine code
- **PRG Output**: Creates standard C64 PRG files that can be loaded directly
- **D64 Support**: Optional D64 disk image generation
- **Assembly Listing**: Generates human-readable 6502 assembly listings

## Supported C# Features

### Types
- `byte` (8-bit unsigned)
- `sbyte` (8-bit signed)
- `short` / `ushort` (16-bit)
- `bool`
- `char` (as PETSCII byte)
- `string` (as pointer to null-terminated string)

### Language Features
- Global and local variables
- Arithmetic operators: `+`, `-`, `*`, `/`, `%`
- Bitwise operators: `&`, `|`, `^`, `~`, `<<`, `>>`
- Comparison operators: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Logical operators: `&&`, `||`, `!`
- Control flow: `if`/`else`, `while`, `for`, `break`, `continue`
- Functions and method calls
- Top-level statements (C# 9+ style)
- Classes and static methods

## Building

```bash
dotnet build
```

## Usage

```bash
dotnet run -- <source.cs> [options]
```

### Options

| Option | Description |
|--------|-------------|
| `-o <file>` | Output file path (default: `<source>.prg`) |
| `-d64` | Also generate a D64 disk image |
| `-name <name>` | Program name for D64 (default: PROGRAM) |
| `-v` | Verbose output |
| `-ir` | Dump intermediate representation |
| `-no-listing` | Don't generate assembly listing |

### Example

```bash
# Compile hello.cs to hello.prg
dotnet run -- Samples/hello.cs -v

# Also create a D64 disk image
dotnet run -- Samples/hello.cs -d64 -name HELLO
```

## Running the Output

The generated `.prg` file can be run in any C64 emulator:

### VICE Emulator
```bash
x64sc hello.prg
```

### Online Emulators
Upload the `.prg` or `.d64` file to:
- [Virtual Consoles](https://virtualconsoles.com/online-emulators/c64/)
- [C64 Online](https://c64online.com/)

## C64 Intrinsics

The compiler recognizes special C64 functions that compile directly to 6502 code:

```csharp
// Screen and Text
C64.Print(string text);           // Print text
C64.PrintLine(string text);       // Print text with newline
C64.PrintChar(byte char);         // Print single character
C64.ClearScreen();                // Clear the screen

// Input
byte key = C64.GetKey();          // Wait for and return keypress

// Colors (0-15)
C64.SetBorderColor(byte color);
C64.SetBackgroundColor(byte color);

// Memory Access
C64.Poke(ushort address, byte value);  // Write to memory
byte val = C64.Peek(ushort address);   // Read from memory
```

`Console.WriteLine()` and `Console.ReadKey()` are also mapped to C64 equivalents.

## Sample Programs

See the `Samples/` directory for example programs:

- `hello.cs` - Simple "Hello World"
- `colors.cs` - Color cycling demo
- `counter.cs` - Counting demonstration
- `poke.cs` - Direct memory access
- `game.cs` - Simple interactive game loop

## Architecture

```
┌─────────────────┐
│   C# Source     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Roslyn Frontend │  Parse & analyze C# code
│   (Frontend/)   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Intermediate    │  Platform-independent IR
│ Representation  │
│     (IR/)       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ 6502 Code Gen   │  Generate machine code
│(CodeGeneration/)│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  PRG Generator  │  Create C64 executable
│(CodeGeneration/)│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  .prg / .d64    │  Ready to run!
└─────────────────┘
```

## Memory Layout

The compiled program uses the following memory layout:

| Address | Usage |
|---------|-------|
| $0002-$000F | Compiler zero-page variables |
| $0801-$CFFF | Program code and data |
| $CF00-$CFFF | Software stack |
| $D000-$DFFF | I/O (VIC-II, SID, CIA) |
| $FFD2 | KERNAL CHROUT (print char) |
| $FFE4 | KERNAL GETIN (get key) |

## Limitations

- No floating-point support (C64 has no FPU)
- No garbage collection (static memory only)
- No exceptions or try/catch
- Limited to 64KB address space
- String operations are basic (no concatenation)
- No generics or LINQ
- Classes are treated as static only

## Contributing

This is an educational project demonstrating:
1. How to use Roslyn for custom compilation
2. 6502 assembly code generation
3. C64 memory layout and KERNAL usage

Feel free to extend it with more features!

## License

MIT License
