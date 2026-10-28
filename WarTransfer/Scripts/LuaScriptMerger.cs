using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Lua.Syntax;
using WarTransfer.FileTypes;

namespace WarTransfer.Scripts
{
    internal sealed class LuaScriptMerger : IMapScriptMerger
    {
        private readonly WarTransferArgs args;
        internal LuaScriptMerger(WarTransferArgs args) { this.args = args; }
        public MapScriptType ScriptType => MapScriptType.Lua;

        public void Merge(IWorkflowHandler handler, string sourceMapDirectory, string terrainMapDirectory,
            string outputMapDirectory, WtsFile outputStrings, WtsFile terrainStrings)
        {
            if (handler.KillProcess) return;
            try
            {
                string sourcePath = Path.Combine(sourceMapDirectory, "war3map.lua");
                string terrainPath = Path.Combine(terrainMapDirectory, "war3map.lua");
                var encoding = new UTF8Encoding(false, true);
                var source = LuaDocument.Parse(File.ReadAllText(sourcePath, encoding), sourcePath);
                var terrain = LuaDocument.Parse(File.ReadAllText(terrainPath, encoding), terrainPath);
                source.Entry("config"); terrain.Entry("config");
                source.Entry("main"); terrain.Entry("main");
                var remapper = new TerrainStrings(outputStrings, terrainStrings);
                var edits = new List<LuaDocument.EditSpan>();
                var additions = new StringBuilder();

                MergeGlobals(source, terrain, remapper, edits);
                foreach (string name in source.Functions.Keys.Union(terrain.Functions.Keys).Where(LuaDocument.IsWorldFunction))
                {
                    FunctionDeclarationStatementSyntax old, replacement;
                    source.Functions.TryGetValue(name, out old);
                    terrain.Functions.TryGetValue(name, out replacement);
                    if (name == "main")
                    {
                        edits.Add(new LuaDocument.EditSpan(old, MergeMain(source, terrain, remapper)));
                        continue;
                    }
                    if (KeepSource(name)) continue;
                    if (replacement != null) ValidateTerrainDependencies(source, terrain, replacement);
                    string text = replacement == null ? "" : remapper.Import(terrain, replacement);
                    if (old != null) edits.Add(new LuaDocument.EditSpan(old, text));
                    else if (replacement != null) additions.AppendLine(text);
                }
                if (additions.Length != 0)
                    edits.Add(new LuaDocument.EditSpan(source.Entry("config").Span.Start, 0, additions + "\n"));

                string result = LuaDocument.Edit(source.Text, edits);
                var merged = LuaDocument.Parse(result); // Fail closed before replacing the staged output.
                foreach (var call in merged.Root.DescendantNodes().OfType<FunctionCallExpressionSyntax>())
                {
                    string name = LuaDocument.CallName(call);
                    if (name != null && LuaDocument.IsWorldFunction(name) && !merged.Functions.ContainsKey(name))
                        throw new FormatException("Merged Lua script calls missing generated function '" + name + "'. Check selected cameras, regions, and random tables.");
                }
                foreach (var identifier in merged.Root.DescendantNodes().OfType<IdentifierNameSyntax>())
                    if (identifier.Parent is ExpressionListFunctionArgumentSyntax && LuaDocument.IsWorldFunction(identifier.Name) && !merged.Functions.ContainsKey(identifier.Name))
                        throw new FormatException("Merged Lua script references missing generated callback '" + identifier.Name + "'.");
                File.WriteAllText(Path.Combine(outputMapDirectory, "war3map.lua"), result, new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException || ex is DecoderFallbackException)
            {
                handler.ReportError("Cannot merge Lua scripts: " + ex.Message, true);
            }
        }

        private bool KeepSource(string name) =>
            (name == "CreateCameras" && args.TransferCameras) ||
            (name == "CreateRegions" && args.TransferRegions) ||
            (name == "InitRandomGroups" && args.TransferUnitTables) ||
            (System.Text.RegularExpressions.Regex.IsMatch(name, @"^ItemTable\d+_DropItems$") && args.TransferItemTables);

        private string GlobalOwner(string name)
        {
            if (name.StartsWith("gg_cam_", StringComparison.Ordinal)) return args.TransferCameras ? "source" : "terrain";
            if (name.StartsWith("gg_rct_", StringComparison.Ordinal)) return args.TransferRegions ? "source" : "terrain";
            if (name.StartsWith("gg_rg_", StringComparison.Ordinal)) return args.TransferUnitTables ? "source" : "terrain";
            if (name.StartsWith("gg_unit_", StringComparison.Ordinal) || name.StartsWith("gg_dest_", StringComparison.Ordinal) || name.StartsWith("gg_item_", StringComparison.Ordinal)) return "terrain";
            return null;
        }

        private IEnumerable<AssignmentStatementSyntax> TerrainGlobals(LuaDocument document)
        {
            foreach (var statement in document.Root.Statements.Statements)
            {
                var assignment = statement as AssignmentStatementSyntax;
                if (assignment == null) continue;
                var owners = assignment.Variables.Select(v => GlobalOwner((v as IdentifierNameSyntax)?.Name ?? "")).ToArray();
                if (!owners.Contains("terrain")) continue;
                if (owners.Any(o => o != "terrain"))
                    throw new FormatException("Cannot separate a Lua assignment mixing terrain globals with gameplay or source-owned globals.");
                yield return assignment;
            }
            if (document.Locals.Any(name => GlobalOwner(name) != null))
                throw new FormatException("Generated Lua terrain globals must not be local variables.");
        }

        private void MergeGlobals(LuaDocument source, LuaDocument terrain, TerrainStrings remapper, List<LuaDocument.EditSpan> edits)
        {
            foreach (var statement in TerrainGlobals(source)) edits.Add(new LuaDocument.EditSpan(statement, ""));
            string imports = string.Join("\n", TerrainGlobals(terrain).Select(s =>
            {
                ValidateTerrainDependencies(source, terrain, s);
                return remapper.Import(terrain, s);
            }));
            // Leave a shebang and leading comments at the beginning of the file.
            int insertion = source.Root.Statements.Statements.Count == 0 ? source.Text.Length : source.Root.Statements.Statements[0].Span.Start;
            if (imports.Length != 0) edits.Add(new LuaDocument.EditSpan(insertion, 0, imports + "\n"));
        }

        private void ValidateTerrainDependencies(LuaDocument source, LuaDocument terrain, SyntaxNode node)
        {
            foreach (var identifier in node.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (GlobalOwner(identifier.Name) == "source" && !source.Globals.Contains(identifier.Name))
                    throw new FormatException("Imported Lua terrain code references source-selected global '" + identifier.Name +
                        "' absent from the source. Use matching declarations or keep the destination option.");
                if (terrain.Locals.Contains(identifier.Name))
                    throw new FormatException("Imported Lua terrain code depends on destination-local '" + identifier.Name + "'; this layout is not supported.");
                if (terrain.Functions.ContainsKey(identifier.Name) && !LuaDocument.IsWorldFunction(identifier.Name) && !source.Functions.ContainsKey(identifier.Name))
                    throw new FormatException("Imported Lua terrain code depends on destination helper '" + identifier.Name + "' absent from the source.");
            }
            foreach (var call in node.DescendantNodes().OfType<FunctionCallExpressionSyntax>())
            {
                string name = LuaDocument.CallName(call);
                if (name != null && terrain.Functions.ContainsKey(name) && !LuaDocument.IsWorldFunction(name) && !source.Functions.ContainsKey(name))
                    throw new FormatException("Imported Lua terrain code depends on destination helper '" + name + "' absent from the source.");
                if (name == "ExecuteFunc" && call.Argument is ExpressionListFunctionArgumentSyntax arguments && arguments.Expressions.Count == 1)
                {
                    string callback = (arguments.Expressions[0] as LiteralExpressionSyntax)?.Token.Value as string;
                    if (callback != null && terrain.Functions.ContainsKey(callback) && !LuaDocument.IsWorldFunction(callback) && !source.Functions.ContainsKey(callback))
                        throw new FormatException("Imported Lua terrain code depends on destination ExecuteFunc helper '" + callback + "' absent from the source.");
                }
            }
        }

        private string MergeMain(LuaDocument source, LuaDocument terrain, TerrainStrings remapper)
        {
            var original = source.Entry("main");
            var target = terrain.Entry("main");
            int sourceMarker = LuaDocument.InitializationMarker(original);
            int terrainMarker = LuaDocument.InitializationMarker(target);
            var originalPrefix = original.Body.Statements.Take(sourceMarker).ToArray();

            var optional = new[] { "InitSounds", "CreateRegions", "CreateCameras", "InitRandomGroups" };
            var prefix = target.Body.Statements.Take(terrainMarker).ToArray();
            ValidateMainLocals(source, original, sourceMarker, originalPrefix, prefix);
            foreach (var statement in originalPrefix.Concat(prefix))
                if (!(statement is ExpressionStatementSyntax) && !(statement is AssignmentStatementSyntax) &&
                    !(statement is EmptyStatementSyntax) && !(statement is LocalVariableDeclarationStatementSyntax))
                    throw new FormatException("Cannot merge Lua main: initialization before InitBlizzard() must be straight-line editor-generated code.");
            foreach (var statement in prefix)
                foreach (var call in statement.DescendantNodes().OfType<FunctionCallExpressionSyntax>())
                    if (optional.Contains(LuaDocument.CallName(call)) && call != LuaDocument.DirectCall(statement))
                        throw new FormatException("Cannot merge conditional or nested Lua initialization calls before InitBlizzard().");

            bool sourceSounds = originalPrefix.Any(s => LuaDocument.CallName(LuaDocument.DirectCall(s)) == "InitSounds");
            var retained = new List<StatementSyntax>();
            var called = new HashSet<string>(StringComparer.Ordinal);
            foreach (var statement in prefix)
            {
                string name = LuaDocument.CallName(LuaDocument.DirectCall(statement));
                if (optional.Contains(name))
                {
                    bool exists = name == "InitSounds" ? sourceSounds :
                        (KeepSource(name) ? source.Functions.ContainsKey(name) : terrain.Functions.ContainsKey(name));
                    if (!exists) continue;
                    if (!called.Add(name)) throw new FormatException("Duplicate Lua initialization call: " + name + "().");
                }
                ValidateTerrainDependencies(source, terrain, statement);
                retained.Add(statement);
            }

            var operations = retained.Select(s => Tuple.Create(LuaDocument.CallName(LuaDocument.DirectCall(s)),
                remapper.Import(terrain, s, includeTrivia: true))).ToList();
            foreach (string name in optional)
            {
                bool needed = name == "InitSounds" ? sourceSounds : (KeepSource(name) ? source : terrain).Functions.ContainsKey(name);
                if (!needed || called.Contains(name)) continue;
                int index = operations.FindIndex(operation => InitializationRank(operation.Item1) > InitializationRank(name));
                if (index < 0) index = operations.Count;
                operations.Insert(index, Tuple.Create(name, "    " + name + "()"));
            }
            var body = new StringBuilder("function main()\n");
            foreach (var operation in operations) body.AppendLine(operation.Item2);
            body.AppendLine(original.Body.Statements[sourceMarker].ToFullString());
            foreach (var statement in original.Body.Statements.Skip(sourceMarker + 1)) body.AppendLine(statement.ToFullString());
            body.Append("end");
            return body.ToString();
        }

        private static void ValidateMainLocals(LuaDocument source, FunctionDeclarationStatementSyntax main, int marker,
            StatementSyntax[] sourcePrefix, StatementSyntax[] terrainPrefix)
        {
            if (sourcePrefix.OfType<LocalVariableDeclarationStatementSyntax>().SelectMany(declaration => declaration.Names).Any(name => name.ToString() == "_ENV"))
                throw new FormatException("Cannot merge Lua main with source-local declarations changing _ENV across InitBlizzard().");
            var terrainLocals = new HashSet<string>(terrainPrefix.OfType<LocalVariableDeclarationStatementSyntax>()
                .SelectMany(declaration => declaration.Names).Select(name => name.ToString()), StringComparer.Ordinal);
            if (terrainLocals.Contains("_ENV"))
                throw new FormatException("Cannot merge Lua main with destination-local declarations changing _ENV across InitBlizzard().");
            if (terrainLocals.Count == 0 && !sourcePrefix.Any(statement => statement is LocalVariableDeclarationStatementSyntax)) return;

            // Resolve actual bindings, so nested locals, parameters and member names
            // are not confused with locals crossing the initialization boundary.
            var bindings = new Script(ImmutableArray.Create<SyntaxTree>(source.Root.SyntaxTree));
            int retainedStart = main.Body.Statements[marker].Span.Start;
            foreach (var identifier in main.Body.Statements.Skip(marker).SelectMany(statement => statement.DescendantNodesAndSelf()).OfType<IdentifierNameSyntax>())
            {
                var variable = bindings.GetVariable(identifier);
                if (variable == null) continue;
                var declaration = variable.Declaration;
                bool declaredInMain = declaration != null && main.Body.Span.Contains(declaration.Span);
                if (declaredInMain && declaration.Span.Start < retainedStart)
                    throw new FormatException("Cannot merge Lua main with source-local declarations used after InitBlizzard(): '" + variable.Name + "'.");
                bool declaredInRetainedCode = declaredInMain && declaration.Span.Start >= retainedStart;
                if (terrainLocals.Contains(variable.Name) && !declaredInRetainedCode)
                    throw new FormatException("Cannot merge Lua main with destination-local declarations shadowing retained source references: '" + variable.Name + "'.");
            }
        }

        private static int InitializationRank(string name)
        {
            switch (name)
            {
                case "InitSounds": return 0;
                case "CreateRegions": return 1;
                case "CreateCameras": return 2;
                case "InitRandomGroups": return 3;
                default: return name != null && name.StartsWith("Create", StringComparison.Ordinal) && LuaDocument.IsWorldFunction(name) ? 4 : -1;
            }
        }

        private sealed class TerrainStrings
        {
            private readonly WtsFile output, terrain;
            private readonly Dictionary<string, string> imports = new Dictionary<string, string>(StringComparer.Ordinal);
            internal TerrainStrings(WtsFile output, WtsFile terrain) { this.output = output; this.terrain = terrain; }

            internal string Import(LuaDocument document, SyntaxNode node, bool includeTrivia = false)
            {
                var span = includeTrivia ? node.FullSpan : node.Span;
                string text = document.Text.Substring(span.Start, span.Length);
                var changes = new List<LuaDocument.EditSpan>();
                foreach (var literal in node.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>())
                {
                    string value = literal.Token.Value as string;
                    if (value == null || !System.Text.RegularExpressions.Regex.IsMatch(value, @"^TRIGSTR_[0-9]+$")) continue;
                    string replacement;
                    if (!imports.TryGetValue(value, out replacement))
                    {
                        int key = StringUtility.ExtractRefKey(value);
                        if (!terrain.FileMap.ContainsKey(key))
                            throw new FormatException("Missing string reference '" + value + "' in terrain strings.");
                        replacement = WtsUtility.AddStrRef(output, terrain, VersionUtility.ChangeVersion(terrain, value));
                        imports.Add(value, replacement);
                    }
                    changes.Add(new LuaDocument.EditSpan(literal.Span.Start - span.Start, literal.Span.Length, "\"" + replacement + "\""));
                }
                return LuaDocument.Edit(text, changes);
            }
        }
    }
}
