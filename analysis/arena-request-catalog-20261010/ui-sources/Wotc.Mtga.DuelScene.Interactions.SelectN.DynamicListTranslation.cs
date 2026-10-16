using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using Google.Protobuf.Collections;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class DynamicListTranslation : IChoiceOptionsTranslation
{
	private readonly IContext _context;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public DynamicListTranslation(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context, context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), assetLookupSystem)
	{
	}

	public DynamicListTranslation(IContext context, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, AssetLookupSystem assetLookupSystem)
	{
		_context = context;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public ChoiceOptions Translate(SelectNRequest request)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		Dictionary<string, uint> dictionary = new Dictionary<string, uint>();
		List<string> list = new List<string>();
		RepeatedField<PromptParameter> parameters = request.ReqPrompt.Parameters;
		for (int i = 0; i < request.UnfilteredIds.Count; i++)
		{
			string text = string.Empty;
			PromptParameter promptParameter = parameters[i];
			switch (promptParameter.Type)
			{
			case ParameterType.PromptId:
			{
				IBlackboard blackboard = _assetLookupSystem.Blackboard;
				blackboard.Clear();
				blackboard.Prompt = request.ReqPrompt;
				blackboard.Prompt.PromptId = (uint)promptParameter.PromptId;
				if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<PromptPayload> loadedTree))
				{
					PromptPayload payload = loadedTree.GetPayload(blackboard);
					if (payload != null)
					{
						(string, string)[] array = new (string, string)[payload.ParameterProviders.Count];
						int num = 0;
						foreach (IPromptParameterProvider parameterProvider in payload.ParameterProviders)
						{
							if (parameterProvider.TryGetValue(request, _context, out var paramValue))
							{
								array[num] = (parameterProvider.GetKey(), paramValue);
							}
							num++;
						}
						text = _cardDatabase.ClientLocProvider.GetLocalizedText(payload.Key, array);
						break;
					}
				}
				text = _cardDatabase.PromptEngine.GetPromptText(promptParameter.PromptId);
				if (string.IsNullOrEmpty(text))
				{
					text = $"Missing Prompt {promptParameter.PromptId}";
				}
				break;
			}
			case ParameterType.Number:
				text = _cardDatabase.PromptEngine.GetPromptText(promptParameter.NumberValue);
				break;
			case ParameterType.NonLocalizedString:
				text = promptParameter.StringValue;
				break;
			case ParameterType.Reference:
			{
				Reference reference = promptParameter.Reference;
				switch (reference.Type)
				{
				case ReferenceType.CatalogId:
					text = _cardDatabase.CardTitleProvider.GetCardTitle(reference.Id, null);
					break;
				case ReferenceType.InstanceId:
				{
					MtgCardInstance cardById = mtgGameState.GetCardById(reference.Id);
					text = ((cardById == null) ? $"Cannot find card w/ ID of {reference.Id} for SelectNRequest" : _cardDatabase.CardTitleProvider.GetCardTitle(cardById));
					break;
				}
				case ReferenceType.LocalizationId:
					text = _cardDatabase.PromptEngine.GetPromptText((int)reference.Id);
					break;
				case ReferenceType.PlayerSeatId:
					text = ((mtgGameState.LocalPlayer.InstanceId == reference.Id) ? _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Local_Player") : _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Opponent"));
					break;
				}
				break;
			}
			default:
				throw new NotImplementedException($"Prompt ParameterType of {promptParameter.Type} not implemented");
			}
			dictionary[text] = request.UnfilteredIds[i];
		}
		if (list.Count == 0)
		{
			list.AddRange(dictionary.Keys);
			list.Sort();
		}
		return new ChoiceOptions(dictionary, list, Array.Empty<string>());
	}
}
