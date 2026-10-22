-- instruction: Build a stone wall 5 blocks long
-- reply: I'll construct a stone wall for you right now.

local WALL_LENGTH = 5
local WALL_HEIGHT = 2
local TOTAL_BLOCKS = WALL_LENGTH * WALL_HEIGHT

function main()
    local origin = memory.place("wall_origin")
    if not origin then
        origin = npc.pos():offset(2, 0, 0)
        memory.remember_place("wall_origin", origin)
    end

    npc.say("Beginning construction of stone wall (" .. TOTAL_BLOCKS .. " blocks).")

    while memory.get("blocks_placed", 0) < TOTAL_BLOCKS do
        local idx = memory.get("blocks_placed", 0)
        local dx = idx % WALL_LENGTH
        local dy = math.floor(idx / WALL_LENGTH)
        local target_pos = origin:offset(dx, dy, 0)

        -- Determine which building material is available
        local material = "stone"
        if npc.count("stone") == 0 and npc.count("cobblestone") > 0 then
            material = "cobblestone"
        end

        -- If out of materials, attempt to retrieve from storage chest
        if npc.count(material) == 0 then
            local chest = memory.place("player_chest")
            if chest then
                local ok, reason = npc.go_near(chest, 2)
                if ok then
                    npc.withdraw(chest, "stone", 10)
                    npc.withdraw(chest, "cobblestone", 10)
                else
                    log("Cannot reach chest to withdraw materials: " .. tostring(reason))
                end
            end

            -- Re-check inventory
            if npc.count("stone") == 0 and npc.count("cobblestone") == 0 then
                npc.say("I need stone or cobblestone to continue building.")
                npc.wait(3)
            end
        else
            -- Approach build position and place block
            local ok, reason = npc.go_near(target_pos, 3)
            if ok then
                local place_ok, place_reason = npc.place(target_pos, material)
                if place_ok then
                    local placed = idx + 1
                    memory.set("blocks_placed", placed)
                    log("Placed block " .. placed .. "/" .. TOTAL_BLOCKS .. " at " .. target_pos:tostring())
                else
                    if place_reason == "occupied" or place_reason == "invalid_target" then
                        -- Position already has a block or cannot be placed; count as filled
                        memory.set("blocks_placed", idx + 1)
                    else
                        log("Could not place block: " .. tostring(place_reason))
                        npc.wait(1)
                    end
                end
            else
                log("Could not walk near build spot: " .. tostring(reason))
                npc.wait(1)
            end
        end
    end

    npc.say("Stone wall construction completed successfully!")
    npc.done("Built 5-block stone wall")
end

function on_attacked(attacker_id, damage)
    npc.say("Watch it! I'm trying to build here!")
end

function on_interrupted(reason)
    log("Wall construction paused by reflex: " .. tostring(reason))
end
