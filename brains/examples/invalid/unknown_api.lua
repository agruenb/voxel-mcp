-- instruction: Execute an action using forbidden or nonexistent APIs
-- reply: This script intentionally calls an unknown API.

function main()
    npc.say("Attempting to invoke an invalid API method...")
    -- 'world.teleport_player_to_orbit' does not exist in the Lua sandbox
    world.teleport_player_to_orbit(123)
end
