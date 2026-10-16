from contextlib import closing
import sqlite3
import tempfile
import unittest
from pathlib import Path
from tools.install_mode_labels import install, restore


class ModeLabelTests(unittest.TestCase):
    def test_install_and_restore_preserve_existing_localization(self):
        with tempfile.TemporaryDirectory() as tmp:
            database = Path(tmp) / 'loc.sqlite'
            backup = Path(tmp) / 'before.json'
            with closing(sqlite3.connect(database)) as db, db:
                db.execute('CREATE TABLE Loc(Key TEXT PRIMARY KEY,Bundle TEXT,enUS TEXT,ptBR TEXT,frFR TEXT,itIT TEXT,deDE TEXT,esES TEXT,jaJP TEXT,koKR TEXT)')
                old = ('MainNav/DeckFormat/Commander', 'Original', *(['original'] * 8))
                other = ('Unrelated', 'Original', *(['keep'] * 8))
                db.executemany('INSERT INTO Loc VALUES (?,?,?,?,?,?,?,?,?,?)', (old, other))
            labels = {'ArenaLab/Modes/Commander': 'Commander - 1 AI', 'MainNav/DeckFormat/Commander': 'Commander'}
            install(database, labels, backup)
            install(database, labels, backup)
            with closing(sqlite3.connect(database)) as db, db:
                self.assertEqual(db.execute('SELECT enUS FROM Loc WHERE Key=?', ('ArenaLab/Modes/Commander',)).fetchone()[0], 'Commander - 1 AI')
                self.assertEqual(db.execute('SELECT * FROM Loc WHERE Key=?', ('Unrelated',)).fetchone(), other)
            restore(backup)
            with closing(sqlite3.connect(database)) as db, db:
                self.assertEqual(db.execute('SELECT * FROM Loc ORDER BY Key').fetchall(), sorted([old, other]))


if __name__ == '__main__':
    unittest.main()
