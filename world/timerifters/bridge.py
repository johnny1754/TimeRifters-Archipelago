"""Versioned local bridge. Never stores server passwords."""
import hashlib
import json
import os
from pathlib import Path
import time

ITEM_BASE = 9473100
TIME_ECHO_ID = ITEM_BASE + 5
POWER_UP_START = ITEM_BASE + 8
POWER_UP_COUNT = 35
LOCATION_BASE = 9473200


def default_directory():
    return Path(os.environ.get("LOCALAPPDATA", str(Path.home() / ".local/share"))) / "TimeRiftersArchipelago"


def session_key(seed, team, slot):
    return hashlib.sha256(json.dumps([seed, team, slot], separators=(",", ":")).encode()).hexdigest()


def location_ids(milestone_count, escape_checks):
    percentages = [(100 * (index + 1) + milestone_count - 1) // milestone_count
                   for index in range(milestone_count)]
    values = [LOCATION_BASE + arena * 101 + percent for arena in range(15) for percent in percentages]
    values += [LOCATION_BASE + 15 * 101 + episode for episode in range(3)]
    if escape_checks:
        values += [LOCATION_BASE + 15 * 101 + 3 + arena for arena in range(15)]
        values += [LOCATION_BASE + 15 * 101 + 18]
    return values


def relative_locations(locations, ids):
    lookup = {location: index for index, location in enumerate(ids)}
    return {lookup[location] for location in locations if location in lookup}


def item_state(items):
    values = list(items)
    known = set(values)
    progression = sum(1 << i for i in range(5) if ITEM_BASE + i in known)
    progression |= 32 if ITEM_BASE + 6 in known else 0
    progression |= 64 if ITEM_BASE + 7 in known else 0
    power_ups = sum(1 << (value - POWER_UP_START) for value in known
                    if POWER_UP_START <= value < POWER_UP_START + POWER_UP_COUNT)
    return progression, values.count(TIME_ECHO_ID), power_ups


def write_snapshot(folder, session, weapons, echoes, power_ups, checks, escape_checks, episode_keys, upgrade_mode, arena_shuffle, arena_order,
                   required_echoes, goal, death_link, death_link_percent, death_link_duration, milestone_count, location_count):
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / "server.txt"
    temp = folder / "server.txt.tmp"
    order = ",".join(str(value) for value in arena_order)
    check_text = ",".join(str(value) for value in sorted(checks))
    temp.write_text(f"19\n{session}\n{int(time.time())}\n{weapons}\n{echoes}\n{power_ups}\n{check_text}\n{1 if escape_checks else 0}\n{1 if episode_keys else 0}\n{upgrade_mode}\n{1 if arena_shuffle else 0}\n{order}\n{required_echoes}\n{goal}\n{1 if death_link else 0}\n{death_link_percent}\n{death_link_duration}\n{milestone_count}\n{location_count}\n", encoding="ascii")
    os.replace(temp, target)


def write_notification(folder, session, message):
    """Write the newest server-confirmed item result for the game overlay."""
    text = " ".join(str(message).split())
    if not text:
        return
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / "notification.txt"
    temp = folder / "notification.txt.tmp"
    temp.write_text(f"1\n{session}\n{time.time_ns()}\n{text}\n", encoding="utf-8")
    os.replace(temp, target)


def read_progress(folder, session):
    path = folder / (session + ".progress")
    if not path.exists():
        return set()
    lines = path.read_text(encoding="ascii").splitlines()
    if len(lines) != 5 or lines[0] != "6" or lines[1] != session:
        raise ValueError("Invalid or mismatched progress file")
    values = set()
    if lines[2]:
        for value in lines[2].split(","):
            value = int(value)
            if value < 0 or value in values:
                raise ValueError("Invalid check list")
            values.add(value)
    return values


def outgoing(checks, server_checked, ids):
    return [ids[value] for value in checks if 0 <= value < len(ids) and ids[value] not in server_checked]


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
