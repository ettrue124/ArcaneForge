using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.GameState;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class IntermissionWorkflow : WorkflowBase<IntermissionRequest>, IUpdateWorkflow, IAutoRespondWorkflow
{
	private const uint ATTEMSIS_GRPID = 69831u;

	private const float AVATAR_DEFEAT_EFFECT_DELAY = 2.5f;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IDuelSceneStateController _duelSceneStateController;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly MatchSceneManager _matchSceneManager;

	private readonly ResultReason _resultReason;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private System.Action _executeEndOfGameVfx;

	private bool _submitted;

	private float _delayTimer;

	public IntermissionWorkflow(IntermissionRequest req, IEntityViewProvider entityViewProvider, IGameStateProvider gameStateProvider, IDuelSceneStateController duelSceneStateController, ICardHolderProvider cardHolderProvider, IBrowserController browserController, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: base(req)
	{
		_resultReason = _request.Reason;
		_entityViewProvider = entityViewProvider;
		_gameStateProvider = gameStateProvider;
		_duelSceneStateController = duelSceneStateController;
		_browserController = browserController;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookupSystem;
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderProvider);
	}

	public override bool CanApply(List<UXEvent> events)
	{
		if (_resultReason == ResultReason.Concede)
		{
			return !events.Exists((UXEvent x) => x is CreatePlayerUXEvent);
		}
		return events.Count == 0;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		MtgGameLossData lossData = gameState.GameLossData;
		uint lossEffectGrpId = lossData.EffectCardGrpId;
		_executeEndOfGameVfx = playEndOfGameVfx;
		if (hack_DetectSpecialCase())
		{
			executeSpecialCase();
		}
		else
		{
			_executeEndOfGameVfx?.Invoke();
		}
		void executeSpecialCase()
		{
			if (lossEffectGrpId == 69831)
			{
				List<DuelScene_CDC> list = new List<DuelScene_CDC>();
				foreach (uint cardId in gameState.OpponentHand.CardIds)
				{
					if (_entityViewProvider.TryGetCardView(cardId, out var cardView))
					{
						list.Add(cardView);
					}
				}
				ViewDismissBrowserProvider viewDismissBrowserProvider = new ViewDismissBrowserProvider(list, () =>
				{
					_executeEndOfGameVfx?.Invoke();
				}, Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Revealed_Card_Title"));
				IBrowser openedBrowser = _browserController.OpenBrowser(viewDismissBrowserProvider);
				viewDismissBrowserProvider.SetOpenedBrowser(openedBrowser);
			}
		}
		bool hack_DetectSpecialCase()
		{
			if (lossData.Reason == LossOfGameReason.GameEffect)
			{
				if (lossEffectGrpId == 69831)
				{
					return gameState.LocalPlayer?.InstanceId == lossData.AffectedId;
				}
				return false;
			}
			return false;
		}
		void playEndOfGameVfx()
		{
			uint winningTeamId = _request.WinningTeamId;
			bool flag = gameState.TryGetPlayer(winningTeamId, out var player) && player.IsLocalPlayer;
			AudioManager.SetState("music", flag ? "win" : "loose");
			AudioManager.StopAmbiance();
			foreach (DuelScene_AvatarView allAvatar in _entityViewProvider.GetAllAvatars())
			{
				if (allAvatar.InstanceId != winningTeamId)
				{
					PlayDefeatEffects(allAvatar, lossData, gameState);
				}
			}
			_executeEndOfGameVfx = null;
		}
	}

	public override void CleanUp()
	{
		base.CleanUp();
		_executeEndOfGameVfx?.Invoke();
		_battlefield.ClearCache();
	}

	public void Update()
	{
		if (AppliedState == InteractionAppliedState.Applied && _executeEndOfGameVfx == null && !_submitted)
		{
			if (_delayTimer >= 2.5f)
			{
				SubmitResponse();
			}
			else
			{
				_delayTimer += Time.deltaTime;
			}
		}
	}

	private void SubmitResponse()
	{
		_submitted = true;
		if (_request.Options.Find((UserOption x) => x.ResponseType == ClientMessageType.EnterSideboardingReq) != null)
		{
			_request.SubmitOption(ClientMessageType.EnterSideboardingReq);
		}
		_duelSceneStateController.SetState(gameComplete: true, lockSceneTransitions: false);
	}

	public bool TryAutoRespond()
	{
		if (UnityEngine.Object.FindFirstObjectByType<SideboardInterface>() != null)
		{
			SubmitResponse();
			return true;
		}
		return false;
	}

	private void PlayDefeatEffects(DuelScene_AvatarView avatar, MtgGameLossData lossData, MtgGameState gameState)
	{
		avatar.PlayDefeatEffect(lossData);
		PlayBattlefieldDefeatEffects(lossData, gameState);
	}

	private void PlayBattlefieldDefeatEffects(MtgGameLossData lossData, MtgGameState gameState)
	{
		MtgPlayer playerById = gameState.GetPlayerById(lossData.AffectedId);
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.GameState.GameLossData = lossData;
		_assetLookupSystem.Blackboard.Player = playerById;
		GameLossVFX payload = _assetLookupSystem.TreeLoader.LoadTree<GameLossVFX>().GetPayload(_assetLookupSystem.Blackboard);
		if (payload == null)
		{
			return;
		}
		foreach (VfxData vfxData in payload.VfxDatas)
		{
			_vfxProvider.PlayVFX(vfxData, null, null, _battlefield.Get().Transform);
		}
	}
}
