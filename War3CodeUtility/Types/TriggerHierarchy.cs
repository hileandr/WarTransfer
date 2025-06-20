using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WarFiles;

namespace War3CodeUtility.Types
{
    public class TriggerHierarchy
    {
        public TriggerNode Root { get; set; }

        public static TriggerHierarchy FromFile(TriggerFile triggerFile, ScriptFile scriptFile)
        {
            uint globalId = 4294967294;

            TriggerHierarchy hierarchy = new TriggerHierarchy();

            TriggerCategory mapNode = triggerFile.Categories.FirstOrDefault();
            if (mapNode != null)
            {
                hierarchy.Root = new TriggerNode(mapNode.Id, mapNode.Name, ElementType.Map);

                TriggerNode globalJass = new TriggerScript(globalId, "Global", scriptFile.Comment, scriptFile.Header);
                hierarchy.Root.Children.Add(globalJass);
            }

            if (hierarchy.Root != null)
            {
                List<TriggerCategory> categoriesRemaining = new List<TriggerCategory>(triggerFile.Categories);
                categoriesRemaining.RemoveAt(0);

                uint maxIterations = 1000;

                while (categoriesRemaining.Count > 0)
                {
                    maxIterations--;
                    if (maxIterations == 0)
                    {

                    }

                    for (int i = categoriesRemaining.Count - 1; i >= 0; i--)
                    {
                        TriggerCategory category = categoriesRemaining[i];

                        if (category.ParentId != uint.MaxValue)
                        {
                            TriggerNode parent = hierarchy.Root.Find(x => x.Id == category.ParentId);
                            if (parent != null)
                            {
                                TriggerNode newNode = new TriggerNode(category.Id, category.Name, ElementType.Category);
                                parent.Children.Insert(0, newNode);
                                categoriesRemaining.RemoveAt(i);
                            }
                        }
                        else
                        {
                            categoriesRemaining.RemoveAt(i);
                        }
                    }
                }

                foreach (Trigger trigger in triggerFile.Triggers)
                {
                    if (trigger.ElementType == ElementType.Script && trigger.IsEnabled)
                    {
                        hierarchy.Root.Add(trigger.ParentId, new TriggerScript(trigger.Id, trigger.Name, trigger.Description, trigger.CustomText));
                    }
                }
            }

            return hierarchy;
        }

        public void WriteToFileSystem(string rootDirectory)
        {
            Root.WriteToFileSystem(rootDirectory);
        }
    }

    public class TriggerNode
    {
        public uint Id { get; }
        public string Name { get; }
        public ElementType NodeType { get; }
        public List<TriggerNode> Children { get; }

        public TriggerNode(uint id, string name, ElementType nodeType)
        {
            Id = id;
            Name = name;
            NodeType = nodeType;

            Children = new List<TriggerNode>();
        }

        public TriggerNode Find(Predicate<TriggerNode> predicate)
        {
            if (predicate(this))
            {
                return this;
            }

            for (int i = 0; i < Children.Count; i++)
            {
                TriggerNode found = Children[i].Find(predicate);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public bool Add(uint parentId, TriggerNode child)
        {
            TriggerNode parent = Find(x => x.Id ==  parentId);
            if (parent != null)
            {
                parent.Children.Add(child);
                return true;
            }

            return false;
        }

        public virtual void WriteToFileSystem(string path)
        {
            string outputDir = Path.Combine(path, Path.GetFileNameWithoutExtension(this.Name));
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].WriteToFileSystem(outputDir);
            }
        }

        public override string ToString()
        {
            return NodeType + ": " + Name;
        }
    }

    public class TriggerScript : TriggerNode
    {
        public string Comment { get; set; }
        public string Script { get; set; }

        public TriggerScript(uint id, string name, string comment, string script) : base(id, name, ElementType.Script)
        {
            Comment = comment;
            Script = script;
        }

        public override string ToString()
        {
            return "Trigger: " + Name;
        }

        public override void WriteToFileSystem(string path)
        {
            string outputFile = Path.Combine(path, Path.GetFileNameWithoutExtension(this.Name)) + ".j";

            string fileContents = Script;
            if (fileContents == null)
            {
                fileContents = "";
            }

            fileContents = fileContents.Replace("\0", "");

            if (!string.IsNullOrWhiteSpace(Comment))
            {
                string comment = Comment.Replace("\0", "");
                string[] commentLines = comment.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                for (int i = commentLines.Length - 1; i >= 0; i--)
                {
                    string commentLine = commentLines[i].Trim();
                    if (commentLine.Length > 0)
                    {
                        fileContents = "// " + commentLine + "\n" + fileContents;
                    }
                }
            }

            File.WriteAllText(outputFile, fileContents.Trim(), Encoding.UTF8);

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].WriteToFileSystem(path);
            }
        }
    }
}
