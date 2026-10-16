using System.Collections.Generic;
using EventPage.Components.NetworkModels;
using Wizards.Arena.Enums.Event;
using Wizards.Unification.Models.Events;

namespace Wotc.Mtga.Events;

public class BasicEventUXInfo : IEventUXInfo
{
	private readonly EventPage.Components.NetworkModels.EventUXInfo _uxInfo;

	public string PublicEventName => _uxInfo.PublicEventName;

	public string TitleLocKey => EventHelper.GetTitleKeyForEvent(this);

	public virtual int DisplayPriority => _uxInfo.DisplayPriority;

	public bool PrioritizeBannerIfPlayerHasToken => _uxInfo.PrioritizeBannerIfPlayerHasToken;

	public Dictionary<string, string> Parameters => _uxInfo.Parameters;

	public string Group => _uxInfo.Group;

	public EventBladeBehavior EventBladeBehavior => _uxInfo.EventBladeBehavior;

	public bool HasEventPage => _uxInfo.EventBladeBehavior == EventBladeBehavior.EventPage;

	public List<string> DynamicFilterTagIds => _uxInfo.DynamicFilterTagIds;

	public bool OpenedFromPlayBlade { get; set; }

	public List<FactionSealedUXInfo> FactionSealedUXInfo
	{
		get
		{
			return _uxInfo.FactionSealedUXInfo;
		}
		set
		{
			_uxInfo.FactionSealedUXInfo = value;
		}
	}

	public string DeckSelectFormat
	{
		get
		{
			return _uxInfo.DeckSelectFormat;
		}
		set
		{
			_uxInfo.DeckSelectFormat = value;
		}
	}

	public EventPage.Components.NetworkModels.EventComponentData EventComponentData => _uxInfo.EventComponentData;

	public JoinType Joinability
	{
		get
		{
			return _uxInfo.Joinability;
		}
		set
		{
			_uxInfo.Joinability = value;
		}
	}

	public BasicEventUXInfo(EventPage.Components.NetworkModels.EventUXInfo uxInfo)
	{
		_uxInfo = uxInfo;
	}
}
