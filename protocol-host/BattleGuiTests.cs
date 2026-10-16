using System.Reflection;
using System.Text.Json;
using Google.Protobuf;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleGuiTests
{
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    public static void Run(Assembly native)
    {
        GREToClientMessage Request(string json) {
            using var input=JsonDocument.Parse(json);
            return GREToClientMessage.Parser.ParseFrom(BattleRequests.Create(input.RootElement,71,9)!.ToByteArray());
        }
        object Parse(string type,object payload,GREToClientMessage message) =>
            Activator.CreateInstance(native.GetType("GreClient.Rules."+type,true)!,new[]{payload,(object)message})!;
        var choice=Request("""
        {"input":{"type":"InputGuiChoice","sourceId":1001,"min":0,"max":2,"entityChoices":false,"options":[{"id":1,"label":"Draw a card"},{"id":2,"label":"Gain 3 life"}]}}
        """);
        var nativeChoice=Parse("SelectNRequest",choice.SelectNReq,choice);
        Check(nativeChoice!=null && choice.SelectNReq.MaxSel==2,"Modal choice request parsing failed");
        var order=Request("""
        {"input":{"type":"InputGuiOrder","options":[{"id":1002},{"id":1003}]}}
        """);
        Parse("OrderRequest",order.OrderReq,order);
        Check(BattleGuiResponses.TryAnswer(new ClientToGREMessage {OrderResp=new OrderResp {Ordering=OrderingType.OrderAsIndicated,Ids={1003,1002}}},"InputGuiOrder",out var ordered)
            && ((uint[])ordered!).SequenceEqual(new uint[]{1002,1003}),"Native reversed order was not restored for Forge");
        var orderBottom=Request("""
        {"input":{"type":"InputGuiOrder","title":"Choose order of cards to put into the library","top":"Closest to bottom","options":[{"id":1002},{"id":1003}]}}
        """);
        Check(orderBottom.Prompt.PromptId==41 && orderBottom.OrderReq.OrderingContext==OrderingContext.OrderingForBottom,
            "Bottom library ordering must use PromptId 41 and OrderingContext.OrderingForBottom");
        var number=Request("""
        {"buttonEventId":72,"input":{"type":"InputChooseNumber","sourceId":1001,"grpId":1,"isX":false,"min":1,"max":5,"allowed":[1,3,5]}}
        """);
        Parse("NumericInputRequest",number.NumericInputReq,number);
        Check(number.CastingTimeOptionsReq==null && number.NumericInputReq.DisallowedValues.SequenceEqual(new uint[]{2,4}),"Non-X amounts used casting UI or lost sparse values");
        var groups=Request("""
        {"input":{"type":"InputGuiGroups","choices":[1002,1003],"toTop":true,"toBottom":true}}
        """);
        Parse("GroupRequest",groups.GroupReq,groups);
        Check(groups.GroupReq.GroupSpecs.Count==2 && groups.GroupReq.GroupSpecs[1].SubZoneType==SubZoneType.Bottom,"Scry destinations incorrect");
        var damage=Request("""
        {"input":{"type":"InputGuiCombatDamage","sourceId":1001,"amount":6,"ignoreOrder":false,"targets":[{"id":1002,"lethal":3},{"id":2,"lethal":0}]}}
        """);
        Parse("AssignDamageRequest",damage.AssignDamageReq,damage);
        Check(damage.AssignDamageReq.DamageAssigners.Single().Assignments.Select(a=>a.AssignedDamage).SequenceEqual(new uint[]{3,3}),"Trample default lost blocker lethal/defender damage");
        var distribution=Request("""
        {"input":{"type":"InputGuiDistribution","sourceId":1001,"amount":4,"minPerTarget":1,"targets":[{"id":1002},{"id":1003}]}}
        """);
        Parse("DistributionRequest",distribution.DistributionReq,distribution);
        Check(distribution.DistributionReq.MinAmount==4 && distribution.DistributionReq.ValidSelectedTargetIds.Count==2,"Distribution lost total or legal recipients");
        var text=Request("""{"input":{"type":"InputGuiText","sourceId":1001}}""");
        Parse("StringInputRequest",text.StringInputReq,text);
        var stackTargets=Request("""
        {"cards":[],"input":{"type":"InputGuiStackTargets","sourceId":1001,"min":1,"max":1,"targets":[1002,1000003],"selected":[],"description":"Counter target spell or ability."}}
        """);
        Parse("SelectTargetsRequest",stackTargets.SelectTargetsReq,stackTargets);
        Check(stackTargets.SelectTargetsReq.Targets.Single().Targets.Select(t=>t.TargetInstanceId).SequenceEqual(new uint[]{1002,1000003}),"Native stack targets lost card/ability identities");
        Check(BattleGuiResponses.TryAnswer(new ClientToGREMessage {SelectTargetsResp=new SelectTargetsResp {Target=new TargetSelection {Targets={new Target {TargetInstanceId=1000003}}}}},"InputGuiStackTargets",out var targetAnswer)
            && ((uint[])targetAnswer!).Single()==1000003,"Counterspell answer changed stack target identity");
        var blocking=Request("""
        {"input":{"type":"InputBlock","blockers":[{"instanceId":1001,"maxAttackers":2,"attackers":[1002,1003],"selected":[1002,1003]}]}}
        """);
        Parse("DeclareBlockersRequest",blocking.DeclareBlockersReq,blocking);
        Check(blocking.DeclareBlockersReq.Blockers.Single().MaxAttackers==2 && blocking.DeclareBlockersReq.Blockers.Single().SelectedAttackerInstanceIds.Count==2,"Native multi-block capacity was reduced to one");
        using var divided=JsonDocument.Parse("""
        {"cards":[],"input":{"type":"InputSelectTargets","divided":true,"sourceId":1001,"min":1,"max":2,"targets":[1002],"selected":[],"cardName":"Test","description":"Divide damage"}}
        """);
        Check(BattleRequests.Create(divided.RootElement,1,1)!.SelectTargetsReq!=null,"Divided target selection still rejected");
        var pay=Request("""
        {"cards":[{"instanceId":1001,"grpId":7,"ownerSeat":1,"zone":"Hand"}],"input":{"type":"InputPayMana","sourceId":1001,"manaCost":"{1}{G}","autoTapSources":[1002]},"legalActions":[{"instanceId":1002,"grpId":8,"abilityId":10,"actionType":4,"isLand":false,"manaCost":"{0}","highlight":3,"description":"Tap for mana","cardName":"Forest"}]}
        """);
        Parse("PayCostsRequest",pay.PayCostsReq,pay);
        Check(pay.PayCostsReq.ManaCost.Count==2 && pay.PayCostsReq.PaymentActions.Actions.Count==1 && pay.PayCostsReq.AutoTapActionsReq.AutoTapSolutions.Single().AutoTapActions.Single().InstanceId==1002,"PayCostsReq creation failed");
        var searchGroups=Request("""
        {"cards":[{"instanceId":1002,"ownerSeat":1,"zone":"Library"}],"input":{"type":"InputGuiChoice","nativeKind":"searchFromGroups","sourceId":1001,"min":1,"max":1,"groups":[{"id":1,"ids":[1002],"faceDown":false}]}}
        """);
        Parse("SearchFromGroupsRequest",searchGroups.SearchFromGroupsReq,searchGroups);
        Check(searchGroups.SearchFromGroupsReq.Groups.Count==1 && searchGroups.SearchFromGroupsReq.ZonesToSearch.Single()==101,"SearchFromGroupsReq failed");
        Check(BattleGuiResponses.TryAnswer(new ClientToGREMessage {SearchFromGroupsResp=new SearchFromGroupsResp {Groups={new Group {GroupId=1,Ids={1002}}}}},"InputGuiChoice",out var groupAnswer,
            """{"input":{"type":"InputGuiChoice","nativeKind":"searchFromGroups","groups":[{"id":1,"ids":[1002]}]}}""")
            && ((uint[])groupAnswer!).Single()==1002,"SearchFromGroups response parsing failed");
        var selectGroups=Request("""
        {"cards":[{"instanceId":1002,"ownerSeat":1,"zone":"Battlefield"}],"input":{"type":"InputGuiChoice","nativeKind":"selectFromGroups","sourceId":1001,"min":1,"max":1,"groups":[{"id":1,"ids":[1002],"faceDown":false}],"zones":[30]}}
        """);
        Parse("SelectFromGroupsRequest",selectGroups.SelectFromGroupsReq,selectGroups);
        Check(selectGroups.SelectFromGroupsReq.Groups.Count==1 && selectGroups.SelectFromGroupsReq.ZoneIds.Single()==30,"SelectFromGroupsReq failed");
        Check(BattleGuiResponses.TryAnswer(new ClientToGREMessage {SelectFromGroupsResp=new SelectFromGroupsResp {Groups={new Group {GroupId=1,Ids={1002}}}}},"InputGuiChoice",out var sfgAnswer,
            """{"input":{"type":"InputGuiChoice","nativeKind":"selectFromGroups","groups":[{"id":1,"ids":[1002]}]}}""")
            && ((uint[])sfgAnswer!).Single()==1002,"SelectFromGroups response parsing failed");
        var counters=Request("""
        {"input":{"type":"InputGuiChoice","nativeKind":"selectCounters","sourceId":1001,"min":1,"max":1,"counterPairs":[{"instanceId":1002,"counterType":"+1/+1","count":1}]}}
        """);
        Parse("SelectCountersRequest",counters.SelectCountersReq,counters);
        Check(counters.SelectCountersReq.CounterPairs.Single().CounterType==CounterType.P1P1,"SelectCountersReq failed");
        Check(BattleGuiResponses.TryAnswer(new ClientToGREMessage {SelectCountersResp=new SelectCountersResp {Selections={new CounterPair {InstanceId=1002,CounterType=CounterType.P1P1,Count=1}}}},"InputGuiChoice",out var counterAnswer,
            """{"input":{"type":"InputGuiChoice","nativeKind":"selectCounters"}}""")
            && counterAnswer!=null,"SelectCounters response parsing failed");
        var gather=Request("""
        {"input":{"type":"InputGuiChoice","nativeKind":"gather","sourceId":1001,"destinationId":1001,"amount":3,"sources":[{"sourceId":1002,"min":1,"max":2},{"sourceId":1003,"min":1,"max":2}]}}
        """);
        Parse("GatherRequest",gather.GatherReq,gather);
        Check(gather.GatherReq.AmountToGather==3 && gather.GatherReq.Sources.Count==2,"GatherReq failed");
        Check(BattleGuiResponses.TryAnswer(new ClientToGREMessage {GatherResp=new GatherResp {Gatherings={new Gathering {InstanceId=1002,Amount=2},new Gathering {InstanceId=1003,Amount=1}}}},"InputGuiChoice",out var gatherAnswer,
            """{"input":{"type":"InputGuiChoice","nativeKind":"gather"}}""")
            && gatherAnswer!=null,"Gather response parsing failed");
        var readAhead=Request("""
        {"buttonEventId":73,"input":{"type":"InputChooseNumber","sourceId":1001,"grpId":1,"isX":false,"min":1,"max":3,"readAhead":true}}
        """);
        Parse("NumericInputRequest",readAhead.NumericInputReq,readAhead);
        Check(readAhead.Prompt?.PromptId==166,"Read ahead did not set promptId 166");
        var timerInfo=BattleTimers.CreateTimerInfo(1,TimerType.Decision,30,5,true,TimerBehavior.Timeout,10);
        var timerMsg=BattleTimers.CreateTimerStateMessage(74,9,1,new[]{timerInfo});
        Check(timerMsg.Type==GREMessageType.TimerStateMessage && timerMsg.TimerStateMessage.Timers.Single().DurationSec==30,"TimerStateMessage creation failed");
        var timeoutMsg=BattleTimers.CreateTimeoutMessage(75,9,1,2,timerInfo);
        Check(timeoutMsg.Type==GREMessageType.TimeoutMessage && timeoutMsg.TimeoutMessage.TimeoutCount==2,"TimeoutMessage creation failed");
        var submitDeck=Request("""
        {"input":{"type":"InputSubmitDeck","deckCards":[101,102],"sideboardCards":[201]}}
        """);
        Parse("SubmitDeckRequest",submitDeck.SubmitDeckReq,submitDeck);
        Check(submitDeck.SubmitDeckReq.Deck.DeckCards.Count==2 && submitDeck.SubmitDeckReq.Deck.SideboardCards.Count==1,"SubmitDeckReq creation failed");
        var manaTypeReq=Request("""
        {"buttonEventId":88,"cards":[{"instanceId":1001,"grpId":84090,"name":"Vraska, Betrayal's Sting"}],"input":{"type":"InputGuiChoice","nativeKind":"manaType","sourceId":1001,"manaCost":"{3}{B}{B/P}","manaColors":["Black","Phyrexian"],"options":[{"id":1,"label":"Pay mana"},{"id":2,"label":"Pay 2 life"}]}}
        """);
        Check(manaTypeReq.Type==GREMessageType.CastingTimeOptionsReq && manaTypeReq.CastingTimeOptionsReq.CastingTimeOptionReq.Single().CastingTimeOptionType==CastingTimeOptionType.ManaType,
            "Phyrexian mana did not create CastingTimeOptionType.ManaType");
        var cto=manaTypeReq.CastingTimeOptionsReq.CastingTimeOptionReq.Single();
        Check(cto.SelectManaTypeReq.ManaColors.SequenceEqual(new[]{ManaColor.Black,ManaColor.Phyrexian}),
            "ManaColors mismatch on SelectManaTypeReq");
        Check(cto.ManaCost.Count==3 && cto.ManaCost[2].Color.Contains(ManaColor.Phyrexian),
            "Parsed mana cost missing Phyrexian mana requirement");
        var phyResp=new ClientToGREMessage {
            CastingTimeOptionsResp=new CastingTimeOptionsResp {
                CastingTimeOptionResp=new CastingTimeOptionResp {
                    CtoId=88,
                    CastingTimeOptionType=CastingTimeOptionType.ManaType,
                    SelectManaTypeResp=new SelectManaTypeResp {ManaColor=ManaColor.Phyrexian}
                }
            }
        };
        Check(BattleGuiResponses.TryAnswer(phyResp,"InputGuiChoice",out var phyAns,"""{"buttonEventId":88,"input":{"type":"InputGuiChoice","nativeKind":"manaType"}}""")
            && ((uint[])phyAns!).SequenceEqual(new uint[]{2}),"ManaColor.Phyrexian did not map to option 2");
        var manaResp=new ClientToGREMessage {
            CastingTimeOptionsResp=new CastingTimeOptionsResp {
                CastingTimeOptionResp=new CastingTimeOptionResp {
                    CtoId=88,
                    CastingTimeOptionType=CastingTimeOptionType.ManaType,
                    SelectManaTypeResp=new SelectManaTypeResp {ManaColor=ManaColor.Black}
                }
            }
        };
        Check(BattleGuiResponses.TryAnswer(manaResp,"InputGuiChoice",out var manaAns,"""{"buttonEventId":88,"input":{"type":"InputGuiChoice","nativeKind":"manaType"}}""")
            && ((uint[])manaAns!).SequenceEqual(new uint[]{1}),"ManaColor.Black did not map to option 1");
        var landTaxReq=Request("""
        {"cards":[{"instanceId":1001,"grpId":100,"name":"Land Tax"}],"stack":[{"instanceId":2001,"parentId":1001}],"input":{"type":"InputConfirm","sourceId":1001,"startingPlayer":false,"optionalTrigger":true,"message":"Use triggered ability of Land Tax?","options":["Yes","No"]}}
        """);
        Check(landTaxReq.Type==GREMessageType.OptionalActionMessage && landTaxReq.OptionalActionMessage.SourceId==2001,
            "Optional trigger did not route to OptionalActionMessage with stack resolving ability ID");
        Console.WriteLine("PASS: installed Arena choice, order, grouping, numeric, text, combat damage, pay costs, search, select groups, counters, gather, timers, submit deck and distribution parsers/responses.");
    }
}
