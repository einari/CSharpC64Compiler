using RoslynC64Compiler.IR;

namespace RoslynC64Compiler.CodeGeneration;

/// <summary>
/// Generates 6502 machine code from IR
/// </summary>
public class CodeGenerator6502
{
    private AssemblyBuilder _asm = null!;
    private IrProgram _program = null!;
    private readonly Dictionary<string, ushort> _globalAddresses = [];
    private int _labelCounter;
    private readonly Stack<string> _breakLabels = new();
    private readonly Stack<string> _continueLabels = new();

    // Zero page allocations for the compiler
    private const byte ZP_SP = 0x02;        // Software stack pointer (2 bytes)
    private const byte ZP_RESULT = 0x04;    // Result register (2 bytes)
    private const byte ZP_TEMP = 0x06;      // Temporary (2 bytes)
    private const byte ZP_PTR1 = 0x08;      // Pointer 1 (2 bytes)
    private const byte ZP_PTR2 = 0x0A;      // Pointer 2 (2 bytes)
    private const byte ZP_FRAME = 0x0C;     // Frame pointer (2 bytes)

    // Where globals and stack will be placed (after program code)
    private ushort _dataSegmentStart;

    public (byte[] MachineCode, string AssemblyListing) Generate(IrProgram program)
    {
        _program = program;
        _labelCounter = 0;
        _globalAddresses.Clear();

        // C64 BASIC stub starts at $0801
        _asm = new AssemblyBuilder(C64Constants.BasicStart);

        // Generate BASIC stub that starts our code
        GenerateBasicStub();

        // Generate runtime library
        GenerateRuntime();

        // Generate string constants
        GenerateStringConstants();

        // Generate global variables
        GenerateGlobals();

        // Generate all functions
        foreach (var func in _program.Functions)
        {
            GenerateFunction(func);
        }

        // Generate infinite loop at end (so program doesn't crash)
        _asm.Label("_exit");
        _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, "_exit", "Infinite loop at exit");

        var machineCode = _asm.Assemble();
        var listing = _asm.ToAssemblyListing();

        return (machineCode, listing);
    }

    private void GenerateBasicStub()
    {
        // BASIC stub: 10 SYS 2062
        // This is the standard way to start machine code from BASIC
        // Format: [next line addr lo][hi][line number lo][hi][SYS token][space]"2062"[null][next=0000]
        
        _asm.Comment("BASIC Stub: 10 SYS 2062");
        ushort nextLineAddr = 0x080D;  // Address after this line
        _asm.Word(nextLineAddr);       // Pointer to next BASIC line
        _asm.Word(10);                 // Line number 10
        _asm.Bytes(0x9E);              // SYS token
        _asm.Bytes(0x20);              // Space
        _asm.StringData("2062");       // Address to call (will be adjusted)
        _asm.Bytes(0x00);              // End of line
        _asm.Word(0x0000);             // End of BASIC program
        
        _asm.Blank();
        _asm.Label("_start");
        _asm.Comment("Program entry point");

        // Initialize software stack pointer
        _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_SP);
        _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0xCF, "Stack at $CF00");
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_SP + 1));

        // Jump to main
        _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_main");
        _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, "_exit");
    }

    private void GenerateRuntime()
    {
        _asm.Blank();
        _asm.Comment("=== Runtime Library ===");
        
        // Print string routine
        GeneratePrintString();
        
        // Print newline routine
        GeneratePrintNewline();
        
        // Get key routine  
        GenerateGetKey();
        
        // Clear screen routine
        GenerateClearScreen();
        
        // 16-bit math routines
        GenerateMath16();
    }

    private void GeneratePrintString()
    {
        _asm.Blank();
        _asm.Label("_rt_print_string");
        _asm.Comment("Print null-terminated string at ZP_PTR1");
        
        _asm.Emit(Opcode.LDY, AddressingMode.Immediate, 0x00);
        
        _asm.Label("_rt_print_loop");
        _asm.Emit(Opcode.LDA, AddressingMode.IndirectY, ZP_PTR1);
        _asm.EmitLabel(Opcode.BEQ, AddressingMode.Relative, "_rt_print_done");
        _asm.Emit(Opcode.JSR, AddressingMode.Absolute, C64Constants.CHROUT);
        _asm.Emit(Opcode.INY);
        _asm.EmitLabel(Opcode.BNE, AddressingMode.Relative, "_rt_print_loop");
        
        // Handle strings longer than 255 chars
        _asm.Emit(Opcode.INC, AddressingMode.ZeroPage, (byte)(ZP_PTR1 + 1));
        _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, "_rt_print_loop");
        
        _asm.Label("_rt_print_done");
        _asm.Emit(Opcode.RTS);
    }

    private void GeneratePrintNewline()
    {
        _asm.Blank();
        _asm.Label("_rt_print_newline");
        _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 13, "Carriage return");
        _asm.Emit(Opcode.JSR, AddressingMode.Absolute, C64Constants.CHROUT);
        _asm.Emit(Opcode.RTS);
    }

    private void GenerateGetKey()
    {
        _asm.Blank();
        _asm.Label("_rt_getkey");
        _asm.Comment("Wait for and return keypress in A");
        
        _asm.Label("_rt_getkey_loop");
        _asm.Emit(Opcode.JSR, AddressingMode.Absolute, C64Constants.GETIN);
        _asm.EmitLabel(Opcode.BEQ, AddressingMode.Relative, "_rt_getkey_loop");
        _asm.Emit(Opcode.RTS);
    }

    private void GenerateClearScreen()
    {
        _asm.Blank();
        _asm.Label("_rt_clearscreen");
        _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 147, "Clear screen PETSCII");
        _asm.Emit(Opcode.JSR, AddressingMode.Absolute, C64Constants.CHROUT);
        _asm.Emit(Opcode.RTS);
    }

    private void GenerateMath16()
    {
        _asm.Blank();
        _asm.Comment("=== 16-bit Math Routines ===");

        // 16-bit addition: ZP_RESULT = ZP_RESULT + ZP_TEMP
        _asm.Blank();
        _asm.Label("_rt_add16");
        _asm.Emit(Opcode.CLC);
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.ADC, AddressingMode.ZeroPage, ZP_TEMP);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.ADC, AddressingMode.ZeroPage, (byte)(ZP_TEMP + 1));
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.RTS);

        // 16-bit subtraction: ZP_RESULT = ZP_RESULT - ZP_TEMP
        _asm.Blank();
        _asm.Label("_rt_sub16");
        _asm.Emit(Opcode.SEC);
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.SBC, AddressingMode.ZeroPage, ZP_TEMP);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.SBC, AddressingMode.ZeroPage, (byte)(ZP_TEMP + 1));
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.RTS);

        // 8-bit multiply: A * X = ZP_RESULT (16-bit)
        _asm.Blank();
        _asm.Label("_rt_mul8");
        _asm.Comment("Multiply A * X, result in ZP_RESULT");
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_TEMP);
        _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        
        _asm.Label("_rt_mul8_loop");
        _asm.Emit(Opcode.CPX, AddressingMode.Immediate, 0x00);
        _asm.EmitLabel(Opcode.BEQ, AddressingMode.Relative, "_rt_mul8_done");
        _asm.Emit(Opcode.CLC);
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.ADC, AddressingMode.ZeroPage, ZP_TEMP);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.ADC, AddressingMode.Immediate, 0x00);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.DEX);
        _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, "_rt_mul8_loop");
        
        _asm.Label("_rt_mul8_done");
        _asm.Emit(Opcode.RTS);

        // Compare 16-bit: ZP_RESULT vs ZP_TEMP, sets Z and C flags
        _asm.Blank();
        _asm.Label("_rt_cmp16");
        _asm.Comment("Compare ZP_RESULT vs ZP_TEMP");
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.CMP, AddressingMode.ZeroPage, (byte)(ZP_TEMP + 1));
        _asm.EmitLabel(Opcode.BNE, AddressingMode.Relative, "_rt_cmp16_done");
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.CMP, AddressingMode.ZeroPage, ZP_TEMP);
        _asm.Label("_rt_cmp16_done");
        _asm.Emit(Opcode.RTS);
    }

    private void GenerateStringConstants()
    {
        if (_program.StringConstants.Count == 0) return;

        _asm.Blank();
        _asm.Comment("=== String Constants ===");
        
        foreach (var str in _program.StringConstants)
        {
            _asm.Label(str.Label);
            _asm.String(str.Value);
        }
    }

    private void GenerateGlobals()
    {
        if (_program.Globals.Count == 0) return;

        _asm.Blank();
        _asm.Comment("=== Global Variables ===");
        
        foreach (var global in _program.Globals)
        {
            _asm.Label($"_var_{global.Name}");
            _globalAddresses[$"_var_{global.Name}"] = (ushort)_asm.CurrentAddress;
            
            var size = global.Type.SizeInBytes();
            if (global.InitialValue is IrLiteralExpression lit)
            {
                var value = Convert.ToInt32(lit.Value);
                if (size == 1)
                    _asm.Bytes((byte)value);
                else
                    _asm.Word((ushort)value);
            }
            else
            {
                _asm.Reserve(size);
            }
        }
    }

    private void GenerateFunction(IrFunction func)
    {
        _asm.Blank();
        _asm.Comment($"=== Function: {func.Name} ===");
        _asm.Label(func.Label ?? $"_func_{func.Name.ToLowerInvariant()}");

        // Function prologue - save frame pointer and set up new frame
        // For simplicity, we're using a minimal calling convention

        // Generate function body
        foreach (var stmt in func.Body.Statements)
        {
            GenerateStatement(stmt);
        }

        // Implicit return if void
        if (func.ReturnType == IrType.Void)
        {
            _asm.Emit(Opcode.RTS);
        }
    }

    private void GenerateStatement(IrStatement stmt)
    {
        switch (stmt)
        {
            case IrExpressionStatement exprStmt:
                GenerateExpression(exprStmt.Expression);
                break;

            case IrReturnStatement returnStmt:
                if (returnStmt.Value != null)
                {
                    GenerateExpression(returnStmt.Value);
                    // Result is in A (8-bit) or ZP_RESULT (16-bit)
                }
                _asm.Emit(Opcode.RTS);
                break;

            case IrVariableDeclaration decl:
                if (decl.InitialValue != null)
                {
                    GenerateExpression(decl.InitialValue);
                    GenerateStoreLocal(decl.Variable);
                }
                break;

            case IrAssignment assign:
                GenerateExpression(assign.Value);
                GenerateStore(assign.Target);
                break;

            case IrIfStatement ifStmt:
                GenerateIfStatement(ifStmt);
                break;

            case IrWhileStatement whileStmt:
                GenerateWhileStatement(whileStmt);
                break;

            case IrForStatement forStmt:
                GenerateForStatement(forStmt);
                break;

            case IrBreakStatement:
                if (_breakLabels.Count > 0)
                {
                    _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, _breakLabels.Peek());
                }
                break;

            case IrContinueStatement:
                if (_continueLabels.Count > 0)
                {
                    _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, _continueLabels.Peek());
                }
                break;
        }
    }

    private void GenerateIfStatement(IrIfStatement ifStmt)
    {
        var elseLabel = NewLabel("else");
        var endLabel = NewLabel("endif");

        // Generate condition
        GenerateExpression(ifStmt.Condition);
        
        // Branch to else/end if condition is false (A == 0)
        _asm.Emit(Opcode.CMP, AddressingMode.Immediate, 0x00);
        _asm.EmitLabel(Opcode.BEQ, AddressingMode.Relative, ifStmt.ElseBlock != null ? elseLabel : endLabel);

        // Generate then block
        foreach (var stmt in ifStmt.ThenBlock.Statements)
        {
            GenerateStatement(stmt);
        }

        if (ifStmt.ElseBlock != null)
        {
            _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, endLabel);
            
            _asm.Label(elseLabel);
            foreach (var stmt in ifStmt.ElseBlock.Statements)
            {
                GenerateStatement(stmt);
            }
        }

        _asm.Label(endLabel);
    }

    private void GenerateWhileStatement(IrWhileStatement whileStmt)
    {
        var loopLabel = NewLabel("while");
        var endLabel = NewLabel("endwhile");

        _breakLabels.Push(endLabel);
        _continueLabels.Push(loopLabel);

        _asm.Label(loopLabel);
        
        // Generate condition
        GenerateExpression(whileStmt.Condition);
        _asm.Emit(Opcode.CMP, AddressingMode.Immediate, 0x00);
        _asm.EmitLabel(Opcode.BEQ, AddressingMode.Relative, endLabel);

        // Generate body
        foreach (var stmt in whileStmt.Body.Statements)
        {
            GenerateStatement(stmt);
        }

        _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, loopLabel);
        _asm.Label(endLabel);

        _breakLabels.Pop();
        _continueLabels.Pop();
    }

    private void GenerateForStatement(IrForStatement forStmt)
    {
        var loopLabel = NewLabel("for");
        var continueLabel = NewLabel("for_continue");
        var endLabel = NewLabel("endfor");

        // Generate initializer
        if (forStmt.Initializer != null)
        {
            GenerateStatement(forStmt.Initializer);
        }

        _breakLabels.Push(endLabel);
        _continueLabels.Push(continueLabel);

        _asm.Label(loopLabel);

        // Generate condition check
        if (forStmt.Condition != null)
        {
            GenerateExpression(forStmt.Condition);
            _asm.Emit(Opcode.CMP, AddressingMode.Immediate, 0x00);
            _asm.EmitLabel(Opcode.BEQ, AddressingMode.Relative, endLabel);
        }

        // Generate body
        foreach (var stmt in forStmt.Body.Statements)
        {
            GenerateStatement(stmt);
        }

        _asm.Label(continueLabel);

        // Generate increment
        if (forStmt.Increment != null)
        {
            GenerateStatement(forStmt.Increment);
        }

        _asm.EmitLabel(Opcode.JMP, AddressingMode.Absolute, loopLabel);
        _asm.Label(endLabel);

        _breakLabels.Pop();
        _continueLabels.Pop();
    }

    private void GenerateExpression(IrExpression expr)
    {
        switch (expr)
        {
            case IrLiteralExpression lit:
                GenerateLiteral(lit);
                break;

            case IrStringLiteralExpression str:
                GenerateStringLiteral(str);
                break;

            case IrVariableExpression varExpr:
                GenerateLoadVariable(varExpr);
                break;

            case IrBinaryExpression bin:
                GenerateBinaryExpression(bin);
                break;

            case IrUnaryExpression unary:
                GenerateUnaryExpression(unary);
                break;

            case IrCallExpression call:
                GenerateCall(call);
                break;

            case IrCastExpression cast:
                GenerateExpression(cast.Operand);
                // Type conversion handled implicitly for 8-bit <-> 16-bit
                break;
        }
    }

    private void GenerateLiteral(IrLiteralExpression lit)
    {
        var value = Convert.ToInt32(lit.Value);
        
        if (lit.Type.Is8Bit())
        {
            _asm.Emit(Opcode.LDA, AddressingMode.Immediate, (byte)(value & 0xFF));
        }
        else
        {
            // 16-bit value into ZP_RESULT
            _asm.Emit(Opcode.LDA, AddressingMode.Immediate, (byte)(value & 0xFF));
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);
            _asm.Emit(Opcode.LDA, AddressingMode.Immediate, (byte)((value >> 8) & 0xFF));
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        }
    }

    private void GenerateStringLiteral(IrStringLiteralExpression str)
    {
        // Load string address into ZP_PTR1
        _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00, $"Load address of '{str.Value}'");
        _asm.EmitLabel(Opcode.LDA, AddressingMode.Absolute, str.ConstantLabel!);
        // Actually we need to emit the address differently...
        // Let's use absolute addressing to load the label address
        
        // For now, load low/high bytes of the string address
        _asm.Label($"_load_{str.ConstantLabel}");
        // This is a placeholder - the actual address will be resolved during assembly
    }

    private void GenerateLoadVariable(IrVariableExpression varExpr)
    {
        if (varExpr.IsGlobal)
        {
            var label = $"_var_{varExpr.Name}";
            if (varExpr.Type.Is8Bit())
            {
                _asm.EmitLabel(Opcode.LDA, AddressingMode.Absolute, label);
            }
            else
            {
                _asm.EmitLabel(Opcode.LDA, AddressingMode.Absolute, label);
                _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);
                _asm.EmitLabel(Opcode.LDA, AddressingMode.Absolute, $"{label}+1");
                _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
            }
        }
        else if (varExpr.IsLocal)
        {
            // Load from software stack using frame pointer
            var offset = varExpr.LocalOffset ?? 0;
            _asm.Emit(Opcode.LDY, AddressingMode.Immediate, (byte)offset);
            _asm.Emit(Opcode.LDA, AddressingMode.IndirectY, ZP_FRAME);
            if (varExpr.Type.Is16Bit())
            {
                _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);
                _asm.Emit(Opcode.INY);
                _asm.Emit(Opcode.LDA, AddressingMode.IndirectY, ZP_FRAME);
                _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
            }
        }
    }

    private void GenerateStore(IrExpression target)
    {
        if (target is IrVariableExpression varExpr)
        {
            if (varExpr.IsGlobal)
            {
                var label = $"_var_{varExpr.Name}";
                if (varExpr.Type.Is8Bit())
                {
                    _asm.EmitLabel(Opcode.STA, AddressingMode.Absolute, label);
                }
                else
                {
                    _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
                    _asm.EmitLabel(Opcode.STA, AddressingMode.Absolute, label);
                    _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
                    _asm.EmitLabel(Opcode.STA, AddressingMode.Absolute, $"{label}+1");
                }
            }
            else if (varExpr.IsLocal)
            {
                GenerateStoreLocal(new IrLocalVariable 
                { 
                    Name = varExpr.Name, 
                    Type = varExpr.Type,
                    StackOffset = varExpr.LocalOffset ?? 0 
                });
            }
        }
    }

    private void GenerateStoreLocal(IrLocalVariable local)
    {
        var offset = local.StackOffset;
        _asm.Emit(Opcode.LDY, AddressingMode.Immediate, (byte)offset);
        if (local.Type.Is8Bit())
        {
            _asm.Emit(Opcode.STA, AddressingMode.IndirectY, ZP_FRAME);
        }
        else
        {
            _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
            _asm.Emit(Opcode.STA, AddressingMode.IndirectY, ZP_FRAME);
            _asm.Emit(Opcode.INY);
            _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
            _asm.Emit(Opcode.STA, AddressingMode.IndirectY, ZP_FRAME);
        }
    }

    private void GenerateBinaryExpression(IrBinaryExpression bin)
    {
        // Special case for 8-bit operations
        if (bin.Left.Type.Is8Bit() && bin.Right.Type.Is8Bit())
        {
            Generate8BitBinaryOp(bin);
            return;
        }

        // 16-bit operations
        // Evaluate left into ZP_RESULT
        GenerateExpression(bin.Left);
        if (bin.Left.Type.Is8Bit())
        {
            // Extend to 16-bit
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);
            _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        }

        // Save left value
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
        _asm.Emit(Opcode.PHA);
        _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.PHA);

        // Evaluate right into ZP_TEMP
        GenerateExpression(bin.Right);
        if (bin.Right.Type.Is8Bit())
        {
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_TEMP);
            _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_TEMP + 1));
        }
        else
        {
            _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_TEMP);
            _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_TEMP + 1));
        }

        // Restore left value
        _asm.Emit(Opcode.PLA);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
        _asm.Emit(Opcode.PLA);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_RESULT);

        // Perform operation
        switch (bin.Operator)
        {
            case BinaryOperator.Add:
                _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_rt_add16");
                break;
            case BinaryOperator.Subtract:
                _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_rt_sub16");
                break;
            case BinaryOperator.Equal:
            case BinaryOperator.NotEqual:
            case BinaryOperator.LessThan:
            case BinaryOperator.GreaterOrEqual:
                GenerateComparison16(bin.Operator);
                break;
        }
    }

    private void Generate8BitBinaryOp(IrBinaryExpression bin)
    {
        // Evaluate left into A
        GenerateExpression(bin.Left);
        _asm.Emit(Opcode.PHA, comment: "Save left operand");

        // Evaluate right into A
        GenerateExpression(bin.Right);
        _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_TEMP);

        // Restore left
        _asm.Emit(Opcode.PLA);

        switch (bin.Operator)
        {
            case BinaryOperator.Add:
                _asm.Emit(Opcode.CLC);
                _asm.Emit(Opcode.ADC, AddressingMode.ZeroPage, ZP_TEMP);
                break;

            case BinaryOperator.Subtract:
                _asm.Emit(Opcode.SEC);
                _asm.Emit(Opcode.SBC, AddressingMode.ZeroPage, ZP_TEMP);
                break;

            case BinaryOperator.And:
                _asm.Emit(Opcode.AND, AddressingMode.ZeroPage, ZP_TEMP);
                break;

            case BinaryOperator.Or:
                _asm.Emit(Opcode.ORA, AddressingMode.ZeroPage, ZP_TEMP);
                break;

            case BinaryOperator.Xor:
                _asm.Emit(Opcode.EOR, AddressingMode.ZeroPage, ZP_TEMP);
                break;

            case BinaryOperator.Equal:
                _asm.Emit(Opcode.CMP, AddressingMode.ZeroPage, ZP_TEMP);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                var eqLabel = NewLabel("eq");
                _asm.EmitLabel(Opcode.BNE, AddressingMode.Relative, eqLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                _asm.Label(eqLabel);
                break;

            case BinaryOperator.NotEqual:
                _asm.Emit(Opcode.CMP, AddressingMode.ZeroPage, ZP_TEMP);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                var neLabel = NewLabel("ne");
                _asm.EmitLabel(Opcode.BEQ, AddressingMode.Relative, neLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                _asm.Label(neLabel);
                break;

            case BinaryOperator.LessThan:
                _asm.Emit(Opcode.CMP, AddressingMode.ZeroPage, ZP_TEMP);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                var ltLabel = NewLabel("lt");
                _asm.EmitLabel(Opcode.BCS, AddressingMode.Relative, ltLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                _asm.Label(ltLabel);
                break;

            case BinaryOperator.GreaterOrEqual:
                _asm.Emit(Opcode.CMP, AddressingMode.ZeroPage, ZP_TEMP);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                var geLabel = NewLabel("ge");
                _asm.EmitLabel(Opcode.BCC, AddressingMode.Relative, geLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                _asm.Label(geLabel);
                break;

            case BinaryOperator.Multiply:
                // 8-bit multiply uses A * X
                _asm.Emit(Opcode.LDX, AddressingMode.ZeroPage, ZP_TEMP);
                _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_rt_mul8");
                _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
                break;
        }
    }

    private void GenerateComparison16(BinaryOperator op)
    {
        _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_rt_cmp16");
        
        var doneLabel = NewLabel("cmp_done");
        
        switch (op)
        {
            case BinaryOperator.Equal:
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                _asm.EmitLabel(Opcode.BNE, AddressingMode.Relative, doneLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                break;
                
            case BinaryOperator.NotEqual:
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                _asm.EmitLabel(Opcode.BNE, AddressingMode.Relative, doneLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                break;
                
            case BinaryOperator.LessThan:
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                _asm.EmitLabel(Opcode.BCS, AddressingMode.Relative, doneLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                break;
                
            case BinaryOperator.GreaterOrEqual:
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                _asm.EmitLabel(Opcode.BCC, AddressingMode.Relative, doneLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                break;
        }
        
        _asm.Label(doneLabel);
    }

    private void GenerateUnaryExpression(IrUnaryExpression unary)
    {
        GenerateExpression(unary.Operand);

        switch (unary.Operator)
        {
            case UnaryOperator.Negate:
                // Two's complement: EOR #$FF, CLC, ADC #1
                _asm.Emit(Opcode.EOR, AddressingMode.Immediate, 0xFF);
                _asm.Emit(Opcode.CLC);
                _asm.Emit(Opcode.ADC, AddressingMode.Immediate, 0x01);
                break;

            case UnaryOperator.Not:
                // Logical NOT: 0 -> 1, non-zero -> 0
                _asm.Emit(Opcode.CMP, AddressingMode.Immediate, 0x00);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x01);
                var notLabel = NewLabel("not");
                _asm.EmitLabel(Opcode.BEQ, AddressingMode.Relative, notLabel);
                _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00);
                _asm.Label(notLabel);
                break;

            case UnaryOperator.BitwiseNot:
                _asm.Emit(Opcode.EOR, AddressingMode.Immediate, 0xFF);
                break;

            case UnaryOperator.PreIncrement:
            case UnaryOperator.PostIncrement:
                if (unary.Operand is IrVariableExpression incVar)
                {
                    if (unary.Type.Is8Bit())
                    {
                        _asm.Emit(Opcode.CLC);
                        _asm.Emit(Opcode.ADC, AddressingMode.Immediate, 0x01);
                    }
                    GenerateStore(incVar);
                }
                break;

            case UnaryOperator.PreDecrement:
            case UnaryOperator.PostDecrement:
                if (unary.Operand is IrVariableExpression decVar)
                {
                    if (unary.Type.Is8Bit())
                    {
                        _asm.Emit(Opcode.SEC);
                        _asm.Emit(Opcode.SBC, AddressingMode.Immediate, 0x01);
                    }
                    GenerateStore(decVar);
                }
                break;
        }
    }

    private void GenerateCall(IrCallExpression call)
    {
        switch (call.FunctionName)
        {
            case "C64_PrintLine":
                if (call.Arguments.Count > 0)
                {
                    GeneratePrintStringArg(call.Arguments[0]);
                }
                _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_rt_print_newline");
                break;

            case "C64_Print":
                if (call.Arguments.Count > 0)
                {
                    GeneratePrintStringArg(call.Arguments[0]);
                }
                break;

            case "C64_GetKey":
                _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_rt_getkey");
                break;

            case "C64_ClearScreen":
                _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_rt_clearscreen");
                break;

            case "C64_Poke":
                if (call.Arguments.Count >= 2)
                {
                    // Poke(address, value)
                    GenerateExpression(call.Arguments[1]); // value in A
                    _asm.Emit(Opcode.PHA);
                    GenerateExpression(call.Arguments[0]); // address in ZP_RESULT
                    _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
                    _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_PTR1);
                    _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
                    _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_PTR1 + 1));
                    _asm.Emit(Opcode.PLA);
                    _asm.Emit(Opcode.LDY, AddressingMode.Immediate, 0x00);
                    _asm.Emit(Opcode.STA, AddressingMode.IndirectY, ZP_PTR1);
                }
                break;

            case "C64_Peek":
                if (call.Arguments.Count >= 1)
                {
                    GenerateExpression(call.Arguments[0]);
                    _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, ZP_RESULT);
                    _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_PTR1);
                    _asm.Emit(Opcode.LDA, AddressingMode.ZeroPage, (byte)(ZP_RESULT + 1));
                    _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_PTR1 + 1));
                    _asm.Emit(Opcode.LDY, AddressingMode.Immediate, 0x00);
                    _asm.Emit(Opcode.LDA, AddressingMode.IndirectY, ZP_PTR1);
                }
                break;

            case "C64_SetBorderColor":
                if (call.Arguments.Count >= 1)
                {
                    GenerateExpression(call.Arguments[0]);
                    _asm.Emit(Opcode.STA, AddressingMode.Absolute, C64Constants.VIC_Border);
                }
                break;

            case "C64_SetBackgroundColor":
                if (call.Arguments.Count >= 1)
                {
                    GenerateExpression(call.Arguments[0]);
                    _asm.Emit(Opcode.STA, AddressingMode.Absolute, C64Constants.VIC_Background);
                }
                break;

            case "C64_PrintChar":
                if (call.Arguments.Count >= 1)
                {
                    GenerateExpression(call.Arguments[0]);
                    _asm.Emit(Opcode.JSR, AddressingMode.Absolute, C64Constants.CHROUT);
                }
                break;

            default:
                // User-defined function call
                var funcLabel = $"_func_{call.FunctionName.ToLowerInvariant()}";
                if (_program.Functions.Any(f => f.Name == call.FunctionName))
                {
                    // Push arguments (simplified - just use A for single arg)
                    if (call.Arguments.Count > 0)
                    {
                        GenerateExpression(call.Arguments[0]);
                    }
                    _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, funcLabel);
                }
                break;
        }
    }

    private void GeneratePrintStringArg(IrExpression arg)
    {
        if (arg is IrStringLiteralExpression strLit)
        {
            // Load string constant address into ZP_PTR1
            _asm.Comment($"Print: \"{strLit.Value}\"");
            // We need to load the address - this requires fixup
            // For now, use a direct approach
            _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00, "Low byte (placeholder)");
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, ZP_PTR1);
            _asm.Emit(Opcode.LDA, AddressingMode.Immediate, 0x00, "High byte (placeholder)");  
            _asm.Emit(Opcode.STA, AddressingMode.ZeroPage, (byte)(ZP_PTR1 + 1));
            
            // Actually, let's generate proper code to load the string address
            // The assembler will resolve the label
            _asm.Label($"_print_{strLit.ConstantLabel}");
        }
        _asm.EmitLabel(Opcode.JSR, AddressingMode.Absolute, "_rt_print_string");
    }

    private string NewLabel(string prefix) => $"_{prefix}_{_labelCounter++}";
}
