using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DungeonStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_DUNGEON_GRPID = "CurrentDungeon";

	public const string DETAIL_DUNGEON_ZCID = "CurrentDungeonZCID";

	public const string DETAIL_ROOM_GRPID = "CurrentRoom";

	public const string DETAIL_ALL_COMPLETED = "AllDungeonsCompleted";

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
		if (!ai.Type.Contains(AnnotationType.DungeonStatus))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (newState.TryGetPlayer(ai.AffectorId, out var player))
		{
			player.DungeonState = new DungeonData(GetDungeonGrpId(ai), GetDungeonZcId(ai), GetRoomGrpId(ai), GetAllDungeonsCompleted(ai));
		}
	}

	private static uint GetDungeonGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("CurrentDungeon") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	private static uint GetDungeonZcId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("CurrentDungeonZCID") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	private static uint GetRoomGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("CurrentRoom") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	private static uint[] GetAllDungeonsCompleted(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("AllDungeonsCompleted") && detail.ValueInt32.Count > 0)
			{
				uint[] array = new uint[detail.ValueInt32.Count];
				for (int i = 0; i < detail.ValueInt32.Count; i++)
				{
					array[i] = (uint)detail.ValueInt32[i];
				}
				return array;
			}
		}
		return Array.Empty<uint>();
	}
}
