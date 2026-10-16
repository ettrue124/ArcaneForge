using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ManaProducedUXEvent : UXEvent
{
	private readonly uint _sourceId;

	public readonly uint _sinkId;

	private readonly MtgMana _mana;

	private readonly IManaProducedAnimationPlayer _manaProducedAnimationPlayer;

	private string _manaRiderCache;

	public bool Blocking { get; set; }

	public override bool IsBlocking => Blocking;

	private string _manaRiderKey
	{
		get
		{
			string text = _manaRiderCache;
			if (text == null)
			{
				string text2 = _mana.GetManaRiderKey() ?? string.Empty;
				string text3 = text2;
				_manaRiderCache = text2;
				text = text3;
			}
			return text;
		}
	}

	public ManaProducedUXEvent(uint sourceId, uint sinkId, MtgMana mana, IManaProducedAnimationPlayer manaProducedAnimationPlayer)
	{
		_sourceId = sourceId;
		_sinkId = sinkId;
		_mana = mana;
		_manaProducedAnimationPlayer = manaProducedAnimationPlayer;
	}

	public override void Execute()
	{
		_manaProducedAnimationPlayer.PlayAnimation(_sourceId, _sinkId, _mana, Complete);
	}

	public bool IsSame(UXEvent evt)
	{
		if (evt is ManaProducedUXEvent manaProducedUXEvent && CanGroupWith(manaProducedUXEvent) && _mana.Color == manaProducedUXEvent._mana.Color)
		{
			return _manaRiderKey == manaProducedUXEvent._manaRiderKey;
		}
		return false;
	}

	public bool CanGroupWith(ManaProducedUXEvent otherManaProduced)
	{
		if (_sourceId == otherManaProduced._sourceId)
		{
			return _sinkId == otherManaProduced._sinkId;
		}
		return false;
	}

	public override string ToString()
	{
		return string.Format("{0}: Source: {1}, Sink: {2}, Color: {3}, ManaRider: {4}", "ManaProducedUXEvent", _sourceId, _sinkId, _mana.Color, _manaRiderKey);
	}
}
