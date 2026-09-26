import asyncio
import logging
import socket

from CommonClient import CommonContext, server_loop, gui_enabled, get_base_parser
from NetUtils import ClientStatus
from .bridge import default_directory, session_key, item_state, location_ids, relative_locations, write_snapshot, read_progress, read_goal, write_deathlink, consume_death, outgoing

logger = logging.getLogger("Client")


class RiftersContext(CommonContext):
    game = "Time Rifters"
    items_handling = 0b111

    def __init__(self, server_address=None, password=None):
        super().__init__(server_address, password)
        self.session = None
        self.protocol_ok = False
        self.folder = default_directory()
        self.sent_goal = False
        self.room_seed = None
        self.escape_checks = False
        self.episode_keys = False
        self.arena_shuffle = False
        self.arena_order = list(range(15))
        self.required_time_echoes = 50
        self.goal = 100
        self.death_link = False
        self.death_link_percent = 50
        self.death_link_duration = 60
        self.arena_percentage_checks = 4
        self.pending_deathlink = False

    async def server_auth(self, password_requested=False):
        if password_requested and not self.password:
            await super().server_auth(password_requested)
        await self.get_username()
        await self.send_connect()

    def on_package(self, cmd, args):
        if cmd == "RoomInfo":
            self.room_seed = args.get("seed_name")
        elif cmd == "Connected":
            data = args.get("slot_data", {})
            self.escape_checks = bool(data.get("escape_checks"))
            self.episode_keys = bool(data.get("episode_keys"))
            self.arena_shuffle = bool(data.get("arena_shuffle"))
            self.required_time_echoes = data.get("required_time_echoes")
            self.goal = data.get("goal")
            self.death_link = bool(data.get("death_link"))
            self.death_link_percent = data.get("death_link_percent")
            self.death_link_duration = data.get("death_link_duration")
            self.arena_percentage_checks = data.get("arena_percentage_checks")
            raw_order = data.get("arena_order")
            valid_order = isinstance(raw_order, list) and len(raw_order) == 15 and sorted(raw_order) == list(range(15))
            if valid_order:
                self.arena_order = list(raw_order)
            expected_locations = 15 * self.arena_percentage_checks + 3 + (16 if self.escape_checks else 0) if isinstance(self.arena_percentage_checks, int) else -1
            self.protocol_ok = (data.get("protocol") == 17 and data.get("feature_set") == "death_link_duration"
                                and data.get("item_base") == 9473100 and data.get("location_base") == 9473200
                                and data.get("location_count") == expected_locations and valid_order
                                and isinstance(self.required_time_echoes, int) and 25 <= self.required_time_echoes <= 84
                                and self.goal in (95, 99, 100, 101)
                                and isinstance(self.death_link_percent, int) and 1 <= self.death_link_percent <= 100
                                and isinstance(self.death_link_duration, int) and 30 <= self.death_link_duration <= 120
                                and isinstance(self.arena_percentage_checks, int) and 4 <= self.arena_percentage_checks <= 20)
            self.sent_goal = False
            seed = self.room_seed or getattr(self, "server_seed_name", None) or self.seed_name or "pending-room-info"
            self.session = session_key(seed, self.team, self.slot)
            if self.protocol_ok:
                logger.info("Time Rifters slot connected. Game bridge is ready.")
            else:
                logger.error("Wrong slot data. Generate a fresh v1.0.0 seed with this APWorld.")

    def on_deathlink(self, data):
        self.pending_deathlink = True
        super().on_deathlink(data)

    def reset_server_state(self):
        self.protocol_ok = False
        super().reset_server_state()

    async def watch_bridge(self):
        last_error = None
        while not self.exit_event.is_set():
            try:
                if self.server and self.slot is not None and self.protocol_ok:
                    if self.death_link != ("DeathLink" in self.tags):
                        await self.update_death_link(self.death_link)
                    weapons, echoes = item_state(item.item for item in self.items_received)
                    location_count = 15 * self.arena_percentage_checks + 3 + (16 if self.escape_checks else 0)
                    ids = location_ids(self.arena_percentage_checks, self.escape_checks)
                    server_checks = relative_locations(self.checked_locations, ids)
                    write_snapshot(self.folder, self.session, weapons, echoes, server_checks, self.escape_checks,
                                   self.episode_keys, self.arena_shuffle, self.arena_order,
                                   self.required_time_echoes, self.goal, self.death_link, self.death_link_percent, self.death_link_duration,
                                   self.arena_percentage_checks, location_count)
                    if self.pending_deathlink:
                        write_deathlink(self.folder, self.session)
                        self.pending_deathlink = False
                        logger.info("DeathLink received: the game will lock firing for %s seconds during the next active arena.", self.death_link_duration)
                    if self.death_link and consume_death(self.folder, self.session):
                        await self.send_death("finished a Time Rifters arena below the DeathLink threshold")
                    local_checks = read_progress(self.folder, self.session)
                    checks = outgoing(local_checks | server_checks, self.checked_locations, ids)
                    if checks:
                        await self.send_msgs([{"cmd": "LocationChecks", "locations": checks}])
                    if read_goal(self.folder, self.session) and not self.sent_goal:
                        await self.send_msgs([{"cmd": "StatusUpdate", "status": ClientStatus.CLIENT_GOAL}])
                        self.sent_goal = True
                        logger.info("Time Rifters goal complete.")
                last_error = None
            except (OSError, ValueError) as error:
                if str(error) != last_error:
                    logger.error("Local bridge: %s", error)
                    last_error = str(error)
            await asyncio.sleep(1)


def launch(*argv):
    lock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        lock.bind(("127.0.0.1", 38297))
    except OSError:
        logger.error("Another Time Rifters client is running, or local port 38297 is occupied. Close it first.")
        lock.close()
        return
    parser = get_base_parser(description="Time Rifters Archipelago")
    parser.add_argument("--name", default=None)
    args = parser.parse_args(argv)

    async def main():
        ctx = RiftersContext(args.connect, args.password)
        ctx.auth = args.name
        ctx.server_task = asyncio.create_task(server_loop(ctx))
        watcher = asyncio.create_task(ctx.watch_bridge())
        if gui_enabled:
            ctx.run_gui()
        ctx.run_cli()
        try:
            await ctx.exit_event.wait()
        finally:
            watcher.cancel()
            await asyncio.gather(watcher, return_exceptions=True)
            await ctx.shutdown()
    try:
        asyncio.run(main())
    finally:
        lock.close()
