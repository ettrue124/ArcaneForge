using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class NewTurnStartedEventParser : IAnnotationEventParser, INeedMtgGameState_New, INeedMtgGameState_Old
{
	public MtgGameState newState { private get; set; }

	public MtgGameState oldState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (newState.TryGetPlayer(ai.AffectorId, out var player))
		{
			MtgPlayer previousPlayer = ((oldState != null && oldState.ActivePlayer != null) ? oldState.ActivePlayer : null);
			yield return new TurnChangeEvent(player, previousPlayer);
		}
	}
}
