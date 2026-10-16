using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class GatherRequest : BaseUserRequest
{
	public readonly uint DestinationId;

	public readonly List<GatherSource> Sources;

	public readonly uint AmountToGather;

	public GatherRequest(GatherReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		DestinationId = req.DestinationId;
		Sources = new List<GatherSource>(req.Sources);
		SourceId = req.DestinationId;
		AmountToGather = req.AmountToGather;
		_outboundMessage.Type = ClientMessageType.GatherResp;
		_outboundMessage.GatherResp = new GatherResp();
	}

	public void SubmitGathering(IEnumerable<Gathering> gatherings)
	{
		_outboundMessage.GatherResp.Gatherings.AddRange(gatherings);
		Submit();
	}
}
