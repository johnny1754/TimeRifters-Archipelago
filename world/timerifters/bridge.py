"""Versioned local bridge. Never stores server passwords."""
import hashlib
import json
import os
from pathlib import Path
import time

ITEM_BASE = 9473100
TIME_ECHO_ID = ITEM_BASE + 5
LOCATION_BASE = 9473200
LOCATION_COUNT = 79
CORE_LOCATION_COUNT = 63
CORE_CHECK_MASK = (1 << CORE_LOCATION_COUNT) - 1
CHECK_MASK = (1 << LOCATION_COUNT) - 1


def default_directory():
    return Path(os.environ.get("LOCALAPPDATA", str(Path.home() / ".local/share"))) / "TimeRiftersArchipelago"


def session_key(seed, team, slot):
    return hashlib.sha256(json.dumps([seed, team, slot], separators=(",", ":")).encode()).hexdigest()


def mask_for_locations(locations, location_count):
    return sum(1 << i for i in range(location_count) if LOCATION_BASE + i in locations)


def item_state(items):
    values = list(items)
    known = set(values)
    progression = sum(1 << i for i in range(5) if ITEM_BASE + i in known)
    progression |= 32 if ITEM_BASE + 6 in known else 0
    progression |= 64 if ITEM_BASE + 7 in known else 0
    return progression, values.count(TIME_ECHO_ID)


def write_snapshot(folder, session, weapons, echoes, checks, escape_checks, episode_keys, arena_shuffle, arena_order,
                   required_echoes, goal, death_link, death_link_percent):
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / "server.txt"
    temp = folder / "server.txt.tmp"
    order = ",".join(str(value) for value in arena_order)
    temp.write_text(f"14\n{session}\n{int(time.time())}\n{weapons}\n{echoes}\n{checks}\n{1 if escape_checks else 0}\n{1 if episode_keys else 0}\n{1 if arena_shuffle else 0}\n{order}\n{required_echoes}\n{goal}\n{1 if death_link else 0}\n{death_link_percent}\n", encoding="ascii")
    os.replace(temp, target)


def read_progress(folder, session):
    path = folder / (session + ".progress")
    if not path.exists():
        return 0
    lines = path.read_text(encoding="ascii").splitlines()
    if len(lines) not in (5, 6) or lines[0] not in ("4", "5") or lines[1] != session:
        raise ValueError("Invalid or mismatched progress file")
    low, high = int(lines[2]), int(lines[3])
    if low < 0 or low >= (1 << 64) or high < 0:
        raise ValueError("Invalid check mask")
    mask = low | (high << 64)
    if not 0 <= mask <= CHECK_MASK:
        raise ValueError("Invalid check mask")
    return mask


def outgoing(check_mask, server_checked, location_count):
    checks = [LOCATION_BASE + i for i in range(location_count)
              if check_mask & (1 << i) and LOCATION_BASE + i not in server_checked]
    return checks


def read_goal(folder, session):
    path = folder / (session + ".goal")
    if not path.exists():
        return False
    return path.read_text(encoding="ascii").splitlines() == ["1", session]


def write_deathlink(folder, session):
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / (session + ".deathlink")
    temp = folder / (session + ".deathlink.tmp")
    temp.write_text(f"1\n{session}\n", encoding="ascii")
    os.replace(temp, target)


def consume_death(folder, session):
    path = folder / (session + ".death")
    if not path.exists():
        return False
    try:
        valid = path.read_text(encoding="ascii").splitlines() == ["1", session]
    finally:
        path.unlink(missing_ok=True)
    return valid
