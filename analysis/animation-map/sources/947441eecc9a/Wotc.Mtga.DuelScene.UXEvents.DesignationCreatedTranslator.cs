using System;
using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DesignationCreatedTranslator : DesignationEventTranslatorBase
{
	public DesignationCreatedTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: base(context, assetLookupSystem)
	{
	}

	public override void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges == null)
		{
			throw new ArgumentNullException("allChanges");
		}
		if (changeIndex < 0 || changeIndex >= allChanges.Count)
		{
			throw new ArgumentOutOfRangeException("changeIndex");
		}
		if (events == null)
		{
			throw new ArgumentNullException("events");
		}
		if (!(allChanges[changeIndex] is DesignationCreatedEvent designationCreatedEvent))
		{
			throw new ArgumentException("Event at given index does not implement DesignationCreatedEvent");
		}
		events.Add(new AddDesignationUXEvent(designationCreatedEvent.Designation, _designationController, _vfxProvider, _assetLookupSystem));
	}
}
