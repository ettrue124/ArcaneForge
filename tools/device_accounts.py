"""List or relink devices that sign in to the private server without a password.

  python tools/device_accounts.py                 list devices and their accounts
  python tools/device_accounts.py link ID NAME    make device ID (from the list) use account NAME

A new device gets its own "Player XXXXXX" account on first sign-in. Linking it
to your existing account keeps that account's decks and collection. Fully close
and reopen Arena after linking. Only the device's hashed token is stored.
"""
import argparse
from contextlib import closing
import datetime
from pathlib import Path
import sqlite3
import sys

DATABASE = Path(__file__).resolve().parents[1] / 'data' / 'server.sqlite3'


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('command', nargs='?', choices=['list', 'link'], default='list')
    parser.add_argument('device', nargs='?')
    parser.add_argument('account', nargs='?')
    args = parser.parse_args()
    with closing(sqlite3.connect(DATABASE, timeout=10)) as db, db:
        db.row_factory = sqlite3.Row
        if not db.execute("SELECT 1 FROM sqlite_master WHERE name='bootstrap_devices'").fetchone():
            print('No device has signed in yet. Start the server and open patched Arena first.')
            return 0
        if args.command == 'link':
            if not args.device or not args.account:
                parser.error('link needs a device ID and an account name')
            matches = db.execute('SELECT device_hash FROM bootstrap_devices WHERE device_hash LIKE ?',
                                 (args.device.lower() + '%',)).fetchall()
            if len(matches) != 1:
                print('Device ID not found or not unique; use the ID shown by the list.')
                return 1
            user = db.execute('SELECT id,username FROM users WHERE name_key=?', (args.account.strip().casefold(),)).fetchone()
            if not user:
                print('No account named ' + args.account)
                return 1
            db.execute('UPDATE bootstrap_devices SET user_id=? WHERE device_hash=?', (user['id'], matches[0]['device_hash']))
            print('Device ' + args.device + ' now signs in as ' + user['username'] + '. Fully close and reopen Arena.')
            return 0
        rows = db.execute('SELECT d.device_hash,u.username,d.last_seen FROM bootstrap_devices d JOIN users u ON u.id=d.user_id '
                          'ORDER BY d.last_seen DESC').fetchall()
        if not rows:
            print('No device has signed in yet.')
        for row in rows:
            seen = datetime.datetime.fromtimestamp(row['last_seen']).strftime('%Y-%m-%d %H:%M')
            # The first 8 characters of the stored hash are enough to pick a device; never print the token.
            print(f"{row['device_hash'][:8]}  {row['username']:<30} last seen {seen}")
    return 0


if __name__ == '__main__':
    sys.exit(main())
