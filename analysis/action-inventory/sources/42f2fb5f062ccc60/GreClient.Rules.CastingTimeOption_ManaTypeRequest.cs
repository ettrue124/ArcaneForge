using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_ManaTypeRequest : BaseUserRequest
{
	public class SelectManaTypeRequest
	{
		public readonly int DefaultIndex;

		public readonly IReadOnlyList<ManaColor> ManaColorOptions;

		public readonly CastingTimeOptionResp Response;

		public SelectManaTypeRequest(CastingTimeOptionReq request)
		{
			DefaultIndex = 0;
			ManaColorOptions = request.SelectManaTypeReq.ManaColors;
			ManaColor manaColor = request.SelectManaTypeReq.ManaColors.FirstOrDefault((ManaColor x) => x >= ManaColor.White && x <= ManaColor.Green);
			if (manaColor != ManaColor.None)
			{
				DefaultIndex = request.SelectManaTypeReq.ManaColors.IndexOf(manaColor);
			}
			Response = new CastingTimeOptionResp
			{
				CastingTimeOptionType = CastingTimeOptionType.ManaType,
				CtoId = request.CtoId,
				SelectManaTypeResp = new SelectManaTypeResp
				{
					ManaColor = ManaColorOptions[0]
				}
			};
		}

		public void SetColor(ManaColor color)
		{
			if (ManaColorOptions.Contains(color))
			{
				Response.SelectManaTypeResp.ManaColor = color;
			}
		}
	}

	public readonly IReadOnlyList<SelectManaTypeRequest> InnerRequests;

	public readonly IReadOnlyList<ManaRequirement> CurrentManaCosts;

	public CastingTimeOption_ManaTypeRequest(CastingTimeOptionsReq request, GREToClientMessage inMsg)
		: base(inMsg)
	{
		List<SelectManaTypeRequest> list = new List<SelectManaTypeRequest>();
		List<ManaRequirement> list2 = new List<ManaRequirement>();
		foreach (CastingTimeOptionReq item in request.CastingTimeOptionReq)
		{
			list.Add(new SelectManaTypeRequest(item));
			if (list2.Count != 0)
			{
				continue;
			}
			foreach (ManaRequirement item2 in item.ManaCost)
			{
				list2.Add(new ManaRequirement(item2));
			}
		}
		InnerRequests = list;
		CurrentManaCosts = list2;
		_outboundMessage.Type = ClientMessageType.CastingTimeOptionsResp;
	}

	public void SubmitSelection(IReadOnlyList<ManaColor> manaColors)
	{
		if (manaColors.Count != InnerRequests.Count)
		{
			throw new ArgumentException("Improper number of ManaColors supplied.");
		}
		for (int i = 0; i < manaColors.Count; i++)
		{
			InnerRequests[i].SetColor(manaColors[i]);
		}
		_outboundMessage.CastingTimeOptionsResp = new CastingTimeOptionsResp
		{
			CastingTimeOptionResps = { InnerRequests.Select((SelectManaTypeRequest x) => x.Response) }
		};
		Submit();
	}
}
