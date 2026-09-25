import asyncio
import logging
import socket

from CommonClient import CommonContext, server_loop, gui_enabled, get_base_parser
from NetUtils import ClientStatus
from .bridge import default_directory, session_key, item_state, mask_for_locations, write_snapshot, read_progress, outgoing

logger = logging.getLogger("Client")


class RiftersContext(CommonContext):
    game = "Time Rifters Prototype"
    items_handling = 0b111

    def __init__(self, server_address=None, password=None):
        super().__init__(server_address, password)
        self.session = None
        self.protocol_ok = False
        self.folder = default_directory()
        self.sent_goal = False
        self.room_seed = None

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
            self.protocol_ok = (data.get("protocol") == 4 and data.get("prototype") == "upgrade_credits"
                                and data.get("item_base") == 9473100 and data.get("location_base") == 9473200
                                and data.get("location_count") == 60)
            self.sent_goal = False
            seed = self.room_seed or getattr(self, "server_seed_name", None) or self.seed_name or "pending-room-info"
            self.session = session_key(seed, self.team, self.slot)
            if self.protocol_ok:
                logger.info("Time Rifters slot connected. Game bridge is ready.")
            else:
                logger.error("Wrong slot data. Generate a fresh v0.5 seed with this APWorld.")

    def reset_server_state(self):
        self.protocol_ok = False
        super().reset_server_state()

    async def watch_bridge(self):
        last_error = None
        while not self.exit_event.is_set():
            try:
                if self.server and self.slot is not None and self.protocol_ok:
                    weapons, echoes = item_state(item.item for item in self.items_received)
                    server_mask = mask_for_locations(self.checked_locations)
                    write_snapshot(self.folder, self.session, weapons, echoes, server_mask)
                    local_mask = read_progress(self.folder, self.session)
                    checks, goal = outgoing(local_mask | server_mask, weapons, self.checked_locations)
                    if checks:
                        await self.send_msgs([{"cmd": "LocationChecks", "locations": checks}])
                    if goal and not self.sent_goal:
                        await self.send_msgs([{"cmd": "StatusUpdate", "status": ClientStatus.CLIENT_GOAL}])
                        self.sent_goal = True
                        logger.info("Prototype goal complete: all arena checks and all weapon unlocks received.")
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
        logger.error("Another prototype client is running, or local port 38297 is occupied. Close it first.")
        lock.close()
        return
    parser = get_base_parser(description="Time Rifters Archipelago prototype")
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
