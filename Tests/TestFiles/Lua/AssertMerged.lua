-- Independent Lua interpreter checks. Only these native stubs execute here.
local cameras, regions, groups, items = arg[2] == 'true', arg[3] == 'true', arg[4] == 'true', arg[5] == 'true'
function Record(value) Trace[#Trace + 1] = value end
function SetMapName(value) assert(value:match('^TRIGSTR_%d+$')); ConfigName = value end
function SetPlayers(value) Players = value end
function SetBounds(value) Record(value) end
function RegisterDrop(value) assert(type(value) == 'function'); value() end
function InitBlizzard() Record('blizzard') end

assert(loadfile(arg[1]))()
config()
main()
assert(Players == 4, 'wrong player configuration')
assert(udg_Gameplay == 73, 'destination gameplay globals leaked')
assert(gg_unit_Terrain and not gg_unit_Source, 'placed unit globals not selected together')
assert(ObsoleteGameplay == nil, 'destination gameplay leaked')
assert(Unit000001_DropItems == nil, 'obsolete source callback leaked')
assert(cameras and gg_cam_Source or not cameras and gg_cam_Terrain, 'camera initializer mismatch')
assert(regions and gg_rct_Source or not regions and gg_rct_Terrain, 'region initializer mismatch')
assert(groups and gg_rg_Source[1] == 11 or not groups and gg_rg_Terrain[1] == 22, 'random group mismatch')
local expected = {
    'terrain bounds', 'source sounds',
    regions and 'source regions' or 'terrain regions',
    cameras and 'source cameras' or 'terrain cameras',
    groups and 'source groups' or 'terrain groups',
    'terrain destructables', 'terrain items', 'terrain units', 'terrain unit drop',
    items and 'source item table' or 'terrain item table',
    'blizzard', 'source globals', 'source triggers', 'source gameplay'
}
assert(#Trace == #expected, 'wrong number of initialization calls')
for index, value in ipairs(expected) do
    assert(Trace[index] == value, ('call %d: expected %s, got %s'):format(index, value, tostring(Trace[index])))
end
print('merged Lua runtime checks passed')
