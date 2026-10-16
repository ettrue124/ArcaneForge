using System;
using System.Collections.Generic;
using Wizards.Mtga.Decks;
using Wizards.Unification.Models.PlayBlade;

namespace Wizards.Mtga.PlayBlade;

public class FindMatchBladeData
{
	private readonly IBladeModel _model;

	public IReadOnlyDictionary<PlayBladeQueueType, List<BladeQueueInfo>> Queues => _model.Queues ?? new Dictionary<PlayBladeQueueType, List<BladeQueueInfo>>();

	public IReadOnlyList<DeckViewInfo> Decks => _model.Decks ?? Array.Empty<DeckViewInfo>();

	public FindMatchBladeData(IBladeModel model)
	{
		_model = model;
	}
}
