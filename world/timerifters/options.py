from dataclasses import dataclass

from Options import Choice, PerGameCommonOptions, Range, Toggle


class EscapeChecks(Toggle):
    """Include the 15 hidden arena escapes and the Title Screen hidden escape."""
    display_name = "Enable hidden escape checks"
    default = 0


class EpisodeKeys(Toggle):
    """Require Archipelago items before Episodes 2 and 3 can be started."""
    display_name = "Require episode keys"
    default = 1


class ArenaShuffle(Toggle):
    """Shuffle all 15 arenas between episode groups; Episode 1 always has five playable arenas."""
    display_name = "Shuffle arenas between episodes"
    default = 0


class ArenaPercentageChecks(Range):
    """Checks per arena, evenly spaced through 100%. Higher values add checks and room for Time Echoes."""
    display_name = "Arena percentage checks"
    range_start = 4
    range_end = 20
    default = 5


class RequiredTimeEchoes(Range):
    """Shop credits guaranteed as progression. Raise checks if the selected number will not fit."""
    display_name = "Required Time Echoes"
    range_start = 25
    range_end = 84
    default = 50


class UpgradeMode(Choice):
    """Off keeps every shop power-up normal. Categories uses six shared items; per weapon uses one item for each real weapon power-up."""
    display_name = "Shop power-up item mode"
    option_off = 0
    option_power_up_categories = 1
    option_per_weapon_power_ups = 2
    default = 0


class Goal(Choice):
    """Completion target, based on best destruction percent rather than checks."""
    display_name = "Goal"
    option_75_percent = 75
    option_95_percent = 95
    option_99_percent = 99
    option_100_percent = 100
    option_final_boss_100_percent = 101
    default = 95


class DeathLink(Toggle):
    """Send a DeathLink after finishing an arena below the selected percent; received links disable firing for one minute."""
    display_name = "Enable DeathLink"
    default = 0


class DeathLinkPercent(Range):
    """Completed arenas below this destruction percent send a DeathLink."""
    display_name = "DeathLink percent"
    range_start = 1
    range_end = 100
    default = 50


class DeathLinkDuration(Range):
    """Seconds that firing is disabled after receiving a DeathLink."""
    display_name = "DeathLink firing-lock duration"
    range_start = 30
    range_end = 120
    default = 60


@dataclass
class TimeRiftersOptions(PerGameCommonOptions):
    escape_checks: EscapeChecks
    episode_keys: EpisodeKeys
    arena_shuffle: ArenaShuffle
    arena_percentage_checks: ArenaPercentageChecks
    required_time_echoes: RequiredTimeEchoes
    upgrade_mode: UpgradeMode
    goal: Goal
    death_link: DeathLink
    death_link_percent: DeathLinkPercent
    death_link_duration: DeathLinkDuration
