using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Duel;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DieRollUXEvent : UXEvent
{
	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IDieRollUxEventData _dieRollUxEventData;

	private readonly IReadOnlyList<DieRollResultData> _rollResultData;

	private readonly GREPlayerNum _controller;

	private IDiceView _diceView;

	private bool _isBlocking;

	public override bool IsBlocking => _isBlocking;

	public DieRollUXEvent(IReadOnlyList<DieRollResultData> rollResultData, IDieRollUxEventData dieRollUxEventData, GREPlayerNum controller, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_rollResultData = rollResultData;
		_dieRollUxEventData = dieRollUxEventData;
		_controller = controller;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
		_isBlocking = true;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return !currentlyRunningEvents.Exists((UXEvent x) => x.HasWeight);
	}

	public override void Execute()
	{
		_diceView = _dieRollUxEventData.InstantiateDiceView(_controller, _vfxProvider, _assetLookupSystem);
		_diceView.RollsCompletedHandlers += OnRollsCompleted;
		_diceView.KeepAndIgnoresCompletedHandlers += OnKeepAndIgnoresCompleted;
		_diceView.Roll(_rollResultData);
	}

	protected override void Cleanup()
	{
		if (_diceView != null)
		{
			_diceView.RollsCompletedHandlers -= OnRollsCompleted;
			_diceView.KeepAndIgnoresCompletedHandlers -= OnKeepAndIgnoresCompleted;
			_diceView.Dispose();
			_diceView = null;
		}
		base.Cleanup();
	}

	private void OnRollsCompleted(IDiceView diceView)
	{
		_isBlocking = false;
		diceView.KeepAndIgnore();
	}

	private void OnKeepAndIgnoresCompleted(IDiceView diceView)
	{
		Complete();
	}
}
