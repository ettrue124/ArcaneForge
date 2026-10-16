using System.Collections.Generic;
using GreClient.Rules;
using UnityEngine;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateQualificationEventTranslator : IEventTranslator
{
	private readonly IQualificationController _controller;

	public UpdateQualificationEventTranslator(IQualificationController controller)
	{
		_controller = controller ?? NullQualificationController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is UpdateQualificationEvent updateQualificationEvent)
		{
			if (newState.TryGetEntity(updateQualificationEvent.AffectedId, out var _))
			{
				events.Add(CreateEvent(updateQualificationEvent.Removed, updateQualificationEvent.Qualification));
				return;
			}
			Debug.LogErrorFormat("QualificationEvent Affected == null\nAffector: {0}, Affected: {1}, Qual: {2}", updateQualificationEvent.Qualification.AffectorId, updateQualificationEvent.AffectedId, updateQualificationEvent.Qualification.Type);
		}
	}

	private UXEvent CreateEvent(bool removed, QualificationData qualification)
	{
		if (removed)
		{
			return new RemoveQualificationUXEvent(qualification, _controller);
		}
		return new AddQualificationUXEvent(qualification, _controller);
	}
}
