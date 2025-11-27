namespace RoslynC64Compiler.IR;

/// <summary>
/// Base class for all IR nodes
/// </summary>
public abstract class IrNode
{
    public string? SourceFile { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
}

/// <summary>
/// Represents a compiled program unit
/// </summary>
public class IrProgram : IrNode
{
    public string Name { get; set; } = "Program";
    public List<IrGlobalVariable> Globals { get; } = [];
    public List<IrFunction> Functions { get; } = [];
    public List<IrStringConstant> StringConstants { get; } = [];
    public IrFunction? EntryPoint { get; set; }
}

/// <summary>
/// Type information for the IR
/// </summary>
public enum IrType
{
    Void,
    Byte,       // 8-bit unsigned (maps to C# byte)
    SByte,      // 8-bit signed (maps to C# sbyte)
    UInt16,     // 16-bit unsigned (maps to C# ushort)
    Int16,      // 16-bit signed (maps to C# short)
    Bool,       // Boolean (stored as byte)
    Pointer,    // 16-bit pointer
    String      // Pointer to null-terminated string
}

public static class IrTypeExtensions
{
    public static int SizeInBytes(this IrType type) => type switch
    {
        IrType.Void => 0,
        IrType.Byte => 1,
        IrType.SByte => 1,
        IrType.Bool => 1,
        IrType.UInt16 => 2,
        IrType.Int16 => 2,
        IrType.Pointer => 2,
        IrType.String => 2,
        _ => throw new ArgumentException($"Unknown type: {type}")
    };

    public static bool Is8Bit(this IrType type) => type.SizeInBytes() == 1;
    public static bool Is16Bit(this IrType type) => type.SizeInBytes() == 2;
}

/// <summary>
/// A global variable
/// </summary>
public class IrGlobalVariable : IrNode
{
    public string Name { get; set; } = "";
    public IrType Type { get; set; }
    public IrExpression? InitialValue { get; set; }
    public ushort? Address { get; set; }  // Assigned during code generation
}

/// <summary>
/// A string constant
/// </summary>
public class IrStringConstant : IrNode
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public ushort? Address { get; set; }
}

/// <summary>
/// A function definition
/// </summary>
public class IrFunction : IrNode
{
    public string Name { get; set; } = "";
    public IrType ReturnType { get; set; }
    public List<IrParameter> Parameters { get; } = [];
    public List<IrLocalVariable> Locals { get; } = [];
    public IrBlock Body { get; set; } = new();
    public bool IsEntryPoint { get; set; }
    public string? Label { get; set; }  // Assembly label
}

/// <summary>
/// A function parameter
/// </summary>
public class IrParameter : IrNode
{
    public string Name { get; set; } = "";
    public IrType Type { get; set; }
    public int Index { get; set; }
}

/// <summary>
/// A local variable
/// </summary>
public class IrLocalVariable : IrNode
{
    public string Name { get; set; } = "";
    public IrType Type { get; set; }
    public int StackOffset { get; set; }  // Offset from frame pointer
}

/// <summary>
/// A block of statements
/// </summary>
public class IrBlock : IrNode
{
    public List<IrStatement> Statements { get; } = [];
}

#region Statements

public abstract class IrStatement : IrNode { }

public class IrExpressionStatement : IrStatement
{
    public IrExpression Expression { get; set; } = null!;
}

public class IrReturnStatement : IrStatement
{
    public IrExpression? Value { get; set; }
}

public class IrIfStatement : IrStatement
{
    public IrExpression Condition { get; set; } = null!;
    public IrBlock ThenBlock { get; set; } = new();
    public IrBlock? ElseBlock { get; set; }
}

public class IrWhileStatement : IrStatement
{
    public IrExpression Condition { get; set; } = null!;
    public IrBlock Body { get; set; } = new();
}

public class IrForStatement : IrStatement
{
    public IrStatement? Initializer { get; set; }
    public IrExpression? Condition { get; set; }
    public IrStatement? Increment { get; set; }
    public IrBlock Body { get; set; } = new();
}

public class IrVariableDeclaration : IrStatement
{
    public IrLocalVariable Variable { get; set; } = null!;
    public IrExpression? InitialValue { get; set; }
}

public class IrAssignment : IrStatement
{
    public IrExpression Target { get; set; } = null!;
    public IrExpression Value { get; set; } = null!;
}

public class IrBreakStatement : IrStatement { }

public class IrContinueStatement : IrStatement { }

#endregion

#region Expressions

public abstract class IrExpression : IrNode
{
    public IrType Type { get; set; }
}

public class IrLiteralExpression : IrExpression
{
    public object Value { get; set; } = null!;
}

public class IrStringLiteralExpression : IrExpression
{
    public string Value { get; set; } = "";
    public string? ConstantLabel { get; set; }  // Reference to string constant
}

public class IrVariableExpression : IrExpression
{
    public string Name { get; set; } = "";
    public bool IsGlobal { get; set; }
    public bool IsLocal { get; set; }
    public bool IsParameter { get; set; }
    public int? LocalOffset { get; set; }
    public int? ParameterIndex { get; set; }
}

public class IrBinaryExpression : IrExpression
{
    public BinaryOperator Operator { get; set; }
    public IrExpression Left { get; set; } = null!;
    public IrExpression Right { get; set; } = null!;
}

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    And,            // Bitwise AND
    Or,             // Bitwise OR
    Xor,            // Bitwise XOR
    ShiftLeft,
    ShiftRight,
    Equal,
    NotEqual,
    LessThan,
    LessOrEqual,
    GreaterThan,
    GreaterOrEqual,
    LogicalAnd,     // &&
    LogicalOr       // ||
}

public class IrUnaryExpression : IrExpression
{
    public UnaryOperator Operator { get; set; }
    public IrExpression Operand { get; set; } = null!;
}

public enum UnaryOperator
{
    Negate,         // -
    Not,            // !
    BitwiseNot,     // ~
    PreIncrement,   // ++x
    PreDecrement,   // --x
    PostIncrement,  // x++
    PostDecrement   // x--
}

public class IrCallExpression : IrExpression
{
    public string FunctionName { get; set; } = "";
    public List<IrExpression> Arguments { get; } = [];
    public bool IsBuiltIn { get; set; }
}

public class IrCastExpression : IrExpression
{
    public IrExpression Operand { get; set; } = null!;
    public IrType TargetType { get; set; }
}

public class IrArrayAccessExpression : IrExpression
{
    public IrExpression Array { get; set; } = null!;
    public IrExpression Index { get; set; } = null!;
}

public class IrAddressOfExpression : IrExpression
{
    public IrExpression Operand { get; set; } = null!;
}

public class IrDereferenceExpression : IrExpression
{
    public IrExpression Pointer { get; set; } = null!;
}

public class IrConditionalExpression : IrExpression
{
    public IrExpression Condition { get; set; } = null!;
    public IrExpression TrueValue { get; set; } = null!;
    public IrExpression FalseValue { get; set; } = null!;
}

#endregion
