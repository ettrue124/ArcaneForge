import unittest
from tools.extract_arena_actions import schema_index, incoming_catalogue, source_evidence, registrations


class ActionInventoryTests(unittest.TestCase):
    def test_nested_fields_oneofs_and_enum_aliases_survive(self):
        messages,enums=schema_index({'file':[{'name':'example.proto','package':'test','messageType':[{
            'name':'Outer','oneofDecl':[{'name':'choice'}],
            'field':[{'name':'option','jsonName':'option','number':7,'type':'TYPE_MESSAGE','typeName':'.test.Outer.Inner','oneofIndex':0}],
            'nestedType':[{'name':'Inner','field':[{'name':'quantity','number':3,'type':'TYPE_UINT32'}]}],
            'enumType':[{'name':'Mode','value':[{'name':'None'},{'name':'Alias','number':0},{'name':'Active','number':5}]}]
        }]}]})
        self.assertEqual(messages['test.Outer']['fields'][0]['oneof'],'choice')
        self.assertEqual(messages['test.Outer']['fields'][0]['number'],7)
        self.assertEqual(messages['test.Outer.Inner']['fields'][0]['type'],'TYPE_UINT32')
        self.assertEqual([v['value'] for v in enums['test.Outer.Mode']['values']],[0,0,5])

    def test_handlers_resolve_payloads_without_inventing_support(self):
        messages,enums=schema_index({'file':[{'name':'gre.proto','package':'gre','messageType':[{
            'name':'GREToClientMessage','field':[{'name':'illegalRequestMessage','number':10,'type':'TYPE_MESSAGE','typeName':'.gre.IllegalRequest'}]
        }], 'enumType':[{'name':'GREMessageType','value':[{'name':'GREMessageType_IllegalRequest','number':12},{'name':'GREMessageType_Unused','number':99}]}]}]})
        evidence=[source_evidence('IllegalRequestNotificationParser','return new Notification(msg.IllegalRequestMessage);','source.cs')]
        rows=incoming_catalogue(messages,enums,[{'message':'IllegalRequest','handler':'IllegalRequestNotificationParser'}],evidence)
        self.assertEqual(rows[0]['payload_type'],'gre.IllegalRequest')
        self.assertTrue(rows[0]['registered'])
        self.assertFalse(rows[1]['registered'])
        self.assertFalse(rows[0]['runtime_verified'])

    def test_schema_only_does_not_claim_a_game_state_handler(self):
        messages={'GREToClientMessage':{'name':'GREToClientMessage','fields':[{'name':'gameStateMessage','type_name':'GameStateMessage','type':'TYPE_MESSAGE'}]}}
        enums={'GREMessageType':{'values':[{'name':'GREMessageType_GameStateMessage','value':1}]}}
        row=incoming_catalogue(messages,enums,[])[0]
        self.assertEqual(row['payload_type'],'GameStateMessage')
        self.assertFalse(row['registered'])

    def test_factory_registration_and_direction_are_separate(self):
        source='GREMessageType.SelectTargetsReq, new SelectTargetsRequestTranslator(getState); ClientMessageType.SelectTargetsResp; const string KEY = "target_ids";'
        self.assertEqual(registrations(source),[{'message':'SelectTargetsReq','handler':'SelectTargetsRequestTranslator'}])
        record=source_evidence('TargetParser',source,'target.cs')
        self.assertEqual(record['incoming_messages'],['SelectTargetsReq'])
        self.assertEqual(record['outgoing_responses'],['SelectTargetsResp'])
        self.assertEqual(record['detail_key_candidates'],['target_ids'])


if __name__=='__main__':unittest.main()
