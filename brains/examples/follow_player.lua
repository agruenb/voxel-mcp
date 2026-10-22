-- instruction: Follow me and stay close
-- reply: Understood! I'll follow you wherever you go.

function main()
    npc.say("I'm ready! Following your lead.")

    while true do
        local target_id = npc.instructor() or world.find_entity("player", 32)
        if target_id then
            local target_entity = world.entity(target_id)
            if target_entity then
                local current_pos = npc.pos()
                local d = current_pos:dist(target_entity.pos)

                if d > 3 then
                    local ok, reason = npc.follow(target_id, 2, 4)
                    if not ok then
                        log("Follow action returned: " .. tostring(reason))
                        npc.wait(1)
                    end
                else
                    npc.look_at(target_entity.pos)
                    npc.wait(1)
                end
            else
                -- Entity no longer perceptible
                npc.wait(2)
            end
        else
            -- No player found within perception radius
            log("No player in sight; waiting...")
            npc.wait(2)
        end
    end
end

function on_player_nearby(player_id)
    local player = world.entity(player_id)
    local name = player and player.name or "friend"
    npc.say("There you are, " .. name .. "! Right with you.")
end

function on_heard(speaker_id, text)
    if string.find(string.lower(text), "stop") or string.find(string.lower(text), "stay") then
        npc.say("Alright, stopping here as requested.")
        npc.done("Follow stopped by command")
    end
end

function on_interrupted(reason)
    log("Follow interrupted: " .. tostring(reason))
end
