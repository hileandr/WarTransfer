using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using WarTransfer.FileTypes;
using WarTransfer.Types;

namespace WarTransfer
{
    public static class JUtility
    {
        public const string GLOBALS_ID = "globals";
        public const string FUNCTION_ID = "function";
        public const string FUNC_USED_PATTERN = "(?:^\\ *call\\ *{0})|(?:[\\(,]\\ *function\\ *{0}\\ *\\))";

        public static JFile ReadFile(IWorkflowHandler e, string filePath)
        {
            JFile file = null;
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open))
                {
                    using (StreamReader reader = new StreamReader(stream, true))
                    {
                        file = JFile.Read(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                e.ReportError($"An error occured while reading file '{filePath}': {ex.Message}", true);
            }

            return file;
        }

        public static void WriteFile(string outputPath, JFile j)
        {
            using (FileStream stream = new FileStream(outputPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    j.Serialize(writer);
                }
            }
        }

        public static JFileNode ParseNode(JFile document, string line)
        {
            NodeType type = ParseNodeType(line);
            switch (type)
            {
                case NodeType.Comment: return new JFileCommentNode(document, line);
                case NodeType.Globals: return new JFileGlobalsNode(document, line);
                case NodeType.Function: return new JFileFunctionNode(document, GetFunctionName(line), line);
                default: return new JFileTextNode(document, line);
            }
        }

        public static NodeType ParseNodeType(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//"))
            {
                return NodeType.Comment;
            }
            else if (trimmed.StartsWith(GLOBALS_ID))
            {
                return NodeType.Globals;
            }
            else if (trimmed.StartsWith(FUNCTION_ID))
            {
                return NodeType.Function;
            }
            else
            {
                return NodeType.Text;
            }
        }

        public static string GetFunctionName(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(FUNCTION_ID))
            {
                string result = "";
                int i = FUNCTION_ID.Length + 1;
                for (; i < trimmed.Length; i++)
                {
                    if (char.IsWhiteSpace(trimmed[i]))
                    {
                        break;
                    }

                    result += trimmed[i];
                }

                return result;
            }

            return null;
        }

        //public static T FindNode<T>(JFileNode root) where T : JFileNode
        //{
        //    if (root is T node)
        //    {
        //        return node;
        //    }

        //    if (root.Children != null)
        //    {
        //        for (int c = 0; c < root.Children.Count; c++)
        //        {
        //            T child = FindNode<T>(root.Children[c]);
        //            if (child != null)
        //            {
        //                return child;
        //            }
        //        }
        //    }

        //    return null;
        //}

        //public static T FindNode<T>(JFileNode root, Predicate<T> match) where T : JFileNode
        //{
        //    if (root is T node && match(node))
        //    {
        //        return node;
        //    }

        //    if (root.Children != null)
        //    {
        //        for (int c = 0; c < root.Children.Count; c++)
        //        {
        //            T child = FindNode<T>(root.Children[c], match);
        //            if (child != null)
        //            {
        //                return child;
        //            }
        //        }
        //    }

        //    return null;
        //}

        //public static JFileNode FindNode(JFileNode root, Predicate<JFileNode> match)
        //{
        //    return FindNode<JFileNode>(root, match);
        //}

        //public static List<T> FindNodes<T>(JFileNode root, Predicate<T> match) where T : JFileNode
        //{
        //    List<T> result = new List<T>();
        //    FindNodes_Impl(root, match, result);
        //    return result;
        //}

        //private static void FindNodes_Impl<T>(JFileNode root, Predicate<T> match, List<T> result) where T : JFileNode
        //{
        //    if (root is T node && match(node))
        //    {
        //        result.Add(node);
        //    }

        //    if (root.Children != null)
        //    {
        //        for (int c = 0; c < root.Children.Count; c++)
        //        {
        //            FindNodes_Impl<T>(root.Children[c], match, result);
        //        }
        //    }
        //}

        ///// <summary>
        ///// Adds a function immediately after the globals node.
        ///// </summary>
        ///// <param name="root"></param>
        ///// <param name="function"></param>
        ///// <returns></returns>
        //public static bool AddFunction(IWorkflowHandler e, JFileNode root, JFileFunctionNode function)
        //{
        //    if (root.NodeType != NodeType.Root)
        //    {
        //        e.ReportError("Can only add functions to a root node.", false);
        //        return false;
        //    }

        //    JFileGlobalsNode globals = root.Find<JFileGlobalsNode>();
        //    if (globals != null)
        //    {
        //        JFileNode insertionPoint = globals.GetNextSibling();
        //        while (insertionPoint != null && insertionPoint is JFileTextNode textNode && textNode.ToString(FormatOptions.Trim).StartsWith("native "))
        //        {
        //            insertionPoint = insertionPoint.GetNextSibling();
        //        }

        //        if (insertionPoint != null)
        //        {
        //            insertionPoint.AddPreviousSibling(function);
        //            return true;
        //        }
        //    }
        //    else if (root.Children != null && root.Children.Count > 0)
        //    {
        //        root.Children[0].AddPreviousSibling(function);
        //        return true;
        //    }
        //    else
        //    {
        //        if (root.Children != null)
        //        {
        //            root.Children = new List<JFileNode>();
        //        }

        //        root.AddChild(function);
        //        return true;
        //    }

        //    return false;
        //}

        public static bool AddFunctionBeforeUse(JFileNode root, JFileFunctionNode function)
        {
            Regex funcUsedRegex = new Regex(string.Format(FUNC_USED_PATTERN, function.FunctionName), RegexOptions.Compiled);

            JFileTextNode firstCall = root.Find<JFileTextNode>(x => funcUsedRegex.IsMatch(x.TextValue));

            if (firstCall != null && firstCall.Parent != null && firstCall.Parent.NodeType == NodeType.Function)
            {
                // Insert before this function and any comments above it
                JFileNode insertionPoint = firstCall.Parent.GetPreviousSibling();
                while (insertionPoint != null && insertionPoint.NodeType == NodeType.Comment)
                {
                    insertionPoint = insertionPoint.GetPreviousSibling();
                }

                if (insertionPoint != null)
                {
                    insertionPoint.AddNextSibling(function);
                    return true;
                }
            }

            return false;
        }

        //public static bool AddFunctionBeforeUse(JFileNode root, JFileFunctionNode function)
        //{
        //    string call = $"call {function.FunctionName}";
        //    JFileTextNode firstCall = root.Find<JFileTextNode>(x => x.ToString(FormatOptions.Trim | FormatOptions.CollapseWhiteSpace).StartsWith(call));

        //    if (firstCall != null && firstCall.Parent != null && firstCall.Parent.NodeType == NodeType.Function)
        //    {
        //        // Insert before this function and any comments above it
        //        JFileNode insertionPoint = firstCall.Parent.GetPreviousSibling();
        //        while (insertionPoint != null && insertionPoint.NodeType == NodeType.Comment)
        //        {
        //            insertionPoint = insertionPoint.GetPreviousSibling();
        //        }

        //        if (insertionPoint != null)
        //        {
        //            insertionPoint.AddNextSibling(function);
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        public static JFileCommentNode[] CreateCommentBlock(JFile document, string comment)
        {
            JFileCommentNode[] newNodes = new JFileCommentNode[5];

            newNodes[0] = new JFileCommentNode(document, "//***************************************************************************");
            newNodes[1] = new JFileCommentNode(document, "//*");
            newNodes[2] = new JFileCommentNode(document, $"//*  {comment}");
            newNodes[3] = new JFileCommentNode(document, "//*");
            newNodes[4] = new JFileCommentNode(document, "//***************************************************************************");

            return newNodes;
        }

        //public static JFileFunctionNode FindFunction(JFileNode root, string functionName)
        //{
        //    if (root is JFileFunctionNode fNode && fNode.FunctionName.Equals(functionName))
        //    {
        //        return fNode;
        //    }
            
        //    if (root.Children != null)
        //    {
        //        for (int c = 0; c < root.Children.Count; c++)
        //        {
        //            JFileFunctionNode child = FindFunction(root.Children[c], functionName);
        //            if (child != null)
        //            {
        //                return child;
        //            }
        //        }
        //    }

        //    return null;
        //}

        public static IEnumerable<FunctionName> FindFunctionNamesMatching(JFile file, Dictionary<string, Regex> namePatterns)
        {
            foreach (string functionName in file.Functions.Keys)
            {
                foreach (var pattern in namePatterns)
                {
                    if (pattern.Value.IsMatch(functionName))
                    {
                        yield return new FunctionName(functionName, pattern.Key);
                    }
                }
            }
        }

        //public static JFileTextNode FindTextNodeThatMatches(JFileNode root, Regex regex)
        //{
        //    if (root is JFileTextNode node && regex.IsMatch(node.TextValue))
        //    {
        //        return node;
        //    }

        //    if (root.Children != null)
        //    {
        //        for (int c = 0; c < root.Children.Count; c++)
        //        {
        //            JFileTextNode child = FindTextNodeThatMatches(root.Children[c], regex);
        //            if (child != null)
        //            {
        //                return child;
        //            }
        //        }
        //    }

        //    return null;
        //}

        //public static JFileTextNode FindTextNodeThatContains(JFileNode root, string text)
        //{
        //    if (root is JFileTextNode node && node.TextValue.Contains(text))
        //    {
        //        return node;
        //    }

        //    if (root.Children != null)
        //    {
        //        for (int c = 0; c < root.Children.Count; c++)
        //        {
        //            JFileTextNode child = FindTextNodeThatContains(root.Children[c], text);
        //            if (child != null)
        //            {
        //                return child;
        //            }
        //        }
        //    }

        //    return null;
        //}

        //public static JFileCommentNode FindComment(JFileNode root, string comment)
        //{
        //    if (root is JFileCommentNode node)
        //    {
        //        string pureText = node.TextValue.Trim('/', '*', ' ');
        //        if (pureText.Equals(comment))
        //        {
        //            return node;
        //        }
        //    }

        //    if (root.Children != null)
        //    {
        //        for (int c = 0; c < root.Children.Count; c++)
        //        {
        //            JFileCommentNode child = FindComment(root.Children[c], comment);
        //            if (child != null)
        //            {
        //                return child;
        //            }
        //        }
        //    }

        //    return null;
        //}

        //public static List<JFileCommentNode> FindComments(JFileNode root, string comment)
        //{
        //    List<JFileCommentNode> results = new List<JFileCommentNode>();
        //    FindComments_Impl(root, comment, results);

        //    return results;
        //}

        //private static void FindComments_Impl(JFileNode root, string comment, List<JFileCommentNode> results)
        //{
        //    if (root is JFileCommentNode node)
        //    {
        //        string pureText = node.TextValue.Trim('/', '*', ' ');
        //        if (pureText.Equals(comment))
        //        {
        //            results.Add(node);
        //        }
        //    }

        //    if (root.Children != null)
        //    {
        //        for (int c = 0; c < root.Children.Count; c++)
        //        {
        //            FindComments_Impl(root.Children[c], comment, results);
        //        }
        //    }
        //}

        //public static bool ReplaceFunction(JFile source, JFile target, string functionName)
        //{
        //    JFileNode sourceNode = FindFunction(source.Root, functionName);
        //    JFileNode targetNode = FindFunction(target.Root, functionName);

        //    if (sourceNode != null && targetNode != null)
        //    {
        //        return ReplaceNode(sourceNode, targetNode);
        //    }

        //    return false;
        //}

        //public static bool RemoveNode(IWorkflowHandler e, JFileNode node)
        //{
        //    if (node.NodeType == NodeType.Root)
        //    {
        //        e.ReportError("Can't remove root nodes!", false);
        //    }
        //    else if (node.Parent != null)
        //    {
        //        return node.Parent.Children.Remove(node);
        //    }

        //    return false;
        //}

        //public static void RemoveNodes(IWorkflowHandler e, IEnumerable<JFileNode> nodes)
        //{
        //    foreach (JFileNode node in nodes)
        //    {
        //        RemoveNode(e, node);
        //    }
        //}

        //public static bool ReplaceNode(JFileNode oldNode, JFileNode newNode)
        //{
        //    if (oldNode.Parent != null)
        //    {
        //        newNode.Parent = oldNode.Parent;
        //        int childIndex = newNode.Parent.Children.IndexOf(oldNode);
        //        newNode.Parent.Children[childIndex] = newNode;
        //        return true;
        //    }

        //    return false;
        //}

        public static void RemoveNextSiblingsAll(JFileNode node)
        {
            if (node.Parent != null && node.Parent.Children != null)
            {
                int index = node.Parent.Children.IndexOf(node);
                if (index != -1)
                {
                    int lastIndex = node.Parent.Children.Count;
                    while (--lastIndex != index)
                    {
                        node.Parent.RemoveChildAt(lastIndex);
                    }
                }
            }
        }

        public static void FindInlineStrings(JFileNode node, List<JFileInlineString> outResult)
        {
            if (node is JFileTextNode textNode)
            {
                string nodeValue = textNode.TextValue;
                string context = null;
                int startIndex = 0;
                InlineString iString;
                do
                {
                    iString = StringUtility.FindInlineString(nodeValue, startIndex);
                    if (iString != null)
                    {
                        int length = iString.Value.Length;
                        if (context == null)
                        {
                            context = BuildInlineStringContext(nodeValue);
                        }

                        outResult.Add(new JFileInlineString(textNode, context, iString));

                        startIndex = iString.StartIndex + length + 1;
                    }
                }
                while (iString != null);
            }

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    FindInlineStrings(node.Children[i], outResult);
                }
            }
        }

        public static void FindInlineStrings(IEnumerable<JFileNode> nodes, List<JFileInlineString> outResult)
        {
            foreach (JFileNode node in nodes)
            {
                FindInlineStrings(node, outResult);
            }
        }

        private static string BuildInlineStringContext(string value)
        {
            string result = "";
            bool inQuotes = false;
            int quoteStartIndex = -1;
            value = value.Trim();

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (!inQuotes && !char.IsWhiteSpace(c))
                {
                    result += c;
                }

                if (c == '"')
                {
                    if (inQuotes)
                    {
                        quoteStartIndex = -1;
                        result += c;
                    }
                    else
                    {
                        quoteStartIndex = i;
                    }

                    inQuotes = !inQuotes;
                }
            }

            if (quoteStartIndex != -1)
            {
                result += value.Substring(quoteStartIndex);
            }

            return result;
        }
    }

    public struct FunctionName
    {
        public string Name { get; }
        public string PatternName { get; }

        public FunctionName(string name, string matchedPatternName)
        {
            this.Name = name;
            this.PatternName = matchedPatternName;
        }

        public override bool Equals(object obj)
        {
            if (obj is FunctionName fname)
            {
                return fname.Name.Equals(this.Name) && fname.PatternName.Equals(this.PatternName);
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return (Name + PatternName).GetHashCode();
        }

        public override string ToString()
        {
            return $"Name: {Name ?? "NULL"}, Pattern: {PatternName ?? "NULL"}";
        }
    }
}
