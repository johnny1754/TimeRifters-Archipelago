"""Versioned local bridge. Never stores server passwords."""
import hashlib
import json
import os
from pathlib import Path
import time

ITEM_BASE = 9473100
TIME_ECHO_ID = ITEM_BASE + 5
LOCATION_BASE = 9473200
LOCATION_COUNT = 60
CHECK_MASK = (1 << LOCATION_COUNT) - 1


def default_directory():
    return Path(os.environ.get("LOCALAPPDATA", str(Path.home() / ".local/share"))) / "TimeRiftersArchipelago"


def session_key(seed, team, slot):
    return hashlib.sha256(json.dumps([seed, team, slot], separators=(",", ":")).encode()).hexdigest()


def mask_for_locations(locations):
    return sum(1 << i for i in range(LOCATION_COUNT) if LOCATION_BASE + i in locations)


def item_state(items):
    values = list(items)
    known = set(values)
    weapons = sum(1 << i for i in range(5) if ITEM_BASE + i in known)
    return weapons, values.count(TIME_ECHO_ID)


def write_snapshot(folder, session, weapons, echoes, checks):
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / "server.txt"
    temp = folder / "server.txt.tmp"
    temp.write_text(f"4\n{session}\n{int(time.time())}\n{weapons}\n{echoes}\n{checks}\n", encoding="ascii")
    os.replace(temp, target)


def read_progress(folder, session):
    path = folder / (session + ".progress")
    if not path.exists():
        return 0
    lines = path.read_text(encoding="ascii").splitlines()
    if len(lines) != 5 or lines[0] != "4" or lines[1] != session:
        raise ValueError("Invalid or mismatched progress file")
    low, high = int(lines[2]), int(lines[3])
    if low < 0 or low >= (1 << 64) or high < 0:
        raise ValueError("Invalid check mask")
    mask = low | (high << 64)
    if not 0 <= mask <= CHECK_MASK:
        raise ValueError("Invalid check mask")
    return mask


def outgoing(check_mask, item_mask, server_checked):
    checks = [LOCATION_BASE + i for i in range(LOCATION_COUNT)
              if check_mask & (1 << i) and LOCATION_BASE + i not in server_checked]
    return checks, check_mask == CHECK_MASK and item_mask == 31
