using System;
using System.Collections.Generic;
using Phantasma.Core.Domain.Interfaces;

namespace Phantasma.Business.Blockchain.VM;

/// <summary>
/// VM helper used by compiler-time description/formatting interops.
/// Phoenix SDK provides base VM primitives, while this class preserves TOMB-specific
/// Format.* interop behavior expected by existing compiler logic.
/// </summary>
public abstract class DescriptionVM : VirtualMachine
{
    public DescriptionVM(byte[] script, uint offset) : base(script, offset, null)
    {
    }

    private readonly Dictionary<string, Func<VirtualMachine, ExecutionState>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    protected void RegisterMethod(string name, Func<VirtualMachine, ExecutionState> handler)
    {
        _handlers[name] = handler;
    }

    public abstract IToken FetchToken(string symbol);
    public abstract string OutputAddress(Address address);
    public abstract string OutputSymbol(string symbol);

    public override void DumpData(List<string> lines)
    {
    }

    private static readonly string FormatInteropTag = "Format.";

    public override ExecutionState ExecuteInterop(string method)
    {
        // Handle the historical Format.* interop family inline.
        if (method.StartsWith(FormatInteropTag, StringComparison.Ordinal))
        {
            var op = method.Substring(FormatInteropTag.Length);
            switch (op)
            {
                case "Decimals":
                {
                    var amount = Stack.Pop().AsNumber();
                    var symbol = Stack.Pop().AsString();
                    var info = FetchToken(symbol);
                    var result = UnitConversion.ToDecimal(amount, (uint)info.Decimals);
                    Stack.Push(VMObject.FromObject(result.ToString()));
                    return ExecutionState.Running;
                }
                case "Account":
                {
                    var temp = Stack.Pop();
                    Address addr;
                    if (temp.Type == VMType.String)
                    {
                        var text = temp.AsString();
                        Expect(Address.IsValidAddress(text), "expected valid address");
                        addr = Address.Parse(text, true);
                    }
                    else if (temp.Type == VMType.Bytes)
                    {
                        addr = Address.FromBytes(temp.AsByteArray());
                    }
                    else
                    {
                        addr = temp.AsInterop<Address>();
                    }

                    Stack.Push(VMObject.FromObject(OutputAddress(addr)));
                    return ExecutionState.Running;
                }
                case "Symbol":
                {
                    var symbol = Stack.Pop().AsString();
                    Stack.Push(VMObject.FromObject(OutputSymbol(symbol)));
                    return ExecutionState.Running;
                }
                default:
                    throw new VMException(this, $"unknown interop: {FormatInteropTag}{op}");
            }
        }

        if (_handlers.TryGetValue(method, out var handler))
        {
            return handler(this);
        }

        throw new VMException(this, "unknown interop: " + method);
    }

    public override PhantasmaPhoenix.VM.ExecutionContext LoadContext(string contextName)
    {
        throw new NotImplementedException();
    }
}
