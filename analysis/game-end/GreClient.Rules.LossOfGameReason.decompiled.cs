namespace GreClient.Rules;

public enum LossOfGameReason
{
	None = -1,
	LifeTotal,
	DrawFromEmptyLib,
	GameEffect,
	Concede,
	Poison,
	Other
}
