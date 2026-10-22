-- instruction: Work on the farm, tending crops and keeping it tidy
-- reply: I'll manage the farm and take care of the crops.

function main()
    local farm_center = memory.place("farm") or npc.home()
    local storage = memory.place("farm_chest") or memory.place("player_chest") or farm_center

    npc.say("Starting farm maintenance duties.")

    while true do
        -- 1. Check for any dropped items on farmland
        local dropped_item = world.find_entity("item", 16)
        if dropped_item then
            local ok, reason = npc.pick_up(dropped_item)
            if not ok then
                log("Failed to pick up farm drop: " .. tostring(reason))
            end
        end

        -- 2. Inspect farm ground for ripe crops or ground blocks to tend
        local crop_pos = world.find_block("wheat", 16) or world.find_block("grass", 16)
        if crop_pos then
            local ok, reason = npc.go_near(crop_pos, 2)
            if ok then
                local mine_ok, mine_reason = npc.mine(crop_pos)
                if mine_ok then
                    -- Replant if seeds/dirt available
                    npc.place(crop_pos, "dirt")
                else
                    log("Could not harvest crop: " .. tostring(mine_reason))
                end
            else
                log("Cannot reach crop at " .. crop_pos:tostring() .. ": " .. tostring(reason))
            end
        end

        -- 3. Deposit produce if inventory is getting full
        if npc.free_slots() < 4 then
            local ok, reason = npc.go_near(storage, 2)
            if ok then
                npc.deposit(storage, "dirt", -1)
                npc.deposit(storage, "wheat", -1)
            else
                log("Cannot reach farm storage: " .. tostring(reason))
            end
        end

        -- 4. Patrol around farm center to keep watch
        local inspect_pos = farm_center:offset(random(-4, 4), 0, random(-4, 4))
        npc.go_to(inspect_pos, { tolerance = 1, timeout = 6, allow_partial = true })
        npc.wait(3)
    end
end

function on_player_nearby(player_id)
    local player = world.entity(player_id)
    local name = player and player.name or "friend"
    npc.say("Welcome to the farm, " .. name .. "! Crops are coming along nicely.")
end

function on_attacked(attacker_id, damage)
    npc.say("Hey! Stay out of the fields!")
end

function on_interrupted(reason)
    log("Farming paused: " .. tostring(reason))
end
