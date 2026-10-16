import unittest
from server.http import mask_snapshot


class SnapshotTests(unittest.TestCase):
    def test_filters_private_identities_without_changing_engine_state(self):
        original = {'cards': [
            {'instanceId': 1001, 'grpId': 10, 'zone': 'Hand', 'ownerSeat': 1},
            {'instanceId': 1002, 'grpId': 11, 'originalGrpId': 12, 'cardState': 'Backside', 'manaCost': '{U}',
             'cardTypes': ['Instant'], 'colors': [2], 'zone': 'Hand', 'ownerSeat': 2, 'power': 2},
            {'instanceId': 1003, 'grpId': 12, 'zone': 'Library', 'ownerSeat': 1},
            {'instanceId': 1004, 'grpId': 13, 'zone': 'Battlefield', 'ownerSeat': 2}
        ]}
        viewer1 = mask_snapshot(original, 1)
        self.assertEqual(viewer1['cards'][0]['grpId'], 10)
        self.assertNotIn('grpId', viewer1['cards'][1])
        self.assertNotIn('power', viewer1['cards'][1])
        for key in ('originalGrpId','cardState','manaCost','cardTypes','colors'):
            self.assertNotIn(key,viewer1['cards'][1], 'Hidden identity leaked through '+key)
        self.assertNotIn('grpId', viewer1['cards'][2])
        self.assertEqual(viewer1['cards'][3]['grpId'], 13)
        viewer2 = mask_snapshot(original, 2)
        self.assertNotIn('grpId', viewer2['cards'][0])
        self.assertEqual(viewer2['cards'][1]['grpId'], 11)
        self.assertEqual(original['cards'][1]['grpId'], 11)


if __name__ == '__main__':
    unittest.main()
