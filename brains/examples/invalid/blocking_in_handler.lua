-- instruction: Listen for speech and perform a blocking walk inside the handler
-- reply: This script intentionally calls a blocking action inside an event handler.

function main()
    while true do
        npc.wait(5)
    end
end

function on_heard(speaker_id, text)
    -- Calling blocking action npc.go_to inside an event handler is strictly forbidden!
    npc.go_to(vec(0, 64, 0))
end
