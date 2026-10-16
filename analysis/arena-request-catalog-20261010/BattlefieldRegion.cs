using System.Collections.Generic;
using Pooling;
using UnityEngine;
using Wizards.Mtga.Utils;
using Wotc.Mtga.CardParts;
using Wotc.Mtga.DuelScene.Universal;

namespace Wotc.Mtga.DuelScene.Battlefield;

public class BattlefieldRegion
{
	private readonly struct LayoutVariantRects(Rect bounds, Rect leftPagingArrow, Rect rightPagingArrow)
	{
		public readonly Rect Bounds = bounds;

		public readonly Rect LeftPagingArrow = leftPagingArrow;

		public readonly Rect RightPagingArrow = rightPagingArrow;
	}

	private string _regionName;

	public RegionPagingButton LeftPagingButton;

	public RegionPagingButton RightPagingButton;

	private readonly PageHandler _pageHandler;

	private readonly LayoutDataGenerator _layoutGenerator;

	private readonly GroupValidator _regionValidator;

	private readonly Dictionary<BattlefieldCardHolder.BattlefieldStack, float> _widthsByStack = new Dictionary<BattlefieldCardHolder.BattlefieldStack, float>();

	private readonly IReadOnlyList<LayoutVariantRects> _initialLayoutRects;

	private readonly IReadOnlyList<BattlefieldRegionDefinition.LayoutVariant> _layoutVariants;

	private readonly StackAgeTracker _stackAges = new StackAgeTracker();

	private readonly BlockerAndAgeComparer _blockerAndAgeComparer;

	private readonly HashSet<ScaffoldShape> _stackShapes = new HashSet<ScaffoldShape>();

	private readonly Dictionary<ScaffoldShape, List<BattlefieldCardHolder.BattlefieldStack>> _stacksByShape = new Dictionary<ScaffoldShape, List<BattlefieldCardHolder.BattlefieldStack>>();

	public readonly List<BattlefieldCardHolder.BattlefieldStack> Stacks = new List<BattlefieldCardHolder.BattlefieldStack>();

	public BattlefieldRegionDefinition RegionDef { get; private set; }

	public bool Opponent { get; private set; }

	public Rect CardBounds { get; private set; }

	public Transform RegionLocator { get; private set; }

	public BattlefieldRegionDefinition.LayoutVariant LayoutVariant { get; private set; }

	public Rect LeftArrowButtonRect { get; private set; }

	public Rect RightArrowButtonRect { get; private set; }

	public int PagedOutLeftCount => _pageHandler.PagedOutLeftCardCount;

	public int PagedOutRightCount => _pageHandler.PagedOutRightCardCount;

	public BinarySemaphore PagingAccess { get; private set; } = new BinarySemaphore();

	public bool HasPendingVariantCalculation { get; private set; }

	public List<DuelScene_CDC> AllCards { get; private set; } = new List<DuelScene_CDC>(3);

	public BattlefieldRegion(BattlefieldRegionDefinition regionDef, GroupValidator regionValidator, string regionName, bool opponent, IBattlefieldCardHolder holder, IObjectPool objectPool)
	{
		RegionDef = regionDef;
		Opponent = opponent;
		_layoutVariants = regionDef.CreateInstancedVariants();
		List<LayoutVariantRects> list = new List<LayoutVariantRects>();
		foreach (BattlefieldRegionDefinition.LayoutVariant layoutVariant in _layoutVariants)
		{
			list.Add(new LayoutVariantRects(layoutVariant.Bounds, layoutVariant.LeftPagingArrow, layoutVariant.RightPagingArrow));
		}
		_initialLayoutRects = list;
		RegionLocator = new GameObject().transform;
		RegionLocator.parent = holder.Transform;
		LayoutVariant = _layoutVariants[0];
		RegionLocator.gameObject.name = regionName + " : " + LayoutVariant.Name;
		RegionLocator.position = new Vector3(LayoutVariant.Bounds.center.x, 0f, LayoutVariant.Bounds.center.y);
		_pageHandler = new PageHandler(objectPool);
		_layoutGenerator = new LayoutDataGenerator(_pageHandler, _widthsByStack, _stacksByShape);
		_regionValidator = regionValidator;
		_regionName = regionName;
		_blockerAndAgeComparer = new BlockerAndAgeComparer(_stackAges);
	}

	public void SetLayoutOffset(Vector3 offset)
	{
		for (int i = 0; i < _layoutVariants.Count; i++)
		{
			BattlefieldRegionDefinition.LayoutVariant layoutVariant = _layoutVariants[i];
			LayoutVariantRects layoutVariantRects = _initialLayoutRects[i];
			layoutVariant.Bounds.x = layoutVariantRects.Bounds.x + offset.x;
			layoutVariant.Bounds.y = layoutVariantRects.Bounds.y + offset.z;
			layoutVariant.LeftPagingArrow.x = layoutVariantRects.LeftPagingArrow.x + offset.x;
			layoutVariant.LeftPagingArrow.y = layoutVariantRects.LeftPagingArrow.y + offset.z;
			layoutVariant.RightPagingArrow.x = layoutVariantRects.RightPagingArrow.x + offset.x;
			layoutVariant.RightPagingArrow.y = layoutVariantRects.RightPagingArrow.y + offset.z;
		}
		RegionLocator.position = new Vector3(LayoutVariant.Bounds.center.x, 0f, LayoutVariant.Bounds.center.y);
	}

	public void PageRight()
	{
		_pageHandler.PageRight();
	}

	public void PageLeft()
	{
		_pageHandler.PageLeft();
	}

	public bool IsInVisibleStack(DuelScene_CDC cardView)
	{
		return _pageHandler.IsCardVisible(cardView);
	}

	public bool IsPagedLeft(DuelScene_CDC cardView)
	{
		return _pageHandler.IsCardPagedLeft(cardView);
	}

	public bool IsPagedRight(DuelScene_CDC cardView)
	{
		return _pageHandler.IsCardPagedRight(cardView);
	}

	public void RemoveCard(DuelScene_CDC cardView)
	{
		foreach (BattlefieldCardHolder.BattlefieldStack stack in Stacks)
		{
			stack.AllCards.Remove(cardView);
			stack.StackedCards.Remove(cardView);
		}
		AllCards.Remove(cardView);
	}

	public bool ContainsCardView(DuelScene_CDC cardView)
	{
		foreach (BattlefieldCardHolder.BattlefieldStack stack in Stacks)
		{
			if (stack.AllCards.Contains(cardView))
			{
				return true;
			}
		}
		return false;
	}

	public bool CardIsValid(ValidatorBlackboard blackboard)
	{
		return _regionValidator.IsValidInGroup(blackboard);
	}

	public void SortStacksByAge()
	{
		_stackAges.SyncTo(Stacks);
		Stacks.Sort(_stackAges);
	}

	public void SortStacksByBlocking(IReadOnlyDictionary<uint, float> attackerPositions)
	{
		_stackAges.SyncTo(Stacks);
		_blockerAndAgeComparer.SetAttackerPositions(attackerPositions);
		Stacks.Sort(_blockerAndAgeComparer);
	}

	public void RemapStackAge(uint oldInstanceId, uint newInstanceId)
	{
		_stackAges.RemapAge(oldInstanceId, newInstanceId);
	}

	public void RemapStackTitleId(uint oldTitleId, uint newTitleId)
	{
		_stackAges.RemapTitleId(oldTitleId, newTitleId);
	}

	public void GenerateData(List<CardLayoutData> layoutData, Vector3 attackOffset, Vector3 tappedRotation, List<BattlefieldCardHolder.StackCounterData> counterData, float battlefieldHeight, List<Rect> takenRects, bool calcVariant = true)
	{
		AllCards.Clear();
		_stackShapes.Clear();
		_stacksByShape.Clear();
		foreach (BattlefieldCardHolder.BattlefieldStack stack in Stacks)
		{
			AllCards.AddRange(stack.AllCards);
			ScaffoldShape stackShape = GetStackShape(stack);
			if (stackShape != ScaffoldShape.None)
			{
				if (_stacksByShape.TryGetValue(stackShape, out var value))
				{
					value.Add(stack);
				}
				else
				{
					_stacksByShape.Add(stackShape, new List<BattlefieldCardHolder.BattlefieldStack> { stack });
				}
				_stackShapes.Add(stackShape);
			}
		}
		if (Stacks.Count == 0)
		{
			LayoutVariant = _layoutVariants[0];
			return;
		}
		BattlefieldRegionDefinition.LayoutVariant layoutVariant = CalculateIdealVariant(_layoutVariants, AllCards.Count, takenRects);
		if ((LayoutVariant == null) | calcVariant)
		{
			HasPendingVariantCalculation = false;
			LayoutVariant = layoutVariant;
		}
		else if (LayoutVariant != layoutVariant)
		{
			HasPendingVariantCalculation = true;
			_widthsByStack.Clear();
		}
		if (LayoutVariant != null)
		{
			RegionLocator.position = new Vector3(LayoutVariant.Bounds.center.x, 0f, LayoutVariant.Bounds.center.y);
			RegionLocator.gameObject.name = _regionName + " " + LayoutVariant.Name;
			if (LayoutVariant.UsesPaging)
			{
				takenRects.Add(LeftArrowButtonRect);
				takenRects.Add(RightArrowButtonRect);
			}
			takenRects.Add(CardBounds);
			_layoutGenerator.GenerateLayoutDatas(CardBounds, LayoutVariant, Stacks, layoutData, attackOffset, tappedRotation, counterData, battlefieldHeight);
			BattlefieldCardHolder.DebugDraw.Square(CardBounds, Opponent ? Color.red : Color.blue, 5f);
			if (LayoutVariant.UsesPaging)
			{
				BattlefieldCardHolder.DebugDraw.Square(LeftArrowButtonRect, Color.magenta, 5f);
				BattlefieldCardHolder.DebugDraw.Square(RightArrowButtonRect, Color.magenta, 5f);
			}
		}
	}

	private ScaffoldShape GetStackShape(BattlefieldCardHolder.BattlefieldStack stack)
	{
		if ((bool)stack.StackParent)
		{
			return stack.StackParent.ActiveScaffold.Shape;
		}
		return ScaffoldShape.None;
	}

	private BattlefieldRegionDefinition.LayoutVariant CalculateIdealVariant(IReadOnlyList<BattlefieldRegionDefinition.LayoutVariant> layoutVariants, int totalCardCount, IReadOnlyList<Rect> takenRects)
	{
		foreach (BattlefieldRegionDefinition.LayoutVariant layoutVariant in layoutVariants)
		{
			if (StackLimitReached(layoutVariant) || (!layoutVariant.SupportedShapes.Contains(ScaffoldShape.None) && !_stackShapes.IsSubsetOf(layoutVariant.SupportedShapes)) || (layoutVariant.RequiresAllSubRegionShapes && !_stackShapes.IsSupersetOf(layoutVariant.SupportedShapes)))
			{
				continue;
			}
			float num = CalculateStackWidths(layoutVariant);
			SetupBounds(layoutVariant, takenRects);
			if (!layoutVariant.UsesPaging)
			{
				if (num / (float)layoutVariant.RowCount > CardBounds.width)
				{
					continue;
				}
				bool flag = true;
				if (layoutVariant.TryGetSingleLayout(out var layout))
				{
					flag = CanFitInSubRegion(layout, layout.CalcBounds(CardBounds), Stacks);
				}
				else
				{
					foreach (KeyValuePair<ScaffoldShape, List<BattlefieldCardHolder.BattlefieldStack>> item in _stacksByShape)
					{
						if (layoutVariant.TryGetLayout(item.Key, out layout) && !CanFitInSubRegion(layout, layout.CalcBounds(CardBounds), item.Value))
						{
							flag = false;
							break;
						}
					}
				}
				if (!flag)
				{
					continue;
				}
			}
			return layoutVariant;
		}
		return null;
	}

	private bool StackLimitReached(BattlefieldRegionDefinition.LayoutVariant variant)
	{
		if (variant.TryGetSingleLayout(out var layout))
		{
			return stackLimitReached(layout, Stacks);
		}
		foreach (KeyValuePair<ScaffoldShape, List<BattlefieldCardHolder.BattlefieldStack>> item in _stacksByShape)
		{
			if (variant.TryGetLayout(item.Key, out layout) && stackLimitReached(layout, item.Value))
			{
				return true;
			}
		}
		return false;
		static bool stackLimitReached(BattlefieldRegionDefinition.SubRegion subRegion, IReadOnlyList<BattlefieldCardHolder.BattlefieldStack> stacks)
		{
			if (subRegion.StackCountLimit > 0)
			{
				return stacks.Count > subRegion.StackCountLimit;
			}
			return false;
		}
	}

	private bool CanFitInSubRegion(BattlefieldRegionDefinition.SubRegion subRegion, Rect bounds, IReadOnlyList<BattlefieldCardHolder.BattlefieldStack> stacks)
	{
		int num = 0;
		float num2 = bounds.width;
		for (int i = 0; i < stacks.Count; i++)
		{
			BattlefieldCardHolder.BattlefieldStack key = stacks[i];
			float num3 = _widthsByStack[key];
			if (num2 >= num3)
			{
				num2 -= num3;
				num2 -= subRegion.HorizontalGutter;
				continue;
			}
			num++;
			if (num == subRegion.RowCount)
			{
				return false;
			}
			num2 = bounds.width;
			num2 -= num3;
			num2 -= subRegion.HorizontalGutter;
		}
		return true;
	}

	private float CalculateStackWidths(BattlefieldRegionDefinition.LayoutVariant variant)
	{
		float num = 0f;
		_widthsByStack.Clear();
		if (variant.TryGetSingleLayout(out var layout))
		{
			foreach (BattlefieldCardHolder.BattlefieldStack stack in Stacks)
			{
				float num2 = stack.CalcWidth(layout.CardScale, layout.StackWidth, layout.MinStackWidth);
				num += num2;
				_widthsByStack[stack] = num2;
			}
		}
		else
		{
			foreach (KeyValuePair<ScaffoldShape, List<BattlefieldCardHolder.BattlefieldStack>> item in _stacksByShape)
			{
				if (!variant.TryGetLayout(item.Key, out layout))
				{
					continue;
				}
				foreach (BattlefieldCardHolder.BattlefieldStack item2 in item.Value)
				{
					float num3 = item2.CalcWidth(layout.CardScale, layout.StackWidth, layout.MinStackWidth);
					num += num3;
					_widthsByStack[item2] = num3;
				}
			}
		}
		return num;
	}

	private void SetupBounds(BattlefieldRegionDefinition.LayoutVariant variant, IReadOnlyList<Rect> takenRects)
	{
		LeftArrowButtonRect = variant.LeftPagingArrow;
		RightArrowButtonRect = variant.RightPagingArrow;
		CardBounds = variant.Bounds;
		if (variant.UsesPaging)
		{
			foreach (Rect takenRect in takenRects)
			{
				if (LeftArrowButtonRect.Overlaps(takenRect))
				{
					Vector2 vector = new Vector2(-0.005f, 0f);
					Rect leftArrowButtonRect = LeftArrowButtonRect;
					while (leftArrowButtonRect.Overlaps(takenRect))
					{
						leftArrowButtonRect.center += vector;
					}
					LeftArrowButtonRect = leftArrowButtonRect;
				}
				if (RightArrowButtonRect.Overlaps(takenRect))
				{
					Vector2 vector2 = new Vector2(0.005f, 0f);
					Rect rightArrowButtonRect = RightArrowButtonRect;
					while (rightArrowButtonRect.Overlaps(takenRect))
					{
						rightArrowButtonRect.center += vector2;
					}
					RightArrowButtonRect = rightArrowButtonRect;
				}
			}
		}
		foreach (Rect takenRect2 in takenRects)
		{
			if (!CardBounds.Overlaps(takenRect2))
			{
				continue;
			}
			Rect cardBounds = CardBounds;
			if (cardBounds.center.x > takenRect2.center.x)
			{
				while (cardBounds.xMin < takenRect2.xMax)
				{
					Debug.DrawRay(new Vector3(cardBounds.xMin, 1f, cardBounds.center.y), Vector3.up, Color.yellow, 2f);
					cardBounds.xMin += 0.005f;
				}
			}
			else
			{
				while (cardBounds.xMax > takenRect2.xMin)
				{
					Debug.DrawRay(new Vector3(cardBounds.xMax, 1f, cardBounds.center.y), Vector3.up, Color.cyan, 2f);
					cardBounds.xMax -= 0.005f;
				}
			}
			CardBounds = cardBounds;
		}
		if (variant.UsesPaging)
		{
			Rect cardBounds2 = CardBounds;
			while (cardBounds2.xMin < RightArrowButtonRect.xMax)
			{
				cardBounds2.xMin += 0.005f;
			}
			while (cardBounds2.xMax > LeftArrowButtonRect.xMin)
			{
				cardBounds2.xMax -= 0.005f;
			}
			CardBounds = cardBounds2;
		}
	}

	public void DrawGizmos()
	{
		int count = _layoutVariants.Count;
		float num = count;
		for (int i = 0; i < count; i++)
		{
			BattlefieldRegionDefinition.LayoutVariant layoutVariant = _layoutVariants[i];
			GizmosDrawSquare(layoutVariant.Bounds, Color.Lerp(Color.black, Color.white, (float)i / num));
			if (layoutVariant.UsesPaging)
			{
				GizmosDrawSquare(layoutVariant.LeftPagingArrow, Color.Lerp(Color.black, Color.cyan, (float)i / num));
				GizmosDrawSquare(layoutVariant.RightPagingArrow, Color.Lerp(Color.black, Color.magenta, (float)i / num));
			}
		}
	}

	private static void GizmosDrawSquare(Rect rect, Color color)
	{
		Gizmos.color = color;
		Gizmos.DrawLine(new Vector3(rect.xMin, 0.1f, rect.yMin), new Vector3(rect.xMin, 0.1f, rect.yMax));
		Gizmos.DrawLine(new Vector3(rect.xMin, 0.1f, rect.yMax), new Vector3(rect.xMax, 0.1f, rect.yMax));
		Gizmos.DrawLine(new Vector3(rect.xMax, 0.1f, rect.yMax), new Vector3(rect.xMax, 0.1f, rect.yMin));
		Gizmos.DrawLine(new Vector3(rect.xMax, 0.1f, rect.yMin), new Vector3(rect.xMin, 0.1f, rect.yMin));
	}
}
