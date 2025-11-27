namespace RoslynC64Compiler.CodeGeneration;

/// <summary>
/// Builds a sequence of 6502 instructions with label support
/// </summary>
public class AssemblyBuilder
{
    private readonly List<AssemblyLine> _lines = [];
    private readonly Dictionary<string, int> _labels = [];
    private int _currentAddress;
    private readonly ushort _baseAddress;

    public AssemblyBuilder(ushort baseAddress = 0x0801)
    {
        _baseAddress = baseAddress;
        _currentAddress = baseAddress;
    }

    public ushort BaseAddress => _baseAddress;
    public int CurrentAddress => _currentAddress;

    /// <summary>
    /// Add a label at the current position
    /// </summary>
    public AssemblyBuilder Label(string name)
    {
        _labels[name] = _currentAddress;
        _lines.Add(new AssemblyLine { Label = name });
        return this;
    }

    /// <summary>
    /// Add an instruction with implied addressing
    /// </summary>
    public AssemblyBuilder Emit(Opcode opcode, string? comment = null)
    {
        var instr = new Instruction(opcode, AddressingMode.Implied) { Comment = comment };
        _lines.Add(new AssemblyLine { Instruction = instr, Address = _currentAddress });
        _currentAddress += instr.Size;
        return this;
    }

    /// <summary>
    /// Add an instruction with a byte operand
    /// </summary>
    public AssemblyBuilder Emit(Opcode opcode, AddressingMode mode, byte operand, string? comment = null)
    {
        var instr = new Instruction(opcode, mode, operand) { Comment = comment };
        _lines.Add(new AssemblyLine { Instruction = instr, Address = _currentAddress });
        _currentAddress += instr.Size;
        return this;
    }

    /// <summary>
    /// Add an instruction with a word operand
    /// </summary>
    public AssemblyBuilder Emit(Opcode opcode, AddressingMode mode, ushort operand, string? comment = null)
    {
        var instr = new Instruction(opcode, mode, operand) { Comment = comment };
        _lines.Add(new AssemblyLine { Instruction = instr, Address = _currentAddress });
        _currentAddress += instr.Size;
        return this;
    }

    /// <summary>
    /// Add an instruction referencing a label
    /// </summary>
    public AssemblyBuilder EmitLabel(Opcode opcode, AddressingMode mode, string label, string? comment = null)
    {
        var instr = new Instruction(opcode, mode, 0, label) { Comment = comment };
        _lines.Add(new AssemblyLine { Instruction = instr, Address = _currentAddress, UnresolvedLabel = label });
        _currentAddress += instr.Size;
        return this;
    }

    /// <summary>
    /// Add raw bytes
    /// </summary>
    public AssemblyBuilder Bytes(params byte[] data)
    {
        _lines.Add(new AssemblyLine { Data = data, Address = _currentAddress });
        _currentAddress += data.Length;
        return this;
    }

    /// <summary>
    /// Add a word (16-bit little-endian)
    /// </summary>
    public AssemblyBuilder Word(ushort value)
    {
        return Bytes((byte)(value & 0xFF), (byte)(value >> 8));
    }

    /// <summary>
    /// Add a null-terminated string (PETSCII)
    /// </summary>
    public AssemblyBuilder String(string text)
    {
        var bytes = text.Select(c => AsciiToPetscii(c)).Append((byte)0).ToArray();
        return Bytes(bytes);
    }

    /// <summary>
    /// Add string data without null terminator
    /// </summary>
    public AssemblyBuilder StringData(string text)
    {
        var bytes = text.Select(c => AsciiToPetscii(c)).ToArray();
        return Bytes(bytes);
    }

    /// <summary>
    /// Reserve bytes (filled with zeros)
    /// </summary>
    public AssemblyBuilder Reserve(int count)
    {
        return Bytes(new byte[count]);
    }

    /// <summary>
    /// Add a comment line
    /// </summary>
    public AssemblyBuilder Comment(string text)
    {
        _lines.Add(new AssemblyLine { Comment = text });
        return this;
    }

    /// <summary>
    /// Add a blank line
    /// </summary>
    public AssemblyBuilder Blank()
    {
        _lines.Add(new AssemblyLine());
        return this;
    }

    /// <summary>
    /// Set the current address (org directive)
    /// </summary>
    public AssemblyBuilder Org(ushort address)
    {
        _currentAddress = address;
        _lines.Add(new AssemblyLine { OrgAddress = address });
        return this;
    }

    /// <summary>
    /// Get the address of a label
    /// </summary>
    public ushort GetLabelAddress(string label)
    {
        if (_labels.TryGetValue(label, out var address))
        {
            return (ushort)address;
        }
        throw new InvalidOperationException($"Label not found: {label}");
    }

    /// <summary>
    /// Check if a label exists
    /// </summary>
    public bool HasLabel(string label) => _labels.ContainsKey(label);

    /// <summary>
    /// Generate assembly listing
    /// </summary>
    public string ToAssemblyListing()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"; Generated by RoslynC64Compiler");
        sb.AppendLine($"; Base address: ${_baseAddress:X4}");
        sb.AppendLine();

        foreach (var line in _lines)
        {
            if (line.OrgAddress.HasValue)
            {
                sb.AppendLine($"    * = ${line.OrgAddress.Value:X4}");
            }
            else if (!string.IsNullOrEmpty(line.Label))
            {
                sb.AppendLine($"{line.Label}:");
            }
            else if (line.Instruction != null)
            {
                sb.AppendLine($"${line.Address:X4}  {line.Instruction}");
            }
            else if (line.Data != null)
            {
                sb.Append($"${line.Address:X4}  .byte ");
                sb.AppendLine(string.Join(", ", line.Data.Select(b => $"${b:X2}")));
            }
            else if (!string.IsNullOrEmpty(line.Comment))
            {
                sb.AppendLine($"; {line.Comment}");
            }
            else
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Assemble to machine code bytes
    /// </summary>
    public byte[] Assemble()
    {
        // First pass: resolve all labels
        ResolveLabels();

        // Second pass: generate bytes
        var bytes = new List<byte>();

        foreach (var line in _lines)
        {
            if (line.Instruction != null)
            {
                var instr = line.Instruction;
                var opcode = OpcodeTable.GetOpcode(instr.Opcode, instr.AddressingMode);
                bytes.Add(opcode);

                ushort operand = instr.Operand ?? 0;
                
                // Resolve label if present
                if (!string.IsNullOrEmpty(line.UnresolvedLabel))
                {
                    operand = GetLabelAddress(line.UnresolvedLabel);
                    
                    // For relative branches, calculate offset
                    if (instr.AddressingMode == AddressingMode.Relative)
                    {
                        int offset = operand - (line.Address + 2); // +2 for instruction size
                        if (offset < -128 || offset > 127)
                        {
                            throw new InvalidOperationException($"Branch target out of range: {line.UnresolvedLabel}");
                        }
                        operand = (ushort)(byte)offset;
                    }
                }

                switch (instr.AddressingMode)
                {
                    case AddressingMode.Immediate:
                    case AddressingMode.ZeroPage:
                    case AddressingMode.ZeroPageX:
                    case AddressingMode.ZeroPageY:
                    case AddressingMode.IndirectX:
                    case AddressingMode.IndirectY:
                    case AddressingMode.Relative:
                        bytes.Add((byte)(operand & 0xFF));
                        break;

                    case AddressingMode.Absolute:
                    case AddressingMode.AbsoluteX:
                    case AddressingMode.AbsoluteY:
                    case AddressingMode.Indirect:
                        bytes.Add((byte)(operand & 0xFF));
                        bytes.Add((byte)(operand >> 8));
                        break;
                }
            }
            else if (line.Data != null)
            {
                bytes.AddRange(line.Data);
            }
        }

        return bytes.ToArray();
    }

    private void ResolveLabels()
    {
        int address = _baseAddress;

        foreach (var line in _lines)
        {
            if (line.OrgAddress.HasValue)
            {
                address = line.OrgAddress.Value;
            }
            else if (!string.IsNullOrEmpty(line.Label) && line.Instruction == null && line.Data == null)
            {
                _labels[line.Label] = address;
            }
            else if (line.Instruction != null)
            {
                line.Address = address;
                address += line.Instruction.Size;
            }
            else if (line.Data != null)
            {
                line.Address = address;
                address += line.Data.Length;
            }
        }
    }

    private static byte AsciiToPetscii(char c)
    {
        // Basic ASCII to PETSCII conversion
        return c switch
        {
            >= 'A' and <= 'Z' => (byte)(c - 'A' + 65),  // Uppercase
            >= 'a' and <= 'z' => (byte)(c - 'a' + 65),  // Lowercase -> uppercase in PETSCII screen codes
            >= '0' and <= '9' => (byte)c,               // Numbers
            ' ' => 32,
            '!' => 33,
            '"' => 34,
            '#' => 35,
            '$' => 36,
            '%' => 37,
            '&' => 38,
            '\'' => 39,
            '(' => 40,
            ')' => 41,
            '*' => 42,
            '+' => 43,
            ',' => 44,
            '-' => 45,
            '.' => 46,
            '/' => 47,
            ':' => 58,
            ';' => 59,
            '<' => 60,
            '=' => 61,
            '>' => 62,
            '?' => 63,
            '@' => 64,
            '\n' => 13,  // Carriage return
            _ => 32      // Default to space
        };
    }

    private class AssemblyLine
    {
        public string? Label { get; set; }
        public Instruction? Instruction { get; set; }
        public byte[]? Data { get; set; }
        public string? Comment { get; set; }
        public ushort? OrgAddress { get; set; }
        public int Address { get; set; }
        public string? UnresolvedLabel { get; set; }
    }
}
