using System;
using System.Collections.Generic;
using System.IO;

namespace WarTransfer.FileTypes
{
    public enum NodeType
    {
        Root = 0,
        Text = 1,
        Comment = 2,
        Globals = 3,
        Function = 4
    }

    [Flags]
    public enum FormatOptions
    {
        None = 0,
        Trim = 1 << 0,
        CollapseWhiteSpace = 1 << 2
    }

    public class JFile
    {
        public JFileNode Root { get; set; }

        public Dictionary<string, JFileFunctionNode> Functions { get; set; }

        public JFile()
        {
            Root = new JFileRootNode(this);
            Functions = new Dictionary<string, JFileFunctionNode>();
        }

        public static JFile Read(TextReader reader)
        {
            JFile file = new JFile();
            file.Deserialize(reader);
            return file;
        }

        public void Serialize(TextWriter writer)
        {
            Root.Serialize(writer);
        }

        public void Deserialize(TextReader reader)
        {
            string line = reader.ReadLine();

            while (line != null)
            {
                JFileNode node = JUtility.ParseNode(this, line);
                Root.AddChild(node);
                node.Deserialize(this, reader);

                if (node.NodeType == NodeType.Function)
                {
                    JFileFunctionNode function = (JFileFunctionNode)node;
                    this.Functions.Add(function.FunctionName, function);
                }

                line = reader.ReadLine();
            }
        }

        public JFileFunctionNode GetFunction(string functionName)
        {
            if (Functions.TryGetValue(functionName, out JFileFunctionNode function))
            {
                return function;
            }

            return null;
        }
    }

    public abstract class JFileNode
    {
        public JFileNode Parent { get; set; }
        public List<JFileNode> Children { get; set; }
        public abstract NodeType NodeType { get; }

        public JFile Document
        {
            get
            {
                return m_document;
            }
        }

        protected JFile m_document;

        public JFileNode(JFile document)
        {
            this.m_document = document;
        }

        public JFileNode AddChild(JFileNode child)
        {
            if (this.Children == null)
            {
                this.Children = new List<JFileNode>();
            }

            child.Parent = this;
            this.Children.Add(child);

            return child;
        }

        public JFileNode AddNextSibling(JFileNode sibling)
        {
            if (Parent != null && Parent.Children != null)
            {
                int insertIndex = Parent.Children.IndexOf(this) + 1;
                sibling.Parent = Parent;
                Parent.Children.Insert(insertIndex, sibling);

                return sibling;
            }

            return null;
        }

        public void AddNextSiblings(IEnumerable<JFileNode> siblings)
        {
            if (Parent != null && Parent.Children != null)
            {
                int insertIndex = Parent.Children.IndexOf(this) + 1;
                foreach (JFileNode sibling in siblings)
                {
                    sibling.Parent = Parent;
                    Parent.Children.Insert(insertIndex, sibling);
                    insertIndex++;
                }
            }
        }

        public JFileNode AddPreviousSibling(JFileNode sibling)
        {
            if (Parent != null && Parent.Children != null)
            {
                int insertIndex = Parent.Children.IndexOf(this);
                sibling.Parent = Parent;
                Parent.Children.Insert(insertIndex, sibling);

                return sibling;
            }

            return null;
        }

        public void AddPreviousSiblings(IEnumerable<JFileNode> siblings)
        {
            if (Parent != null && Parent.Children != null)
            {
                int insertIndex = Parent.Children.IndexOf(this);
                foreach (JFileNode sibling in siblings)
                {
                    sibling.Parent = Parent;
                    Parent.Children.Insert(insertIndex, sibling);
                    insertIndex++;
                }
            }
        }

        public bool RemoveChild(JFileNode child)
        {
            if (this.Children.Remove(child))
            {
                PostRemove(child);

                return true;
            }

            return false;
        }

        public void RemoveChildAt(int index)
        {
            if (index < 0 || index >= this.Children.Count)
                throw new IndexOutOfRangeException();

            JFileNode child = this.Children[index];
            this.Children.RemoveAt(index);

            PostRemove(child);
        }

        private void PostRemove(JFileNode removed)
        {
            if (removed.NodeType == NodeType.Function)
            {
                JFileFunctionNode funcNode = removed as JFileFunctionNode;
                removed.m_document.Functions.Remove(funcNode.FunctionName);
            }

            removed.m_document = null;
            removed.Parent = null;
        }

        public bool Remove()
        {
            if (this.Parent != null)
            {
                return this.Parent.RemoveChild(this);
            }
            else if (this.NodeType == NodeType.Root)
            {
                throw new InvalidOperationException("Cannot remove a root node!");
            }

            return false;
        }

        public bool ReplaceChild(JFileNode child, JFileNode replacement)
        {
            if (child.Parent == null)
            {
                throw new InvalidOperationException("Cannot replace a child with no parent!");
            }
            else if (child.Parent != this)
            {
                throw new InvalidOperationException("Cannot replace a child from another parent!");
            }
            else
            {
                int childIndex = this.Children.IndexOf(child);
                if (childIndex != -1)
                {
                    if (replacement.Parent != null)
                    {
                        replacement.Remove();
                    }

                    replacement.m_document = this.m_document;
                    replacement.Parent = this;

                    this.Children[childIndex] = replacement;

                    PostRemove(child);

                    if (replacement is JFileFunctionNode func)
                    {
                        this.Document.Functions.Add(func.FunctionName, func);
                    }

                    return true;
                }
            }

            return false;
        }

        public JFileNode GetNextSibling()
        {
            if (Parent != null)
            {
                int index = Parent.Children.IndexOf(this);
                if (index < Parent.Children.Count - 1)
                {
                    return Parent.Children[index + 1];
                }
            }

            return null;
        }

        public JFileNode GetPreviousSibling()
        {
            if (Parent != null)
            {
                int index = Parent.Children.IndexOf(this);
                if (index > 0)
                {
                    return Parent.Children[index - 1];
                }
            }

            return null;
        }

        public virtual void Serialize(TextWriter writer)
        {
            if (Children != null)
            {
                for (int c = 0; c < Children.Count; c++)
                {
                    Children[c].Serialize(writer);
                }
            }
        }

        public virtual void Deserialize(JFile document, TextReader reader)
        {
        }

        protected abstract JFileNode Copy(JFile targetDocument);

        public JFileNode Clone(JFile targetDocument)
        {
            JFileNode clone = Copy(targetDocument);
            if (clone.Children != null)
            {
                for (int c = 0; c < clone.Children.Count; c++)
                {
                    clone.Children[c] = clone.Children[c].Clone(targetDocument);
                    clone.Children[c].Parent = clone;
                }
            }

            return clone;
        }

        public T Find<T>() where T : JFileNode
        {
            if (this is T node)
            {
                return node;
            }

            if (this.Children != null)
            {
                for (int c = 0; c < this.Children.Count; c++)
                {
                    T child = this.Children[c].Find<T>();
                    if (child != null)
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        public T Find<T>(Predicate<T> match) where T : JFileNode
        {
            if (this is T node && match(node))
            {
                return node;
            }

            if (this.Children != null)
            {
                for (int c = 0; c < this.Children.Count; c++)
                {
                    T child = this.Children[c].Find(match);
                    if (child != null)
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        public List<T> FindAll<T>(Predicate<T> match) where T : JFileNode
        {
            List<T> result = new List<T>();
            FindAll_Recursive(match, result);
            return result;
        }

        private void FindAll_Recursive<T>(Predicate <T> match, List<T> result) where T : JFileNode
        {
            if (this is T node && match(node))
            {
                result.Add(node);
            }

            if (this.Children != null)
            {
                for (int c = 0; c < this.Children.Count; c++)
                {
                    this.Children[c].FindAll_Recursive(match, result);
                }
            }
        }
    }

    public class JFileRootNode : JFileNode
    {
        public override NodeType NodeType
        {
            get
            {
                return NodeType.Root;
            }
        }

        public JFileRootNode(JFile document) : base(document)
        {
        }

        protected override JFileNode Copy(JFile targetDocument)
        {
            return new JFileRootNode(targetDocument);
        }
    }

    public class JFileTextNode : JFileNode
    {
        public string TextValue { get; set; }

        public override NodeType NodeType
        {
            get
            {
                return NodeType.Text;
            }
        }

        public JFileTextNode(JFile document, string textValue) : base(document)
        {
            this.TextValue = textValue;
        }

        protected override JFileNode Copy(JFile targetDocument)
        {
            return new JFileTextNode(targetDocument, this.TextValue);
        }

        public override void Serialize(TextWriter writer)
        {
            writer.WriteLine(TextValue);

            base.Serialize(writer);
        }

        public override string ToString()
        {
            return TextValue;
        }

        public string ToString(FormatOptions options)
        {
            string text = this.ToString();

            if ((options & FormatOptions.Trim) != 0)
            {
                text = text.Trim();
            }

            if ((options & FormatOptions.CollapseWhiteSpace) != 0)
            {
                string newText = "";
                int whiteSpaceCounter = 0;

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == ' ')
                    {
                        whiteSpaceCounter++;
                        if (whiteSpaceCounter <= 1)
                        {
                            newText += c;
                        }
                    }
                    else
                    {
                        newText += c;
                        whiteSpaceCounter = 0;
                    }
                }

                text = newText;
            }

            return text;
        }
    }

    public class JFileCommentNode : JFileTextNode
    {
        public override NodeType NodeType
        {
            get
            {
                return NodeType.Comment;
            }
        }

        public JFileCommentNode(JFile document, string textValue) : base(document, textValue)
        {
        }

        protected override JFileNode Copy(JFile targetDocument)
        {
            return new JFileCommentNode(targetDocument, this.TextValue);
        }
    }

    public class JFileGlobalsNode : JFileTextNode
    {
        public override NodeType NodeType
        {
            get
            {
                return NodeType.Globals;
            }
        }

        public JFileGlobalsNode(JFile document, string textValue) : base(document, textValue) { }

        protected override JFileNode Copy(JFile targetDocument)
        {
            return new JFileGlobalsNode(targetDocument, this.TextValue);
        }

        public override void Deserialize(JFile document, TextReader reader)
        {
            string line = reader.ReadLine();
            while (line != null)
            {
                JFileNode child = JUtility.ParseNode(document, line);
                child.Deserialize(document, reader);
                AddChild(child);

                if (line.TrimStart().StartsWith($"end{JUtility.GLOBALS_ID}"))
                {
                    break;
                }

                line = reader.ReadLine();
            }
        }

        public override string ToString()
        {
            int childCount = (Children != null) ? Children.Count : 0;
            return $"{JUtility.GLOBALS_ID} ({childCount} Children)";
        }
    }

    public class JFileFunctionNode : JFileTextNode
    {
        public string FunctionName { get; set; }

        public override NodeType NodeType
        {
            get
            {
                return NodeType.Function;
            }
        }

        public JFileFunctionNode(JFile document, string functionName, string textValue) : base(document, textValue)
        {
            this.FunctionName = functionName;
        }

        public static JFileFunctionNode CreateEmpty(JFile document, string functionName)
        {
            JFileFunctionNode func = new JFileFunctionNode(document, functionName, $"function {functionName} takes nothing returns nothing");
            func.AddChild(new JFileTextNode(document, "endfunction"));
            return func;
        }

        protected override JFileNode Copy(JFile targetDocument)
        {
            return new JFileFunctionNode(targetDocument, this.FunctionName, this.TextValue);
        }

        public override void Deserialize(JFile document, TextReader reader)
        {
            string line = reader.ReadLine();
            while (line != null)
            {
                JFileNode child = JUtility.ParseNode(document, line);
                child.Deserialize(document, reader);
                AddChild(child);

                if (line.TrimStart().StartsWith($"end{JUtility.FUNCTION_ID}"))
                {
                    break;
                }

                line = reader.ReadLine();
            }
        }

        public override string ToString()
        {
            int childCount = (Children != null) ? Children.Count : 0;
            return $"{JUtility.FUNCTION_ID} {FunctionName} ({childCount} Children)";
        }
    }
}
