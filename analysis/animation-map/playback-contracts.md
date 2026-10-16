# Arena visual playback contracts

Recovered with ILSpy from the installed client. Includes all nonnested UXEvent-named classes in DuelScene.UXEvents plus playback classes reached by annotation/event mapping. Base classes, scheduling and tutorial events are included; these are not a count of unique Unity clips or cosmetic variations.

| Playback class | Asset lookup types | Asset/audio field references | Animation/effect symbol candidates | Direct audio events | Decompiled source |
|---|---|---|---|---|---|
| ResolutionEffectModel |  |  | IgnoreCoinFlipEffects, IgnoreDamageEffects, IgnoreDestroyEffects, ResolutionEffectModel, SuppressProjectileDamageEffects, ignoreCoinFlipEffects, ignoreDamageEffects, ignoreDestroyEffects, suppressProjectileDamageEffects |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.ResolutionEffectModel.cs) |
| AbilityAddedOrRemovedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AbilityAddedOrRemovedUXEvent.cs) |
| AbilityAddedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AbilityAddedUXEvent.cs) |
| AbilityRemovedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AbilityRemovedUXEvent.cs) |
| AddDelayedTriggerUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AddDelayedTriggerUXEvent.cs) |
| AddDesignationUXEvent | AddedVfx |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AddDesignationUXEvent.cs) |
| AddGamewideCountUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AddGamewideCountUXEvent.cs) |
| AddPendingEffectUXEvent |  |  | AddPendingEffect, AddPendingEffectUXEvent, IPendingEffectController, PendingEffectData, PendingEffectUXEvent, _pendingEffectController, pendingEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AddPendingEffectUXEvent.cs) |
| AddPlayerNumericAidUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AddPlayerNumericAidUXEvent.cs) |
| AddQualificationUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AddQualificationUXEvent.cs) |
| AddReplacementEffectUXEvent | StartSFX, StartVFX | AudioEvents | AddReplacementEffectUXEvent, IReplacementEffectController, ReplacementEffect, ReplacementEffectData, ReplacementEffectUXEvent, TryAddReplacementEffect, replacementEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AddReplacementEffectUXEvent.cs) |
| AnonimityUXEventGrouper |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AnonimityUXEventGrouper.cs) |
| AttachmentAddedUXEventGrouper |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AttachmentAddedUXEventGrouper.cs) |
| AttackDecrementUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AttackDecrementUXEvent.cs) |
| AttackLobUXEvent |  |  | ISplineMovementSystem, SplineAnimation | sfx_npe_target | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.AttackLobUXEvent.cs) |
| CallbackUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CallbackUXEvent.cs) |
| CardNamedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CardNamedUXEvent.cs) |
| CardReactionUXEvent |  | PlayReactionAnimation, ReactionAnimation | PlayReactionAnimation, ReactionAnimation |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CardReactionUXEvent.cs) |
| CardViewImmediateUpdateUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CardViewImmediateUpdateUXEvent.cs) |
| ChoiceResultUXEvent_General | ChoiceResult | AudioEvents |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ChoiceResultUXEvent_General.cs) |
| ChoiceResultUXEvent_Permanent | ChoiceResult | AudioEvents | EffectsRoot, LayeredEffects |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ChoiceResultUXEvent_Permanent.cs) |
| ChoiceResultUXEvent_Random |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ChoiceResultUXEvent_Random.cs) |
| ChooseRandomUXEvent_CoinFlip | CoinFlipData | CoinFlipUXEventDataRef, HeadsAnimation, TailsAnimation | Animation, AnimationClip, HeadsAnimation, TailsAnimation |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ChooseRandomUXEvent_CoinFlip.cs) |
| CounterProducedUXEvent |  | PlayAnimation | ICounterProducedAnimationPlayer, PlayAnimation, _counterProducedAnimationPlayer, counterProducedAnimationPlayer |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CounterProducedUXEvent.cs) |
| CountersChangedUXEvent | HurtSfx | AudioEvents |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CountersChangedUXEvent.cs) |
| CreatePlayerUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CreatePlayerUXEvent.cs) |
| CreateSubCardHolders |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CreateSubCardHolders.cs) |
| CreateZoneUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.CreateZoneUXEvent.cs) |
| DeckShuffleUXEvent |  |  |  | sfx_basicloc_shuff | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.DeckShuffleUXEvent.cs) |
| DelayedTriggerUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.DelayedTriggerUXEvent.cs) |
| DeletePlayerUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.DeletePlayerUXEvent.cs) |
| DeleteZoneUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.DeleteZoneUXEvent.cs) |
| DesignationUXEventBase |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.DesignationUXEventBase.cs) |
| DesignationUXEventGrouper |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.DesignationUXEventGrouper.cs) |
| DieRollUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.DieRollUXEvent.cs) |
| DisqualifiedEffectUXEvent | EffectDisqualified_SFX, EffectDisqualified_VFX | AudioEvents | DisqualifiedEffectUXEvent, EffectDisqualified_SFX, EffectDisqualified_VFX, EffectsRoot, GetAllGameEffects, IGameEffectController, NullGameEffectController, _gameEffectController, allGameEffect, gameEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.DisqualifiedEffectUXEvent.cs) |
| GameEndUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.GameEndUXEvent.cs) |
| GameStatePlaybackCommencedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.GameStatePlaybackCommencedUXEvent.cs) |
| GameStatePlaybackCompletedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.GameStatePlaybackCompletedUXEvent.cs) |
| GamewideCountUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.GamewideCountUXEvent.cs) |
| GrePromptUXEvent | GrePromptPrefabs |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.GrePromptUXEvent.cs) |
| HypotheticalActionsUXChangedEvent |  |  | AddActionEffect, IActionEffectController, NullActionEffectController, RemoveActionEffect, _actionEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.HypotheticalActionsUXChangedEvent.cs) |
| IllegalAttachmentUXEvent | ProtectionApplied_SFX, ProtectionApplied_VFX | AudioEvents |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.IllegalAttachmentUXEvent.cs) |
| LifeTotalUpdateUXEvent | LifeGainSfx, LifeGainVfx, LifeLossProjectile, LifeLossSfx, LifeLossVfx | AudioEvents, SplineRef | EffectsRoot, ISplineMovementSystem, PlayLifeChangeEffects, PlayProjectileEffects, Spline, SplineData, SplineEvent, SplineEventCallbackWithParams, SplineEventData, SplineMovementData, SplineRef |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.LifeTotalUpdateUXEvent.cs) |
| LifeTotalUpdateUXEventBase |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.LifeTotalUpdateUXEventBase.cs) |
| ManaProducedUXEvent |  | PlayAnimation | IManaProducedAnimationPlayer, PlayAnimation, _manaProducedAnimationPlayer, manaProducedAnimationPlayer |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ManaProducedUXEvent.cs) |
| MultistepEffectCompletedUXEvent | MultistepEffect_End_SFX, MultistepEffect_End_VFX |  | MultistepEffectCompletedUXEvent, MultistepEffectUXEventBase, MultistepEffect_End_SFX, MultistepEffect_End_VFX, MultistepEffects_FX |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.MultistepEffectCompletedUXEvent.cs) |
| MultistepEffectStartedUXEvent | MultistepEffect_Start_SFX, MultistepEffect_Start_VFX |  | MultistepEffectStartedUXEvent, MultistepEffectUXEventBase, MultistepEffect_Start_SFX, MultistepEffect_Start_VFX, MultistepEffects_FX |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.MultistepEffectStartedUXEvent.cs) |
| MultistepEffectUXEventBase |  | AudioEvents | MultistepEffectUXEventBase |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.MultistepEffectUXEventBase.cs) |
| MutationMergeUXEvent | MutationMergeData | AudioEvents, OverSplineRef, UnderSplineRef | EffectsRoot, ISplineMovementSystem, LayeredEffectData, LayeredEffects, LoopingAnimationManager, OverSplineBirthSfxData, OverSplineBirthVfxData, OverSplineHitSfxData, OverSplineHitVfxData, OverSplineRef, OverSplineTrailVfxData, RemoveAllLoopingEffects, SplineEventAudio, SplineEventCallback, SplineEventData, SplineEventPayloadVFXALT, SplineMovementData, UnderSplineBirthSfxData, UnderSplineBirthVfxData, UnderSplineHitSfxData, UnderSplineHitVfxData, UnderSplineRef, UnderSplineTrailVfxData |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.MutationMergeUXEvent.cs) |
| NPEDialogUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEDialogUXEvent.cs) |
| NPEDismissableDeluxeTooltipUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEDismissableDeluxeTooltipUXEvent.cs) |
| NPEPauseUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEPauseUXEvent.cs) |
| NPEReminderUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEReminderUXEvent.cs) |
| NPEShowBattlefieldHangerUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEShowBattlefieldHangerUXEvent.cs) |
| NPEShowExplainerOnBattlefieldUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEShowExplainerOnBattlefieldUXEvent.cs) |
| NPETooltipBumperUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPETooltipBumperUXEvent.cs) |
| NPEUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEUXEvent.cs) |
| NPEUXEventWithDuration |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEUXEventWithDuration.cs) |
| NPEWarningUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NPEWarningUXEvent.cs) |
| NullUXEventGrouper |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.NullUXEventGrouper.cs) |
| ObjectDeselectedUXEvent |  |  | EffectsRoot, LoopingAnimationManager, RemoveLoopingEffect, _loopingEffectKey |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ObjectDeselectedUXEvent.cs) |
| ObjectSelectedUXEvent | ChoiceVFX |  | EffectsRoot, _loopingEffectKey |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ObjectSelectedUXEvent.cs) |
| ParallelPlaybackUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ParallelPlaybackUXEvent.cs) |
| PendingEffectUXEvent |  |  | IPendingEffectController, NullPendingEffectController, PendingEffectData, PendingEffectUXEvent, _pendingEffectController, pendingEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.PendingEffectUXEvent.cs) |
| PlayVFXUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.PlayVFXUXEvent.cs) |
| PlayerControllerChangedUXEvent | ControllerChangedVFX |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.PlayerControllerChangedUXEvent.cs) |
| QualificationUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.QualificationUXEvent.cs) |
| QuarryHaloUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.QuarryHaloUXEvent.cs) |
| RegeneratedCardUXEvent | RegenerateVFX |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RegeneratedCardUXEvent.cs) |
| RegisterPlayersToBattlefield |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RegisterPlayersToBattlefield.cs) |
| RemoveDelayedTriggerUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RemoveDelayedTriggerUXEvent.cs) |
| RemoveDesignationUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RemoveDesignationUXEvent.cs) |
| RemoveGamewideCountUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RemoveGamewideCountUXEvent.cs) |
| RemovePendingEffectUXEvent |  |  | IPendingEffectController, PendingEffectData, PendingEffectUXEvent, RemovePendingEffect, RemovePendingEffectUXEvent, _pendingEffectController, pendingEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RemovePendingEffectUXEvent.cs) |
| RemovePlayerNumericAidUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RemovePlayerNumericAidUXEvent.cs) |
| RemoveQualificationUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RemoveQualificationUXEvent.cs) |
| RemoveReplacementEffectUXEvent | EndSFX, EndVFX | AudioEvents | IReplacementEffectController, RemoveReplacementEffect, RemoveReplacementEffectUXEvent, ReplacementEffect, ReplacementEffectData, ReplacementEffectUXEvent, replacementEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RemoveReplacementEffectUXEvent.cs) |
| ReplacementEffectAppliedUXEvent | ProtectionApplied_SFX, ProtectionApplied_VFX | AudioEvents | ReplacementEffectAppliedUXEvent |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ReplacementEffectAppliedUXEvent.cs) |
| ReplacementEffectUXEvent |  |  | IReplacementEffectController, ReplacementEffectData, ReplacementEffectUXEvent, replacementEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ReplacementEffectUXEvent.cs) |
| ResolutionEffectUXEventBase |  | AudioEvents, SplineRef | EffectsRoot, IResolutionEffectController, ISplineMovementSystem, NullResolutionEffectController, ResolutionEffectUXEventBase, Spline, SplineData, SplineEventAudio, SplineEventCallbackWithParams, SplineEventData, SplineMovementData, SplineRef, _resolutionEffectController, resolutionEffectController, soundEffect, soundEffects, visualEffect, visualEffects |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ResolutionEffectUXEventBase.cs) |
| ResolutionEventEndedUXEvent |  |  | IResolutionEffectController, ISplineMovementSystem, ResolutionEffectUXEventBase, SplineMovementData, _resolutionEffectController, resolutionEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ResolutionEventEndedUXEvent.cs) |
| ResolutionEventStartedUXEvent |  |  | IResolutionEffectController, ISplineMovementSystem, ResolutionEffectModel, ResolutionEffectUXEventBase, SplineMovementData, _resolutionEffectController, _resolutionEffectModel, resolutionEffectController, resolutionEffectModel |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ResolutionEventStartedUXEvent.cs) |
| ResolutionOverrideUXEvent | EventOverrideVFX |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ResolutionOverrideUXEvent.cs) |
| RevealCardsUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.RevealCardsUXEvent.cs) |
| ScryResultUXEvent | ScryResultData | ParticleEffectPrefab, PerCardAudioEvent, PerScryAudioEvent, SryResultUXEventDataRef | EffectGrpId, ISplineMovementSystem, ParticleEffectOffset, ParticleEffectPrefab, ParticleEffectRotation, ParticleEffectScale, ScryEffectResultsEmote, SplineEventCallback, SplineEventData, SplineMovementData, ToBottomSpline_Local, ToBottomSpline_Opponent, ToTopSpline_Local, ToTopSpline_Opponent, _pendingAnimation |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ScryResultUXEvent.cs) |
| SetCardholderDirtyUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.SetCardholderDirtyUXEvent.cs) |
| SparkyChatterUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.SparkyChatterUXEvent.cs) |
| SuspendLikeInfoCreatedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.SuspendLikeInfoCreatedUXEvent.cs) |
| SuspendLikeInfoRemovedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.SuspendLikeInfoRemovedUXEvent.cs) |
| SuspendLikeInfoUpdatedUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.SuspendLikeInfoUpdatedUXEvent.cs) |
| SyntheticEventUXEvent | PropertyUpdateVFX, SyntheticEventEffects | AudioEvents | SyntheticEventEffects |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.SyntheticEventUXEvent.cs) |
| ToggleCombatUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.ToggleCombatUXEvent.cs) |
| UXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UXEvent.cs) |
| UXEventDamageDealt |  | PlayReactionAnimation | AbortDamageEffect, CombatAnimationPlayer, IResolutionEffectProvider, IgnoreDamageEffects, PlayDamageEffect, PlayReactionAnimation, ResolutionEffect, ResolutionEffectModel, SuppressProjectileDamageEffects, _combatAnimationPlayer, _resolutionEffectProvider, combatAnimationPlayer, resolutionEffect, resolutionEffectProvider |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UXEventDamageDealt.cs) |
| UXEventGroup |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UXEventGroup.cs) |
| UXEventPostProcessAggregate |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UXEventPostProcessAggregate.cs) |
| UXEventPostProcessing |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UXEventPostProcessing.cs) |
| UXEventQueue |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UXEventQueue.cs) |
| UXEventUpdateDecider | DeciderChangeVFX |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UXEventUpdateDecider.cs) |
| UXEventUpdatePhase |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UXEventUpdatePhase.cs) |
| UnlockTutorialButtonUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UnlockTutorialButtonUXEvent.cs) |
| UpdateCardModelUXEvent | BirthSFX, BirthVFX, HitSFX, HitVFX, PrePropertyUpdateVFX, SplineOffsetsPayload, SplinePayload, SustainSFX, SustainVFX, TappedCardVFX, TransformSFX, TransformVFX | AddedReferences, AudioEvents, Reference, ReferenceMap, ReferenceType, RemovedReferences | EffectsRoot, ISplineMovementSystem, PlayTappedEffect, PlayTransformEffect, Spline, SplineData, SplineDataRef, SplineEventAudio, SplineEventCallback, SplineEventCallbackWithParams, SplineEventData, SplineMovementData, SplineOffsetsPayload, SplinePayload | sfx_combat_declare_defenders, sfx_combat_declare_no_attackers, sfx_combat_declare_no_defenders, sfx_combat_declareattackers, sfx_combat_tap, sfx_combat_untap, sfx_exert_active, sfx_exert_break | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateCardModelUXEvent.cs) |
| UpdateDelayedTriggerUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateDelayedTriggerUXEvent.cs) |
| UpdateDesignationUXEvent | AddedVfx |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateDesignationUXEvent.cs) |
| UpdateExtraTurnUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateExtraTurnUXEvent.cs) |
| UpdateGamewideCountUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateGamewideCountUXEvent.cs) |
| UpdateManaPoolUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateManaPoolUXEvent.cs) |
| UpdatePlayerHandSizeUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdatePlayerHandSizeUXEvent.cs) |
| UpdatePlayerNumericAidUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdatePlayerNumericAidUXEvent.cs) |
| UpdateReplacementEffectUXEvent |  |  | IReplacementEffectController, ReplacementEffectData, ReplacementEffectUXEvent, UpdateReplacementEffect, UpdateReplacementEffectUXEvent, replacementEffectController |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateReplacementEffectUXEvent.cs) |
| UpdateRevealedCardUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateRevealedCardUXEvent.cs) |
| UpdateTurnUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateTurnUXEvent.cs) |
| UpdateZoneUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UpdateZoneUXEvent.cs) |
| UserActionTakenUXEvent | UserActionTakenSfx, UserActionTakenVfx | AudioEvents | EffectsRoot, GeneralEffect |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.UserActionTakenUXEvent.cs) |
| WaitForSecondsUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.WaitForSecondsUXEvent.cs) |
| WaitUntilUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.WaitUntilUXEvent.cs) |
| WaitingOnStartingPlayerUXEvent |  |  |  |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.UXEvents.WaitingOnStartingPlayerUXEvent.cs) |
| NullVfxProvider |  |  | GenerateEtbSplineEvents, SplineEvent, playHitEffects |  | [source](sources\3e85d45e88b1\Wotc.Mtga.DuelScene.VFX.NullVfxProvider.cs) |
| ZoneTransferUXEvent | CardMovementVFX, CreateCard_OriginOffsets, DelayPayload, IsCreateEvent, IsDestroyEvent, ToZoneOverridePayload | CombatAnimationPlayer | ActiveResolutionEffect, AddZteVfxToMoveSpline, CombatAnimationPlayer, EffectsRoot, ISplineMovementSystem, IgnoreDestroyEffects, LoopingAnimationManager, PlayCardDestroyedEffect, PlayZoneTransferEffect, RemoveAllLoopingEffectsDuringZoneTransfer, SetupSplineEventsALT, SplineEventCallbackWithParams, SplineEventData, ZoneTransferEffect, _zoneTransferEffect, combatAnimationPlayer |  | [source](sources\3e85d45e88b1\ZoneTransferUXEvent.cs) |

The asset field names are code references. Their concrete Unity bundle payloads, runtime-selected variants and visible timing remain unverified. See animation-map.md for message-to-playback candidates.
