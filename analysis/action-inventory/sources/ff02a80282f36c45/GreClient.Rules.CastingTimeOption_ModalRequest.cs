using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_ModalRequest : BaseUserRequest
{
	public readonly bool IsRequired;

	public readonly bool RepeatSelectionAllowed;

	public readonly uint Min;

	public readonly uint Max;

	public readonly uint AbilityGrpId;

	public readonly List<uint> OtherSelection;

	public IReadOnlyList<uint> ModalOptions { get; private set; }

	public IReadOnlyList<uint> ExcludedOptions { get; private set; }

	public ModalReq ModalRequest { get; private set; }

	public CastingTimeOption_ModalRequest(CastingTimeOptionReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		SourceId = req.AffectorId;
		IsRequired = req.IsRequired;
		RepeatSelectionAllowed = req.ModalReq.RepeatedSelectAllowed;
		Min = req.ModalReq.MinSel;
		Max = req.ModalReq.MaxSel;
		AbilityGrpId = req.ModalReq.AbilityGrpId;
		OtherSelection = new List<uint>(req.ModalReq.OtherSel);
		ModalRequest = req.ModalReq;
		List<ModalOption> list = new List<ModalOption>(req.ModalReq.ModalOptions);
		ModalOptions = list.ConvertAll((ModalOption x) => x.GrpId);
		List<ModalOption> list2 = new List<ModalOption>(req.ModalReq.ExcludedOptions);
		ExcludedOptions = list2.ConvertAll((ModalOption x) => x.GrpId);
		_outboundMessage.Type = ClientMessageType.CastingTimeOptionsResp;
		_outboundMessage.CastingTimeOptionsResp = new CastingTimeOptionsResp
		{
			CastingTimeOptionResp = new CastingTimeOptionResp
			{
				CtoId = req.CtoId,
				CastingTimeOptionType = req.CastingTimeOptionType,
				ChooseModalResp = new ChooseModalResp()
			}
		};
	}

	public void SubmitModal(IEnumerable<uint> grpIds)
	{
		CastingTimeOptionResp castingTimeOptionResp = _outboundMessage.CastingTimeOptionsResp.CastingTimeOptionResp;
		castingTimeOptionResp.ChooseModalResp.GrpIds.Clear();
		castingTimeOptionResp.ChooseModalResp.GrpIds.AddRange(grpIds);
		Submit();
	}
}
