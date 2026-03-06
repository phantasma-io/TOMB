using System.Numerics;
using PhantasmaPhoenix.VM;

namespace TOMBLib.Tests.Bridge
{
	// Test VM helpers for deterministic stack pop conversions.
	public static class VirtualMachineStackExtensions
	{
		public static string PopString(this VirtualMachine vm, string fieldName)
		{
			return vm.Stack.Pop().AsString();
		}

		public static BigInteger PopNumber(this VirtualMachine vm, string fieldName)
		{
			return vm.Stack.Pop().AsNumber();
		}
	}
}
