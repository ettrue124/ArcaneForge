import hashlib
from contextlib import closing
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
import zipfile
from scripts.prepare_client import extract_catalogue, extract_client, extract_installation


class PrepareClientTests(unittest.TestCase):
    def test_installed_client_import_and_ambiguity(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            install = root / 'MTGA'
            managed = install / 'MTGA_Data/Managed'
            raw = install / 'MTGA_Data/Downloads/Raw'
            managed.mkdir(parents=True)
            raw.mkdir(parents=True)
            (managed / 'Core.dll').write_bytes(b'updated')
            (raw / 'Raw_CardDatabase_test.mtga').write_bytes(b'db')
            (install / 'version').write_text('{}')
            manifest = extract_installation(install, root / 'output')
            self.assertEqual(len(manifest['files']), 3)
            self.assertEqual((root / 'output/Core.dll').read_bytes(), b'updated')
            with self.assertRaisesRegex(ValueError, 'outside'):
                extract_installation(install, install / 'output')
            (raw / 'Raw_CardDatabase_other.mtga').write_bytes(b'db')
            with self.assertRaisesRegex(ValueError, 'exactly one'):
                extract_installation(install, root / 'output2')

    def test_selective_extraction_hash_and_reject_traversal(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / 'client.zip'
            with zipfile.ZipFile(archive, 'w') as package:
                package.writestr('MTGA/MTGA_Data/Managed/Core.dll', b'dll')
                package.writestr('MTGA/MTGA_Data/Downloads/Raw/Raw_CardDatabase_test.mtga', b'db')
                package.writestr('MTGA/version', b'version')
                package.writestr('MTGA/MTGA.exe', b'never extracted')
            manifest = extract_client(archive, root / 'out')
            self.assertEqual(len(manifest['files']), 3)
            core = next(item for item in manifest['files'] if item['file'] == 'Core.dll')
            self.assertEqual(core['sha256'], hashlib.sha256(b'dll').hexdigest())
            self.assertFalse((root / 'out/MTGA.exe').exists())
            with zipfile.ZipFile(archive, 'w') as package:
                package.writestr('MTGA/MTGA_Data/Managed/../../evil.dll', b'evil')
            with self.assertRaisesRegex(ValueError, 'Unsafe'):
                extract_client(archive, root / 'unsafe')

    def test_duplicate_basename_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            archive = Path(temporary) / 'client.zip'
            with zipfile.ZipFile(archive, 'w') as package:
                package.writestr('MTGA/MTGA_Data/Managed/Core.dll', b'a')
                package.writestr('OTHER/Managed/core.dll', b'b')
            with self.assertRaisesRegex(ValueError, 'duplicate'):
                extract_client(archive, Path(temporary) / 'out')

    def test_catalogue_filters_name_fallback_and_limits(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            database = root / 'cards.mtga'
            with closing(sqlite3.connect(database)) as connection:
                connection.executescript('''
                    CREATE TABLE Cards(GrpId INT,TitleId INT,Order_Title TEXT,IsPrimaryCard INT,IsToken INT,
                    Supertypes TEXT,Types TEXT,Subtypes TEXT,AlternateDeckLimit INT,
                    OldSchoolManaText TEXT,Power TEXT,Toughness TEXT);
                    CREATE TABLE Localizations_enUS(LocId INT,Formatted INT,Loc TEXT);
                    CREATE TABLE Enums(Type TEXT,Value INT,LocId INT);
                    CREATE TABLE Versions(Type TEXT,Version TEXT);
                    INSERT INTO Enums VALUES('SuperType',1,20),('CardType',5,21),('CardType',2,22);
                    INSERT INTO Localizations_enUS VALUES(20,1,'Basic'),(21,1,'Land'),(22,1,'Creature'),
                    (10,1,'Forest'),(11,1,'Formatted'),(11,0,'Plain Name'),(12,1,'Seven Dwarves');
                    INSERT INTO Cards VALUES
                    (1,10,'fallback',1,0,'1','5','',250,'','',''),
                    (2,11,'fallback',1,0,'','2','',0,'oW','1','1'),
                    (3,12,'fallback',1,0,'','2','',7,'oR','2','2'),
                    (4,999,'Token',0,1,'','2','',0,'','1','1'),
                    (5,999,'Reverse Face',0,0,'','2','',0,'','1','1');
                    INSERT INTO Versions VALUES('Data','test');
                ''')
                connection.commit()
            cards = extract_catalogue(database, root / 'catalogue.json')
            self.assertEqual(len(cards), 3)
            self.assertEqual([c['name'] for c in cards], ['Forest','Plain Name','Seven Dwarves'])
            self.assertEqual([c['cardLimit'] for c in cards], [0,4,7])
            self.assertTrue(cards[0]['isBasicLand'])
            self.assertEqual(cards[1]['arenaMetadata']['Power'], '1')
            metadata = json.loads((root / 'catalogue-metadata.json').read_text())
            self.assertFalse(metadata['rulesEngineIncluded'])


if __name__ == '__main__':
    unittest.main()
