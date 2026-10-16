using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdatePendingEffectEventTranslator : IEventTranslator
{
	private readonly IPendingEffectController _controller;

	public UpdatePendingEffectEventTranslator(IPendingEffectController controller)
	{
		_controller = controller ?? NullPendingEffectController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (!(allChanges[changeIndex] is UpdatePendingEffectEvent { AffectedId: var affectedId } updatePendingEffectEvent))
		{
			return;
		}
		if (updatePendingEffectEvent.Removed)
		{
			if (affectedId != 0 && newState.TryGetEntity(affectedId, out var _))
			{
				events.Add(new RemovePendingEffectUXEvent(updatePendingEffectEvent.PendingEffect, _controller));
			}
			return;
		}
		uint affectorId = updatePendingEffectEvent.PendingEffect.AffectorId;
		if (affectedId != 0 && affectorId != 0 && newState.TryGetEntity(affectedId, out var mtgEntity2) && (newState.TryGetCard(affectorId, out var card) || oldState.TryGetCard(affectorId, out card)))
		{
			events.Add(new AddPendingEffectUXEvent(card, mtgEntity2, updatePendingEffectEvent.PendingEffect, _controller));
		}
	}
}
