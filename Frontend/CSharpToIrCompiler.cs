using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynC64Compiler.IR;

namespace RoslynC64Compiler.Frontend;

/// <summary>
/// Compiles C# source code to IR using Roslyn
/// </summary>
public class CSharpToIrCompiler
{
    private readonly List<Diagnostic> _diagnostics = [];
    private IrProgram _program = null!;
    private SemanticModel _semanticModel = null!;
    private int _stringConstantCounter;
    private int _localOffset;
    private readonly Dictionary<string, IrLocalVariable> _currentLocals = [];
    private readonly Dictionary<string, IrParameter> _currentParameters = [];

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public IrProgram? Compile(string sourceCode, string fileName = "source.cs")
    {
        _diagnostics.Clear();
        _program = new IrProgram { Name = Path.GetFileNameWithoutExtension(fileName) };
        _stringConstantCounter = 0;

        // Parse the source code
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: fileName);
        
        // Check for parse errors
        var parseErrors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        if (parseErrors.Any())
        {
            _diagnostics.AddRange(parseErrors);
            return null;
        }

        // Create a compilation for semantic analysis
        var compilation = CSharpCompilation.Create(
            "C64Program",
            [syntaxTree],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithAllowUnsafe(true)
        );

        _semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Get semantic errors (but be lenient - we only support a subset)
        var semanticErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => !IsIgnorableDiagnostic(d));
        
        _diagnostics.AddRange(semanticErrors);

        // Visit the syntax tree
        var root = syntaxTree.GetCompilationUnitRoot();
        VisitCompilationUnit(root);

        // Find entry point
        _program.EntryPoint = _program.Functions.FirstOrDefault(f => f.IsEntryPoint)
                           ?? _program.Functions.FirstOrDefault(f => f.Name == "Main");

        return _program;
    }

    private static bool IsIgnorableDiagnostic(Diagnostic diagnostic)
    {
        // Ignore missing reference errors for C64 intrinsics
        return diagnostic.Id switch
        {
            "CS0103" => true,  // Name does not exist in current context (for C64 intrinsics)
            "CS0246" => true,  // Type or namespace not found (for our custom attributes)
            "CS0518" => true,  // Predefined type not defined
            "CS0012" => true,  // Type defined in assembly not referenced
            _ => false
        };
    }

    private void VisitCompilationUnit(CompilationUnitSyntax root)
    {
        foreach (var member in root.Members)
        {
            switch (member)
            {
                case GlobalStatementSyntax globalStatement:
                    // Top-level statements become Main
                    EnsureMainFunction();
                    var stmt = VisitStatement(globalStatement.Statement);
                    if (stmt != null)
                    {
                        _program.EntryPoint!.Body.Statements.Add(stmt);
                    }
                    break;

                case NamespaceDeclarationSyntax ns:
                    VisitNamespace(ns);
                    break;

                case FileScopedNamespaceDeclarationSyntax ns:
                    VisitFileScopedNamespace(ns);
                    break;

                case ClassDeclarationSyntax classDecl:
                    VisitClass(classDecl);
                    break;
                    
                case StructDeclarationSyntax structDecl:
                    VisitStruct(structDecl);
                    break;
            }
        }
    }

    private void EnsureMainFunction()
    {
        if (_program.EntryPoint == null)
        {
            var mainFunc = new IrFunction
            {
                Name = "Main",
                ReturnType = IrType.Void,
                IsEntryPoint = true,
                Label = "_main"
            };
            _program.Functions.Add(mainFunc);
            _program.EntryPoint = mainFunc;
        }
    }

    private void VisitNamespace(NamespaceDeclarationSyntax ns)
    {
        foreach (var member in ns.Members)
        {
            switch (member)
            {
                case ClassDeclarationSyntax classDecl:
                    VisitClass(classDecl);
                    break;
                case StructDeclarationSyntax structDecl:
                    VisitStruct(structDecl);
                    break;
            }
        }
    }

    private void VisitFileScopedNamespace(FileScopedNamespaceDeclarationSyntax ns)
    {
        foreach (var member in ns.Members)
        {
            switch (member)
            {
                case ClassDeclarationSyntax classDecl:
                    VisitClass(classDecl);
                    break;
                case StructDeclarationSyntax structDecl:
                    VisitStruct(structDecl);
                    break;
            }
        }
    }

    private void VisitClass(ClassDeclarationSyntax classDecl)
    {
        foreach (var member in classDecl.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    VisitField(field);
                    break;
                case MethodDeclarationSyntax method:
                    VisitMethod(method);
                    break;
            }
        }
    }

    private void VisitStruct(StructDeclarationSyntax structDecl)
    {
        foreach (var member in structDecl.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    VisitField(field);
                    break;
                case MethodDeclarationSyntax method:
                    VisitMethod(method);
                    break;
            }
        }
    }

    private void VisitField(FieldDeclarationSyntax field)
    {
        var type = GetIrType(field.Declaration.Type);

        foreach (var variable in field.Declaration.Variables)
        {
            var global = new IrGlobalVariable
            {
                Name = variable.Identifier.Text,
                Type = type
            };

            if (variable.Initializer != null)
            {
                global.InitialValue = VisitExpression(variable.Initializer.Value);
            }

            _program.Globals.Add(global);
        }
    }

    private void VisitMethod(MethodDeclarationSyntax method)
    {
        _currentLocals.Clear();
        _currentParameters.Clear();
        _localOffset = 0;

        var isMain = method.Identifier.Text == "Main";
        var func = new IrFunction
        {
            Name = method.Identifier.Text,
            ReturnType = GetIrType(method.ReturnType),
            IsEntryPoint = isMain,
            Label = isMain ? "_main" : $"_func_{method.Identifier.Text.ToLowerInvariant()}"
        };

        // Process parameters
        foreach (var param in method.ParameterList.Parameters)
        {
            var irParam = new IrParameter
            {
                Name = param.Identifier.Text,
                Type = GetIrType(param.Type!),
                Index = func.Parameters.Count
            };
            func.Parameters.Add(irParam);
            _currentParameters[irParam.Name] = irParam;
        }

        // Process body
        if (method.Body != null)
        {
            func.Body = VisitBlock(method.Body);
        }
        else if (method.ExpressionBody != null)
        {
            var expr = VisitExpression(method.ExpressionBody.Expression);
            if (expr != null)
            {
                if (func.ReturnType != IrType.Void)
                {
                    func.Body.Statements.Add(new IrReturnStatement { Value = expr });
                }
                else
                {
                    func.Body.Statements.Add(new IrExpressionStatement { Expression = expr });
                }
            }
        }

        func.Locals.AddRange(_currentLocals.Values);
        _program.Functions.Add(func);
    }

    private IrBlock VisitBlock(BlockSyntax block)
    {
        var irBlock = new IrBlock();
        foreach (var statement in block.Statements)
        {
            var stmt = VisitStatement(statement);
            if (stmt != null)
            {
                irBlock.Statements.Add(stmt);
            }
        }
        return irBlock;
    }

    private IrStatement? VisitStatement(StatementSyntax statement)
    {
        switch (statement)
        {
            case ExpressionStatementSyntax exprStmt:
                var expr = VisitExpression(exprStmt.Expression);
                return expr != null ? new IrExpressionStatement { Expression = expr } : null;

            case LocalDeclarationStatementSyntax localDecl:
                return VisitLocalDeclaration(localDecl);

            case ReturnStatementSyntax returnStmt:
                return new IrReturnStatement
                {
                    Value = returnStmt.Expression != null ? VisitExpression(returnStmt.Expression) : null
                };

            case IfStatementSyntax ifStmt:
                return VisitIfStatement(ifStmt);

            case WhileStatementSyntax whileStmt:
                return VisitWhileStatement(whileStmt);

            case ForStatementSyntax forStmt:
                return VisitForStatement(forStmt);

            case BlockSyntax block:
                // Flatten block into parent
                var irBlock = VisitBlock(block);
                if (irBlock.Statements.Count == 1)
                    return irBlock.Statements[0];
                // For multiple statements, we'd need a block statement type
                // For now, just return the first
                return irBlock.Statements.FirstOrDefault();

            case BreakStatementSyntax:
                return new IrBreakStatement();

            case ContinueStatementSyntax:
                return new IrContinueStatement();

            default:
                // Unsupported statement type
                return null;
        }
    }

    private IrStatement? VisitLocalDeclaration(LocalDeclarationStatementSyntax localDecl)
    {
        var type = GetIrType(localDecl.Declaration.Type);
        
        IrStatement? lastDecl = null;
        foreach (var variable in localDecl.Declaration.Variables)
        {
            var local = new IrLocalVariable
            {
                Name = variable.Identifier.Text,
                Type = type,
                StackOffset = _localOffset
            };
            _localOffset += type.SizeInBytes();
            _currentLocals[local.Name] = local;

            lastDecl = new IrVariableDeclaration
            {
                Variable = local,
                InitialValue = variable.Initializer != null 
                    ? VisitExpression(variable.Initializer.Value) 
                    : null
            };
        }

        return lastDecl;
    }

    private IrIfStatement VisitIfStatement(IfStatementSyntax ifStmt)
    {
        var result = new IrIfStatement
        {
            Condition = VisitExpression(ifStmt.Condition)!
        };

        if (ifStmt.Statement is BlockSyntax block)
        {
            result.ThenBlock = VisitBlock(block);
        }
        else
        {
            var stmt = VisitStatement(ifStmt.Statement);
            if (stmt != null)
                result.ThenBlock.Statements.Add(stmt);
        }

        if (ifStmt.Else != null)
        {
            result.ElseBlock = new IrBlock();
            if (ifStmt.Else.Statement is BlockSyntax elseBlock)
            {
                result.ElseBlock = VisitBlock(elseBlock);
            }
            else
            {
                var stmt = VisitStatement(ifStmt.Else.Statement);
                if (stmt != null)
                    result.ElseBlock.Statements.Add(stmt);
            }
        }

        return result;
    }

    private IrWhileStatement VisitWhileStatement(WhileStatementSyntax whileStmt)
    {
        var result = new IrWhileStatement
        {
            Condition = VisitExpression(whileStmt.Condition)!
        };

        if (whileStmt.Statement is BlockSyntax block)
        {
            result.Body = VisitBlock(block);
        }
        else
        {
            var stmt = VisitStatement(whileStmt.Statement);
            if (stmt != null)
                result.Body.Statements.Add(stmt);
        }

        return result;
    }

    private IrForStatement VisitForStatement(ForStatementSyntax forStmt)
    {
        var result = new IrForStatement();

        if (forStmt.Declaration != null)
        {
            var type = GetIrType(forStmt.Declaration.Type);
            foreach (var variable in forStmt.Declaration.Variables)
            {
                var local = new IrLocalVariable
                {
                    Name = variable.Identifier.Text,
                    Type = type,
                    StackOffset = _localOffset
                };
                _localOffset += type.SizeInBytes();
                _currentLocals[local.Name] = local;

                result.Initializer = new IrVariableDeclaration
                {
                    Variable = local,
                    InitialValue = variable.Initializer != null 
                        ? VisitExpression(variable.Initializer.Value) 
                        : null
                };
            }
        }
        else if (forStmt.Initializers.Count > 0)
        {
            var expr = VisitExpression(forStmt.Initializers[0]);
            if (expr != null)
                result.Initializer = new IrExpressionStatement { Expression = expr };
        }

        if (forStmt.Condition != null)
        {
            result.Condition = VisitExpression(forStmt.Condition);
        }

        if (forStmt.Incrementors.Count > 0)
        {
            var expr = VisitExpression(forStmt.Incrementors[0]);
            if (expr != null)
                result.Increment = new IrExpressionStatement { Expression = expr };
        }

        if (forStmt.Statement is BlockSyntax block)
        {
            result.Body = VisitBlock(block);
        }
        else
        {
            var stmt = VisitStatement(forStmt.Statement);
            if (stmt != null)
                result.Body.Statements.Add(stmt);
        }

        return result;
    }

    private IrExpression? VisitExpression(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal:
                return VisitLiteral(literal);

            case IdentifierNameSyntax identifier:
                return VisitIdentifier(identifier);

            case BinaryExpressionSyntax binary:
                return VisitBinaryExpression(binary);

            case PrefixUnaryExpressionSyntax prefixUnary:
                return VisitPrefixUnary(prefixUnary);

            case PostfixUnaryExpressionSyntax postfixUnary:
                return VisitPostfixUnary(postfixUnary);

            case InvocationExpressionSyntax invocation:
                return VisitInvocation(invocation);

            case AssignmentExpressionSyntax assignment:
                return VisitAssignment(assignment);

            case ParenthesizedExpressionSyntax paren:
                return VisitExpression(paren.Expression);

            case CastExpressionSyntax cast:
                return new IrCastExpression
                {
                    Operand = VisitExpression(cast.Expression)!,
                    TargetType = GetIrType(cast.Type),
                    Type = GetIrType(cast.Type)
                };

            case ElementAccessExpressionSyntax elementAccess:
                return new IrArrayAccessExpression
                {
                    Array = VisitExpression(elementAccess.Expression)!,
                    Index = VisitExpression(elementAccess.ArgumentList.Arguments[0].Expression)!
                };

            case ConditionalExpressionSyntax conditional:
                return new IrConditionalExpression
                {
                    Condition = VisitExpression(conditional.Condition)!,
                    TrueValue = VisitExpression(conditional.WhenTrue)!,
                    FalseValue = VisitExpression(conditional.WhenFalse)!
                };

            case MemberAccessExpressionSyntax memberAccess:
                // Handle simple member access (e.g., Console.WriteLine becomes just WriteLine call)
                return VisitMemberAccess(memberAccess);

            default:
                return null;
        }
    }

    private IrExpression VisitLiteral(LiteralExpressionSyntax literal)
    {
        switch (literal.Kind())
        {
            case SyntaxKind.NumericLiteralExpression:
                var value = literal.Token.Value;
                return new IrLiteralExpression
                {
                    Value = value!,
                    Type = value switch
                    {
                        byte => IrType.Byte,
                        sbyte => IrType.SByte,
                        short => IrType.Int16,
                        ushort => IrType.UInt16,
                        int i when i >= 0 && i <= 255 => IrType.Byte,
                        int i when i >= -128 && i <= 127 => IrType.SByte,
                        int i when i >= 0 && i <= 65535 => IrType.UInt16,
                        _ => IrType.Int16
                    }
                };

            case SyntaxKind.StringLiteralExpression:
                var str = literal.Token.ValueText;
                var label = $"_str_{_stringConstantCounter++}";
                _program.StringConstants.Add(new IrStringConstant { Label = label, Value = str });
                return new IrStringLiteralExpression
                {
                    Value = str,
                    ConstantLabel = label,
                    Type = IrType.String
                };

            case SyntaxKind.CharacterLiteralExpression:
                return new IrLiteralExpression
                {
                    Value = (byte)(char)literal.Token.Value!,
                    Type = IrType.Byte
                };

            case SyntaxKind.TrueLiteralExpression:
                return new IrLiteralExpression { Value = (byte)1, Type = IrType.Bool };

            case SyntaxKind.FalseLiteralExpression:
                return new IrLiteralExpression { Value = (byte)0, Type = IrType.Bool };

            case SyntaxKind.NullLiteralExpression:
                return new IrLiteralExpression { Value = (ushort)0, Type = IrType.Pointer };

            default:
                return new IrLiteralExpression { Value = 0, Type = IrType.Byte };
        }
    }

    private IrExpression VisitIdentifier(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.Text;

        // Check locals first
        if (_currentLocals.TryGetValue(name, out var local))
        {
            return new IrVariableExpression
            {
                Name = name,
                IsLocal = true,
                LocalOffset = local.StackOffset,
                Type = local.Type
            };
        }

        // Check parameters
        if (_currentParameters.TryGetValue(name, out var param))
        {
            return new IrVariableExpression
            {
                Name = name,
                IsParameter = true,
                ParameterIndex = param.Index,
                Type = param.Type
            };
        }

        // Check globals
        var global = _program.Globals.FirstOrDefault(g => g.Name == name);
        if (global != null)
        {
            return new IrVariableExpression
            {
                Name = name,
                IsGlobal = true,
                Type = global.Type
            };
        }

        // Unknown variable - might be a C64 intrinsic reference
        return new IrVariableExpression
        {
            Name = name,
            Type = IrType.UInt16
        };
    }

    private IrExpression? VisitMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        // For now, just return the member name as a variable
        // This handles cases like C64.PrintChar -> just track as "PrintChar" call
        return new IrVariableExpression
        {
            Name = memberAccess.Name.Identifier.Text,
            Type = IrType.UInt16
        };
    }

    private IrExpression VisitBinaryExpression(BinaryExpressionSyntax binary)
    {
        var left = VisitExpression(binary.Left)!;
        var right = VisitExpression(binary.Right)!;
        var op = binary.Kind() switch
        {
            SyntaxKind.AddExpression => BinaryOperator.Add,
            SyntaxKind.SubtractExpression => BinaryOperator.Subtract,
            SyntaxKind.MultiplyExpression => BinaryOperator.Multiply,
            SyntaxKind.DivideExpression => BinaryOperator.Divide,
            SyntaxKind.ModuloExpression => BinaryOperator.Modulo,
            SyntaxKind.BitwiseAndExpression => BinaryOperator.And,
            SyntaxKind.BitwiseOrExpression => BinaryOperator.Or,
            SyntaxKind.ExclusiveOrExpression => BinaryOperator.Xor,
            SyntaxKind.LeftShiftExpression => BinaryOperator.ShiftLeft,
            SyntaxKind.RightShiftExpression => BinaryOperator.ShiftRight,
            SyntaxKind.EqualsExpression => BinaryOperator.Equal,
            SyntaxKind.NotEqualsExpression => BinaryOperator.NotEqual,
            SyntaxKind.LessThanExpression => BinaryOperator.LessThan,
            SyntaxKind.LessThanOrEqualExpression => BinaryOperator.LessOrEqual,
            SyntaxKind.GreaterThanExpression => BinaryOperator.GreaterThan,
            SyntaxKind.GreaterThanOrEqualExpression => BinaryOperator.GreaterOrEqual,
            SyntaxKind.LogicalAndExpression => BinaryOperator.LogicalAnd,
            SyntaxKind.LogicalOrExpression => BinaryOperator.LogicalOr,
            _ => BinaryOperator.Add
        };

        // Determine result type
        var resultType = (left.Type, right.Type) switch
        {
            (IrType.Int16, _) or (_, IrType.Int16) => IrType.Int16,
            (IrType.UInt16, _) or (_, IrType.UInt16) => IrType.UInt16,
            _ => IrType.Byte
        };

        // Comparisons return bool
        if (op is BinaryOperator.Equal or BinaryOperator.NotEqual or 
            BinaryOperator.LessThan or BinaryOperator.LessOrEqual or
            BinaryOperator.GreaterThan or BinaryOperator.GreaterOrEqual)
        {
            resultType = IrType.Bool;
        }

        return new IrBinaryExpression
        {
            Left = left,
            Right = right,
            Operator = op,
            Type = resultType
        };
    }

    private IrExpression VisitPrefixUnary(PrefixUnaryExpressionSyntax prefixUnary)
    {
        var operand = VisitExpression(prefixUnary.Operand)!;
        var op = prefixUnary.Kind() switch
        {
            SyntaxKind.UnaryMinusExpression => UnaryOperator.Negate,
            SyntaxKind.LogicalNotExpression => UnaryOperator.Not,
            SyntaxKind.BitwiseNotExpression => UnaryOperator.BitwiseNot,
            SyntaxKind.PreIncrementExpression => UnaryOperator.PreIncrement,
            SyntaxKind.PreDecrementExpression => UnaryOperator.PreDecrement,
            _ => UnaryOperator.Negate
        };

        return new IrUnaryExpression
        {
            Operand = operand,
            Operator = op,
            Type = op == UnaryOperator.Not ? IrType.Bool : operand.Type
        };
    }

    private IrExpression VisitPostfixUnary(PostfixUnaryExpressionSyntax postfixUnary)
    {
        var operand = VisitExpression(postfixUnary.Operand)!;
        var op = postfixUnary.Kind() switch
        {
            SyntaxKind.PostIncrementExpression => UnaryOperator.PostIncrement,
            SyntaxKind.PostDecrementExpression => UnaryOperator.PostDecrement,
            _ => UnaryOperator.PostIncrement
        };

        return new IrUnaryExpression
        {
            Operand = operand,
            Operator = op,
            Type = operand.Type
        };
    }

    private IrExpression VisitInvocation(InvocationExpressionSyntax invocation)
    {
        string functionName;
        bool isBuiltIn = false;

        switch (invocation.Expression)
        {
            case IdentifierNameSyntax identifier:
                functionName = identifier.Identifier.Text;
                break;

            case MemberAccessExpressionSyntax memberAccess:
                // Handle Console.WriteLine, C64.Print, etc.
                var typeName = memberAccess.Expression.ToString();
                functionName = memberAccess.Name.Identifier.Text;
                
                // Map .NET methods to C64 equivalents
                if (typeName == "Console")
                {
                    isBuiltIn = true;
                    functionName = functionName switch
                    {
                        "WriteLine" => "C64_PrintLine",
                        "Write" => "C64_Print",
                        "ReadKey" => "C64_GetKey",
                        "Clear" => "C64_ClearScreen",
                        _ => functionName
                    };
                }
                else if (typeName == "C64")
                {
                    isBuiltIn = true;
                    functionName = $"C64_{functionName}";
                }
                break;

            default:
                functionName = "unknown";
                break;
        }

        var call = new IrCallExpression
        {
            FunctionName = functionName,
            IsBuiltIn = isBuiltIn,
            Type = IrType.Void  // Will be refined based on function signature
        };

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var argExpr = VisitExpression(arg.Expression);
            if (argExpr != null)
            {
                call.Arguments.Add(argExpr);
            }
        }

        return call;
    }

    private IrExpression VisitAssignment(AssignmentExpressionSyntax assignment)
    {
        var left = VisitExpression(assignment.Left)!;
        var right = VisitExpression(assignment.Right)!;

        // For compound assignments, create binary expression
        BinaryOperator? compoundOp = assignment.Kind() switch
        {
            SyntaxKind.AddAssignmentExpression => BinaryOperator.Add,
            SyntaxKind.SubtractAssignmentExpression => BinaryOperator.Subtract,
            SyntaxKind.MultiplyAssignmentExpression => BinaryOperator.Multiply,
            SyntaxKind.DivideAssignmentExpression => BinaryOperator.Divide,
            SyntaxKind.ModuloAssignmentExpression => BinaryOperator.Modulo,
            SyntaxKind.AndAssignmentExpression => BinaryOperator.And,
            SyntaxKind.OrAssignmentExpression => BinaryOperator.Or,
            SyntaxKind.ExclusiveOrAssignmentExpression => BinaryOperator.Xor,
            SyntaxKind.LeftShiftAssignmentExpression => BinaryOperator.ShiftLeft,
            SyntaxKind.RightShiftAssignmentExpression => BinaryOperator.ShiftRight,
            _ => null
        };

        if (compoundOp.HasValue)
        {
            right = new IrBinaryExpression
            {
                Left = left,
                Right = right,
                Operator = compoundOp.Value,
                Type = left.Type
            };
        }

        // Return as a "fake" expression that represents assignment
        // The code generator will handle this specially
        return new IrBinaryExpression
        {
            Left = left,
            Right = right,
            Operator = BinaryOperator.Equal,  // Abuse Equal to mean assignment in expression context
            Type = left.Type
        };
    }

    private IrType GetIrType(TypeSyntax typeSyntax)
    {
        var typeName = typeSyntax.ToString();
        return typeName switch
        {
            "void" => IrType.Void,
            "byte" => IrType.Byte,
            "sbyte" => IrType.SByte,
            "short" => IrType.Int16,
            "ushort" => IrType.UInt16,
            "int" => IrType.Int16,  // Map int to 16-bit on C64
            "uint" => IrType.UInt16,
            "bool" => IrType.Bool,
            "char" => IrType.Byte,  // PETSCII character
            "string" => IrType.String,
            "var" => IrType.Int16,  // Default for var
            _ when typeName.EndsWith("*") => IrType.Pointer,
            _ => IrType.UInt16  // Default to 16-bit
        };
    }
}
