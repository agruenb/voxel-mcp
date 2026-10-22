-- instruction: Collect 10 oak logs and put them in my chest
-- reply: Alright, I'll go chop down trees and gather logs for you.

local TARGET_COUNT = 10

function main()
    local gathered = memory.get("gathered", 0)
    local chest = memory.place("player_chest") or npc.home()

    npc.say("I'm starting to gather logs. Target: " .. TARGET_COUNT)

    while memory.get("gathered", 0) < TARGET_COUNT do
        -- 1. Check if we are carrying enough logs to deposit
        local held = npc.count("oak_log")
        local current_total = memory.get("gathered", 0)

        if held >= 4 or (held > 0 and current_total + held >= TARGET_COUNT) then
            local ok, reason = npc.go_near(chest, 2)
            if ok then
                local dep_ok, dep_reason = npc.deposit(chest, "oak_log", -1)
                if dep_ok then
                    local new_total = current_total + held
                    memory.set("gathered", new_total)
                    npc.say("Deposited logs. Total stored: " .. new_total .. "/" .. TARGET_COUNT)
                else
                    log("Failed to deposit logs: " .. tostring(dep_reason))
                    npc.wait(1)
                end
            else
                log("Failed to reach chest: " .. tostring(reason))
                npc.wait(2)
            end
        end

        -- If target achieved after depositing, break loop
        if memory.get("gathered", 0) >= TARGET_COUNT then
            break
        end

        -- 2. Pick up any dropped log items nearby
        local item_id = world.find_entity("item", 16)
        if item_id then
            local ent = world.entity(item_id)
            if ent and ent.item_id == "oak_log" then
                local ok, reason = npc.pick_up(item_id)
                if not ok then
                    log("Could not pick up log item: " .. tostring(reason))
                end
            end
        end

        -- 3. Search for standing oak log block to mine
        local log_pos = world.find_block("oak_log", 24)
        if log_pos then
            local ok, reason = npc.go_near(log_pos, 2)
            if ok then
                local mine_ok, mine_reason = npc.mine(log_pos)
                if not mine_ok then
                    log("Failed to mine oak log: " .. tostring(mine_reason))
                    npc.wait(1)
                end
            else
                log("Cannot reach oak log: " .. tostring(reason))
                npc.wait(1)
            end
        else
            -- No oak logs found in range; explore around home
            log("No oak logs in sight; scouting nearby area...")
            local wander_target = npc.home():offset(random(-12, 12), 0, random(-12, 12))
            npc.go_to(wander_target, { tolerance = 2, allow_partial = true, timeout = 6 })
            npc.wait(2)
        end
    end

    -- Final deposit of any remaining logs held
    local remaining = npc.count("oak_log")
    if remaining > 0 then
        local ok, reason = npc.go_near(chest, 2)
        if ok then
            npc.deposit(chest, "oak_log", -1)
        end
    end

    npc.say("All " .. TARGET_COUNT .. " logs collected and stored safely!")
    npc.done("Gathered " .. TARGET_COUNT .. " oak logs")
end

function on_attacked(attacker_id, damage)
    npc.say("Ouch! I'm taking damage while gathering logs!")
end

function on_interrupted(reason)
    log("Task interrupted by reflex: " .. tostring(reason))
end
