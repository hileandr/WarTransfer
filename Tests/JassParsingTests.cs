using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using WarTransfer;
using WarTransfer.FileTypes;
using WarTransfer.Types;

namespace Tests
{
    [TestClass]
    public class JassParsingTests
    {
        private static JFile Parse(string text) => JFile.Read(new StringReader(text));

        [DataTestMethod]
        [DataRow("function    main takes nothing returns nothing")]
        [DataRow("\tfunction\tmain\ttakes nothing returns nothing // comment")]
        public void FunctionDeclarationsAcceptWhitespaceAndComments(string header)
        {
            Assert.IsNotNull(Parse(header + "\nendfunction // done\n").GetFunction("main"));
        }

        [DataTestMethod]
        [DataRow("globals\n", "endglobals")]
        [DataRow("function main takes nothing returns nothing\n", "endfunction")]
        [DataRow("globals\nfunction main takes nothing returns nothing\nendfunction", "endglobals")]
        [DataRow("endfunction", "Unexpected")]
        [DataRow("globals\nendglobals\nglobals\nendglobals", "Duplicate")]
        [DataRow("function f takes nothing returns nothing\nendfunction\nfunction f takes nothing returns nothing\nendfunction", "Duplicate")]
        [DataRow("function takes nothing returns nothing\nendfunction", "declaration")]
        [DataRow("globals\nstring s = \"unterminated\nendglobals", "Unterminated")]
        public void MalformedStructureReportsUsefulError(string script, string expected)
        {
            var ex = Assert.ThrowsException<FormatException>(() => Parse(script));
            StringAssert.Contains(ex.Message, expected);
        }

        [TestMethod]
        public void LiteralScannerHandlesEscapesEmptyStringsRawCodesAndComments()
        {
            var doc = new JFile();
            var node = new JFileTextNode(doc, "call F(\"say \\\"hi\\\"\", \"\", 'hfoo', \"C:\\\\dir\") // \"ignored\"");
            var literals = new List<JFileInlineString>();
            JUtility.FindInlineStrings(node, literals);
            CollectionAssert.AreEqual(new[] { "say \\\"hi\\\"", "", "C:\\\\dir" }, literals.ConvertAll(x => x.Value));
            var comment = new JFileCommentNode(doc, "// call ExecuteFunc(\"ignored\")");
            JUtility.FindInlineStrings(comment, literals);
            Assert.AreEqual(3, literals.Count);
        }

        [TestMethod]
        public void CloningAFunctionCopiesAllChildrenIndependently()
        {
            var original = Parse("function f takes nothing returns nothing\ncall F()\nendfunction").GetFunction("f");
            var clone = (JFileFunctionNode)original.Clone(new JFile());
            Assert.AreEqual(original.Children.Count, clone.Children.Count);
            ((JFileTextNode)clone.Children[0]).TextValue = "call G()";
            Assert.AreEqual("call F()", ((JFileTextNode)original.Children[0]).TextValue);
            Assert.AreSame(clone, clone.Children[0].Parent);
        }

        [TestMethod]
        public void ParseSerializePreservesCommentsLiteralsAndFormatting()
        {
            string script = "// comment\r\nglobals\r\n\tstring s = \"a \\\"quote\\\" // literal\"\r\nendglobals\r\n" +
                "function   main takes nothing returns nothing\r\n    // endfunction\r\nendfunction\r\n";
            var writer = new StringWriter();
            Parse(script).Serialize(writer);
            Assert.AreEqual(script, writer.ToString());
        }

        [TestMethod]
        public void FunctionReferencesIgnoreCommentsLiteralsAndLongerNames()
        {
            var doc = Parse("globals\nendglobals\nfunction main takes nothing returns nothing\n" +
                "// call CreateUnitsForPlayer2()\ncall F(\"call CreateUnitsForPlayer2()\")\ncall CreateUnitsForPlayer20()\nendfunction");
            Assert.IsFalse(JUtility.AddFunctionBeforeUse(doc.Root, JFileFunctionNode.CreateEmpty(doc, "CreateUnitsForPlayer2")));
        }
    }
}
