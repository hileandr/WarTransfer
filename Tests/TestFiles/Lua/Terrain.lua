-- Synthetic destination containing old gameplay that must not survive.
Trace = {'obsolete destination state'}
udg_Gameplay = -1
gg_rct_Terrain = nil
gg_cam_Terrain = nil
gg_rg_Terrain = {}
gg_unit_Terrain = nil
gg_snd_Terrain = nil

function ObsoleteGameplay() error('old destination gameplay ran') end
function InitSounds() error('old destination sounds ran') end
function CreateRegions() gg_rct_Terrain = {}; Record('terrain regions') end
function CreateCameras() gg_cam_Terrain = {}; Record('terrain cameras') end
function InitRandomGroups() gg_rg_Terrain[1] = 22; Record('terrain groups') end
function Unit000002_DropItems() Record('terrain unit drop') end
function ItemTable000001_DropItems() Record('terrain item table') end
function CreateAllDestructables() Record('terrain destructables') end
function CreateAllItems() Record('terrain items') end
function CreateAllUnits()
    gg_unit_Terrain = {}
    Record('terrain units')
    RegisterDrop(Unit000002_DropItems)
    RegisterDrop(ItemTable000001_DropItems)
end

function config()
    SetMapName([=[TRIGSTR_001]=])
    SetPlayers(4)
end

function main()
    local we
    SetBounds('terrain bounds')
    we = AddWeatherEffect(Rect(-32, -32, 32, 32), FourCC('RAlr'))
    EnableWeatherEffect(we, true)
    InitSounds()
    CreateRegions()
    CreateCameras()
    InitRandomGroups()
    CreateAllDestructables()
    CreateAllItems()
    CreateAllUnits()
    InitBlizzard()
    ObsoleteGameplay()
end
