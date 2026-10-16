using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectCountersRequest : BaseUserRequest
{
	public readonly uint MinSelect;

	public readonly uint MaxSelect;

	public readonly SelectionContext SelectionContext;

	public readonly OptionContext OptionContext;

	public readonly List<CounterPair> CounterPairs = new List<CounterPair>();

	public SelectCountersRequest(SelectCountersReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		MinSelect = req.MinSel;
		MaxSelect = req.MaxSel;
		SourceId = req.SourceId;
		SelectionContext = req.Context;
		OptionContext = req.OptionContext;
		CounterPairs.AddRange(req.CounterPairs);
		_outboundMessage.Type = ClientMessageType.SelectCountersResp;
		_outboundMessage.SelectCountersResp = new SelectCountersResp();
	}

	public void SubmitCountersResponse(IEnumerable<CounterPair> counterPairs)
	{
		SelectCountersResp selectCountersResp = new SelectCountersResp();
		selectCountersResp.Selections.AddRange(counterPairs);
		_outboundMessage.SelectCountersResp = selectCountersResp;
		Submit();
	}
}
