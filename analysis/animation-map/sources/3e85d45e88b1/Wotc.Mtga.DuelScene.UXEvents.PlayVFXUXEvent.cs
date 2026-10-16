using System;
using System.Collections.Generic;
using AssetLookupTree;
using GreClient.CardData;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class PlayVFXUXEvent : UXEvent
{
	private ICardDataAdapter _affectedData;

	private readonly IVfxProvider _vfxProvider;

	private readonly IEnumerable<VfxData> _vfxs;

	public PlayVFXUXEvent(ICardDataAdapter affectedData, IVfxProvider vfxProvider, IEnumerable<VfxData> vfxDatas)
	{
		_vfxProvider = vfxProvider;
		_affectedData = affectedData;
		_vfxs = vfxDatas ?? Array.Empty<VfxData>();
	}

	public override void Execute()
	{
		foreach (VfxData vfx in _vfxs)
		{
			_vfxProvider.PlayVFX(vfx, _affectedData);
		}
		Complete();
	}
}
