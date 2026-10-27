using WarTransfer.FileTypes;

namespace WarTransfer.Scripts
{
    internal interface IMapScriptMerger
    {
        MapScriptType ScriptType { get; }

        // Implementations own script reading, merging, and writing. No language-specific
        // document types escape into the map/archive transfer workflow.
        void Merge(IWorkflowHandler handler, string sourceMapDirectory, string terrainMapDirectory,
            string outputMapDirectory, WtsFile outputStrings, WtsFile terrainStrings);
    }
}
