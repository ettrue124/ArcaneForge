using System;
using System.Collections.Generic;
using Wizards.MDN.Services.Models.Event;

namespace Wizards.Mtga.PlayBlade;

public class BladeEventInfo
{
	public string EventName;

	public string FormatName;

	public DeckFormat Format;

	public string LocShortTitle;

	public string BackgroundImagePath;

	public string BladeImagePath;

	public string LogoImagePath;

	public string RankImagePath;

	public bool IsBotMatch;

	public bool IsLimited;

	public bool IsRanked;

	public string DeckFormat;

	public string LocDescription;

	public string LocTitle;

	public BladeTimerType TimerType;

	public DateTime StartTime;

	public DateTime LockTime;

	public DateTime CloseTime;

	public MatchWinCondition WinCondition;

	public List<string> DynamicFilterTagIds;

	public bool IsInProgress;

	public int TotalProgressPips = -1;

	public int PlayerProgress = -1;
}
