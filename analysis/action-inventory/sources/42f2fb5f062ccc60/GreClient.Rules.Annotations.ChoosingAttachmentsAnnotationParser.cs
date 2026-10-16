using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ChoosingAttachmentsAnnotationParser : IAnnotationStateParser
{
	private const string DETAIL_TYPE_SELECTIONS = "Selections";

	private const string DETAIL_TYPE_SELECTING_FOR = "SelectingFor";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.ChoosingAttachments))
		{
			throw new ArgumentException("Annotation type does not match parser type ChoosingAttachments.");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affector id.", "ai");
		}
		uint affectorId = ai.AffectorId;
		List<uint> affectedIds = new List<uint>(ai.AffectedIds);
		int num = -1;
		num = GetSelectingForIndex(ai, affectedIds);
		List<uint> selectionsIds = GetSelectionsIds(ai, affectedIds);
		newState.ChoosingAttachmentsInfos.Add(new ChoosingAttachmentsInfo(affectorId, affectedIds, selectionsIds, num));
	}

	private int GetSelectingForIndex(AnnotationInfo ai, List<uint> affectedIds)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("SelectingFor") && detail?.ValueInt32 != null && detail.ValueInt32.Count > 0)
			{
				uint item = (uint)detail.ValueInt32[0];
				return affectedIds.IndexOf(item);
			}
		}
		return -1;
	}

	private List<uint> GetSelectionsIds(AnnotationInfo ai, List<uint> affectedIds)
	{
		List<uint> list = new List<uint>();
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("Selections") && detail?.ValueInt32 != null)
			{
				for (int i = 0; i < detail.ValueInt32.Count; i++)
				{
					list.Add((uint)detail.ValueInt32[i]);
				}
				break;
			}
		}
		while (list.Count < affectedIds.Count)
		{
			list.Insert(0, 0u);
		}
		return list;
	}
}
