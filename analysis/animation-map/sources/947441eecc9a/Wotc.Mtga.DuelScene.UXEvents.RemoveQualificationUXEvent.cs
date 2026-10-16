using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemoveQualificationUXEvent : QualificationUXEvent
{
	public RemoveQualificationUXEvent(QualificationData data, IQualificationController qualificationController)
		: base(data, qualificationController)
	{
	}

	public override void Execute()
	{
		_qualificationController.RemoveQualification(_data);
		Complete();
	}
}
