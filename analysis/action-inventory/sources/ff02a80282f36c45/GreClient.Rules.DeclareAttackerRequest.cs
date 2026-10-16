using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class DeclareAttackerRequest : BaseUserRequest
{
	public readonly IReadOnlyList<Attacker> Attackers;

	public readonly IReadOnlyList<Attacker> QualifiedAttackers;

	public readonly IReadOnlyList<Attacker> DeclaredAttackers;

	public readonly IReadOnlyList<AttackWarning> AttackWarnings;

	public readonly IReadOnlyList<ManaRequirement> ManaRequirements;

	public readonly bool HasRequirements;

	public readonly bool HasRestrictions;

	public readonly bool CanSubmit;

	public DeclareAttackerRequest(DeclareAttackersReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Attackers = req.Attackers;
		QualifiedAttackers = req.QualifiedAttackers;
		AttackWarnings = req.AttackWarnings;
		ManaRequirements = req.GetNonEmptyManaCosts();
		HasRequirements = req.HasRequirements;
		HasRestrictions = req.HasRestrictions;
		CanSubmit = req.CanSubmitAttackers;
		List<Attacker> list = new List<Attacker>(req.Attackers);
		list.RemoveAll((Attacker x) => x.SelectedDamageRecipient == null);
		DeclaredAttackers = list;
	}

	public void UpdateAttacker(params Attacker[] attackers)
	{
		_outboundMessage.Type = ClientMessageType.DeclareAttackersResp;
		_outboundMessage.DeclareAttackersResp = new DeclareAttackersResp();
		DeclareAttackersResp declareAttackersResp = _outboundMessage.DeclareAttackersResp;
		foreach (Attacker item in attackers)
		{
			declareAttackersResp.SelectedAttackers.Add(item);
		}
		Submit();
	}

	public void DeclareAllAttackers(DamageRecipient damageRecipient)
	{
		_outboundMessage.Type = ClientMessageType.DeclareAttackersResp;
		_outboundMessage.DeclareAttackersResp = new DeclareAttackersResp
		{
			AutoDeclare = true,
			AutoDeclareDamageRecipient = damageRecipient
		};
		Submit();
	}

	public void SubmitAttackers()
	{
		_outboundMessage.Type = ClientMessageType.SubmitAttackersReq;
		Submit();
	}
}
