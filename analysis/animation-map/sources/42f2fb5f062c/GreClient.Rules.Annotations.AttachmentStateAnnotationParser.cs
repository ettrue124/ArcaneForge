using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class AttachmentStateAnnotationParser : IAnnotationStateParser
{
	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.Attachment))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint num = ai.AffectedIds[0];
		uint affectorId = ai.AffectorId;
		if (newState.TryGetCard(affectorId, out var card))
		{
			card.AttachedToId = num;
		}
		if (newState.TryGetEntity(num, out var mtgEntity))
		{
			mtgEntity.AttachedWithIds.Add(affectorId);
		}
		if (newState.TryGetPlayer(num, out var player) && newState.TryGetCard(affectorId, out var card2))
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			AnnotationDataUtils.ExtractDetails(ai, dictionary);
			dictionary.Add("TargetLinkType", "AttributedInstance");
			LinkInfoData item = new LinkInfoData(ai.Id, LinkType.Target, card2.InstanceId, player.InstanceId, 0u, dictionary);
			card2.AffectorOfLinkInfos.Add(item);
		}
	}
}
