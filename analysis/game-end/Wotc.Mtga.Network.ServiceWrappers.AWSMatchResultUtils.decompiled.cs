using System;
using System.Collections.Generic;
using Wizards.Arena.Enums.Player;
using Wizards.Arena.Enums.Quest;
using Wizards.MDN.Services.Models.PlayerInventory.CampaignGraph;
using Wizards.Models;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Unification.Models.Booster;
using Wizards.Unification.Models.Deck;
using Wizards.Unification.Models.Event;
using Wizards.Unification.Models.Player;
using Wizards.Unification.Models.Quest;

namespace Wotc.Mtga.Network.ServiceWrappers;

public static class AWSMatchResultUtils
{
	public static PostMatchClientUpdate ConvertToAzure(this MatchResultReport awsMatchResult)
	{
		List<CampaignGraphUpdate> list = null;
		PostMatchClientUpdate postMatchClientUpdate = new PostMatchClientUpdate();
		postMatchClientUpdate.FoundMatch = awsMatchResult.FoundMatch;
		postMatchClientUpdate.battlePassUpdate = null;
		if (awsMatchResult.questUpdates != null)
		{
			postMatchClientUpdate.questUpdate = new List<QuestData>();
			foreach (ClientQuestDescription questUpdate in awsMatchResult.questUpdates)
			{
				postMatchClientUpdate.questUpdate.Add(questUpdate.ConvertToQuestData());
			}
		}
		List<Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem> list2 = new List<Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem>();
		List<Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem> list3 = new List<Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem>();
		if (awsMatchResult.InventoryInfo != null)
		{
			foreach (InventoryChange change in awsMatchResult.InventoryInfo.Changes)
			{
				if (change.Source == InventoryChangeSource.DailyWins)
				{
					list2.Add(change.ConvertToInventoryUpdateReportItem());
				}
				else if (change.Source == InventoryChangeSource.WeeklyWins)
				{
					list3.Add(change.ConvertToInventoryUpdateReportItem());
				}
				else if (change.Source == InventoryChangeSource.CampaignGraphAutomaticPayoutNode)
				{
					if (list == null)
					{
						list = new List<CampaignGraphUpdate>();
					}
					list.Add(new CampaignGraphUpdate
					{
						graphName = GetGraphNameFromInventoryChangeSourceId(change.SourceId),
						inventoryUpdateReportItem = new Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem
						{
							delta = change.ConvertToAzure()
						}
					});
				}
			}
		}
		postMatchClientUpdate.dailyWinUpdates = new PeriodicRewardsTrackUpdate
		{
			InventoryUpdates = ((list2.Count > 0) ? list2 : null),
			PreviousSequenceId = awsMatchResult.periodicRewardsProgress?._dailyRewardPreviousSequenceId,
			CurrentSequenceId = awsMatchResult.periodicRewardsProgress?._dailyRewardCurrentSequenceId
		};
		postMatchClientUpdate.weeklyWinUpdates = new PeriodicRewardsTrackUpdate
		{
			InventoryUpdates = ((list3.Count > 0) ? list3 : null),
			PreviousSequenceId = awsMatchResult.periodicRewardsProgress?._weeklyRewardPreviousSequenceId,
			CurrentSequenceId = awsMatchResult.periodicRewardsProgress?._weeklyRewardCurrentSequenceId
		};
		postMatchClientUpdate.campaignGraphUpdates = list?.ToArray();
		return postMatchClientUpdate;
	}

	private static string GetGraphNameFromInventoryChangeSourceId(string inventoryChangeSourceId)
	{
		if (inventoryChangeSourceId == null)
		{
			return null;
		}
		string[] array = inventoryChangeSourceId.Split('.');
		if (array.Length != 2)
		{
			return inventoryChangeSourceId;
		}
		if (string.IsNullOrWhiteSpace(array[0]))
		{
			return inventoryChangeSourceId;
		}
		return array[0];
	}

	private static Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem ConvertToInventoryUpdateReportItem(this InventoryChange awsInventoryChange)
	{
		return new Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem
		{
			context = awsInventoryChange.Source.ConvertToInventoryUpdateContext(),
			delta = awsInventoryChange.ConvertToAzure(),
			xpGained = awsInventoryChange.SetMasteryProgress
		};
	}

	private static Wizards.Mtga.FrontDoorModels.InventoryUpdateContext ConvertToInventoryUpdateContext(this InventoryChangeSource awsChangeSource)
	{
		Wizards.Mtga.FrontDoorModels.InventoryUpdateContext inventoryUpdateContext = new Wizards.Mtga.FrontDoorModels.InventoryUpdateContext();
		switch (awsChangeSource)
		{
		case InventoryChangeSource.WeeklyWins:
			inventoryUpdateContext.source = InventoryUpdateSource.WeeklyWins;
			break;
		case InventoryChangeSource.DailyWins:
			inventoryUpdateContext.source = InventoryUpdateSource.DailyWins;
			break;
		default:
			inventoryUpdateContext.source = InventoryUpdateSource.Unknown;
			break;
		}
		return inventoryUpdateContext;
	}

	private static QuestData ConvertToQuestData(this ClientQuestDescription awsQuestDescription)
	{
		QuestData questData = new QuestData();
		questData.canSwap = awsQuestDescription.canSwap;
		questData.chestDescription = new ClientChestDescription();
		questData.chestDescription.image1 = awsQuestDescription.chestDescription.image1;
		questData.chestDescription.image1 = awsQuestDescription.chestDescription.image1;
		questData.chestDescription.image2 = awsQuestDescription.chestDescription.image2;
		questData.chestDescription.image3 = awsQuestDescription.chestDescription.image3;
		questData.chestDescription.prefab = awsQuestDescription.chestDescription.prefab;
		questData.chestDescription.referenceId = awsQuestDescription.chestDescription.referenceId;
		questData.chestDescription.headerLocKey = awsQuestDescription.chestDescription.headerLocKey;
		questData.chestDescription.descriptionLocKey = awsQuestDescription.chestDescription.descriptionLocKey;
		questData.chestDescription.quantity = awsQuestDescription.chestDescription.quantity;
		questData.chestDescription.locParams = awsQuestDescription.chestDescription.locParams;
		questData.chestDescription.availableDate = awsQuestDescription.chestDescription.availableDate;
		questData.endingProgress = awsQuestDescription.endingProgress;
		questData.startingProgress = awsQuestDescription.startingProgress;
		questData.goal = awsQuestDescription.goal;
		questData.questId = awsQuestDescription.questId;
		questData.locKey = awsQuestDescription.locKey;
		questData.tileResourceId = awsQuestDescription.tileResourceId;
		questData.treasureResourceId = awsQuestDescription.treasureResourceId;
		questData.questTrack = awsQuestDescription.questTrack.ConvertToAzure();
		questData.isNewQuest = awsQuestDescription.isNewQuest;
		if (awsQuestDescription.inventoryUpdate != null)
		{
			questData.inventoryUpdate = awsQuestDescription.inventoryUpdate.ConvertToAzure();
		}
		questData.hoursWaitAfterComplete = awsQuestDescription.hoursWaitAfterComplete;
		return questData;
	}

	private static Wizards.Mtga.FrontDoorModels.QuestPoolEnum ConvertToAzure(this QuestPool awsEnum)
	{
		return awsEnum switch
		{
			QuestPool.Default => Wizards.Mtga.FrontDoorModels.QuestPoolEnum.Default, 
			QuestPool.EpicQuests => Wizards.Mtga.FrontDoorModels.QuestPoolEnum.EpicQuests, 
			QuestPool.Promotional => Wizards.Mtga.FrontDoorModels.QuestPoolEnum.Promotional, 
			_ => Wizards.Mtga.FrontDoorModels.QuestPoolEnum.Default, 
		};
	}

	public static Guid[] ConvertToAzure(this Dictionary<Guid, List<DeckCard>> deckCards)
	{
		List<Guid> list = new List<Guid>(deckCards.Keys);
		list.Sort();
		return list.ToArray();
	}

	public static int[] ConvertToAzure(this List<GrantedCard> grantedCards)
	{
		List<int> list = new List<int>();
		foreach (GrantedCard grantedCard in grantedCards)
		{
			if (grantedCard.CardAdded && !grantedCard.IsGrantedWithDeck)
			{
				list.Add(grantedCard.GrpId);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		list.Sort();
		return list.ToArray();
	}

	public static CustomTokenDeltaInfo[] ConvertToAzure(this Dictionary<string, int> inventoryCustomTokens)
	{
		if (inventoryCustomTokens == null)
		{
			return null;
		}
		CustomTokenDeltaInfo[] array = new CustomTokenDeltaInfo[inventoryCustomTokens.Count];
		int num = 0;
		foreach (KeyValuePair<string, int> inventoryCustomToken in inventoryCustomTokens)
		{
			array[num] = new CustomTokenDeltaInfo
			{
				id = inventoryCustomToken.Key,
				delta = inventoryCustomToken.Value
			};
		}
		return array;
	}

	public static T[] ToArray<T>(this HashSet<T> hs)
	{
		if (hs == null)
		{
			return null;
		}
		T[] array = new T[hs.Count];
		int num = 0;
		foreach (T h in hs)
		{
			array[num] = h;
			num++;
		}
		return array;
	}

	public static Wizards.Mtga.FrontDoorModels.BoosterStack[] ConvertToAzure(this List<Wizards.Unification.Models.Booster.BoosterStack> awsBoosterStackList)
	{
		Wizards.Mtga.FrontDoorModels.BoosterStack[] array = new Wizards.Mtga.FrontDoorModels.BoosterStack[awsBoosterStackList.Count];
		uint num = 0u;
		foreach (Wizards.Unification.Models.Booster.BoosterStack awsBoosterStack in awsBoosterStackList)
		{
			array[num] = new Wizards.Mtga.FrontDoorModels.BoosterStack
			{
				collationId = awsBoosterStack.CollationId,
				count = awsBoosterStack.Count
			};
			num++;
		}
		return array;
	}
}
