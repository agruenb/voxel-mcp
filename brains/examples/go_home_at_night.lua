-- instruction: Roam outside during daytime, but return home before night falls
-- reply: Sounds good! I will be home every evening before nightfall.

function main()
    local home = npc.home()
    npc.say("Understood. I will stay outside by day and sleep inside by night.")

    while true do
        local is_day = world.is_day()

        if not is_day then
            -- Nighttime: return home and stay safe
            if npc.pos():dist(home) > 2 then
                npc.say("Night has fallen. Returning home now.")
                local ok, reason = npc.go_near(home, 2)
                if not ok then
                    log("Could not reach home directly: " .. tostring(reason))
                end
            end

            -- Rest inside while it remains dark
            while not world.is_day() do
                npc.look_at(home:offset(0, 0, 1))
                npc.wait(4)
            end

            npc.say("Sun is up! Time for daytime activities.")
        else
            -- Daytime: wander and inspect area around home
            local wander_target = home:offset(random(-8, 8), 0, random(-8, 8))
            local ok, reason = npc.go_to(wander_target, { tolerance = 2, timeout = 8, allow_partial = true })
            if not ok then
                log("Wander step interrupted or unreachable: " .. tostring(reason))
            end

            npc.wait(random(2, 5))
        end
    end
end

function on_attacked(attacker_id, damage)
    npc.say("Help! I am under attack!")
    npc.go_near(npc.home(), 2)
end

function on_player_nearby(player_id)
    if world.is_day() then
        npc.say("Fine weather today, isn't it?")
    else
        npc.say("Careful out there in the dark!")
    end
end

function on_interrupted(reason)
    log("Routine interrupted: " .. tostring(reason))
end
