"""Add reversible local-mode labels to the installed Arena localization database."""
import argparse
import json
import sqlite3
from contextlib import closing
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def install(database, labels, backup):
    backup = Path(backup)
    with closing(sqlite3.connect(database, timeout=3)) as db, db:
        columns = [c[1] for c in db.execute('PRAGMA table_info(Loc)')]
        expected = ['Key', 'Bundle', 'enUS', 'ptBR', 'frFR', 'itIT', 'deDE', 'esES', 'jaJP', 'koKR']
        if columns != expected:
            raise ValueError('Arena localization schema changed; labels were not modified')
        if not backup.exists():
            original = [list(row) for key in labels for row in db.execute('SELECT * FROM Loc WHERE Key=?', (key,))]
            backup.parent.mkdir(parents=True, exist_ok=True)
            backup.write_text(json.dumps({'database': str(Path(database).resolve()), 'keys': list(labels), 'originalRows': original}, indent=2), encoding='utf8')
        for key, text in labels.items():
            db.execute('INSERT OR REPLACE INTO Loc VALUES (?,?,?,?,?,?,?,?,?,?)', (key, 'ArenaLab', *([text] * 8)))


def restore(backup):
    data = json.loads(Path(backup).read_text(encoding='utf8'))
    with closing(sqlite3.connect(data['database'], timeout=3)) as db, db:
        for key in data['keys']:
            db.execute('DELETE FROM Loc WHERE Key=?', (key,))
        db.executemany('INSERT OR REPLACE INTO Loc VALUES (?,?,?,?,?,?,?,?,?,?)', data['originalRows'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--restore', action='store_true')
    opts = parser.parse_args()
    state = json.loads((ROOT / 'data/local-client/configuration-state.json').read_text(encoding='utf-8-sig'))
    databases = sorted((Path(state['installation']) / 'MTGA_Data/Downloads/Raw').glob('Raw_ClientLocalization_*.mtga'))
    if len(databases) != 1:
        raise ValueError('Expected exactly one active Arena localization database')
    backup = ROOT / 'data/local-client' / (databases[0].stem + '.labels-before.json')
    if opts.restore:
        if backup.exists():
            restore(backup)
        print('Local mode labels restored.')
    else:
        install(databases[0], json.loads((ROOT / 'data/mode-labels.json').read_text(encoding='utf8')), backup)
        print('Installed game-mode names, deck-size labels and Commander format names. Restart Arena to load them.')
