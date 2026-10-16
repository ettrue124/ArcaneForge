using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_ChooseOrCostRequest : BaseUserRequest
{
	private List<KeyValuePair<uint, uint>> _options = new List<KeyValuePair<uint, uint>>();

	public readonly uint GrpId;

	public readonly int Min;

	public readonly uint Max;

	public List<KeyValuePair<uint, uint>> Options => new List<KeyValuePair<uint, uint>>(_options);

	public CastingTimeOption_ChooseOrCostRequest(CastingTimeOptionReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		if (req.SelectNReq.Prompt != null && req.SelectNReq.Prompt.PromptId != 0)
		{
			Prompt = req.SelectNReq.Prompt;
		}
		SourceId = req.SelectNReq.SourceId;
		GrpId = req.GrpId;
		Min = req.SelectNReq.MinSel;
		Max = req.SelectNReq.MaxSel;
		if (req.SelectNReq.ListType == SelectionListType.Dynamic)
		{
			if (req.SelectNReq.IdType == IdType.PromptParameterIndex)
			{
				for (int i = 0; i < req.SelectNReq.Ids.Count; i++)
				{
					int index = (int)(req.SelectNReq.Ids[i] - 1);
					PromptParameter promptParameter = req.SelectNReq.Prompt.Parameters[index];
					_options.Add(new KeyValuePair<uint, uint>((uint)promptParameter.PromptId, req.SelectNReq.Ids[i]));
				}
			}
			_outboundMessage.Type = ClientMessageType.CastingTimeOptionsResp;
			_outboundMessage.CastingTimeOptionsResp = new CastingTimeOptionsResp
			{
				CastingTimeOptionResp = new CastingTimeOptionResp
				{
					CastingTimeOptionType = req.CastingTimeOptionType,
					CtoId = req.CtoId,
					SelectNResp = new SelectNResp()
				}
			};
			return;
		}
		throw new InvalidOperationException("This list type is not yet supported by CastingTimeOption_ChooseOrCostRequest CastTimeReqs: " + req.SelectNReq.ListType);
	}

	public void SubmitChoice(params uint[] choices)
	{
		CastingTimeOptionResp castingTimeOptionResp = _outboundMessage.CastingTimeOptionsResp.CastingTimeOptionResp;
		castingTimeOptionResp.SelectNResp.Ids.Clear();
		castingTimeOptionResp.SelectNResp.Ids.AddRange(choices);
		Submit();
	}
}
