using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_SpecializeRequest : BaseUserRequest
{
	private const string PROMPT_PARAMNAME_COST = "Cost";

	public readonly uint SourceAbilityId;

	public HashSet<CardColor> SelectableColors { get; private set; } = new HashSet<CardColor>();

	public List<uint> HotIds { get; private set; } = new List<uint>();

	public List<uint> OptionGrpIds { get; private set; } = new List<uint>();

	public CastingTimeOption_SpecializeRequest(CastingTimeOptionReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		SelectNReq selectNReq = req.SelectNReq;
		if (selectNReq == null)
		{
			throw new ArgumentNullException("Null SelectNReq for Specialize Request");
		}
		Prompt prompt = selectNReq.Prompt;
		if (prompt == null)
		{
			throw new ArgumentException("Null SelectNPrompt for Specialize Request");
		}
		foreach (uint id in selectNReq.Ids)
		{
			SelectableColors.Add((CardColor)id);
		}
		foreach (uint hotId in selectNReq.HotIds)
		{
			HotIds.Add(hotId);
		}
		foreach (PromptParameter parameter in prompt.Parameters)
		{
			if (parameter.ParameterName == "Cost" && parameter.Type == ParameterType.PromptId)
			{
				OptionGrpIds.Add((uint)parameter.PromptId);
			}
		}
		SourceAbilityId = req.GrpId;
		_outboundMessage.Type = ClientMessageType.CastingTimeOptionsResp;
		_outboundMessage.CastingTimeOptionsResp = new CastingTimeOptionsResp
		{
			CastingTimeOptionResp = new CastingTimeOptionResp
			{
				CtoId = req.CtoId,
				CastingTimeOptionType = req.CastingTimeOptionType,
				SelectNResp = new SelectNResp()
			}
		};
	}

	public void SubmitSpecialization(CardColor color)
	{
		CastingTimeOptionResp castingTimeOptionResp = _outboundMessage.CastingTimeOptionsResp.CastingTimeOptionResp;
		castingTimeOptionResp.SelectNResp.Ids.Clear();
		castingTimeOptionResp.SelectNResp.Ids.Add((uint)color);
		Submit();
	}
}
