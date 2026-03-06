namespace Phantasma.Core.Domain.Triggers.Enums
{
    // Contract trigger enum shape must stay stable for compiler source expectations.
    public enum ContractTrigger
    {
        OnMint,
        OnBurn,
        OnSend,
        OnReceive,
        OnWitness,
        OnUpgrade,
        OnMigrate,
        OnKill,
    }
}
