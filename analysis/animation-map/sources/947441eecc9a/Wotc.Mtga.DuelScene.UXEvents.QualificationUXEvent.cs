using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class QualificationUXEvent : UXEvent
{
	protected readonly QualificationData _data;

	protected readonly IQualificationController _qualificationController;

	public QualificationUXEvent(QualificationData data, IQualificationController qualificationController)
	{
		_data = data;
		_qualificationController = qualificationController ?? NullQualificationController.Default;
	}
}
