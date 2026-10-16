using System.Collections.Generic;
using Wizards.Arena.Models.Network;
using Wizards.Arena.Promises;
using Wizards.Mtga;
using Wizards.Unification.Models.PlayBlade;

namespace Wotc.Mtga.Network.ServiceWrappers;

public class AwsPlayBladeConfigServiceWrapper : IPlayBladeConfigServiceWrapper
{
	private readonly FrontDoorConnectionAWS _fdc;

	public static IPlayBladeConfigServiceWrapper Create()
	{
		return new AwsPlayBladeConfigServiceWrapper(Pantry.Get<IFrontDoorConnectionServiceWrapper>().FDCAWS);
	}

	public AwsPlayBladeConfigServiceWrapper(FrontDoorConnectionAWS fdcAWS)
	{
		_fdc = fdcAWS;
	}

	public Promise<List<PlayBladeQueueEntry>> GetPlayBladeConfig()
	{
		return _fdc.SendMessage<List<PlayBladeQueueEntry>>(CmdType.GetPlayBladeQueueConfig, (object)null);
	}
}
