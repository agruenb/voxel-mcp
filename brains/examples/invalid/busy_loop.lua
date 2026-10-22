-- instruction: Run an endless loop without yielding or calling actions
-- reply: This script intentionally performs a busy loop.

function main()
    local counter = 0
    while true do
        counter = counter + 1
    end
end
