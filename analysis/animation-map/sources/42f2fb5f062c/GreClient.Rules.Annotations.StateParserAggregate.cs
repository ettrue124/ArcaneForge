using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class StateParserAggregate : IAnnotationStateParser
{
	private IAnnotationStateParser[] _elements;

	public StateParserAggregate(params IAnnotationStateParser[] elements)
	{
		_elements = elements ?? Array.Empty<IAnnotationStateParser>();
	}

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		IAnnotationStateParser[] elements = _elements;
		for (int i = 0; i < elements.Length; i++)
		{
			elements[i].Process(ai, oldState, newState);
		}
	}
}
