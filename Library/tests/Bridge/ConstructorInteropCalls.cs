using System;
using PhantasmaPhoenix.Core;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;

namespace TOMBLib.Tests.Bridge
{
    // Test-only constructor interops used by in-memory VM harness.
    public static class ConstructorInteropCalls
    {
        public static ExecutionState Constructor_ABI(VirtualMachine vm)
        {
            if (vm.Stack.Count > 0)
            {
                var top = vm.Stack.Pop();
                vm.Stack.Push(top);
            }

            return ExecutionState.Running;
        }

        public static ExecutionState Constructor_Address(VirtualMachine vm)
        {
            if (vm.Stack.Count == 0)
            {
                vm.Stack.Push(VMObject.FromObject(Address.Null));
                return ExecutionState.Running;
            }

            var input = vm.Stack.Pop();
            Address value;
            if (input.Type == VMType.String)
            {
                value = Address.Parse(input.AsString(), true);
            }
            else if (input.Type == VMType.Bytes)
            {
                value = Address.FromBytes(input.AsByteArray());
            }
            else if (input.Type == VMType.Object)
            {
                value = input.AsInterop<Address>();
            }
            else
            {
                value = Address.Null;
            }

            vm.Stack.Push(VMObject.FromObject(value));
            return ExecutionState.Running;
        }

        public static ExecutionState Constructor_Hash(VirtualMachine vm)
        {
            if (vm.Stack.Count == 0)
            {
                vm.Stack.Push(VMObject.FromObject(Hash.Null));
                return ExecutionState.Running;
            }

            var input = vm.Stack.Pop();
            Hash value;
            if (input.Type == VMType.String)
            {
                value = Hash.FromString(input.AsString());
            }
            else if (input.Type == VMType.Bytes)
            {
                value = Hash.FromBytes(input.AsByteArray());
            }
            else if (input.Type == VMType.Object)
            {
                value = input.AsInterop<Hash>();
            }
            else
            {
                value = Hash.Null;
            }

            vm.Stack.Push(VMObject.FromObject(value));
            return ExecutionState.Running;
        }

        public static ExecutionState Constructor_Timestamp(VirtualMachine vm)
        {
            if (vm.Stack.Count == 0)
            {
                vm.Stack.Push(VMObject.FromObject((Timestamp)0u));
                return ExecutionState.Running;
            }

            var input = vm.Stack.Pop();
            Timestamp value;
            if (input.Type == VMType.Number)
            {
                value = (Timestamp)(uint)input.AsNumber();
            }
            else if (input.Type == VMType.String && uint.TryParse(input.AsString(), out var ticks))
            {
                value = (Timestamp)ticks;
            }
            else if (input.Type == VMType.Object)
            {
                value = input.AsInterop<Timestamp>();
            }
            else
            {
                value = (Timestamp)0u;
            }

            vm.Stack.Push(VMObject.FromObject(value));
            return ExecutionState.Running;
        }
    }
}
