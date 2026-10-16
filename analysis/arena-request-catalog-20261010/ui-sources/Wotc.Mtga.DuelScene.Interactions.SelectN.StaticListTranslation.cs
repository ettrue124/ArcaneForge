using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class StaticListTranslation : IChoiceOptionsTranslation
{
	private readonly Dictionary<StaticList, IChoiceOptionsTranslation> _typeToTranslationMap;

	public StaticListTranslation(Dictionary<StaticList, IChoiceOptionsTranslation> typeToTranslationMap)
	{
		_typeToTranslationMap = typeToTranslationMap;
	}

	public ChoiceOptions Translate(SelectNRequest request)
	{
		if (_typeToTranslationMap.TryGetValue(request.StaticList, out var value))
		{
			return value.Translate(request);
		}
		throw new ArgumentException($"Provided SelectNReq could not be used as a SelectNKeyword request. StaticList type {request.StaticList} is not supported.");
	}
}
