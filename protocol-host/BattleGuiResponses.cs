using Wotc.Mtgo.Gre.External.Messaging;

static class BattleGuiResponses
{
    // Forge revalidates IDs, totals, ranges, order, and the pending event ID.
    public static bool TryAnswer(ClientToGREMessage message,string inputType,out object? answer,string? snapshotJson=null)
    {
        answer=null;
        if(snapshotJson!=null) {
            using var snapshot=System.Text.Json.JsonDocument.Parse(snapshotJson);
            if(snapshot.RootElement.TryGetProperty("input",out var input)) {
                if(inputType=="InputGuiStackTargets" && input.TryGetProperty("readableStackTargets",out var readable)
                    && readable.GetBoolean() && message.SelectNResp is {} stackMenu) {
                    var options=input.GetProperty("options");
                    answer=stackMenu.Ids.Select(id=>id>0 && id<=options.GetArrayLength()
                        ?options[(int)id-1].GetProperty("id").GetUInt32()
                        :throw new InvalidDataException("Unknown stack choice")).ToArray();
                    return true;
                }
                if(BattleNativeMenus.TryAnswer(message,snapshot.RootElement,out answer))return true;
            }
        }
        switch(inputType) {
            case "InputGuiStackTargets" when message.SelectNResp is { } stackSelect:
                answer=stackSelect.Ids.ToArray();return true;
            case "InputGuiStackTargets" when message.SelectTargetsResp is { } target:
                answer=target.Target.Targets.Select(t=>t.TargetInstanceId).ToArray();return true;
            case "InputGuiStackTargets" when message.CancelActionReq is not null || message.Type is (ClientMessageType)37:
                answer=Array.Empty<uint>();return true;
            case "InputChooseNumber" when message.SelectNResp is { } numbered:
                if(numbered.Ids.Count!=1)throw new InvalidDataException("Choose one number");
                answer=checked((int)numbered.Ids[0]-1);return true;
            case "InputChooseNumber" when message.NumericInputResp is { } numeric:
                answer=numeric.NumericInputValue;return true;
            case "InputGuiChoice" when message.SelectNResp is { } select:
                answer=select.Ids.ToArray();return true;
            case "InputGuiOrder" when message.OrderResp is { } order:
                if(order.Ordering!=OrderingType.OrderAsIndicated)throw new InvalidDataException("Explicit Forge card order required");
                // Arena SubmitOrder reverses the visual top-first order on the wire.
                answer=order.Ids.Reverse().ToArray();return true;
            case "InputGuiText" when message.StringInputResp is { } text:
                answer=text.InputValue;return true;
            case "InputGuiGroups" when message.GroupResp is { } groups:
                answer=groups.Groups.Select(g=>new {ids=g.Ids.ToArray(),zoneType=(int)g.ZoneType,subZoneType=(int)g.SubZoneType}).ToArray();return true;
            case "InputGuiDistribution" when message.DistributionResp is { } distribution:
                answer=distribution.Distributions.Select(d=>new {id=d.InstanceId,amount=d.Amount}).ToArray();return true;
            case "InputGuiCombatDamage" when message.AssignDamageResp is { } damage:
                if(damage.Assigners.Count!=1)throw new InvalidDataException("Expected one Forge damage assigner");
                answer=damage.Assigners[0].Assignments.Select(d=>new {id=d.InstanceId,amount=d.AssignedDamage}).ToArray();return true;
            case "InputGuiGather" when message.GatherResp is { } gather:
                answer=gather.Gatherings.Select(g=>new {id=g.InstanceId,amount=g.Amount}).ToArray();return true;
        }
        return false;
    }
}
