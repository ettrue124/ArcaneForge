using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class DistributionRequest : BaseUserRequest
{
	public readonly uint Min;

	public readonly uint Max;

	public readonly uint MinPer;

	public readonly uint MaxPer;

	public readonly List<uint> TargetIds;

	public readonly List<uint> LegalTargetIds = new List<uint>();

	public readonly List<uint> IllegalTargetIds = new List<uint>();

	public readonly List<uint> RequiredDistributions;

	public readonly List<uint> ExistingDistributions;

	public DistributionRequest(DistributionReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		TargetIds = new List<uint>(req.TargetIds);
		foreach (uint targetId in TargetIds)
		{
			(req.ValidSelectedTargetIds.Contains(targetId) ? LegalTargetIds : IllegalTargetIds).Add(targetId);
		}
		RequiredDistributions = new List<uint>(req.RequiredDistributionValues);
		ExistingDistributions = new List<uint>(req.ExistingDistributionValues);
		Min = req.MinAmount;
		Max = req.MaxAmount;
		MinPer = req.MinPerTarget;
		if (TargetIds.Count == 1)
		{
			MinPer = Min;
		}
		MaxPer = (uint)(req.MaxAmount - req.TargetIds.Count * MinPer + MinPer);
		_outboundMessage.Type = ClientMessageType.DistributionResp;
		_outboundMessage.DistributionResp = new DistributionResp();
	}

	public void SubmitDistribution(IReadOnlyDictionary<uint, uint> distributions)
	{
		DistributionResp distributionResp = _outboundMessage.DistributionResp;
		distributionResp.Distributions.Clear();
		foreach (KeyValuePair<uint, uint> distribution in distributions)
		{
			distributionResp.Distributions.Add(new Distribution
			{
				InstanceId = distribution.Key,
				Amount = distribution.Value
			});
		}
		Submit();
	}
}
