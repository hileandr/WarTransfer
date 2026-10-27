using System.IO;

namespace WarTransfer.Scripts
{
    internal enum MapScriptType
    {
        Jass = 0,
        Lua = 1
    }

    internal static class MapScripts
    {
        // Called on extracted maps before creating any merged map. Metadata selects the
        // active language even when an archive also contains an inactive script file.
        internal static IMapScriptMerger Open(IWorkflowHandler handler, string mapDirectory,
            string role, WarTransferArgs args)
        {
            if (handler.KillProcess) return null;
            var info = W3iUtility.ReadFile(handler, Path.Combine(mapDirectory, "war3map.w3i"));
            if (handler.KillProcess) return null;

            // Formats before 28 have no language field and always use JASS.
            var type = info.FormatVersion < 28 ? MapScriptType.Jass : (MapScriptType)info.ScriptTypeId;
            string description = role + " map '" + mapDirectory + "'";
            switch (type)
            {
                case MapScriptType.Jass:
                    if (!File.Exists(Path.Combine(mapDirectory, "war3map.j")))
                    {
                        string lua = File.Exists(Path.Combine(mapDirectory, "war3map.lua"))
                            ? " A Lua script (war3map.lua) is present, but the map metadata declares JASS." : "";
                        handler.ReportError("Missing JASS script (war3map.j) in " + description + "." + lua, true);
                        return null;
                    }
                    return new JassScriptMerger(args);
                case MapScriptType.Lua:
                    handler.ReportError("Lua scripts are not supported: " + description +
                        " declares Lua in war3map.w3i. Only JASS-to-JASS transfers are currently supported.", true);
                    return null;
                default:
                    handler.ReportError("Unsupported script type " + info.ScriptTypeId + " in " + description +
                        " (war3map.w3i).", true);
                    return null;
            }
        }
    }
}
