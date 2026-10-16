using System;
using System.Collections.Generic;
using GreClient.CardData;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public abstract class MtgEntity
{
	public uint InstanceId;

	public Dictionary<CounterType, int> Counters = new Dictionary<CounterType, int>();

	public List<CounterData> CounterDatas = new List<CounterData>();

	public List<AbilityWordData> ActiveAbilityWords = new List<AbilityWordData>();

	public List<DesignationData> Designations = new List<DesignationData>();

	public List<QualificationData> AffectorOfQualifications = new List<QualificationData>();

	public List<QualificationData> AffectedByQualifications = new List<QualificationData>();

	public List<ReplacementEffectData> ReplacementEffects = new List<ReplacementEffectData>();

	public HashSet<uint> AttachedWithIds = new HashSet<uint>();

	public HashSet<GamewideCountData> GamewideCounts = new HashSet<GamewideCountData>();

	public List<LinkInfoData> AffectorOfLinkInfos = new List<LinkInfoData>();

	public List<LinkInfoData> AffectedByLinkInfos = new List<LinkInfoData>();

	public List<MtgEntity> TargetedBy = new List<MtgEntity>();

	public List<uint> TargetedByIds = new List<uint>();

	public List<MtgAbilityInstance> AbilityInstances = new List<MtgAbilityInstance>();

	public List<AbilityPrintingData> Abilities = new List<AbilityPrintingData>();

	public HashSet<uint> AbilityOriginalCardGrpIds = new HashSet<uint>();

	public List<RemovedAbilityData> AbilityRemovers = new List<RemovedAbilityData>();

	public List<AddedAbilityData> AbilityAdders = new List<AddedAbilityData>();

	public List<AbilityModification> AbilityModifications = new List<AbilityModification>();

	public List<LayeredEffectData> LayeredEffects = new List<LayeredEffectData>();

	public MtgEntity GetCopy()
	{
		if (!(this is MtgCardInstance mtgCardInstance))
		{
			if (this is MtgPlayer mtgPlayer)
			{
				return mtgPlayer.GetCopy();
			}
			MtgEntity mtgEntity = (MtgEntity)MemberwiseClone();
			CopyValuesTo(mtgEntity);
			return mtgEntity;
		}
		return mtgCardInstance.GetCopy();
	}

	protected virtual void CopyValuesTo(MtgEntity target)
	{
		target.Counters = new Dictionary<CounterType, int>(Counters);
		target.CounterDatas = new List<CounterData>(CounterDatas);
		target.ActiveAbilityWords = new List<AbilityWordData>(ActiveAbilityWords);
		target.Designations = new List<DesignationData>(Designations);
		target.AffectorOfQualifications = new List<QualificationData>(AffectorOfQualifications);
		target.AffectedByQualifications = new List<QualificationData>(AffectedByQualifications);
		target.ReplacementEffects = new List<ReplacementEffectData>(ReplacementEffects);
		target.AttachedWithIds = new HashSet<uint>(AttachedWithIds);
		target.AffectorOfLinkInfos = new List<LinkInfoData>(AffectorOfLinkInfos);
		target.AffectedByLinkInfos = new List<LinkInfoData>(AffectedByLinkInfos);
		target.TargetedBy = new List<MtgEntity>(TargetedBy);
		target.TargetedByIds = new List<uint>(TargetedByIds);
		IEnumerable<AbilityPrintingData> abilities = Abilities;
		target.Abilities = new List<AbilityPrintingData>(abilities ?? Array.Empty<AbilityPrintingData>());
		target.AbilityOriginalCardGrpIds = new HashSet<uint>(AbilityOriginalCardGrpIds);
		target.AbilityRemovers = new List<RemovedAbilityData>(AbilityRemovers);
		target.AbilityAdders = new List<AddedAbilityData>(AbilityAdders);
		target.AbilityModifications = new List<AbilityModification>(AbilityModifications);
		target.AbilityInstances = new List<MtgAbilityInstance>(AbilityInstances);
	}
}
