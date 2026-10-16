using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class MultiplayerNumericAidStateParser : IAnnotationStateParser
{
	private const string DETAIL_VALUE = "value";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		uint id = ai.Id;
		uint affectorId = ai.AffectorId;
		int num = -1;
		foreach (int @int in ai.GetIntList("value"))
		{
			num++;
			uint instanceId = ai.AffectedIds[num];
			if (newState.TryGetPlayer(instanceId, out var player))
			{
				player.PlayerNumericAids.Add(new PlayerNumericAid(id, affectorId, (uint)@int));
			}
		}
	}
}
