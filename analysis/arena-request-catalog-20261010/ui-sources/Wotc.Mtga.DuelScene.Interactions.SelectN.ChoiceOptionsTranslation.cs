using System;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ChoiceOptionsTranslation : IChoiceOptionsTranslation
{
	private readonly IChoiceOptionsTranslation _staticTranslation;

	private readonly IChoiceOptionsTranslation _dynamicTranslation;

	public ChoiceOptionsTranslation(IChoiceOptionsTranslation staticTranslation, IChoiceOptionsTranslation dynamicTranslation)
	{
		_staticTranslation = staticTranslation;
		_dynamicTranslation = dynamicTranslation;
	}

	public ChoiceOptions Translate(SelectNRequest request)
	{
		SelectionListType listType = request.ListType;
		if (listType == SelectionListType.Static || listType == SelectionListType.StaticSubset)
		{
			return _staticTranslation.Translate(request);
		}
		if (request.ListType == SelectionListType.Dynamic && request.IdType == IdType.PromptParameterIndex)
		{
			return _dynamicTranslation.Translate(request);
		}
		throw new ArgumentException($"Provided SelectNReq could not be used as a SelectNKeyword request. StaticList type {request.StaticList} is not supported.");
	}
}
