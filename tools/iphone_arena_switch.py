"""Switch the official iPhone Arena app between Wizards' servers and this server.

Arena on iOS loads /Documents/services.conf when present (confirmed
2026-10-27). "private" writes that file; "official" removes it. Only that one
file is touched, and only a file this tool wrote is replaced or removed. Uses
the phone's existing USB pairing; never pairs. Arena must be fully closed and
reopened after switching.
"""
import argparse
import asyncio
import json
import shutil
import subprocess

from pymobiledevice3.lockdown import create_using_usbmux
from pymobiledevice3.services.house_arrest import HouseArrestService
from pymobiledevice3.usbmux import list_devices

REMOTE = "/Documents/services.conf"
ENVIRONMENT = "ArcaneForge"


def configuration(host):
    base = f"http://{host}:8787"
    return {"$environments": [ENVIRONMENT], "environments": {ENVIRONMENT: {
        "name": ENVIRONMENT, "fdHost": host, "fdPort": 9445, "mdHost": host, "mdPort": 9444,
        "doorbellUri": base, "accountSystemBaseUri": base + "/",
        "accountSystemId": "arena-local", "accountSystemSecret": "local-only",
        "accountSystemEnvironment": 1, "HostPlatform": 1,
        "ecoUri": base + "/eco", "bikeUri": base + "/local-business-events"}}}


def tailscale_address():
    executable = shutil.which("tailscale") or r"C:\Program Files\Tailscale\tailscale.exe"
    try:
        result = subprocess.run([executable, "ip", "-4"], capture_output=True, text=True, timeout=10,
                                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        return result.stdout.split()[0] if result.returncode == 0 and result.stdout.split() else ""
    except (OSError, subprocess.SubprocessError):
        return ""


async def _session(action, host=None):
    devices = [device for device in await list_devices() if device.is_usb]
    if len(devices) != 1:
        raise RuntimeError("Connect exactly one unlocked, already-trusted iPhone by USB")
    async with await create_using_usbmux(serial=devices[0].serial, connection_type="USB", autopair=False) as lockdown:
        if not lockdown.paired:
            raise RuntimeError("Phone pairing unavailable; unlock it and trust this PC")
        async with await HouseArrestService.create(lockdown, "com.wizards.mtga", documents_only=True) as afc:
            current = None
            if "services.conf" in await afc.listdir("/Documents"):
                try:
                    current = json.loads(await afc.get_file_contents(REMOTE))
                except ValueError:
                    current = {}
            ours = isinstance(current, dict) and current.get("$environments") == [ENVIRONMENT]
            if current is not None and not ours and action != "status":
                raise RuntimeError("services.conf on the phone was not written by this tool; left unchanged")
            if action == "private":
                await afc.set_file_contents(REMOTE, json.dumps(configuration(host), indent=1).encode())
                current, ours = json.loads(await afc.get_file_contents(REMOTE)), True
            elif action == "official" and current is not None:
                await afc.rm(REMOTE)
                current = None if "services.conf" not in await afc.listdir("/Documents") else current
            if current is None:
                return "Official servers"
            if ours:
                return "Private server (" + current["environments"][ENVIRONMENT]["fdHost"] + ")"
            return "Unknown services.conf present"


def run(action, host=None):
    return asyncio.run(asyncio.wait_for(_session(action, host), timeout=45))


def describe(error):
    # Library exceptions may contain device identifiers; show only our own messages.
    return str(error) if isinstance(error, RuntimeError) else "Phone not reachable (" + type(error).__name__ + ")"


def window():
    import tkinter as tk

    root = tk.Tk()
    root.title("iPhone Arena server")
    root.resizable(False, False)
    frame = tk.Frame(root, padx=16, pady=14)
    frame.pack()
    status = tk.StringVar(value="Checking phone...")
    tk.Label(frame, text="Current mode:").grid(row=0, column=0, sticky="w")
    tk.Label(frame, textvariable=status, font=("Segoe UI", 11, "bold")).grid(row=0, column=1, columnspan=2, sticky="w")
    tk.Label(frame, text="Server address:").grid(row=1, column=0, sticky="w", pady=(10, 0))
    host = tk.Entry(frame, width=22)
    host.insert(0, tailscale_address())
    host.grid(row=1, column=1, columnspan=2, sticky="w", pady=(10, 0))

    def act(action):
        address = host.get().strip()
        if action == "private" and not address:
            status.set("Enter the server address first")
            return
        status.set("Working...")
        root.update_idletasks()
        try:
            status.set(run(action, address) + " - now fully close and reopen Arena" if action != "status" else run(action))
        except Exception as error:
            status.set(describe(error))

    buttons = [("Private server", "private"), ("Official servers", "official"), ("Refresh", "status")]
    for column, (label, action) in enumerate(buttons):
        tk.Button(frame, text=label, width=16, command=lambda a=action: act(a)).grid(row=2, column=column, padx=4, pady=(14, 0))
    root.after(100, lambda: act("status"))
    root.mainloop()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", nargs="?", choices=["status", "private", "official"])
    parser.add_argument("--host", help="Server address for 'private' (default: this PC's Tailscale address)")
    args = parser.parse_args()
    if args.action is None:
        return window()
    host = args.host or tailscale_address()
    if args.action == "private" and not host:
        parser.error("No Tailscale address found; pass --host")
    try:
        print(run(args.action, host))
    except Exception as error:
        print(describe(error))
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
