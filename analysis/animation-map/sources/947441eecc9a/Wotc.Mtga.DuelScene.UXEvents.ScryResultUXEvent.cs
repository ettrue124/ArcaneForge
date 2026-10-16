using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.UXEventData;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using Pooling;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ScryResultUXEvent : UXEvent
{
	private const float MaxCardsBeforeScalingStagger = 4f;

	private readonly ScryResultEvent _eventData;

	private readonly ScryResultUXEvent_Data _visualData;

	private readonly Queue<DuelScene_CDC> _pendingAnimation;

	private readonly List<DuelScene_CDC> _animating;

	private readonly IObjectPool _objectPool;

	private readonly IUnityObjectPool _unityObjPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ISplineMovementSystem _movementSystem;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IEntityDialogControllerProvider _dialogueProvider;

	private readonly CardHolderReference<LibraryCardHolder> _libraryCardHolder;

	private float _timer;

	private float _stagger;

	public ScryResultUXEvent(ScryResultEvent eventData, IObjectPool objectPool, IUnityObjectPool unityObjPool, ICardDatabaseAdapter cardDatabase, ICardHolderProvider cardHolderProvider, ICardViewProvider cardViewProvider, IBrowserManager browserManager, IEntityDialogControllerProvider dialogueProvider, ISplineMovementSystem movementSystem, AssetLookupSystem assetLookupSystem)
	{
		_eventData = eventData;
		_objectPool = objectPool;
		_unityObjPool = unityObjPool;
		_cardDatabase = cardDatabase;
		_movementSystem = movementSystem;
		_libraryCardHolder = new CardHolderReference<LibraryCardHolder>(cardHolderProvider, _eventData.PlayerEnum, CardHolderType.Library);
		_cardViewProvider = cardViewProvider;
		_browserManager = browserManager;
		_dialogueProvider = dialogueProvider;
		assetLookupSystem.Blackboard.Clear();
		ScryResultData payload = assetLookupSystem.TreeLoader.LoadTree<ScryResultData>().GetPayload(assetLookupSystem.Blackboard);
		_visualData = AssetLoader.GetObjectData(payload.SryResultUXEventDataRef);
		_pendingAnimation = _objectPool.PopObject<Queue<DuelScene_CDC>>();
		_animating = _objectPool.PopObject<List<DuelScene_CDC>>();
		_browserManager.BrowserOpened += OnBrowserOpened;
	}

	public override void Execute()
	{
		if (_dialogueProvider.TryGetDialogControllerByPlayerType(_eventData.PlayerEnum, out var dialogController))
		{
			Dictionary<string, string> dictionary = _objectPool.PopObject<Dictionary<string, string>>();
			dictionary.Add("topCount", _eventData.TopIds.Count.ToString());
			dictionary.Add("bottomCount", _eventData.BottomIds.Count.ToString());
			string empty = string.Empty;
			CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(_eventData.EffectGrpId);
			if (cardPrintingById != null)
			{
				dictionary.Add("source", _cardDatabase.CardTitleProvider.GetCardTitle(cardPrintingById.GrpId, null));
				empty = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/ScryEffectResultsEmote", dictionary.AsTuples());
			}
			else
			{
				empty = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/ScryResultsEmote", dictionary.AsTuples());
			}
			dialogController.ShowPlayerChoice(empty);
			_objectPool.PushObject(dictionary);
		}
		else
		{
			Debug.LogErrorFormat("ScryResultUXEvent could not find Avatar object for player enum: {0}", _eventData.PlayerEnum);
		}
		LibraryCardHolder libraryCardHolder = _libraryCardHolder.Get();
		if (libraryCardHolder != null)
		{
			if (!string.IsNullOrEmpty(_visualData.PerScryAudioEvent))
			{
				AudioManager.PlayAudio(_visualData.PerScryAudioEvent, libraryCardHolder.gameObject);
			}
			if (_visualData.ParticleEffectPrefab != null)
			{
				GameObject gameObject = _unityObjPool.PopObject(_visualData.ParticleEffectPrefab);
				gameObject.transform.parent = libraryCardHolder.transform;
				gameObject.transform.localPosition = _visualData.ParticleEffectOffset;
				gameObject.transform.localEulerAngles = _visualData.ParticleEffectRotation;
				gameObject.transform.localScale = _visualData.ParticleEffectScale;
				gameObject.AddOrGetComponent<SelfCleanup>();
			}
			foreach (uint affectedId in _eventData.AffectedIds)
			{
				if (_cardViewProvider.TryGetCardView(affectedId, out var cardView) && libraryCardHolder.CardViews.Contains(cardView))
				{
					_movementSystem.AddTemporaryGoal(cardView.Root, new IdealPoint(cardView.Root));
					_pendingAnimation.Enqueue(cardView);
				}
			}
			if ((float)_eventData.AffectedIds.Count > 4f)
			{
				_stagger = 3f * _visualData.Stagger / (float)(_eventData.AffectedIds.Count - 1);
			}
			else
			{
				_stagger = _visualData.Stagger;
			}
		}
		else
		{
			Debug.LogErrorFormat("ScryResultUXEvent could not find library for player enum: {0}", _eventData.PlayerEnum);
		}
	}

	private void OnBrowserOpened(BrowserBase browser)
	{
		if (_animating.Count != 0 || _pendingAnimation.Count != 0)
		{
			_pendingAnimation.Clear();
			while (_animating.Count > 0)
			{
				DuelScene_CDC duelScene_CDC = _animating[0];
				_movementSystem.RemoveTemporaryGoal(duelScene_CDC.Root);
				_animating.Remove(duelScene_CDC);
			}
			_libraryCardHolder.ClearCache();
			Complete();
		}
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (IsComplete)
		{
			return;
		}
		if (_pendingAnimation.Count > 0)
		{
			_timer -= dt;
			if (_timer <= 0f)
			{
				_timer += _stagger;
				AnimatePendingScryResult();
			}
		}
		_animating.RemoveAll((DuelScene_CDC x) => !x);
		if (_timeRunning > _stagger && _pendingAnimation.Count == 0 && _animating.Count == 0)
		{
			Complete();
		}
	}

	protected override void Cleanup()
	{
		base.Cleanup();
		_pendingAnimation.Clear();
		_objectPool.PushObject(_pendingAnimation, tryClear: false);
		_animating.Clear();
		_objectPool.PushObject(_animating, tryClear: false);
		_browserManager.BrowserOpened -= OnBrowserOpened;
		Resources.UnloadAsset(_visualData);
	}

	private void AnimatePendingScryResult()
	{
		while (_pendingAnimation.Count > 0)
		{
			DuelScene_CDC duelScene_CDC = _pendingAnimation.Dequeue();
			if ((bool)duelScene_CDC)
			{
				AnimateCDC(duelScene_CDC);
				break;
			}
		}
	}

	private void AnimateCDC(DuelScene_CDC cdc)
	{
		_animating.Add(cdc);
		if (!string.IsNullOrEmpty(_visualData.PerCardAudioEvent))
		{
			AudioManager.PlayAudio(_visualData.PerCardAudioEvent, cdc.gameObject);
		}
		bool flag = _eventData.TopIds.Contains(cdc.InstanceId);
		SplineMovementData spline;
		if (_eventData.PlayerEnum != GREPlayerNum.LocalPlayer)
		{
			spline = (flag ? _visualData.ToTopSpline_Opponent : _visualData.ToBottomSpline_Opponent);
		}
		else
		{
			spline = (flag ? _visualData.ToTopSpline_Local : _visualData.ToBottomSpline_Local);
		}
		SplineEventData splineEventData = new SplineEventData();
		splineEventData.Events.Add(new SplineEventCallback(1f, (float _) =>
		{
			if ((bool)cdc)
			{
				_movementSystem.RemoveTemporaryGoal(cdc.Root);
				_animating.Remove(cdc);
			}
		}));
		IdealPoint layoutEndpoint = _libraryCardHolder.Get().GetLayoutEndpoint(cdc);
		_movementSystem.AddTemporaryGoal(cdc.Root, layoutEndpoint, allowInteractions: false, spline, splineEventData);
	}
}
