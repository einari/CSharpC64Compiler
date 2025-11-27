namespace RoslynC64Compiler.CodeGeneration;

/// <summary>
/// Represents the 6502 CPU registers
/// </summary>
public enum Register
{
    A,      // Accumulator
    X,      // X Index Register
    Y,      // Y Index Register
    SP,     // Stack Pointer
    PC      // Program Counter
}

/// <summary>
/// Addressing modes for 6502 instructions
/// </summary>
public enum AddressingMode
{
    Implied,            // No operand (e.g., RTS)
    Accumulator,        // Operates on A register (e.g., ASL A)
    Immediate,          // #$nn - 8-bit constant
    ZeroPage,           // $nn - 8-bit address in zero page
    ZeroPageX,          // $nn,X - Zero page indexed with X
    ZeroPageY,          // $nn,Y - Zero page indexed with Y
    Absolute,           // $nnnn - 16-bit address
    AbsoluteX,          // $nnnn,X - Absolute indexed with X
    AbsoluteY,          // $nnnn,Y - Absolute indexed with Y
    Indirect,           // ($nnnn) - Indirect (only for JMP)
    IndirectX,          // ($nn,X) - Indexed indirect
    IndirectY,          // ($nn),Y - Indirect indexed
    Relative            // Branch offset
}

/// <summary>
/// 6502 Opcodes
/// </summary>
public enum Opcode
{
    // Load/Store Operations
    LDA,    // Load Accumulator
    LDX,    // Load X Register
    LDY,    // Load Y Register
    STA,    // Store Accumulator
    STX,    // Store X Register
    STY,    // Store Y Register

    // Register Transfers
    TAX,    // Transfer A to X
    TAY,    // Transfer A to Y
    TXA,    // Transfer X to A
    TYA,    // Transfer Y to A
    TSX,    // Transfer Stack Pointer to X
    TXS,    // Transfer X to Stack Pointer

    // Stack Operations
    PHA,    // Push Accumulator
    PHP,    // Push Processor Status
    PLA,    // Pull Accumulator
    PLP,    // Pull Processor Status

    // Logical Operations
    AND,    // Logical AND
    EOR,    // Exclusive OR
    ORA,    // Logical Inclusive OR
    BIT,    // Bit Test

    // Arithmetic Operations
    ADC,    // Add with Carry
    SBC,    // Subtract with Carry
    CMP,    // Compare Accumulator
    CPX,    // Compare X Register
    CPY,    // Compare Y Register

    // Increments & Decrements
    INC,    // Increment Memory
    INX,    // Increment X Register
    INY,    // Increment Y Register
    DEC,    // Decrement Memory
    DEX,    // Decrement X Register
    DEY,    // Decrement Y Register

    // Shifts
    ASL,    // Arithmetic Shift Left
    LSR,    // Logical Shift Right
    ROL,    // Rotate Left
    ROR,    // Rotate Right

    // Jumps & Calls
    JMP,    // Jump
    JSR,    // Jump to Subroutine
    RTS,    // Return from Subroutine

    // Branches
    BCC,    // Branch if Carry Clear
    BCS,    // Branch if Carry Set
    BEQ,    // Branch if Equal (Zero Set)
    BMI,    // Branch if Minus (Negative Set)
    BNE,    // Branch if Not Equal (Zero Clear)
    BPL,    // Branch if Plus (Negative Clear)
    BVC,    // Branch if Overflow Clear
    BVS,    // Branch if Overflow Set

    // Status Flag Changes
    CLC,    // Clear Carry Flag
    CLD,    // Clear Decimal Mode
    CLI,    // Clear Interrupt Disable
    CLV,    // Clear Overflow Flag
    SEC,    // Set Carry Flag
    SED,    // Set Decimal Flag
    SEI,    // Set Interrupt Disable

    // System Functions
    BRK,    // Force Interrupt
    NOP,    // No Operation
    RTI     // Return from Interrupt
}

/// <summary>
/// Represents a single 6502 instruction
/// </summary>
public class Instruction
{
    public Opcode Opcode { get; }
    public AddressingMode AddressingMode { get; }
    public ushort? Operand { get; }
    public string? Label { get; }
    public string? Comment { get; set; }

    public Instruction(Opcode opcode, AddressingMode mode = AddressingMode.Implied, 
                       ushort? operand = null, string? label = null)
    {
        Opcode = opcode;
        AddressingMode = mode;
        Operand = operand;
        Label = label;
    }

    /// <summary>
    /// Get the size of this instruction in bytes
    /// </summary>
    public int Size => AddressingMode switch
    {
        AddressingMode.Implied => 1,
        AddressingMode.Accumulator => 1,
        AddressingMode.Immediate => 2,
        AddressingMode.ZeroPage => 2,
        AddressingMode.ZeroPageX => 2,
        AddressingMode.ZeroPageY => 2,
        AddressingMode.Relative => 2,
        AddressingMode.Absolute => 3,
        AddressingMode.AbsoluteX => 3,
        AddressingMode.AbsoluteY => 3,
        AddressingMode.Indirect => 3,
        AddressingMode.IndirectX => 2,
        AddressingMode.IndirectY => 2,
        _ => 1
    };

    public override string ToString()
    {
        var operandStr = AddressingMode switch
        {
            AddressingMode.Implied => "",
            AddressingMode.Accumulator => "A",
            AddressingMode.Immediate => $"#${Operand:X2}",
            AddressingMode.ZeroPage => $"${Operand:X2}",
            AddressingMode.ZeroPageX => $"${Operand:X2},X",
            AddressingMode.ZeroPageY => $"${Operand:X2},Y",
            AddressingMode.Absolute => Label ?? $"${Operand:X4}",
            AddressingMode.AbsoluteX => (Label ?? $"${Operand:X4}") + ",X",
            AddressingMode.AbsoluteY => (Label ?? $"${Operand:X4}") + ",Y",
            AddressingMode.Indirect => $"(${Operand:X4})",
            AddressingMode.IndirectX => $"(${Operand:X2},X)",
            AddressingMode.IndirectY => $"(${Operand:X2}),Y",
            AddressingMode.Relative => Label ?? $"${Operand:X4}",
            _ => ""
        };

        var result = $"    {Opcode,-4} {operandStr}";
        if (!string.IsNullOrEmpty(Comment))
        {
            result += $"  ; {Comment}";
        }
        return result;
    }
}

/// <summary>
/// Lookup table for 6502 opcodes to machine code bytes
/// </summary>
public static class OpcodeTable
{
    private static readonly Dictionary<(Opcode, AddressingMode), byte> _opcodes = new()
    {
        // LDA
        { (Opcode.LDA, AddressingMode.Immediate), 0xA9 },
        { (Opcode.LDA, AddressingMode.ZeroPage), 0xA5 },
        { (Opcode.LDA, AddressingMode.ZeroPageX), 0xB5 },
        { (Opcode.LDA, AddressingMode.Absolute), 0xAD },
        { (Opcode.LDA, AddressingMode.AbsoluteX), 0xBD },
        { (Opcode.LDA, AddressingMode.AbsoluteY), 0xB9 },
        { (Opcode.LDA, AddressingMode.IndirectX), 0xA1 },
        { (Opcode.LDA, AddressingMode.IndirectY), 0xB1 },

        // LDX
        { (Opcode.LDX, AddressingMode.Immediate), 0xA2 },
        { (Opcode.LDX, AddressingMode.ZeroPage), 0xA6 },
        { (Opcode.LDX, AddressingMode.ZeroPageY), 0xB6 },
        { (Opcode.LDX, AddressingMode.Absolute), 0xAE },
        { (Opcode.LDX, AddressingMode.AbsoluteY), 0xBE },

        // LDY
        { (Opcode.LDY, AddressingMode.Immediate), 0xA0 },
        { (Opcode.LDY, AddressingMode.ZeroPage), 0xA4 },
        { (Opcode.LDY, AddressingMode.ZeroPageX), 0xB4 },
        { (Opcode.LDY, AddressingMode.Absolute), 0xAC },
        { (Opcode.LDY, AddressingMode.AbsoluteX), 0xBC },

        // STA
        { (Opcode.STA, AddressingMode.ZeroPage), 0x85 },
        { (Opcode.STA, AddressingMode.ZeroPageX), 0x95 },
        { (Opcode.STA, AddressingMode.Absolute), 0x8D },
        { (Opcode.STA, AddressingMode.AbsoluteX), 0x9D },
        { (Opcode.STA, AddressingMode.AbsoluteY), 0x99 },
        { (Opcode.STA, AddressingMode.IndirectX), 0x81 },
        { (Opcode.STA, AddressingMode.IndirectY), 0x91 },

        // STX
        { (Opcode.STX, AddressingMode.ZeroPage), 0x86 },
        { (Opcode.STX, AddressingMode.ZeroPageY), 0x96 },
        { (Opcode.STX, AddressingMode.Absolute), 0x8E },

        // STY
        { (Opcode.STY, AddressingMode.ZeroPage), 0x84 },
        { (Opcode.STY, AddressingMode.ZeroPageX), 0x94 },
        { (Opcode.STY, AddressingMode.Absolute), 0x8C },

        // Register Transfers
        { (Opcode.TAX, AddressingMode.Implied), 0xAA },
        { (Opcode.TAY, AddressingMode.Implied), 0xA8 },
        { (Opcode.TXA, AddressingMode.Implied), 0x8A },
        { (Opcode.TYA, AddressingMode.Implied), 0x98 },
        { (Opcode.TSX, AddressingMode.Implied), 0xBA },
        { (Opcode.TXS, AddressingMode.Implied), 0x9A },

        // Stack Operations
        { (Opcode.PHA, AddressingMode.Implied), 0x48 },
        { (Opcode.PHP, AddressingMode.Implied), 0x08 },
        { (Opcode.PLA, AddressingMode.Implied), 0x68 },
        { (Opcode.PLP, AddressingMode.Implied), 0x28 },

        // AND
        { (Opcode.AND, AddressingMode.Immediate), 0x29 },
        { (Opcode.AND, AddressingMode.ZeroPage), 0x25 },
        { (Opcode.AND, AddressingMode.ZeroPageX), 0x35 },
        { (Opcode.AND, AddressingMode.Absolute), 0x2D },
        { (Opcode.AND, AddressingMode.AbsoluteX), 0x3D },
        { (Opcode.AND, AddressingMode.AbsoluteY), 0x39 },
        { (Opcode.AND, AddressingMode.IndirectX), 0x21 },
        { (Opcode.AND, AddressingMode.IndirectY), 0x31 },

        // EOR
        { (Opcode.EOR, AddressingMode.Immediate), 0x49 },
        { (Opcode.EOR, AddressingMode.ZeroPage), 0x45 },
        { (Opcode.EOR, AddressingMode.ZeroPageX), 0x55 },
        { (Opcode.EOR, AddressingMode.Absolute), 0x4D },
        { (Opcode.EOR, AddressingMode.AbsoluteX), 0x5D },
        { (Opcode.EOR, AddressingMode.AbsoluteY), 0x59 },
        { (Opcode.EOR, AddressingMode.IndirectX), 0x41 },
        { (Opcode.EOR, AddressingMode.IndirectY), 0x51 },

        // ORA
        { (Opcode.ORA, AddressingMode.Immediate), 0x09 },
        { (Opcode.ORA, AddressingMode.ZeroPage), 0x05 },
        { (Opcode.ORA, AddressingMode.ZeroPageX), 0x15 },
        { (Opcode.ORA, AddressingMode.Absolute), 0x0D },
        { (Opcode.ORA, AddressingMode.AbsoluteX), 0x1D },
        { (Opcode.ORA, AddressingMode.AbsoluteY), 0x19 },
        { (Opcode.ORA, AddressingMode.IndirectX), 0x01 },
        { (Opcode.ORA, AddressingMode.IndirectY), 0x11 },

        // BIT
        { (Opcode.BIT, AddressingMode.ZeroPage), 0x24 },
        { (Opcode.BIT, AddressingMode.Absolute), 0x2C },

        // ADC
        { (Opcode.ADC, AddressingMode.Immediate), 0x69 },
        { (Opcode.ADC, AddressingMode.ZeroPage), 0x65 },
        { (Opcode.ADC, AddressingMode.ZeroPageX), 0x75 },
        { (Opcode.ADC, AddressingMode.Absolute), 0x6D },
        { (Opcode.ADC, AddressingMode.AbsoluteX), 0x7D },
        { (Opcode.ADC, AddressingMode.AbsoluteY), 0x79 },
        { (Opcode.ADC, AddressingMode.IndirectX), 0x61 },
        { (Opcode.ADC, AddressingMode.IndirectY), 0x71 },

        // SBC
        { (Opcode.SBC, AddressingMode.Immediate), 0xE9 },
        { (Opcode.SBC, AddressingMode.ZeroPage), 0xE5 },
        { (Opcode.SBC, AddressingMode.ZeroPageX), 0xF5 },
        { (Opcode.SBC, AddressingMode.Absolute), 0xED },
        { (Opcode.SBC, AddressingMode.AbsoluteX), 0xFD },
        { (Opcode.SBC, AddressingMode.AbsoluteY), 0xF9 },
        { (Opcode.SBC, AddressingMode.IndirectX), 0xE1 },
        { (Opcode.SBC, AddressingMode.IndirectY), 0xF1 },

        // CMP
        { (Opcode.CMP, AddressingMode.Immediate), 0xC9 },
        { (Opcode.CMP, AddressingMode.ZeroPage), 0xC5 },
        { (Opcode.CMP, AddressingMode.ZeroPageX), 0xD5 },
        { (Opcode.CMP, AddressingMode.Absolute), 0xCD },
        { (Opcode.CMP, AddressingMode.AbsoluteX), 0xDD },
        { (Opcode.CMP, AddressingMode.AbsoluteY), 0xD9 },
        { (Opcode.CMP, AddressingMode.IndirectX), 0xC1 },
        { (Opcode.CMP, AddressingMode.IndirectY), 0xD1 },

        // CPX
        { (Opcode.CPX, AddressingMode.Immediate), 0xE0 },
        { (Opcode.CPX, AddressingMode.ZeroPage), 0xE4 },
        { (Opcode.CPX, AddressingMode.Absolute), 0xEC },

        // CPY
        { (Opcode.CPY, AddressingMode.Immediate), 0xC0 },
        { (Opcode.CPY, AddressingMode.ZeroPage), 0xC4 },
        { (Opcode.CPY, AddressingMode.Absolute), 0xCC },

        // INC
        { (Opcode.INC, AddressingMode.ZeroPage), 0xE6 },
        { (Opcode.INC, AddressingMode.ZeroPageX), 0xF6 },
        { (Opcode.INC, AddressingMode.Absolute), 0xEE },
        { (Opcode.INC, AddressingMode.AbsoluteX), 0xFE },

        // INX, INY
        { (Opcode.INX, AddressingMode.Implied), 0xE8 },
        { (Opcode.INY, AddressingMode.Implied), 0xC8 },

        // DEC
        { (Opcode.DEC, AddressingMode.ZeroPage), 0xC6 },
        { (Opcode.DEC, AddressingMode.ZeroPageX), 0xD6 },
        { (Opcode.DEC, AddressingMode.Absolute), 0xCE },
        { (Opcode.DEC, AddressingMode.AbsoluteX), 0xDE },

        // DEX, DEY
        { (Opcode.DEX, AddressingMode.Implied), 0xCA },
        { (Opcode.DEY, AddressingMode.Implied), 0x88 },

        // ASL
        { (Opcode.ASL, AddressingMode.Accumulator), 0x0A },
        { (Opcode.ASL, AddressingMode.ZeroPage), 0x06 },
        { (Opcode.ASL, AddressingMode.ZeroPageX), 0x16 },
        { (Opcode.ASL, AddressingMode.Absolute), 0x0E },
        { (Opcode.ASL, AddressingMode.AbsoluteX), 0x1E },

        // LSR
        { (Opcode.LSR, AddressingMode.Accumulator), 0x4A },
        { (Opcode.LSR, AddressingMode.ZeroPage), 0x46 },
        { (Opcode.LSR, AddressingMode.ZeroPageX), 0x56 },
        { (Opcode.LSR, AddressingMode.Absolute), 0x4E },
        { (Opcode.LSR, AddressingMode.AbsoluteX), 0x5E },

        // ROL
        { (Opcode.ROL, AddressingMode.Accumulator), 0x2A },
        { (Opcode.ROL, AddressingMode.ZeroPage), 0x26 },
        { (Opcode.ROL, AddressingMode.ZeroPageX), 0x36 },
        { (Opcode.ROL, AddressingMode.Absolute), 0x2E },
        { (Opcode.ROL, AddressingMode.AbsoluteX), 0x3E },

        // ROR
        { (Opcode.ROR, AddressingMode.Accumulator), 0x6A },
        { (Opcode.ROR, AddressingMode.ZeroPage), 0x66 },
        { (Opcode.ROR, AddressingMode.ZeroPageX), 0x76 },
        { (Opcode.ROR, AddressingMode.Absolute), 0x6E },
        { (Opcode.ROR, AddressingMode.AbsoluteX), 0x7E },

        // JMP
        { (Opcode.JMP, AddressingMode.Absolute), 0x4C },
        { (Opcode.JMP, AddressingMode.Indirect), 0x6C },

        // JSR, RTS
        { (Opcode.JSR, AddressingMode.Absolute), 0x20 },
        { (Opcode.RTS, AddressingMode.Implied), 0x60 },

        // Branches
        { (Opcode.BCC, AddressingMode.Relative), 0x90 },
        { (Opcode.BCS, AddressingMode.Relative), 0xB0 },
        { (Opcode.BEQ, AddressingMode.Relative), 0xF0 },
        { (Opcode.BMI, AddressingMode.Relative), 0x30 },
        { (Opcode.BNE, AddressingMode.Relative), 0xD0 },
        { (Opcode.BPL, AddressingMode.Relative), 0x10 },
        { (Opcode.BVC, AddressingMode.Relative), 0x50 },
        { (Opcode.BVS, AddressingMode.Relative), 0x70 },

        // Status Flag Changes
        { (Opcode.CLC, AddressingMode.Implied), 0x18 },
        { (Opcode.CLD, AddressingMode.Implied), 0xD8 },
        { (Opcode.CLI, AddressingMode.Implied), 0x58 },
        { (Opcode.CLV, AddressingMode.Implied), 0xB8 },
        { (Opcode.SEC, AddressingMode.Implied), 0x38 },
        { (Opcode.SED, AddressingMode.Implied), 0xF8 },
        { (Opcode.SEI, AddressingMode.Implied), 0x78 },

        // System
        { (Opcode.BRK, AddressingMode.Implied), 0x00 },
        { (Opcode.NOP, AddressingMode.Implied), 0xEA },
        { (Opcode.RTI, AddressingMode.Implied), 0x40 }
    };

    public static byte GetOpcode(Opcode opcode, AddressingMode mode)
    {
        if (_opcodes.TryGetValue((opcode, mode), out var result))
        {
            return result;
        }
        throw new InvalidOperationException($"Invalid opcode/addressing mode combination: {opcode} with {mode}");
    }

    public static bool TryGetOpcode(Opcode opcode, AddressingMode mode, out byte result)
    {
        return _opcodes.TryGetValue((opcode, mode), out result);
    }
}
