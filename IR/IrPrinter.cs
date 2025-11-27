using System.Text;

namespace RoslynC64Compiler.IR;

/// <summary>
/// Pretty prints IR for debugging purposes
/// </summary>
public class IrPrinter
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    public string Print(IrProgram program)
    {
        _sb.Clear();
        _indent = 0;

        AppendLine($"// Program: {program.Name}");
        AppendLine();

        if (program.StringConstants.Count > 0)
        {
            AppendLine("// String Constants");
            foreach (var str in program.StringConstants)
            {
                AppendLine($"const string {str.Label} = \"{Escape(str.Value)}\";");
            }
            AppendLine();
        }

        if (program.Globals.Count > 0)
        {
            AppendLine("// Globals");
            foreach (var global in program.Globals)
            {
                var init = global.InitialValue != null ? $" = {PrintExpression(global.InitialValue)}" : "";
                AppendLine($"{global.Type} {global.Name}{init};");
            }
            AppendLine();
        }

        foreach (var func in program.Functions)
        {
            PrintFunction(func);
            AppendLine();
        }

        return _sb.ToString();
    }

    private void PrintFunction(IrFunction func)
    {
        var entry = func.IsEntryPoint ? "[EntryPoint] " : "";
        var parameters = string.Join(", ", func.Parameters.Select(p => $"{p.Type} {p.Name}"));
        AppendLine($"{entry}{func.ReturnType} {func.Name}({parameters})");
        AppendLine("{");
        _indent++;

        if (func.Locals.Count > 0)
        {
            AppendLine("// Locals");
            foreach (var local in func.Locals)
            {
                AppendLine($"{local.Type} {local.Name}; // offset: {local.StackOffset}");
            }
            AppendLine();
        }

        PrintBlock(func.Body);

        _indent--;
        AppendLine("}");
    }

    private void PrintBlock(IrBlock block)
    {
        foreach (var stmt in block.Statements)
        {
            PrintStatement(stmt);
        }
    }

    private void PrintStatement(IrStatement stmt)
    {
        switch (stmt)
        {
            case IrExpressionStatement expr:
                AppendLine($"{PrintExpression(expr.Expression)};");
                break;

            case IrReturnStatement ret:
                if (ret.Value != null)
                    AppendLine($"return {PrintExpression(ret.Value)};");
                else
                    AppendLine("return;");
                break;

            case IrVariableDeclaration decl:
                var init = decl.InitialValue != null ? $" = {PrintExpression(decl.InitialValue)}" : "";
                AppendLine($"{decl.Variable.Type} {decl.Variable.Name}{init};");
                break;

            case IrAssignment assign:
                AppendLine($"{PrintExpression(assign.Target)} = {PrintExpression(assign.Value)};");
                break;

            case IrIfStatement ifStmt:
                AppendLine($"if ({PrintExpression(ifStmt.Condition)})");
                AppendLine("{");
                _indent++;
                PrintBlock(ifStmt.ThenBlock);
                _indent--;
                if (ifStmt.ElseBlock != null)
                {
                    AppendLine("}");
                    AppendLine("else");
                    AppendLine("{");
                    _indent++;
                    PrintBlock(ifStmt.ElseBlock);
                    _indent--;
                }
                AppendLine("}");
                break;

            case IrWhileStatement whileStmt:
                AppendLine($"while ({PrintExpression(whileStmt.Condition)})");
                AppendLine("{");
                _indent++;
                PrintBlock(whileStmt.Body);
                _indent--;
                AppendLine("}");
                break;

            case IrForStatement forStmt:
                var initStr = forStmt.Initializer != null ? PrintStatementInline(forStmt.Initializer) : "";
                var condStr = forStmt.Condition != null ? PrintExpression(forStmt.Condition) : "";
                var incrStr = forStmt.Increment != null ? PrintStatementInline(forStmt.Increment) : "";
                AppendLine($"for ({initStr}; {condStr}; {incrStr})");
                AppendLine("{");
                _indent++;
                PrintBlock(forStmt.Body);
                _indent--;
                AppendLine("}");
                break;

            case IrBreakStatement:
                AppendLine("break;");
                break;

            case IrContinueStatement:
                AppendLine("continue;");
                break;
        }
    }

    private string PrintStatementInline(IrStatement stmt)
    {
        return stmt switch
        {
            IrVariableDeclaration decl => 
                decl.InitialValue != null 
                    ? $"{decl.Variable.Type} {decl.Variable.Name} = {PrintExpression(decl.InitialValue)}"
                    : $"{decl.Variable.Type} {decl.Variable.Name}",
            IrAssignment assign => $"{PrintExpression(assign.Target)} = {PrintExpression(assign.Value)}",
            IrExpressionStatement expr => PrintExpression(expr.Expression),
            _ => ""
        };
    }

    private string PrintExpression(IrExpression expr)
    {
        return expr switch
        {
            IrLiteralExpression lit => lit.Value?.ToString() ?? "null",
            IrStringLiteralExpression str => $"\"{Escape(str.Value)}\"",
            IrVariableExpression var => var.Name,
            
            IrBinaryExpression bin => $"({PrintExpression(bin.Left)} {GetOperator(bin.Operator)} {PrintExpression(bin.Right)})",
            
            IrUnaryExpression unary => unary.Operator switch
            {
                UnaryOperator.PostIncrement => $"{PrintExpression(unary.Operand)}++",
                UnaryOperator.PostDecrement => $"{PrintExpression(unary.Operand)}--",
                UnaryOperator.PreIncrement => $"++{PrintExpression(unary.Operand)}",
                UnaryOperator.PreDecrement => $"--{PrintExpression(unary.Operand)}",
                UnaryOperator.Negate => $"-{PrintExpression(unary.Operand)}",
                UnaryOperator.Not => $"!{PrintExpression(unary.Operand)}",
                UnaryOperator.BitwiseNot => $"~{PrintExpression(unary.Operand)}",
                _ => $"?{PrintExpression(unary.Operand)}"
            },
            
            IrCallExpression call => $"{call.FunctionName}({string.Join(", ", call.Arguments.Select(PrintExpression))})",
            
            IrCastExpression cast => $"({cast.TargetType}){PrintExpression(cast.Operand)}",
            
            IrArrayAccessExpression arr => $"{PrintExpression(arr.Array)}[{PrintExpression(arr.Index)}]",
            
            IrAddressOfExpression addr => $"&{PrintExpression(addr.Operand)}",
            
            IrDereferenceExpression deref => $"*{PrintExpression(deref.Pointer)}",
            
            IrConditionalExpression cond => $"({PrintExpression(cond.Condition)} ? {PrintExpression(cond.TrueValue)} : {PrintExpression(cond.FalseValue)})",
            
            _ => "?"
        };
    }

    private static string GetOperator(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.And => "&",
        BinaryOperator.Or => "|",
        BinaryOperator.Xor => "^",
        BinaryOperator.ShiftLeft => "<<",
        BinaryOperator.ShiftRight => ">>",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.LogicalAnd => "&&",
        BinaryOperator.LogicalOr => "||",
        _ => "?"
    };

    private static string Escape(string s) => s
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t");

    private void AppendLine(string text = "")
    {
        if (string.IsNullOrEmpty(text))
        {
            _sb.AppendLine();
        }
        else
        {
            _sb.Append(new string(' ', _indent * 4));
            _sb.AppendLine(text);
        }
    }
}
