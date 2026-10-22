-- instruction: Guard this area and keep watch for intruders
-- reply: Standing guard! I will patrol the perimeter and keep watch.

function main()
    local center = memory.place("guard_post")
    if not center then
        center = npc.pos()
        memory.remember_place("guard_post", center)
    end

    local offsets = {
        { 4, 0 },
        { 0, 4 },
        { -4, 0 },
        { 0, -4 }
    }

    local wp_index = memory.get("wp_index", 1)
    npc.say("Guard perimeter established around my post.")

    while true do
        local offset = offsets[wp_index]
        local wp = center:offset(offset[1], 0, offset[2])

        -- Walk to patrol waypoint
        local ok, reason = npc.go_to(wp, { tolerance = 1, timeout = 8, allow_partial = true })
        if not ok then
            log("Failed to reach waypoint " .. wp_index .. ": " .. tostring(reason))
        end

        -- Look outwards and scan the perimeter
        local look_target = wp:offset(offset[1] * 2, 1, offset[2] * 2)
        npc.look_at(look_target)
        npc.wait(3)

        -- Advance to next waypoint and persist progress
        wp_index = (wp_index % #offsets) + 1
        memory.set("wp_index", wp_index)
    end
end

function on_attacked(attacker_id, damage)
    npc.say("Alert! I am under attack by entity " .. tostring(attacker_id) .. "!")
    local attacker = world.entity(attacker_id)
    if attacker then
        npc.look_at(attacker.pos)
    end
end

function on_player_nearby(player_id)
    local ent = world.entity(player_id)
    local name = ent and ent.name or "traveler"
    npc.say("Halt! Who goes there? Ah, greetings " .. name .. ". The area is secure.")
end

function on_interrupted(reason)
    log("Guard routine interrupted: " .. tostring(reason))
end
