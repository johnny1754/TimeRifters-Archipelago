from BaseClasses import Item, ItemClassification, Location, Region
from Options import PerGameCommonOptions
from worlds.AutoWorld import World
from worlds.LauncherComponents import Component, Type, components, launch_subprocess

GAME = "Time Rifters Prototype"
ITEM_BASE, LOCATION_BASE = 9473100, 9473200
ARENAS = ("Greeble Box", "Arena Boss", "Long Bridge", "Holodeck", "Tram", "Cave", "Channel", "Long Caves", "Egypt Holodeck", "Wide Printer", "Donut Printer", "Water", "Tree", "Jungle Holodeck", "Cylinder")
MILESTONES = (25, 50, 75, 100)
ITEMS = {"Flak Unlock": ITEM_BASE, "Lightning Unlock": ITEM_BASE + 1, "Alien Disk Unlock": ITEM_BASE + 2, "Rocket Launcher Unlock": ITEM_BASE + 3, "Rifle Unlock": ITEM_BASE + 4, "Time Echo": ITEM_BASE + 5}
LOCATIONS = {arena + " - " + str(percent) + "% Clear": LOCATION_BASE + index * 4 + milestone for index, arena in enumerate(ARENAS) for milestone, percent in enumerate(MILESTONES)}


def launch_client(*args):
    from .client import launch
    launch_subprocess(launch, name="TimeRiftersPrototypeClient", args=args)


components.append(Component("Time Rifters Prototype Client", func=launch_client, component_type=Type.CLIENT, game_name=GAME))


class RiftersItem(Item):
    game = GAME


class RiftersLocation(Location):
    game = GAME


class TimeRiftersPrototypeWorld(World):
    game = GAME
    options_dataclass = PerGameCommonOptions
    item_name_to_id = ITEMS
    location_name_to_id = LOCATIONS

    def create_regions(self):
        menu, campaign = Region("Menu", self.player, self.multiworld), Region("Time Rifters Campaign", self.player, self.multiworld)
        self.multiworld.regions += [menu, campaign]
        menu.connect(campaign)
        for name, address in LOCATIONS.items():
            campaign.locations.append(RiftersLocation(self.player, name, address, campaign))
        victory = RiftersLocation(self.player, "Prototype Goal", None, campaign)
        victory.access_rule = lambda state: state.has_all(tuple(ITEMS)[:5], self.player)
        victory.place_locked_item(RiftersItem("Victory", ItemClassification.progression, None, self.player))
        campaign.locations.append(victory)
        self.multiworld.completion_condition[self.player] = lambda state: state.has("Victory", self.player)

    def create_item(self, name):
        return RiftersItem(name, ItemClassification.filler if name == "Time Echo" else ItemClassification.progression, ITEMS[name], self.player)

    def create_items(self):
        self.multiworld.itempool += [self.create_item(name) for name in tuple(ITEMS)[:5]]
        self.multiworld.itempool += [self.create_item("Time Echo") for _ in range(len(LOCATIONS) - 5)]

    def get_filler_item_name(self):
        return "Time Echo"

    def fill_slot_data(self):
        return {"protocol": 4, "prototype": "upgrade_credits", "item_base": ITEM_BASE, "location_base": LOCATION_BASE, "location_count": len(LOCATIONS)}
