using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class HighlightReasonStateParser : IAnnotationStateParser
{
	private const string DETAIL_REASON = "Reason";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		uint affectorId = ai.AffectorId;
		string stringDetail = ai.GetStringDetail("Reason");
		HighlightReason.ReasonType reason = ToReasonType(stringDetail);
		foreach (uint affectedId in ai.AffectedIds)
		{
			newState.HighlightReasons.Add(new HighlightReason(affectorId, affectedId, reason));
		}
	}

	private HighlightReason.ReasonType ToReasonType(string reason)
	{
		if (reason == "SacrificeResourceIsSourceOfCasualty")
		{
			return HighlightReason.ReasonType.SacrificeResourceIsSourceOfCasualty;
		}
		return HighlightReason.ReasonType.None;
	}
}
