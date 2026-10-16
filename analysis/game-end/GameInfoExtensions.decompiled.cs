using Wotc.Mtgo.Gre.External.Messaging;

public static class GameInfoExtensions
{
	public static ResultSpec ResultForCurrentGame(this GameInfo gameInfo)
	{
		if (gameInfo == null)
		{
			return new ResultSpec();
		}
		int num = (int)(gameInfo.GameNumber - 1);
		if (num >= gameInfo.Results.Count)
		{
			return new ResultSpec();
		}
		return gameInfo.Results[num];
	}
}
