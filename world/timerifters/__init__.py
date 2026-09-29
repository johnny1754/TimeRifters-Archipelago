from BaseClasses import Item, ItemClassification, Location, Region
from Options import OptionError
from worlds.AutoWorld import World
from worlds.LauncherComponents import Component, Type, components, launch_subprocess

from .options import TimeRiftersOptions

GAME = "Time Rifters"
ITEM_BASE, LOCATION_BASE = 9473100, 9473200
ARENAS = ("Greeble Box", "Arena Boss", "Long Bridge", "Holodeck", "Tram", "Cave", "Channel", "Long Caves", "Egypt Holodeck", "Wide Printer", "Donut Printer", "Water", "Tree", "Jungle Holodeck", "Cylinder")
EPISODES = ("Episode 1", "Episode 2", "Episode 3")
ITEMS = {"Flak Cannon": ITEM_BASE, "Plasma Beam": ITEM_BASE + 1, "Particle Ball": ITEM_BASE + 2,
         "Rocket Launcher": ITEM_BASE + 3, "Spread Rifle": ITEM_BASE + 4, "Time Echo": ITEM_BASE + 5,
         "Episode 2 Key": ITEM_BASE + 6, "Episode 3 Key": ITEM_BASE + 7}
WEAPON_POWER_UPS = (
    ("Scatter Pistol", ("Scatter", "Plasma", "Rapid", "Reflect", "Acid")),
    ("Flak Cannon", ("Flak", "Focus", "Rapid", "Reflect", "Acid")),
    ("Plasma Beam", ("Wave", "Beam", "Punch", "Acid")),
    ("Particle Ball", ("Proton", "Electron", "Tether", "Collider", "Acid")),
    ("Rocket Launcher", ("Rocket", "Speed", "Rapid", "To The Moon", "Acid")),
    ("Spread Rifle", ("Spread", "Focus", "Rapid", "Punch", "Acid")),
)
GLOBAL_POWER_UPS = ("Projectile", "Damage", "Rapid", "Punch", "Special", "Acid")
GLOBAL_POWER_UP_MEMBERS = {
    "Projectile": ("Scatter", "Flak", "Wave", "Proton", "Spread"),
    "Damage": ("Plasma", "Focus", "Beam", "Electron", "Rocket"),
    "Rapid": ("Rapid", "Collider"),
    "Punch": ("Punch", "Speed"),
    "Special": ("Reflect", "Tether", "To The Moon"),
    "Acid": ("Acid",),
}
for index, power_up in enumerate(GLOBAL_POWER_UPS): ITEMS[power_up + " Upgrade"] = ITEM_BASE + 8 + index
PER_WEAPON_POWER_UPS = tuple((weapon, power_up) for weapon, power_ups in WEAPON_POWER_UPS for power_up in power_ups)
for index, (weapon, power_up) in enumerate(PER_WEAPON_POWER_UPS): ITEMS[weapon + " - " + power_up + " Upgrade"] = ITEM_BASE + 8 + len(GLOBAL_POWER_UPS) + index
def milestones_for(count):
    return tuple((100 * (index + 1) + count - 1) // count for index in range(count))


def location_tables(count):
    milestones = milestones_for(count)
    core = {arena + " - " + str(percent) + "% Clear": LOCATION_BASE + index * 101 + percent
            for index, arena in enumerate(ARENAS) for milestone, percent in enumerate(milestones)}
    core.update({episode + " Complete": LOCATION_BASE + len(ARENAS) * 101 + index for index, episode in enumerate(EPISODES)})
    escapes = {arena + " - Hidden Escape": LOCATION_BASE + len(ARENAS) * 101 + len(EPISODES) + index
               for index, arena in enumerate(ARENAS)}
    escapes["Title Screen - Hidden Escape"] = LOCATION_BASE + len(ARENAS) * 101 + len(EPISODES) + len(ARENAS)
    return milestones, core, escapes


MILESTONES, CORE_LOCATIONS, ESCAPE_LOCATIONS = location_tables(4)
ALL_PERCENTS = tuple(sorted({percent for count in range(4, 21) for percent in milestones_for(count)}))
LOCATIONS = {arena + " - " + str(percent) + "% Clear": LOCATION_BASE + index * 101 + percent
             for index, arena in enumerate(ARENAS) for percent in ALL_PERCENTS}
LOCATIONS.update({episode + " Complete": LOCATION_BASE + len(ARENAS) * 101 + index for index, episode in enumerate(EPISODES)})
LOCATIONS.update(ESCAPE_LOCATIONS)


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
        self.milestone_count = self.options.arena_percentage_checks.value
        self.milestones, self.core_locations, self.escape_locations = location_tables(self.milestone_count)
        self.location_name_to_id = {**self.core_locations, **self.escape_locations}
        self.upgrade_mode = self.options.upgrade_mode.value
        self.power_up_item_count = (len(GLOBAL_POWER_UPS) if self.upgrade_mode == 1
                                    else len(PER_WEAPON_POWER_UPS) if self.upgrade_mode == 2 else 0)
        location_count = len(self.core_locations) + (len(self.escape_locations) if self.options.escape_checks.value else 0)
        non_echo_items = (7 if self.options.episode_keys.value else 5) + self.power_up_item_count
        maximum_echoes = location_count - non_echo_items
        requested_echoes = self.options.required_time_echoes.value
        if requested_echoes > maximum_echoes:
            escape_note = "Enable hidden escape checks for 16 more checks. " if not self.options.escape_checks.value else "Hidden escape checks are already enabled. "
            raise OptionError(
                f"Required Time Echoes is set to {requested_echoes}, but this configuration has room for only "
                f"{maximum_echoes} Time Echoes. {escape_note}Choose a lower Echo amount or add more percentage checks "
                "before generating this seed."
            )
        if self.options.episode_keys.value:
            # Episode 1 is the player's opening playground.  Guarantee that
            # its successor key is distributed in an early reachable sphere
            # rather than allowing a long, all-Episode-1 opening.
            self.multiworld.early_items[self.player]["Episode 2 Key"] = 1
        if self.upgrade_mode:
            # A received power-up is harmless before its weapon arrives, but
            # forcing all weapon unlocks into the early reachable pool keeps
            # the item modes from creating a long unusable-upgrade opening.
            for weapon in tuple(ITEMS)[:5]:
                self.multiworld.early_items[self.player][weapon] = 1
        # This is Time Rifters' normal episode layout, expressed as the
        # game's arena IDs.  ARENAS itself is in ID/name order.
        self.arena_order = [0, 5, 6, 3, 11, 7, 2, 4, 13, 12, 14, 9, 8, 10, 1]
        if self.options.arena_shuffle.value:
            # Arena Boss is the game's final boss. Keep it as Episode 3, Arena 5
            # while allowing every other playable arena to move freely.
            slot_data = getattr(self.multiworld, "re_gen_passthrough", {}).get(self.game)
            if slot_data and "arena_order" in slot_data:
                # Universal Tracker regenerates the world from server slot data.
                # Reuse the original shuffle instead of making a new one.
                self.arena_order = [int(arena) for arena in slot_data["arena_order"]]
            else:
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
            for milestone in self.milestones:
                name = arena + " - " + str(milestone) + "% Clear"
                region.locations.append(RiftersLocation(self.player, name, self.core_locations[name], region))
            if self.options.escape_checks.value:
                name = arena + " - Hidden Escape"
                region.locations.append(RiftersLocation(self.player, name, self.escape_locations[name], region))
        for index, episode in enumerate(EPISODES):
            name = episode + " Complete"
            region = episode_regions[index]
            region.locations.append(RiftersLocation(self.player, name, self.core_locations[name], region))
        if self.options.escape_checks.value:
            episode_regions[0].locations.append(RiftersLocation(self.player, "Title Screen - Hidden Escape",
                                                                 self.escape_locations["Title Screen - Hidden Escape"], episode_regions[0]))
        victory = RiftersLocation(self.player, "Time Rifters Goal", None, menu)
        required = ("Flak Cannon", "Plasma Beam", "Particle Ball", "Rocket Launcher", "Spread Rifle")
        if self.options.episode_keys.value:
            required += ("Episode 2 Key", "Episode 3 Key")
        # Acid is a real progression gate in the optional power-up modes.
        # Making it part of the completion rule lets Archipelago place it in
        # progression spheres instead of treating it as an optional extra.
        if self.upgrade_mode == 1:
            required += ("Acid Upgrade",)
        elif self.upgrade_mode == 2:
            required += tuple(weapon + " - Acid Upgrade" for weapon, power_ups in WEAPON_POWER_UPS
                              if "Acid" in power_ups)
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
        if self.upgrade_mode == 1:
            self.multiworld.itempool += [self.create_item(name + " Upgrade", ItemClassification.progression if name == "Acid" else ItemClassification.useful) for name in GLOBAL_POWER_UPS]
        elif self.upgrade_mode == 2:
            self.multiworld.itempool += [self.create_item(weapon + " - " + name + " Upgrade", ItemClassification.progression if name == "Acid" else ItemClassification.useful) for weapon, name in PER_WEAPON_POWER_UPS]
        location_count = len(self.core_locations) + (len(self.escape_locations) if self.options.escape_checks.value else 0)
        echo_count = location_count - ((7 if self.options.episode_keys.value else 5) + self.power_up_item_count)
        required_echoes = self.options.required_time_echoes.value
        self.multiworld.itempool += [self.create_item("Time Echo", ItemClassification.progression) for _ in range(required_echoes)]
        self.multiworld.itempool += [self.create_item("Time Echo") for _ in range(echo_count - required_echoes)]

    def get_filler_item_name(self):
        return "Time Echo"

    def fill_slot_data(self):
        enabled = bool(self.options.escape_checks.value)
        return {"protocol": 19, "feature_set": "power_up_items", "item_base": ITEM_BASE,
                "location_base": LOCATION_BASE, "location_count": len(self.core_locations) + (len(self.escape_locations) if enabled else 0),
                "escape_checks": enabled, "episode_keys": bool(self.options.episode_keys.value),
                "upgrade_mode": self.upgrade_mode,
                "arena_shuffle": bool(self.options.arena_shuffle.value), "arena_order": self.arena_order,
                "required_time_echoes": self.options.required_time_echoes.value,
                "goal": self.options.goal.value,
                "death_link": bool(self.options.death_link.value),
                "death_link_percent": self.options.death_link_percent.value,
                "death_link_duration": self.options.death_link_duration.value,
                "arena_percentage_checks": self.milestone_count}

    @staticmethod
    def interpret_slot_data(slot_data):
        """Return server slot data so Universal Tracker can regenerate shuffled arenas exactly."""
        return slot_data
