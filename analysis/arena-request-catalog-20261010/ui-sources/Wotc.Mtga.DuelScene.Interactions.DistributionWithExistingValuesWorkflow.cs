using System.Collections.Generic;
using GreClient.Rules;
using Pooling;
using WorkflowVisuals;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class DistributionWithExistingValuesWorkflow : DistributionWorkflow
{
	private class DistributionData
	{
		public Wotc.Mtgo.Gre.External.Messaging.Distribution Dist;

		public bool LegalTarget;

		public bool Required;
	}

	private readonly IObjectPool _genericPool;

	private bool _canSwapDistValuesWithInvalidIds;

	private uint minDistValue = uint.MaxValue;

	private uint maxDistValue;

	private List<DistributionData> _distributionData = new List<DistributionData>();

	private Dictionary<uint, DistributionData> _distributionsById = new Dictionary<uint, DistributionData>();

	public DistributionWithExistingValuesWorkflow(DistributionRequest request, IObjectPool objectPool, ICardHolderProvider cardHolderProvider, SpinnerController spinnerController)
		: base(request, cardHolderProvider, spinnerController)
	{
		_genericPool = objectPool ?? NullObjectPool.Default;
	}

	protected override void ApplyInteractionInternal()
	{
		List<uint> legalTargetIds = _request.LegalTargetIds;
		List<uint> illegalTargetIds = _request.IllegalTargetIds;
		_canSwapDistValuesWithInvalidIds = _request.LegalTargetIds.Count != _request.RequiredDistributions.Count;
		List<uint> list = _genericPool.PopObject<List<uint>>();
		list.AddRange(_request.ExistingDistributions);
		list.Sort();
		List<uint> list2 = _genericPool.PopObject<List<uint>>();
		list2.AddRange(_request.RequiredDistributions);
		list2.Sort();
		foreach (uint item in legalTargetIds)
		{
			uint num = 0u;
			bool required = false;
			if (list2.Count > 0)
			{
				required = true;
				num = list2[0];
				list2.RemoveAt(0);
			}
			else
			{
				num = list[0];
			}
			list.Remove(num);
			if (minDistValue > num)
			{
				minDistValue = num;
			}
			if (maxDistValue < num)
			{
				maxDistValue = num;
			}
			DistributionData distributionData = new DistributionData
			{
				Dist = new Wotc.Mtgo.Gre.External.Messaging.Distribution
				{
					InstanceId = item,
					Amount = num
				},
				LegalTarget = true,
				Required = required
			};
			_distributionData.Add(distributionData);
			_distributionsById[item] = distributionData;
		}
		foreach (uint item2 in illegalTargetIds)
		{
			uint num2 = list[0];
			list.RemoveAt(0);
			if (_canSwapDistValuesWithInvalidIds)
			{
				if (minDistValue > num2)
				{
					minDistValue = num2;
				}
				if (maxDistValue < num2)
				{
					maxDistValue = num2;
				}
			}
			_distributionData.Add(new DistributionData
			{
				Dist = new Wotc.Mtgo.Gre.External.Messaging.Distribution
				{
					InstanceId = item2,
					Amount = num2
				},
				LegalTarget = false,
				Required = false
			});
		}
		_distributionData.Sort((DistributionData lhs, DistributionData rhs) => lhs.Dist.Amount.CompareTo(rhs.Dist.Amount));
		_spinnerController.ValueChanged += OnValueChanged;
		UpdateSpinners();
		_battlefield.Get().LayoutNow();
		SetButtons();
	}

	private void UpdateSpinners()
	{
		List<SpinnerData> list = _genericPool.PopObject<List<SpinnerData>>();
		foreach (DistributionData distributionDatum in _distributionData)
		{
			if (distributionDatum.LegalTarget)
			{
				Wotc.Mtgo.Gre.External.Messaging.Distribution dist = distributionDatum.Dist;
				list.Add(new SpinnerData(dist.InstanceId, (int)dist.Amount, (int)minDistValue, (int)maxDistValue));
			}
		}
		_spinnerController.Open(list);
		_localHand.Get().SetHandCollapse(collapsed: true);
		_genericPool.PushObject(list);
	}

	private void OnValueChanged(uint id, uint value)
	{
		if (_distributionsById.TryGetValue(id, out var value2))
		{
			uint oldValue = value2.Dist.Amount;
			bool required = value2.Required;
			List<DistributionData> list = _genericPool.PopObject<List<DistributionData>>();
			foreach (DistributionData distributionDatum in _distributionData)
			{
				if (distributionDatum != value2 && (distributionDatum.LegalTarget || (!required && _canSwapDistValuesWithInvalidIds)))
				{
					list.Add(distributionDatum);
				}
			}
			if (value > oldValue)
			{
				DistributionData distributionData = list.Find((DistributionData x) => x.Dist.Amount > oldValue);
				if (distributionData != null)
				{
					value2.Dist.Amount = distributionData.Dist.Amount;
					value2.Required = distributionData.Required;
					distributionData.Dist.Amount = oldValue;
					distributionData.Required = required;
				}
			}
			else if (value < oldValue)
			{
				list.Reverse();
				DistributionData distributionData2 = list.Find((DistributionData x) => x.Dist.Amount < oldValue);
				if (distributionData2 != null)
				{
					value2.Dist.Amount = distributionData2.Dist.Amount;
					value2.Required = distributionData2.Required;
					distributionData2.Dist.Amount = oldValue;
					distributionData2.Required = required;
				}
			}
			_genericPool.PushObject(list);
		}
		_distributionData.Sort((DistributionData lhs, DistributionData rhs) => lhs.Dist.Amount.CompareTo(rhs.Dist.Amount));
		UpdateSpinners();
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit",
			Style = ButtonStyle.StyleType.Main,
			ButtonCallback = () =>
			{
				Dictionary<uint, uint> dictionary = new Dictionary<uint, uint>();
				foreach (DistributionData distributionDatum in _distributionData)
				{
					Wotc.Mtgo.Gre.External.Messaging.Distribution dist = distributionDatum.Dist;
					dictionary[dist.InstanceId] = dist.Amount;
				}
				_request.SubmitDistribution(dictionary);
			},
			Enabled = true
		});
		if (_request.CanCancel)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Wotc.Mtga.Loc.Utils.GetCancelLocKey(_request.CancellationType),
				Style = ButtonStyle.StyleType.Secondary,
				ButtonCallback = () =>
				{
					_request.Cancel();
				},
				ButtonSFX = WwiseEvents.sfx_ui_cancel.EventName
			};
		}
		if (_request.AllowUndo)
		{
			Buttons.UndoData = new PromptButtonData
			{
				ButtonCallback = () =>
				{
					_request.Undo();
				}
			};
		}
		OnUpdateButtons(Buttons);
	}

	public override void CleanUp()
	{
		if (_spinnerController != null)
		{
			_spinnerController.ValueChanged -= OnValueChanged;
		}
		base.CleanUp();
	}
}
