#!/usr/bin/env python3
"""Drives the installed OpenDIAL and checks it actually works.

The headless tests drive view models and the visual ones compare rendered frames. Neither runs the
packaged application, so a defect that only appears there passes both: an asset that did not make it
into the bundle, a font that stopped resolving, a plugin that fails to load, a shortcut that never
fires. The last one is not hypothetical — every workspace shortcut had been dead since the day it
was written, and nothing in the suite noticed.

So this launches the real bundle through LaunchServices, the way a person does, opens a real
project, and then asserts on what the window says it is showing. It reads that from the probe file
the application writes when OPENDIAL_UI_PROBE names one, rather than guessing from pixels.

    opendial/scripts/smoke-ui.py --project ~/…/Project-2609071200.mdproject

Exits non-zero on the first failure and says what it expected. Needs a logged-in graphical session;
clicks additionally need cliclick (brew install cliclick), and are skipped with a warning if it is
not there.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time

APP_PROCESS = "OpenDIAL"

# the digit keys, which is how a shortcut has to be sent: a character event never reaches one
KEY_CODES = {1: 18, 2: 19, 3: 20, 4: 21, 5: 23}

WORKSPACES = ["Explorer", "Analytics", "Method", "Samples", "Statistics"]


class Failed(Exception):
    pass


def say(message: str) -> None:
    print(message, flush=True)


def osascript(script: str, check: bool = False) -> str:
    result = subprocess.run(["osascript", "-e", script], capture_output=True, text=True)
    if check and result.returncode != 0:
        raise Failed(f"osascript failed: {result.stderr.strip()}")
    return result.stdout.strip()


def running() -> bool:
    return bool(subprocess.run(["pgrep", "-f", "OpenDIAL.app/Contents/MacOS"],
                               capture_output=True).stdout.strip())


def quit_running() -> None:
    """Makes sure nothing is left running.

    It has to be thorough: `open --env` cannot apply an environment to an instance that already
    exists, and fails with a bare "-600" instead of saying so. And a polite quit can be refused —
    the window asks before discarding an unsaved project — so this escalates.
    """
    if not running():
        return
    osascript(f'tell application "{APP_PROCESS}" to quit')
    for _ in range(10):
        if not running():
            return
        time.sleep(0.5)
    subprocess.run(["pkill", "-f", "OpenDIAL.app/Contents/MacOS"], capture_output=True)
    for _ in range(10):
        if not running():
            return
        time.sleep(0.5)
    subprocess.run(["pkill", "-9", "-f", "OpenDIAL.app/Contents/MacOS"], capture_output=True)
    time.sleep(1)


def front() -> None:
    osascript(f'tell application "System Events" to set frontmost of process "{APP_PROCESS}" to true')
    time.sleep(0.4)


def press(key_code: int, modifier: str = "command") -> None:
    front()
    osascript(f'tell application "System Events" to key code {key_code} using {modifier} down', check=True)


def window_frame() -> tuple[float, float, float, float]:
    """The window's frame in screen points: where it is, and how big including its title bar."""
    position = osascript(f'tell application "System Events" to tell process "{APP_PROCESS}" '
                         'to get position of window 1')
    size = osascript(f'tell application "System Events" to tell process "{APP_PROCESS}" '
                     'to get size of window 1')
    try:
        x, y = [float(v) for v in position.split(", ")]
        w, h = [float(v) for v in size.split(", ")]
    except ValueError as error:
        raise Failed(f"could not read the window frame: {position!r} {size!r}") from error
    return x, y, w, h


def to_screen(state: dict, control: dict) -> tuple[float, float]:
    """Turns a place inside the window into a place on the screen.

    The application reports where its controls sit inside its own content, and refuses to guess at
    the rest; the frame and the height of the title bar belong to the window manager. Both are read
    here, and the difference between the frame and the content is the title bar.
    """
    frame_x, frame_y, frame_w, frame_h = window_frame()
    client = state.get("client") or {}
    client_w = float(client.get("width") or frame_w)
    client_h = float(client.get("height") or frame_h)
    title_bar = max(0.0, frame_h - client_h)
    border = max(0.0, (frame_w - client_w) / 2)
    return frame_x + border + float(control["x"]), frame_y + title_bar + float(control["y"])


def click(x: float, y: float) -> None:
    front()
    origin = subprocess.run(["cliclick", "p"], capture_output=True, text=True).stdout.strip()
    subprocess.run(["cliclick", "w:150", f"c:{x:.0f},{y:.0f}", "w:400"], check=True, capture_output=True)
    if origin:
        subprocess.run(["cliclick", f"m:{origin}"], capture_output=True)


def read_probe(path: str) -> dict:
    for _ in range(5):
        try:
            with open(path, encoding="utf-8") as handle:
                return json.load(handle)
        except (FileNotFoundError, json.JSONDecodeError):
            time.sleep(0.2)
    raise Failed(f"no readable probe at {path}")


def wait_for(path: str, predicate, what: str, timeout: float) -> dict:
    """Waits for the window to report something, and says what it was still saying if it never did."""
    deadline = time.time() + timeout
    last: dict = {}
    while time.time() < deadline:
        try:
            last = read_probe(path)
        except Failed:
            time.sleep(0.5)
            continue
        if predicate(last):
            return last
        time.sleep(0.5)
    raise Failed(f"timed out after {timeout:.0f} s waiting for {what}; the window was reporting "
                 f"{json.dumps({k: v for k, v in last.items() if k != 'controls'}, indent=2)}")


def check(condition: bool, message: str) -> None:
    if not condition:
        raise Failed(message)
    say(f"  ok  {message}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--bundle", default="/Applications/OpenDIAL.app",
                        help="the application to drive (default: the installed one)")
    parser.add_argument("--project", help="a project to open; without it only the empty window is checked")
    parser.add_argument("--shots", help="directory to save screenshots into")
    parser.add_argument("--open-timeout", type=float, default=600,
                        help="seconds to allow for the project to load (default: 600)")
    parser.add_argument("--keep", action="store_true", help="leave the application running at the end")
    args = parser.parse_args()

    if sys.platform != "darwin":
        say("skipped: this drives a macOS application")
        return 0
    # `open -a` only takes an absolute path, and says "unable to find" for anything else
    args.bundle = os.path.abspath(args.bundle)
    if not os.path.isdir(args.bundle):
        say(f"FAIL: no application at {args.bundle}; build and install it first "
            f"(opendial/scripts/make-app-bundle.sh)")
        return 1
    if args.project and not os.path.exists(args.project):
        say(f"FAIL: no project at {args.project}")
        return 1

    can_click = shutil.which("cliclick") is not None
    if not can_click:
        say("note: cliclick is not installed, so the click checks are skipped (brew install cliclick)")

    work = tempfile.mkdtemp(prefix="opendial-smoke-")
    probe = os.path.join(work, "probe.json")
    out_log = os.path.join(work, "stdout.log")
    err_log = os.path.join(work, "stderr.log")
    shots = args.shots or os.path.join(work, "shots")
    os.makedirs(shots, exist_ok=True)

    say(f"driving {args.bundle}")
    say(f"  probe {probe}")
    quit_running()

    launch = ["open", "-a", args.bundle, "--env", f"OPENDIAL_UI_PROBE={probe}",
              "--stdout", out_log, "--stderr", err_log]
    if args.project:
        launch += ["--args", args.project]
    result = subprocess.run(launch, capture_output=True, text=True)
    if result.returncode != 0:
        say(f"FAIL: could not launch it: {result.stderr.strip() or result.stdout.strip()}")
        if running():
            say("      an instance is already running, and `open --env` cannot attach to one")
        return 1

    failures = []

    def shot(name: str) -> None:
        path = os.path.join(shots, name + ".png")
        front()
        subprocess.run(["screencapture", "-x", "-o", path], capture_output=True)
        say(f"  shot {path}")

    try:
        say("the window comes up")
        state = wait_for(probe, lambda s: bool(s.get("title")), "the window to report itself", 90)
        check(APP_PROCESS in state["title"], f"the title reads {state['title']!r}")
        check(bool(state.get("controls")), "the window can say where its workspace tabs are")

        if args.project:
            say("the project opens")
            expected = os.path.basename(os.path.dirname(os.path.abspath(args.project)))
            state = wait_for(probe, lambda s: s.get("hasResults") and s.get("ionRows", 0) > 0,
                             "the results to load", args.open_timeout)
            check(expected in state["title"], f"the title names the project: {state['title']!r}")
            check(state["ionRows"] > 0, f"the ion table holds {state['ionRows']} features")
            check(state["workspaceName"] == "Analytics",
                  "opening a processed project lands on the review workspace")
            shot("opened")

        say("every workspace shortcut goes where it says")
        for number, code in KEY_CODES.items():
            press(code)
            state = wait_for(probe, lambda s, n=number: s.get("workspace") == n - 1,
                             f"Cmd+{number} to select {WORKSPACES[number - 1]}", 10)
            check(state["workspaceName"] == WORKSPACES[number - 1],
                  f"Cmd+{number} selects {WORKSPACES[number - 1]}")

        say("and so does the control key, for a keyboard without a command key")
        press(KEY_CODES[2], modifier="control")
        wait_for(probe, lambda s: s.get("workspace") == 1, "Ctrl+2 to select Analytics", 10)
        check(True, "Ctrl+2 selects Analytics")

        if can_click:
            say("the workspace tabs answer a click, where the window says they are")
            press(KEY_CODES[1])
            wait_for(probe, lambda s: s.get("workspace") == 0, "the Explorer", 10)
            state = read_probe(probe)
            spot = state["controls"].get("workspace.Statistics")
            check(spot is not None, "the window reports where the Statistics tab is")
            x, y = to_screen(state, spot)
            say(f"       the tab is at {x:.0f}, {y:.0f} on screen")
            click(x, y)
            state = wait_for(probe, lambda s: s.get("workspace") == 4, "the click to select Statistics", 10)
            check(state["workspaceName"] == "Statistics", "clicking the Statistics tab selects it")
            if args.project:
                check(state["statistics"]["hasResults"], "the statistics workspace has the batch")
            shot("statistics")

        say("nothing fell over")
        alive = subprocess.run(["pgrep", "-f", "OpenDIAL.app/Contents/MacOS"], capture_output=True)
        check(bool(alive.stdout.strip()), "the application is still running")
        for name, path in (("stdout", out_log), ("stderr", err_log)):
            if not os.path.exists(path):
                continue
            text = open(path, encoding="utf-8", errors="replace").read()
            bad = [line for line in text.splitlines()
                   if "Unhandled exception" in line or "System.NullReferenceException" in line]
            check(not bad, f"nothing unhandled on {name}" + (f": {bad[:2]}" if bad else ""))

    except Failed as failure:
        failures.append(str(failure))
        shot("failure")
    finally:
        if not args.keep:
            quit_running()

    if failures:
        say("")
        for failure in failures:
            say("FAIL: " + failure)
        say(f"logs and screenshots under {work}")
        return 1

    say("")
    say("the installed application opens, loads a real project, and answers both the keyboard and the mouse")
    return 0


if __name__ == "__main__":
    sys.exit(main())
