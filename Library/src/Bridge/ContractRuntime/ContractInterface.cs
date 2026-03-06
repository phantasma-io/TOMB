using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Core.Domain.Events.Structs;

namespace Phantasma.Core.Domain.Contract;

/// <summary>
/// ABI shape consumed by the compiler/codegen pipeline.
/// Phoenix SDK currently does not expose this compiler ABI container, so TOMB
/// keeps the model locally for deterministic ABI serialization and lookups.
/// </summary>
public sealed class ContractInterface : ISerializable
{
	public static readonly ContractInterface Empty = new ContractInterface(Enumerable.Empty<ContractMethod>(), Enumerable.Empty<ContractEvent>());

	private readonly Dictionary<string, ContractMethod> _methods = new(StringComparer.OrdinalIgnoreCase);
	private ContractEvent[] _events;

	public IEnumerable<ContractMethod> Methods => _methods.Values;
	public IEnumerable<ContractEvent> Events => _events;

	public ContractInterface(IEnumerable<ContractMethod> methods, IEnumerable<ContractEvent> events)
	{
		foreach (var entry in methods)
		{
			_methods[entry.name] = entry;
		}

		_events = events.ToArray();
	}

	public ContractInterface()
	{
		_events = Array.Empty<ContractEvent>();
	}

	public bool HasMethod(string name) => _methods.ContainsKey(name);

	public ContractMethod FindMethod(string name)
	{
		if (_methods.TryGetValue(name, out var method))
		{
			return method;
		}

		throw new InvalidOperationException($"Method not found in ABI: {name}");
	}

	public ContractMethod? TryFindMethod(string name)
	{
		return _methods.TryGetValue(name, out var method) ? method : null;
	}

	public ContractEvent FindEvent(byte value)
	{
		foreach (var evt in _events)
		{
			if (evt.value == value) return evt;
		}

		throw new InvalidOperationException($"Event not found in ABI: {value}");
	}

	public ContractEvent? TryFindEvent(byte value)
	{
		foreach (var evt in _events)
		{
			if (evt.value == value) return evt;
		}

		return null;
	}

	public bool Implements(ContractEvent evt)
	{
		foreach (var entry in Events)
		{
			if (entry.name == evt.name && entry.value == evt.value && entry.returnType == evt.returnType)
			{
				return true;
			}
		}

		return false;
	}

	public bool Implements(ContractMethod method)
	{
		if (!_methods.TryGetValue(method.name, out var thisMethod)) return false;
		if (thisMethod.parameters.Length != method.parameters.Length) return false;

		for (int i = 0; i < method.parameters.Length; i++)
		{
			if (thisMethod.parameters[i].type != method.parameters[i].type)
			{
				return false;
			}
		}

		return true;
	}

	public bool Implements(ContractInterface other)
	{
		foreach (var method in other.Methods)
		{
			if (!Implements(method)) return false;
		}

		foreach (var evt in other.Events)
		{
			if (!Implements(evt)) return false;
		}

		return true;
	}

	public void SerializeData(BinaryWriter writer)
	{
		// The binary layout is part of the compiler artifact contract.
		// Keep method/event ordering and field widths stable.
		writer.Write((byte)_methods.Count);
		foreach (var method in _methods.Values)
		{
			method.Serialize(writer);
		}

		writer.Write((byte)_events.Length);
		foreach (var evt in _events)
		{
			evt.Serialize(writer);
		}
	}

	public void UnserializeData(BinaryReader reader)
	{
		// Reverse operation for ABI payloads emitted by compiler output.
		var methodCount = reader.ReadByte();
		_methods.Clear();
		for (int i = 0; i < methodCount; i++)
		{
			var method = ContractMethod.Unserialize(reader);
			_methods[method.name] = method;
		}

		var eventCount = reader.ReadByte();
		_events = new ContractEvent[eventCount];
		for (int i = 0; i < eventCount; i++)
		{
			_events[i] = ContractEvent.Unserialize(reader);
		}
	}

	public static ContractInterface Unserialize(BinaryReader reader)
	{
		var result = new ContractInterface();
		result.UnserializeData(reader);
		return result;
	}

	public static ContractInterface FromBytes(byte[] bytes)
	{
		using var stream = new MemoryStream(bytes);
		using var reader = new BinaryReader(stream);
		return Unserialize(reader);
	}

	public byte[] ToByteArray()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		SerializeData(writer);
		return stream.ToArray();
	}
}
