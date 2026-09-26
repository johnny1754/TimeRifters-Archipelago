from dataclasses import dataclass

from BaseClasses import Item, ItemClassification, Location, Region
from Options import Choice, OptionError, PerGameCommonOptions, Range, Toggle
from worlds.AutoWorld import World
from worlds.LauncherComponents import Component, Type, components, launch_subprocess

GAME = "Time Rifters"
ITEM_BASE, LOCATION_BASE = 9473100, 9473200
ARENAS = ("Greeble Box", "Arena Boss", "Long Bridge", "Holodeck", "Tram", "Cave", "Channel", "Long Caves", "Egypt Holodeck", "Wide Printer", "Donut Printer", "Water", "Tree", "Jungle Holodeck", "Cylinder")
MILESTONES = (25, 50, 75, 100)
EPISODES = ("Episode 1", "Episode 2", "Episode 3")
ITEMS = {"Flak Cannon": ITEM_BASE, "Plasma Beam": ITEM_BASE + 1, "Particle Ball": ITEM_BASE + 2,
         "Rocket Launcher": ITEM_BASE + 3, "Spread Rifle": ITEM_BASE + 4, "Time Echo": ITEM_BASE + 5,
         "Episode 2 Key": ITEM_BASE + 6, "Episode 3 Key": ITEM_BASE + 7}
CORE_LOCATIONS = {arena + " - " + str(percent) + "% Clear": LOCATION_BASE + index * 4 + milestone for index, arena in enumerate(ARENAS) for milestone, percent in enumerate(MILESTONES)}
CORE_LOCATIONS.update({episode + " Complete": LOCATION_BASE + len(ARENAS) * len(MILESTONES) + index for index, episode in enumerate(EPISODES)})
ESCAPE_LOCATIONS = {arena + " - Hidden Escape": LOCATION_BASE + 63 + index for index, arena in enumerate(ARENAS)}
ESCAPE_LOCATIONS["Title Screen - Hidden Escape"] = LOCATION_BASE + 78
LOCATIONS = dict(CORE_LOCATIONS)
LOCATIONS.update(ESCAPE_LOCATIONS)


class EscapeChecks(Toggle):
    """Include the 15 hidden arena escapes and the Title Screen hidden escape."""
    display_name = "Enable hidden escape checks"
    default = 1


class EpisodeKeys(Toggle):
    """Require Archipelago items before Episodes 2 and 3 can be started."""
    display_name = "Require episode keys"
    default = 1


class ArenaShuffle(Toggle):
    """Shuffle all 15 arenas between episode groups; Episode 1 always has five playable arenas."""
    display_name = "Shuffle arenas between episodes"
    default = 0


class RequiredTimeEchoes(Range):
    """Shop credits guaranteed as progression. Raise checks if the selected number will not fit."""
    display_name = "Required Time Echoes"
    range_start = 25
    range_end = 84
    default = 50


class Goal(Choice):
    """Completion target, based on best destruction percent rather than checks."""
    display_name = "Goal"
    option_95_percent = 95
    option_99_percent = 99
    option_100_percent = 100
    option_final_boss_100_percent = 101
    default = 100


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


@dataclass
class TimeRiftersOptions(PerGameCommonOptions):
    escape_checks: EscapeChecks
    episode_keys: EpisodeKeys
    arena_shuffle: ArenaShuffle
    required_time_echoes: RequiredTimeEchoes
    goal: Goal
    death_link: DeathLink
    death_link_percent: DeathLinkPercent


def launch_client(*args):
    from .client import launch
    launch_subprocess(launch, name="TimeRiftersClient", args=args)


components.append(Component("Time Rifters Client", func=launch_client, component_type=Type.CLIENT, game_name=GAME))


class RiftersItem(Item):
    game = GAME


class RiftersLocation(Location):
    game = GAME


class TimeRiftersWorld(World):
    game = GAME
    options_dataclass = TimeRiftersOptions
    item_name_to_id = ITEMS
    location_name_to_id = LOCATIONS

    def generate_early(self):
        location_count = len(CORE_LOCATIONS) + (len(ESCAPE_LOCATIONS) if self.options.escape_checks.value else 0)
        non_echo_items = 7 if self.options.episode_keys.value else 5
        maximum_echoes = location_count - non_echo_items
        requested_echoes = self.options.required_time_echoes.value
        if requested_echoes > maximum_echoes:
            escape_note = "Enable hidden escape checks for 16 more checks. " if not self.options.escape_checks.value else "Hidden escape checks are already enabled. "
            raise OptionError(
                f"Required Time Echoes is set to {requested_echoes}, but this configuration has room for only "
                f"{maximum_echoes} Time Echoes. {escape_note}Choose a lower Echo amount or add more percentage checks "
                "before generating this seed."
            )
        # This is Time Rifters' normal episode layout, expressed as the
        # game's arena IDs.  ARENAS itself is in ID/name order.
        self.arena_order = [0, 5, 6, 3, 11, 7, 2, 4, 13, 12, 14, 9, 8, 10, 1]
        if self.options.arena_shuffle.value:
            # Arena Boss is the game's final boss. Keep it as Episode 3, Arena 5
            # while allowing every other playable arena to move freely.
            shuffled_arenas = [arena for arena in range(len(ARENAS)) if arena != 1]
            self.random.shuffle(shuffled_arenas)
            self.arena_order = shuffled_arenas + [1]

    def create_regions(self):
        menu = Region("Menu", self.player, self.multiworld)
        episode_regions = [Region(episode, self.player, self.multiworld) for episode in EPISODES]
        self.multiworld.regions += [menu] + episode_regions
        menu.connect(episode_regions[0])
        if self.options.episode_keys.value:
            menu.connect(episode_regions[1], rule=lambda state: state.has("Episode 2 Key", self.player))
            menu.connect(episode_regions[2], rule=lambda state: state.has("Episode 3 Key", self.player))
        else:
            menu.connect(episode_regions[1])
            menu.connect(episode_regions[2])
        for slot, arena_index in enumerate(self.arena_order):
            region = episode_regions[slot // 5]
            arena = ARENAS[arena_index]
            for milestone in MILESTONES:
                name = arena + " - " + str(milestone) + "% Clear"
                region.locations.append(RiftersLocation(self.player, name, CORE_LOCATIONS[name], region))
            if self.options.escape_checks.value:
                name = arena + " - Hidden Escape"
                region.locations.append(RiftersLocation(self.player, name, ESCAPE_LOCATIONS[name], region))
        for index, episode in enumerate(EPISODES):
            name = episode + " Complete"
            region = episode_regions[index]
            region.locations.append(RiftersLocation(self.player, name, CORE_LOCATIONS[name], region))
        if self.options.escape_checks.value:
            episode_regions[0].locations.append(RiftersLocation(self.player, "Title Screen - Hidden Escape",
                                                                 LOCATION_BASE + 78, episode_regions[0]))
        victory = RiftersLocation(self.player, "Time Rifters Goal", None, menu)
        required = ("Flak Cannon", "Plasma Beam", "Particle Ball", "Rocket Launcher", "Spread Rifle")
        if self.options.episode_keys.value:
            required += ("Episode 2 Key", "Episode 3 Key")
        required_echoes = self.options.required_time_echoes.value
        victory.access_rule = lambda state: state.has_all(required, self.player) and state.has("Time Echo", self.player, required_echoes)
        victory.place_locked_item(RiftersItem("Victory", ItemClassification.progression, None, self.player))
        menu.locations.append(victory)
        self.multiworld.completion_condition[self.player] = lambda state: state.has("Victory", self.player)

    def create_item(self, name, classification=None):
        if classification is None:
            classification = ItemClassification.filler if name == "Time Echo" else ItemClassification.progression
        return RiftersItem(name, classification, ITEMS[name], self.player)

    def create_items(self):
        self.multiworld.itempool += [self.create_item(name) for name in tuple(ITEMS)[:5]]
        if self.options.episode_keys.value:
            self.multiworld.itempool += [self.create_item("Episode 2 Key"), self.create_item("Episode 3 Key")]
        location_count = len(CORE_LOCATIONS) + (len(ESCAPE_LOCATIONS) if self.options.escape_checks.value else 0)
        progression_count = 7 if self.options.episode_keys.value else 5
        echo_count = location_count - progression_count
        required_echoes = self.options.required_time_echoes.value
        self.multiworld.itempool += [self.create_item("Time Echo", ItemClassification.progression) for _ in range(required_echoes)]
        self.multiworld.itempool += [self.create_item("Time Echo") for _ in range(echo_count - required_echoes)]

    def get_filler_item_name(self):
        return "Time Echo"

    def fill_slot_data(self):
        enabled = bool(self.options.escape_checks.value)
        return {"protocol": 14, "feature_set": "death_link_percent", "item_base": ITEM_BASE,
                "location_base": LOCATION_BASE, "location_count": len(CORE_LOCATIONS) + (len(ESCAPE_LOCATIONS) if enabled else 0),
                "escape_checks": enabled, "episode_keys": bool(self.options.episode_keys.value),
                "arena_shuffle": bool(self.options.arena_shuffle.value), "arena_order": self.arena_order,
                "required_time_echoes": self.options.required_time_echoes.value,
                "goal": self.options.goal.value,
                "death_link": bool(self.options.death_link.value),
                "death_link_percent": self.options.death_link_percent.value}
