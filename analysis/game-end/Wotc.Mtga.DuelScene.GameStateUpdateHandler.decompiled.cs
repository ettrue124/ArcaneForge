using System;
using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene;

public class GameStateUpdateHandler : IClientUpdateHandler<GameStateUpdate>
{
	private readonly IReadOnlyList<IClientUpdateHandler<GameStateUpdate>> _elements;

	public GameStateUpdateHandler(IReadOnlyList<IClientUpdateHandler<GameStateUpdate>> elements)
	{
		_elements = elements ?? Array.Empty<IClientUpdateHandler<GameStateUpdate>>();
	}

	public void Handle(GameStateUpdate clientUpdate)
	{
		foreach (IClientUpdateHandler<GameStateUpdate> element in _elements)
		{
			element.Handle(clientUpdate);
		}
	}
}
