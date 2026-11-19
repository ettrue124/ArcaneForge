"""Give iPhone Arena the server's imported cards (text only) over USB.

The phone's card database (Raw_CardDatabase_*.mtga, SQLite) is the official set;
PC Arena's copy is that set plus the cards and tokens this server imported. The
phone's original is saved once to ignored data/ios/card-database/, rows the
phone lacks are added from the PC copy (the PC copy wins where both differ, so
token links match the server's printings), and the result replaces the phone's
file. Imported cards have no iPhone artwork. --restore puts the original back.
Close Arena on the phone first. Uses the existing USB pairing; never pairs.
"""
import argparse
import asyncio
import json
import shutil
import sqlite3
from contextlib import closing
from pathlib import Path

from pymobiledevice3.lockdown import create_using_usbmux
from pymobiledevice3.services.house_arrest import HouseArrestService
from pymobiledevice3.usbmux import list_devices

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / 'data/ios/card-database'
RAW = '/Documents/Downloads/Raw'


def pc_database():
    state = json.loads((ROOT / 'data/local-client/configuration-state.json').read_text(encoding='utf-8-sig'))
    found = sorted((Path(state['installation']) / 'MTGA_Data/Downloads/Raw').glob('Raw_CardDatabase_*.mtga'))
    if len(found) != 1:
        raise RuntimeError('Expected exactly one PC card database')
    return found[0]


def merge(original, pc, staged):
    shutil.copyfile(original, staged)
    with closing(sqlite3.connect(staged.as_uri(), uri=True)) as db:
        db.execute('ATTACH DATABASE ? AS pc', (pc.as_uri() + '?mode=ro',))
        tables = [r[0] for r in db.execute("SELECT name FROM main.sqlite_master WHERE type='table'")]
        for table in tables:
            mine = db.execute('SELECT sql FROM main.sqlite_master WHERE name=?', (table,)).fetchone()
            theirs = db.execute('SELECT sql FROM pc.sqlite_master WHERE name=?', (table,)).fetchone()
            if mine != theirs:
                raise RuntimeError('Card database layout differs for ' + table + '; phone unchanged')
        added = {}
        with db:
            for table in tables:
                before = db.execute(f'SELECT count(*) FROM main."{table}"').fetchone()[0]
                db.execute(f'INSERT OR REPLACE INTO main."{table}" SELECT * FROM pc."{table}" EXCEPT SELECT * FROM main."{table}"')
                added[table] = db.execute(f'SELECT count(*) FROM main."{table}"').fetchone()[0] - before
        db.execute('DETACH DATABASE pc')
        if db.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
            raise RuntimeError('Combined card database failed its integrity check; phone unchanged')
        db.execute('VACUUM')
    return added


async def _run(restore):
    devices = [device for device in await list_devices() if device.is_usb]
    if len(devices) != 1:
        raise RuntimeError('Connect exactly one unlocked, already-trusted iPhone by USB')
    async with await create_using_usbmux(serial=devices[0].serial, connection_type='USB', autopair=False) as lockdown:
        async with await HouseArrestService.create(lockdown, 'com.wizards.mtga', documents_only=True) as afc:
            names = [n for n in await afc.listdir(RAW) if n.startswith('Raw_CardDatabase_') and n.endswith('.mtga')]
            if len(names) != 1:
                raise RuntimeError('Expected exactly one card database on the phone, found %d' % len(names))
            remote = RAW + '/' + names[0]
            original = STATE / ('original-' + names[0])
            if restore:
                if not original.exists():
                    raise RuntimeError('No saved original for ' + names[0])
                data = original.read_bytes()
                message = 'Restored the original card database on the phone.'
            else:
                STATE.mkdir(parents=True, exist_ok=True)
                if not original.exists():
                    data = await afc.get_file_contents(remote)
                    with closing(sqlite3.connect(':memory:')) as probe:
                        probe.deserialize(data)
                        if probe.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                            raise RuntimeError('Phone card database failed its integrity check')
                    original.write_bytes(data)
                staged = STATE / ('combined-' + names[0])
                added = merge(original, pc_database(), staged)
                data = staged.read_bytes()
                message = 'Installed: ' + ', '.join(f'{t} +{n}' for t, n in added.items() if n) + '. Reopen Arena.'
            await afc.set_file_contents(remote, data)
            if (await afc.stat(remote)).get('st_size') != len(data):
                raise RuntimeError('Upload size did not verify; run --restore')
            return message


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--restore', action='store_true')
    args = parser.parse_args()
    try:
        print(asyncio.run(asyncio.wait_for(_run(args.restore), timeout=1800)))
    except RuntimeError as error:
        print(error)
        return 1
    except Exception as error:
        # Library exceptions may contain device identifiers.
        print('Phone not reachable (' + type(error).__name__ + ')')
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
