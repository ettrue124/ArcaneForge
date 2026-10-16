using System.Collections.Generic;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public static class AnnotationParserFactory
{
	public static IReadOnlyDictionary<AnnotationType, IAnnotationEventParser> CreateEventParsers(IAbilityDataProvider abilityProvider)
	{
		return new Dictionary<AnnotationType, IAnnotationEventParser>
		{
			{
				AnnotationType.DieRoll,
				new DieRollResultEventAnnotationParser()
			},
			{
				AnnotationType.MultistepEffectStarted,
				new MultistepEffectStartedAnnotationParser()
			},
			{
				AnnotationType.MultistepEffectComplete,
				new MultistepEffectCompleteAnnotationParser()
			},
			{
				AnnotationType.PlayerSelectingTargets,
				new PlayerSelectingTargetsAnnotationParser()
			},
			{
				AnnotationType.PlayerSubmittedTargets,
				new PlayerSubmittedTargetsAnnotationParser()
			},
			{
				AnnotationType.CardRevealed,
				new CardRevealedAnnotationParaser()
			},
			{
				AnnotationType.LinkInfo,
				new LinkInfoEventAnnotationParser()
			},
			{
				AnnotationType.DisqualifiedEffect,
				new DisqualifiedEffectAnnotationParser()
			},
			{
				AnnotationType.Scry,
				new ScryEventAnnotationParser()
			},
			{
				AnnotationType.Shuffle,
				new ShuffleAnnotationParser()
			},
			{
				AnnotationType.ObjectIdChanged,
				new ObjectIDChangedAnnotationParser()
			},
			{
				AnnotationType.ManaPaid,
				new ManaPaidAnnotationParser()
			},
			{
				AnnotationType.TokenImmediatelyDied,
				new TokenImmediatelyDiedAnnotationParser()
			},
			{
				AnnotationType.GainDesignation,
				new GainDesignationAnnotationParser()
			},
			{
				AnnotationType.LoseDesignation,
				new LoseDesignationAnnotationParser()
			},
			{
				AnnotationType.PhasedIn,
				new PhasedInAnnotationParser()
			},
			{
				AnnotationType.PhasedOut,
				new PhasedOutAnnotationParser()
			},
			{
				AnnotationType.NewTurnStarted,
				new NewTurnStartedEventParser()
			},
			{
				AnnotationType.PhaseOrStepModified,
				new PhaseModifiedEventParser()
			},
			{
				AnnotationType.AbilityInstanceCreated,
				new AbilityCreatedEventParser()
			},
			{
				AnnotationType.AbilityInstanceDeleted,
				new AbilityDeletedEventParser()
			},
			{
				AnnotationType.TokenCreated,
				new TokenCreatedEventParser()
			},
			{
				AnnotationType.TokenDeleted,
				new TokenDeletedEventParser()
			},
			{
				AnnotationType.ZoneTransfer,
				new ZoneTransferAnnotationParser()
			},
			{
				AnnotationType.TappedUntappedPermanent,
				new TappedUntappedPermanentAnnotationParser()
			},
			{
				AnnotationType.CounterAdded,
				new CounterAddedAnnotationParser()
			},
			{
				AnnotationType.CounterRemoved,
				new CounterRemovedAnnotationParser()
			},
			{
				AnnotationType.ModifiedLife,
				new ModifiedLifeAnnotationParser()
			},
			{
				AnnotationType.DamageDealt,
				new DamageDealtAnnotationParser()
			},
			{
				AnnotationType.CoinFlip,
				new CoinFlipEventParser()
			},
			{
				AnnotationType.ResolutionStart,
				new ResolutionStartEventParser()
			},
			{
				AnnotationType.ResolutionComplete,
				new ResolutionCompleteEventParser()
			},
			{
				AnnotationType.RevealedCardDeleted,
				new RevealedCardDeletedEventParser()
			},
			{
				AnnotationType.ControllerChanged,
				new ControllerChangedAnnotationParser()
			},
			{
				AnnotationType.RevealedCardCreated,
				new RevealedCardCreatedEventParser()
			},
			{
				AnnotationType.ChoiceResult,
				new ChoiceResultEventParser()
			},
			{
				AnnotationType.PendingEffect,
				new PendingEffectEventParser()
			},
			{
				AnnotationType.SyntheticEvent,
				new SyntheticEventAnnotationParser()
			},
			{
				AnnotationType.UserActionTaken,
				new UserActionTakenAnnotationParser()
			},
			{
				AnnotationType.RemoveAttachment,
				new RemoveAttachmentEventParser()
			},
			{
				AnnotationType.ReplacementEffectApplied,
				new ReplacementEffectAppliedEventParser()
			},
			{
				AnnotationType.PowerToughnessModCreated,
				new PowerToughnessModCreatedEventParser()
			},
			{
				AnnotationType.LayeredEffectCreated,
				new LayeredEffectCreatedEventParser(abilityProvider)
			},
			{
				AnnotationType.AttachmentCreated,
				new AttachmentCreatedEventParser()
			},
			{
				AnnotationType.PermanentRegenerated,
				new PermanentRegeneratedEventAnnotationParser()
			}
		};
	}

	public static IReadOnlyDictionary<AnnotationType, IAnnotationStateParser> CreateStateParsers(ICardDatabaseAdapter cardDatabase)
	{
		return new Dictionary<AnnotationType, IAnnotationStateParser>
		{
			{
				AnnotationType.LossOfGame,
				new LossOfGameStateAnnotationParser()
			},
			{
				AnnotationType.Designation,
				new DesignationStateAnnotationParser()
			},
			{
				AnnotationType.RemainingSelections,
				new RemainingSelectionsStateAnnotationParser()
			},
			{
				AnnotationType.TargetSpec,
				new TargetSpecStateAnnotationParser()
			},
			{
				AnnotationType.Attachment,
				new AttachmentStateAnnotationParser()
			},
			{
				AnnotationType.Counter,
				new CounterStateAnnotationParser()
			},
			{
				AnnotationType.AbilityExhausted,
				new ExhaustedAbilityStateAnnotationParser()
			},
			{
				AnnotationType.FaceDown,
				new FaceDownStateAnnotationParser()
			},
			{
				AnnotationType.DelayedTriggerAffectees,
				new DelayedTriggerAffecteesStateAnnotationParser()
			},
			{
				AnnotationType.DisplayCardUnderCard,
				new DisplayCardUnderCardStateAnnotationParser()
			},
			{
				AnnotationType.AbilityWordActive,
				new AbilityWordActiveStateAnnotationParser()
			},
			{
				AnnotationType.SupplementalText,
				new AbilityWordActiveStateAnnotationParser()
			},
			{
				AnnotationType.LayeredEffect,
				new LayeredEffectStateAnnotationParser()
			},
			{
				AnnotationType.ReplacementEffectApplied,
				new ReplacementEffectAppliedStateAnnotationParser()
			},
			{
				AnnotationType.ReplacementEffect,
				new ReplacementEffectStateAnnotationParser()
			},
			{
				AnnotationType.PredictedDirectDamage,
				new PredictedDirectDamageStateAnnotationParser()
			},
			{
				AnnotationType.SwitchPowerToughness,
				new SwitchPowerToughnessStateAnnotationParser()
			},
			{
				AnnotationType.DieRoll,
				new DieRollResultStateAnnotationParser()
			},
			{
				AnnotationType.DungeonStatus,
				new DungeonStateAnnotationParser()
			},
			{
				AnnotationType.LinkedDamage,
				new LinkedDamageAnnotationParser()
			},
			{
				AnnotationType.ClassLevel,
				new ClassLevelStateAnnotationParser()
			},
			{
				AnnotationType.GamewideHistoryCount,
				new GamewideHistoryCountStateAnnotationParser()
			},
			{
				AnnotationType.LinkInfo,
				new LinkInfoStateAnnotationParser()
			},
			{
				AnnotationType.AddAbility,
				new AddAbilityStateAnnotationParser(cardDatabase.AbilityDataProvider)
			},
			{
				AnnotationType.DamagedThisTurn,
				new DamagedThisTurnStateAnnotationParser()
			},
			{
				AnnotationType.ModifiedPower,
				new ModifiedPowerAnnotationParser()
			},
			{
				AnnotationType.ModifiedToughness,
				new ModifiedToughnessAnnotationParser()
			},
			{
				AnnotationType.ModifiedType,
				new ModifiedTypeAnnotationParser()
			},
			{
				AnnotationType.ModifiedColor,
				new ModifiedColorAnnotationParser()
			},
			{
				AnnotationType.ShouldntPlay,
				new ShouldntPlayStateAnnotationParser()
			},
			{
				AnnotationType.SuspendLike,
				new SuspendLikeStateAnnotationParser()
			},
			{
				AnnotationType.MiscContinuousEffect,
				new MiscContinuousEffectStateAnnotationParser()
			},
			{
				AnnotationType.TemporaryPermanent,
				new TemporaryPermanentStateAnnotationParser()
			},
			{
				AnnotationType.ReferencedObjects,
				new ReferencedObjectsStateAnnotationParser()
			},
			{
				AnnotationType.ObjectsSelected,
				new ObjectsSelectedStateAnnotationParser()
			},
			{
				AnnotationType.InstanceRevealedToOpponent,
				new InstanceIdRevealedToOpponentParser()
			},
			{
				AnnotationType.EnteredZoneThisTurn,
				new EnteredZoneThisTurnAnnotationParser()
			},
			{
				AnnotationType.ResolutionStart,
				new ResolutionStartAnnotationParser()
			},
			{
				AnnotationType.ResolutionComplete,
				new ResolutionCompleteAnnotationParser()
			},
			{
				AnnotationType.CastingTimeOption,
				new CastingTimeOptionAnnotationParser()
			},
			{
				AnnotationType.ChoosingAttachments,
				new ChoosingAttachmentsAnnotationParser()
			},
			{
				AnnotationType.RemoveAbility,
				new RemoveAbilityAnnotationParser(cardDatabase)
			},
			{
				AnnotationType.TriggeringObject,
				new TriggeringObjectAnnotationParser()
			},
			{
				AnnotationType.ReferencedCardNames,
				new ReferencedCardNamesAnnotationParser()
			},
			{
				AnnotationType.CrewedThisTurn,
				new CrewedSaddledAnnotationParser()
			},
			{
				AnnotationType.LoyaltyActivationsRemaining,
				new LoyaltyActivationsRemainingAnnotationParser()
			},
			{
				AnnotationType.ManaDetails,
				new ManaDetailsAnnotationParser()
			},
			{
				AnnotationType.UseOrCostsManaCost,
				new UseOrCostsManaCostAnnotationParser()
			},
			{
				AnnotationType.Qualification,
				new QualificationAnnotationParser()
			},
			{
				AnnotationType.GroupedIds,
				new GroupedIdsStateAnnotationParser()
			},
			{
				AnnotationType.AbilityInstanceCreated,
				StateParserFactory.AbilityCreatedStateParser()
			},
			{
				AnnotationType.RegeneratePending,
				new RegeneratePendingStateAnnotationParser()
			},
			{
				AnnotationType.ObjectsToDistinguish,
				new ObjectsToDistinguishAnnotationParser()
			},
			{
				AnnotationType.BoonInfo,
				new BoonInfoAnnotationParser()
			},
			{
				AnnotationType.TurnPermanent,
				new TurnedPermanentAnnotationParser()
			},
			{
				AnnotationType.CopiedObject,
				new CopiedObjectAnnotationParser()
			},
			{
				AnnotationType.SaddledThisTurn,
				new CrewedSaddledAnnotationParser()
			},
			{
				AnnotationType.HiddenInformation,
				new HiddenInformationStateAnnotationParser()
			},
			{
				AnnotationType.LoopCount,
				new LoopCountAnnotationParser()
			},
			{
				AnnotationType.SuppressedPowerAndToughness,
				new SuppressedPowerAndToughnessStateParser()
			},
			{
				AnnotationType.ColorProduction,
				new ColorProductionStateAnnotationParser()
			},
			{
				AnnotationType.CopyException,
				new CopyExceptionAnnotationParser()
			},
			{
				AnnotationType.MultiplayerNumericAid,
				new MultiplayerNumericAidStateParser()
			},
			{
				AnnotationType.HighlightReason,
				new HighlightReasonStateParser()
			},
			{
				AnnotationType.GenericRelationship,
				new GenericRelationshipStateAnnotationParser()
			}
		};
	}
}
