using System;
using System.Collections.Generic;
using System.Linq;

namespace GreClient.Rules.EventGeneration;

public class GameStateEventGeneratorAggregate : IGameStateEventGenerator, IEventGenerator<MtgGameState>
{
	private readonly IGameStateEventGenerator[] _elements;

	public GameStateEventGeneratorAggregate(params IGameStateEventGenerator[] elements)
	{
		_elements = elements ?? Array.Empty<IGameStateEventGenerator>();
	}

	public IEnumerable<GameRulesEvent> Generate(MtgGameState oldState, MtgGameState newState)
	{
		return _elements.SelectMany((IGameStateEventGenerator element) => element.Generate(oldState, newState));
	}
}
