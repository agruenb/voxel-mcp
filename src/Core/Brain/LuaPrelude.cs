namespace VoxelKing.Core.Brain;

/// <summary>
/// Provides the Lua prelude chunk that defines the <c>pos</c> metatable, <c>vec</c> constructor,
/// <c>dist</c> utility, and yielding coroutine wrappers for blocking NPC actions (ARCHITECTURE §5.4).
/// </summary>
public static class LuaPrelude
{
    public const string Source = @"
local _yield = __yield
local _begin = __begin_action

-- 1. pos metatable and vector utilities
local pos_mt = {}

local function create_pos(x, y, z)
    return setmetatable({
        x = math.floor(tonumber(x) or 0),
        y = math.floor(tonumber(y) or 0),
        z = math.floor(tonumber(z) or 0)
    }, pos_mt)
end

pos_mt.__index = {
    dist = function(self, other)
        if not other or type(other) ~= 'table' or not other.x or not other.y or not other.z then
            error(""bad argument #1 to ':dist' (pos expected)"")
        end
        local dx = self.x - other.x
        local dy = self.y - other.y
        local dz = self.z - other.z
        return math.sqrt(dx * dx + dy * dy + dz * dz)
    end,
    offset = function(self, dx, dy, dz)
        return create_pos(self.x + (dx or 0), self.y + (dy or 0), self.z + (dz or 0))
    end,
    tostring = function(self)
        return string.format('pos(%d, %d, %d)', self.x, self.y, self.z)
    end
}

pos_mt.__add = function(a, b)
    if type(a) ~= 'table' or type(b) ~= 'table' or not a.x or not b.x then
        error('attempt to perform addition on non-pos value')
    end
    return create_pos(a.x + b.x, a.y + b.y, a.z + b.z)
end

pos_mt.__sub = function(a, b)
    if type(a) ~= 'table' or type(b) ~= 'table' or not a.x or not b.x then
        error('attempt to perform subtraction on non-pos value')
    end
    return create_pos(a.x - b.x, a.y - b.y, a.z - b.z)
end

pos_mt.__eq = function(a, b)
    if type(a) ~= 'table' or type(b) ~= 'table' or not a.x or not b.x then
        return false
    end
    return a.x == b.x and a.y == b.y and a.z == b.z
end

pos_mt.__tostring = function(self)
    return string.format('pos(%d, %d, %d)', self.x, self.y, self.z)
end

pos_mt.__metatable = 'protected'

function vec(x, y, z)
    if x == nil or y == nil or z == nil then
        error('vec(x, y, z) expects 3 arguments')
    end
    return create_pos(x, y, z)
end

function dist(a, b)
    if not a or type(a) ~= 'table' or not a.x or not a.y or not a.z or
       not b or type(b) ~= 'table' or not b.x or not b.y or not b.z then
        error(""bad arguments to 'dist' (two pos tables expected)"")
    end
    local dx = a.x - b.x
    local dy = a.y - b.y
    local dz = a.z - b.z
    return math.sqrt(dx * dx + dy * dy + dz * dz)
end

-- 2. Blocking action wrappers
npc = npc or {}

function npc.go_to(pos, opts)
    local action_id = _begin('go_to', pos, opts)
    return _yield(action_id)
end

function npc.go_near(pos, dist_val)
    local action_id = _begin('go_near', pos, dist_val)
    return _yield(action_id)
end

function npc.follow(entity_id, dist_val, seconds)
    local action_id = _begin('follow', entity_id, dist_val, seconds)
    return _yield(action_id)
end

function npc.mine(pos)
    local action_id = _begin('mine', pos)
    return _yield(action_id)
end

function npc.place(pos, block_id)
    local action_id = _begin('place', pos, block_id)
    return _yield(action_id)
end

function npc.pick_up(entity_id)
    local action_id = _begin('pick_up', entity_id)
    return _yield(action_id)
end

function npc.drop(item_id, count)
    local action_id = _begin('drop', item_id, count)
    return _yield(action_id)
end

function npc.give(entity_id, item_id, count)
    local action_id = _begin('give', entity_id, item_id, count)
    return _yield(action_id)
end

function npc.deposit(pos, item_id, count)
    local action_id = _begin('deposit', pos, item_id, count)
    return _yield(action_id)
end

function npc.withdraw(pos, item_id, count)
    local action_id = _begin('withdraw', pos, item_id, count)
    return _yield(action_id)
end

function npc.wait(seconds)
    local action_id = _begin('wait', seconds)
    return _yield(action_id)
end
";
}
