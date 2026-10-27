-- Synthetic editor-style source. No Warcraft III assets or scripts are included.
Trace = {}
udg_Gameplay = 73
gg_rct_Source = nil
gg_cam_Source = nil
gg_rg_Source = {}
gg_unit_Source = nil
gg_snd_Source = nil
gg_trg_Gameplay = nil
local sourceClosure = 17

function Gameplay()
    -- Words that must not confuse the parser: function main() end InitBlizzard()
    local quoted = [==[a nested [=[long string]=] and "TRIGSTR_999"]==]
    local nested = function(value)
        if value then return (sourceClosure & 7) // 1 end
    end
    assert(nested(true) == 1 and quoted:find('TRIGSTR_999', 1, true))
    Record('source gameplay')
end

function InitGlobals() Record('source globals') end
function InitCustomTriggers() Record('source triggers') end
function InitSounds() Record('source sounds') end
function CreateRegions() gg_rct_Source = {}; Record('source regions') end
function CreateCameras() gg_cam_Source = {}; Record('source cameras') end
function InitRandomGroups() gg_rg_Source[1] = 11; Record('source groups') end
function Unit000001_DropItems() Record('obsolete source unit drop') end
function ItemTable000001_DropItems() Record('source item table') end
function CreateAllUnits() Record('obsolete source units') end
function CreateAllItems() Record('obsolete source items') end
function CreateAllDestructables() Record('obsolete source destructables') end

function config()
    SetMapName('TRIGSTR_001')
    SetPlayers(2)
end

function main()
    SetBounds('source bounds')
    InitSounds()
    CreateRegions()
    CreateCameras()
    InitRandomGroups()
    CreateAllDestructables()
    CreateAllItems()
    CreateAllUnits()
    InitBlizzard()
    InitGlobals()
    InitCustomTriggers()
    Gameplay()
end
