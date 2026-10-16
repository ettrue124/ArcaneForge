using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SearchRequest : BaseUserRequest
{
	public readonly int Min;

	public readonly uint Max;

	public List<uint> Options { get; private set; }

	public List<uint> ZonesToSearch { get; private set; } = new List<uint>();

	public List<uint> AdditionalZones { get; private set; } = new List<uint>();

	public List<uint> ContextOptions { get; private set; } = new List<uint>();

	public bool IsMultiZoneSearch => ZonesToSearch.Count + AdditionalZones.Count > 1;

	public SearchRequest(SearchReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Min = (int)req.MinFind;
		Max = req.MaxFind;
		Options = new List<uint>(req.ItemsSought);
		foreach (uint item in req.ItemsToSearch)
		{
			if (!Options.Contains(item))
			{
				ContextOptions.Add(item);
			}
		}
		foreach (uint item2 in req.ZonesToSearch)
		{
			if (!ZonesToSearch.Contains(item2))
			{
				ZonesToSearch.Add(item2);
			}
		}
		foreach (uint additionalZone in req.AdditionalZones)
		{
			if (!AdditionalZones.Contains(additionalZone))
			{
				AdditionalZones.Add(additionalZone);
			}
		}
		_outboundMessage.Type = ClientMessageType.SearchResp;
		_outboundMessage.SearchResp = new SearchResp();
	}

	public void SubmitSelection(IEnumerable<uint> selection)
	{
		SearchResp searchResp = _outboundMessage.SearchResp;
		searchResp.ItemsFound.Clear();
		searchResp.ItemsFound.AddRange(selection);
		Submit();
	}

	public void SubmitZone(uint zone)
	{
		_outboundMessage.SearchResp.AddZoneToSearchScope = zone;
		Submit();
	}
}
