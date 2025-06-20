using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WarTransfer.FileTypes;

namespace WarTransfer.Types
{
    public class InlineString
    {
        public int StartIndex { get; }
        public string Value { get; }

        public InlineString(int startIndex, string value)
        {
            this.StartIndex = startIndex;
            this.Value = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public class JFileInlineString : InlineString
    {
        public JFileTextNode Node { get; }
        public string Context { get; }

        public JFileInlineString(JFileTextNode node, int startIndex, string value, string context) : base(startIndex, value)
        {
            this.Node = node;
            this.Context = context;
        }

        public JFileInlineString(JFileTextNode node, string context, InlineString inlineString) : this(node, inlineString.StartIndex, inlineString.Value, context)
        {

        }
    }
}
