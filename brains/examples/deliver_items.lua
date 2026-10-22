-- instruction: Deliver all items in your inventory to the storage chest
-- reply: Right away! I'll take everything to the storage chest.

function main()
    local chest = memory.place("player_chest") or memory.place("home") or npc.home()
    local inv = npc.inventory()

    -- Check if inventory has any items
    local total_items = 0
    for item_id, count in pairs(inv) do
        if count > 0 then
            total_items = total_items + count
        end
    end

    if total_items == 0 then
        npc.say("My inventory is empty. Nothing to deliver.")
        npc.done("No items to deliver")
        return
    end

    npc.say("Carrying " .. total_items .. " items to the storage chest.")

    -- Approach chest
    local ok, reason = npc.go_near(chest, 2)
    if not ok then
        npc.say("I could not reach the storage chest: " .. tostring(reason))
        return
    end

    -- Deposit all items
    local deposited_count = 0
    for item_id, count in pairs(inv) do
        if count > 0 then
            local dep_ok, dep_reason = npc.deposit(chest, item_id, -1)
            if dep_ok then
                deposited_count = deposited_count + count
                log("Deposited " .. count .. "x " .. item_id)
            else
                log("Failed to deposit " .. item_id .. ": " .. tostring(dep_reason))
            end
        end
    end

    npc.say("Successfully deposited " .. deposited_count .. " items into the chest!")
    npc.done("Delivered " .. deposited_count .. " items to storage")
end

function on_attacked(attacker_id, damage)
    npc.say("Hands off the cargo! I'm making a delivery!")
end

function on_interrupted(reason)
    log("Delivery interrupted: " .. tostring(reason))
end
