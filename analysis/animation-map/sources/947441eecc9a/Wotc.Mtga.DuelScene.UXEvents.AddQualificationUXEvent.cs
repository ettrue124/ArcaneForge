using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AddQualificationUXEvent : QualificationUXEvent
{
	public AddQualificationUXEvent(QualificationData data, IQualificationController qualificationController)
		: base(data, qualificationController)
	{
	}

	public override void Execute()
	{
		_qualificationController.AddQualification(_data);
		Complete();
	}
}
