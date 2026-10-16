namespace GreClient.Rules;

public struct MtgGameLossData
{
	public uint AffectorId;

	public uint AffectedId;

	public LossOfGameReason Reason;

	public uint EffectCardGrpId;
}
