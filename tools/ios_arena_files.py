"""Read Arena's shared Documents over USB using an existing device pairing.

This helper never writes to the phone or initiates pairing. Local log copies
can contain private information; keep the output under ignored data/ios/.
"""
import argparse
import asyncio
import json
from pathlib import Path, PurePosixPath

from pymobiledevice3.lockdown import create_using_usbmux
from pymobiledevice3.services.house_arrest import HouseArrestService
from pymobiledevice3.usbmux import list_devices


def documents_path(value):
    path = PurePosixPath(value)
    if not path.is_absolute() or ".." in path.parts or path.parts[:2] != ("/", "Documents"):
        raise argparse.ArgumentTypeError("Path must stay inside /Documents")
    return str(path)


async def inspect(args):
    devices = [device for device in await list_devices() if device.is_usb]
    if len(devices) != 1:
        raise RuntimeError("Connect exactly one unlocked, already-trusted iPhone over USB")
    async with await create_using_usbmux(
        serial=devices[0].serial, connection_type="USB", autopair=False
    ) as lockdown:
        if not lockdown.paired:
            raise RuntimeError("Existing pairing unavailable; unlock/trust the phone manually")
        async with await HouseArrestService.create(
            lockdown, "com.wizards.mtga", documents_only=True
        ) as afc:
            if args.command == "list":
                for name in sorted(await afc.listdir(args.path)):
                    if name in (".", "..") or "/" in name or "\\" in name:
                        continue
                    path = args.path.rstrip("/") + "/" + name
                    stat = await afc.stat(path)
                    print(json.dumps({"path": path, "kind": stat.get("st_ifmt"),
                                      "bytes": stat.get("st_size")}))
                return

            if args.command == "pull":
                limit = args.max_mib * 1024**2
                stat = await afc.stat(args.path)
                if stat.get("st_ifmt") != "S_IFREG" or not 0 <= stat["st_size"] <= limit:
                    raise RuntimeError("Pull requires a regular file within the selected size limit")
                if args.output.exists():
                    raise FileExistsError("Local destination already exists; preserved")
                content = await afc.get_file_contents(args.path)
                if len(content) > limit:
                    raise RuntimeError("File grew beyond the copy limit")
                args.output.parent.mkdir(parents=True, exist_ok=True)
                with args.output.open("xb") as handle:
                    handle.write(content)
                print(json.dumps({"file": str(args.output), "bytes": len(content)}))
                return

            remote = "/Documents/Logs/Logs"
            candidates = []
            for name in await afc.listdir(remote):
                if "/" in name or "\\" in name or not name.lower().endswith(".log"):
                    continue
                stat = await afc.stat(remote + "/" + name)
                if stat.get("st_ifmt") == "S_IFREG" and 0 < stat["st_size"] <= 8 * 1024**2:
                    candidates.append((stat["st_mtime"], name))
            args.output.mkdir(parents=True, exist_ok=True)
            for _, name in sorted(candidates, reverse=True)[:args.count]:
                content = await afc.get_file_contents(remote + "/" + name)
                if len(content) > 8 * 1024**2:
                    raise RuntimeError("Log grew beyond the local copy limit")
                destination = args.output / name
                if destination.exists():
                    print(json.dumps({"file": str(destination), "status": "already exists; preserved"}))
                    continue
                with destination.open("xb") as handle:
                    handle.write(content)
                print(json.dumps({"file": str(destination), "bytes": len(content)}))
            if not candidates:
                print("No regular Arena logs found within the copy size limit")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    listing = sub.add_parser("list")
    listing.add_argument("path", nargs="?", type=documents_path, default="/Documents")
    pull = sub.add_parser("pull", help="Copy one shared file locally without overwriting")
    pull.add_argument("path", type=documents_path)
    pull.add_argument("output", type=Path)
    pull.add_argument("--max-mib", type=int, choices=range(1, 513), default=64)
    logs = sub.add_parser("logs")
    logs.add_argument("--count", type=int, choices=range(1, 11), default=3)
    logs.add_argument("--output", type=Path,
                      default=Path(__file__).resolve().parents[1] / "data/ios/device-logs")
    args = parser.parse_args()
    try:
        asyncio.run(asyncio.wait_for(inspect(args), timeout=45))
    except Exception as error:
        # Exceptions may contain device identifiers or pairing details.
        print("Arena USB read failed:", type(error).__name__)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
