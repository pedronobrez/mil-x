#!/usr/bin/env bash
# Drives the running MIL-X from a script, for screenshots and for checking something in the real
# application rather than in a headless test.
#
# Two things about macOS make this less obvious than it looks:
#
#   * System Events' "click at" asks the accessibility layer to press a control. Avalonia publishes
#     almost nothing to that layer, so a click at a coordinate does nothing at all. Real mouse events
#     have to be posted instead, which is what cliclick does: brew install cliclick.
#   * Keystrokes do arrive, but `keystroke "5" using command down` sends a character event that never
#     reaches a shortcut, while `key code 23 using command down` sends the physical key and does.
#
# And the window has to be frontmost *before* the click, or the click only activates it.
#
# Coordinates are screen points with the origin at the top left, which on a Retina display is half
# the pixel coordinate you read off a screenshot.
#
#   ./ui-drive.sh front
#   ./ui-drive.sh click 371 107          # the Statistics workspace tab
#   ./ui-drive.sh key 23 cmd             # Cmd+5, the same thing by keyboard
#   ./ui-drive.sh shot /tmp/now.png
#   ./ui-drive.sh where                  # the cursor, for working coordinates out
set -euo pipefail

APP="${MILX_APP_PROCESS:-MIL-X}"

need_cliclick() {
    if ! command -v cliclick >/dev/null 2>&1; then
        echo "cliclick is not installed: brew install cliclick" >&2
        exit 2
    fi
}

front() {
    osascript -e "tell application \"System Events\" to set frontmost of process \"$APP\" to true" >/dev/null 2>&1 || {
        echo "$APP does not seem to be running" >&2
        exit 3
    }
    sleep 0.4
}

case "${1:-}" in
    front)
        front
        ;;
    click)
        need_cliclick
        [ $# -ge 3 ] || { echo "usage: $0 click X Y" >&2; exit 2; }
        # put the pointer back where it was: this is someone's machine
        origin=$(cliclick p)
        front
        cliclick w:150 "c:$2,$3" w:400
        cliclick "m:$origin" >/dev/null
        ;;
    key)
        [ $# -ge 2 ] || { echo "usage: $0 key CODE [cmd|ctrl|shift|alt]" >&2; exit 2; }
        front
        modifier=""
        case "${3:-}" in
            cmd) modifier=" using command down" ;;
            ctrl) modifier=" using control down" ;;
            shift) modifier=" using shift down" ;;
            alt) modifier=" using option down" ;;
        esac
        osascript -e "tell application \"System Events\" to key code $2$modifier"
        ;;
    shot)
        [ $# -ge 2 ] || { echo "usage: $0 shot PATH" >&2; exit 2; }
        front
        screencapture -x -o "$2"
        echo "$2"
        ;;
    where)
        need_cliclick
        cliclick p
        ;;
    *)
        sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'
        exit 1
        ;;
esac
