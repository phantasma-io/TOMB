namespace Phantasma.Core.Domain.Triggers.Enums
{
	// Token trigger enum shape must stay stable for compiler source expectations.
	public enum TokenTrigger
	{
		OnMint,
		OnBurn,
		OnSend,
		OnReceive,
		OnInfuse,
		OnAttach,
		OnUpgrade,
		OnSeries,
		OnWrite,
		OnMigrate,
		OnKill,
	}
}
