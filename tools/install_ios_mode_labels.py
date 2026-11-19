"""Add the server's game-mode labels to iPhone Arena's localization database over USB.

Same labels and rules as tools/install_mode_labels.py (PC). The phone's
original file is copied to ignored data/ios/localization/ once, edited locally,
then uploaded in place of the original. --restore uploads that original back.
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

from install_mode_labels import install

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / 'data/ios/localization'
RAW = '/Documents/Downloads/Raw'


async def _run(restore):
    devices = [device for device in await list_devices() if device.is_usb]
    if len(devices) != 1:
        raise RuntimeError('Connect exactly one unlocked, already-trusted iPhone by USB')
    async with await create_using_usbmux(serial=devices[0].serial, connection_type='USB', autopair=False) as lockdown:
        async with await HouseArrestService.create(lockdown, 'com.wizards.mtga', documents_only=True) as afc:
            names = [n for n in await afc.listdir(RAW) if n.startswith('Raw_ClientLocalization_') and n.endswith('.mtga')]
            if len(names) != 1:
                raise RuntimeError('Expected exactly one localization database on the phone, found %d' % len(names))
            name = names[0]
            original = STATE / ('original-' + name)
            if restore:
                if not original.exists():
                    raise RuntimeError('No saved original for ' + name)
                await afc.set_file_contents(RAW + '/' + name, original.read_bytes())
                return 'Restored the original localization on the phone.'
            STATE.mkdir(parents=True, exist_ok=True)
            if not original.exists():
                data = await afc.get_file_contents(RAW + '/' + name)
                with closing(sqlite3.connect(':memory:')) as probe:
                    probe.deserialize(data)
                    if probe.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                        raise RuntimeError('Phone localization database failed its integrity check')
                original.write_bytes(data)
            staged = STATE / ('labelled-' + name)
            shutil.copyfile(original, staged)
            labels = json.loads((ROOT / 'data/mode-labels.json').read_text(encoding='utf8'))
            install(staged, labels, STATE / (Path(name).stem + '.labels-before.json'))
            with closing(sqlite3.connect(staged)) as db:
                if db.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                    raise RuntimeError('Edited localization failed its integrity check; phone unchanged')
            data = staged.read_bytes()
            await afc.set_file_contents(RAW + '/' + name, data)
            if await afc.get_file_contents(RAW + '/' + name) != data:
                raise RuntimeError('Upload did not verify')
            return 'Installed %d game-mode labels on the phone. Reopen Arena.' % len(labels)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--restore', action='store_true')
    args = parser.parse_args()
    try:
        print(asyncio.run(asyncio.wait_for(_run(args.restore), timeout=300)))
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
