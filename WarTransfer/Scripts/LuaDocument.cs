using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Lua.Syntax;

namespace WarTransfer.Scripts
{
    // Retain original text and edit only selected spans. Parsing never executes a map.
    internal sealed class LuaDocument
    {
        internal readonly string Text;
        internal readonly CompilationUnitSyntax Root;
        internal readonly Dictionary<string, FunctionDeclarationStatementSyntax> Functions;
        internal readonly HashSet<string> Locals = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> Globals = new HashSet<string>(StringComparer.Ordinal);

        private LuaDocument(string text, CompilationUnitSyntax root)
        {
            Text = text;
            Root = root;
            Functions = new Dictionary<string, FunctionDeclarationStatementSyntax>(StringComparer.Ordinal);
            foreach (var statement in root.Statements.Statements)
            {
                var global = statement as AssignmentStatementSyntax;
                if (global != null)
                    foreach (var variable in global.Variables.OfType<IdentifierNameSyntax>()) Globals.Add(variable.Name);
                var function = statement as FunctionDeclarationStatementSyntax;
                if (function?.Name is SimpleFunctionNameSyntax)
                {
                    string name = function.Name.ToString();
                    if (Functions.ContainsKey(name) && IsWorldFunction(name))
                        throw new FormatException("Duplicate generated Lua function '" + name + "'.");
                    Functions[name] = function;
                }
                var local = statement as LocalVariableDeclarationStatementSyntax;
                if (local != null)
                    foreach (var name in local.Names) Locals.Add(name.ToString());
                var localFunction = statement as LocalFunctionDeclarationStatementSyntax;
                if (localFunction != null) Locals.Add(localFunction.Name.ToString());
            }

            if (Locals.Any(IsWorldFunction))
                throw new FormatException("Generated Lua entrypoints must not be shadowed by local variables.");

            foreach (var node in root.DescendantNodes())
            {
                var function = node as FunctionDeclarationStatementSyntax;
                if (function?.Name is SimpleFunctionNameSyntax && IsWorldFunction(function.Name.ToString()) &&
                    function.Parent != root.Statements)
                    throw new FormatException("Generated Lua functions must be declared at top level: '" + function.Name + "'.");
                var localFunction = node as LocalFunctionDeclarationStatementSyntax;
                if (localFunction != null && IsWorldFunction(localFunction.Name.ToString()))
                    throw new FormatException("Generated Lua functions cannot be local: '" + localFunction.Name + "'.");
                var assignment = node as AssignmentStatementSyntax;
                if (assignment != null && assignment.Variables.OfType<IdentifierNameSyntax>().Any(n => IsWorldFunction(n.Name)))
                    throw new FormatException("Generated Lua entrypoints must use function declarations, not assignments.");
            }
        }

        internal static LuaDocument Parse(string text, string path = "war3map.lua")
        {
            var options = new LuaParseOptions(LuaSyntaxOptions.Lua53.With(acceptNestingOfLongStrings: false));
            var tree = LuaSyntaxTree.ParseText(text, options, path);
            var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(8).ToArray();
            if (errors.Length != 0)
                throw new FormatException("Invalid Lua script: " + string.Join("; ", errors.Select(d => d.ToString())));
            return new LuaDocument(text, (CompilationUnitSyntax)tree.GetRoot());
        }

        internal static bool IsWorldFunction(string name) => Regex.IsMatch(name,
            @"^(config|main|CreateAllUnits|CreateNeutralPassiveBuildings|CreatePlayerBuildings|CreateNeutralPassive|CreateNeutralHostile|CreatePlayerUnits|CreateUnitsForPlayer\d+|CreateBuildingsForPlayer\d+|CreateAllDestructables|CreateAllItems|CreateDestructables|CreateItems|InitCustomPlayerSlots|InitCustomTeams|InitAllyPriorities|InitRandomGroups|CreateRegions|CreateCameras|Unit\d+_DropItems|ItemTable\d+_DropItems)$");

        internal FunctionDeclarationStatementSyntax Entry(string name)
        {
            FunctionDeclarationStatementSyntax function;
            if (!Functions.TryGetValue(name, out function) || function.Parameters.Parameters.Count != 0)
                throw new FormatException("Lua maps must contain a top-level function " + name + "() declaration with no parameters.");
            return function;
        }

        internal static FunctionCallExpressionSyntax DirectCall(StatementSyntax statement) =>
            (statement as ExpressionStatementSyntax)?.Expression as FunctionCallExpressionSyntax;

        internal static string CallName(FunctionCallExpressionSyntax call) => (call?.Expression as IdentifierNameSyntax)?.Name;

        internal static int InitializationMarker(FunctionDeclarationStatementSyntax main)
        {
            var all = main.Body.DescendantNodes().OfType<FunctionCallExpressionSyntax>()
                .Where(c => CallName(c) == "InitBlizzard").ToArray();
            var statements = main.Body.Statements;
            for (int i = 0; i < statements.Count; i++)
            {
                var call = DirectCall(statements[i]);
                if (all.Length == 1 && call == all[0] && call.Argument is ExpressionListFunctionArgumentSyntax args && args.Expressions.Count == 0)
                    return i;
            }
            throw new FormatException("Cannot merge Lua main: source and destination must each have exactly one unconditional InitBlizzard() call.");
        }

        internal string Slice(SyntaxNode node) => Text.Substring(node.Span.Start, node.Span.Length);
        internal string FunctionText(string name) => Slice(Entry(name));

        internal static string Edit(string text, IEnumerable<EditSpan> edits)
        {
            var result = new StringBuilder(text);
            int next = text.Length;
            foreach (var edit in edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.Length))
            {
                if (edit.Start < 0 || edit.Start + edit.Length > next)
                    throw new InvalidOperationException("Overlapping Lua edits.");
                result.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Value);
                next = edit.Start;
            }
            return result.ToString();
        }

        internal sealed class EditSpan
        {
            internal readonly int Start, Length;
            internal readonly string Value;
            internal EditSpan(int start, int length, string value) { Start = start; Length = length; Value = value; }
            internal EditSpan(SyntaxNode node, string value) : this(node.Span.Start, node.Span.Length, value) { }
        }
    }
}
