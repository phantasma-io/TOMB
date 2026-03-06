using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace Phantasma.Tomb.CodeGen
{
	public struct BuiltinArg
	{
		public readonly string Name;
		public readonly VarType Type;

		public BuiltinArg(string name, VarType type)
		{
			Name = name;
			Type = type;
		}
	}

	public readonly struct BuiltinInfo
	{
		public readonly string Alias;
		public readonly string LibraryName;
		public readonly string MethodName;
		public readonly VarKind ReturnType;
		public readonly BuiltinArg[] Args;
		public readonly string Code;

		public readonly string[] InternalVariables;

		public BuiltinInfo(string alias, string libraryName, string methodName, VarKind returnType, IEnumerable<BuiltinArg> args, string code, IEnumerable<string> internalVariables)
		{
			Alias = alias;
			LibraryName = libraryName;
			MethodName = methodName;
			ReturnType = returnType;
			Args = args.ToArray();
			Code = code;
			InternalVariables = internalVariables.ToArray();
		}

		public override string ToString()
		{
			return $"{Alias}:{ReturnType} ({LibraryName})";
		}
	}

	public static class Builtins
	{
		private static Dictionary<string, BuiltinInfo> _builtins = null;

		public static void FillLibrary(LibraryDeclaration libDecl)
		{
			if (_builtins == null)
			{
				Initialize();
			}

			foreach (var entry in _builtins.Values)
			{
				if (entry.LibraryName.Equals(libDecl.Name, StringComparison.OrdinalIgnoreCase))
				{
					var args = entry.Args.Select(x => new MethodParameter(x.Name, x.Type)).ToArray();
					libDecl.AddMethod(entry.MethodName, MethodImplementationType.LocalCall, entry.ReturnType, args, null, isBuiltin: true).SetAlias(entry.Alias);
				}
			}
		}

		private static readonly string TAG = "// #BUILTIN";

		private static string DecodeAlias(string line)
		{
			var tmp = line.Split('$');
			line = tmp[1];

			tmp = line.Split("//");
			line = tmp[0];

			return line.Trim();
		}

		private static void Initialize()
		{
			_builtins = new Dictionary<string, BuiltinInfo>();

			var lines = BUILTIN_ASM.Split('\n');

			var builtinAlias = new Dictionary<string, string>();

			int i = 0;
			while (i < lines.Length)
			{
				if (lines[i].StartsWith(TAG))
				{
					i++;
					var libName = lines[i].Substring(11).Trim();
					i++;
					var methodName = lines[i].Substring(10).Trim();
					i++;
					var tmp = lines[i].Substring(10).Trim();
					var returnType = Enum.Parse<VarKind>(tmp, true);
					i++;
					// skip ARGS line
					i++;
					var args = new List<BuiltinArg>();
					do
					{
						if (!lines[i].StartsWith("//"))
						{
							break;
						}
						tmp = lines[i].Substring(3).Trim();

						var tmp2 = tmp.Split(':');

						var argType = Enum.Parse<VarKind>(tmp2[1].Trim(), true);
						var arg = new BuiltinArg(tmp2[0].Trim(), VarType.Find(argType));
						args.Add(arg);
						i++;
					} while (true);

					var code = new StringBuilder();

					var usedAlias = new HashSet<string>();

					var prevIndex = -1;
					do
					{
						if (i >= lines.Length)
						{
							break;
						}

						var curLine = lines[i];

						if (curLine.StartsWith(TAG))
						{
							if (prevIndex >= 0)
							{
								code.Length = prevIndex;
							}

							i--;
							break;
						}
						else
						if (builtinAlias.Count > 0 && curLine.Contains("$"))
						{
							foreach (var aliasKey in builtinAlias.Keys)
							{
								if (usedAlias.Contains(aliasKey))
								{
									continue;
								}

								var varName = "$" + aliasKey;
								if (curLine.Contains(varName))
								{
									var aliasLine = builtinAlias[aliasKey];
									usedAlias.Add(aliasKey);
									break;
								}
							}
						}

						prevIndex = code.Length;
						code.AppendLine(curLine);
						i++;
					} while (true);

					if (Compiler.DebugMode)
					{
						Console.WriteLine($"Detected builtin: {libName}.{methodName}");
					}

					var alias = ("tomb_" + libName + "_" + methodName).ToLowerInvariant();

					var builtinCode = code.ToString();

					var builtin = new BuiltinInfo(alias, libName, methodName, returnType, args, builtinCode, usedAlias);
					_builtins[alias] = builtin;
				}
				else
				if (_builtins.Count == 0 && lines[i].StartsWith("ALIAS "))
				{
					var curLine = lines[i];
					var aliasKey = DecodeAlias(curLine);
					builtinAlias[aliasKey] = curLine;
				}

				i++;
			}
		}

		// NOTE - The ASM here is obtained by compiling builtins.tomb
		// In a future version this can be done at runtime instead of having to hardcode the asm here.
		public static BuiltinInfo GetMethod(string methodName)
		{
			if (_builtins != null)
			{
				if (_builtins.ContainsKey(methodName))
				{
					return _builtins[methodName];
				}
			}

			throw new CompilerException("Unknown builtin method name: " + methodName);
		}

		private static readonly string BUILTIN_ASM = @"
// Line 1:contract builtins {
// Line 2:	import Array;
// Line 3:	import Runtime;
// Line 4:	import Storage;

// ********* tomb_math_sqrt Method ***********
// #BUILTIN
// LIBRARY:math
// METHOD:sqrt
// RETURN:Number
// ARGS:
// n:Number
@entry_tomb_math_sqrt: // 0
// Line 5:	
// Line 6:	private tomb_math_sqrt(n:number) : number {
ALIAS r1 $n // 1
POP $n // 1
CAST $n $n #Number // 3
// Line 7:		local root:number = n / 2;
	ALIAS r2 $root // 7
	COPY $n r3 // 7
	ALIAS r4 $literalexpression91 // 10
	LOAD $literalexpression91 2 // 10
	DIV r3 $literalexpression91 r5 // 15
	COPY r5 $root // 19
	@loop_start_whilestatement95: NOP // 23
// Line 8:		while (n < root * root) {
	COPY $n r3 // 23
	COPY $root r4 // 26
	LT r3 r4 r5 // 29
	COPY $root r3 // 33
	MUL r5 r3 r4 // 36
		JMPNOT r4 @loop_end_whilestatement95 // 40
// Line 9:			root += n / root;
		COPY $root r3 // 44
		COPY $n r5 // 47
		COPY $root r6 // 50
		DIV r5 r6 r7 // 53
		ADD r3 r7 r5 // 57
		COPY r5 $root // 61
// Line 10:			root /= 2;
		COPY $root r3 // 64
		ALIAS r5 $literalexpression109 // 67
		LOAD $literalexpression109 2 // 67
		DIV r3 $literalexpression109 r6 // 72
		COPY r6 $root // 76
		JMP @loop_start_whilestatement95 // 79
		@loop_end_whilestatement95: NOP // 83
// Line 11:		}
// Line 12:		
// Line 13:		return root;
	COPY $root r3 // 83
	PUSH r3 // 86
	JMP @exit_tomb_math_sqrt // 88
@exit_tomb_math_sqrt: // 91
RET // 92
// Line 14:	}

// ********* tomb_string_toUpper Method ***********
// #BUILTIN
// LIBRARY:string
// METHOD:toUpper
// RETURN:String
// ARGS:
// s:String
@entry_tomb_string_toUpper: // 93
// Line 15:	
// Line 16:	private tomb_string_toUpper(s:string):string
ALIAS r1 $s // 94
POP $s // 94
// Line 17:	{        
// Line 18:		local my_array: array<number>;		
	ALIAS r2 $my_array // 96
// Line 19:		
// Line 20:		// extract chars from string into an array
// Line 21:		my_array = s.toArray();	
	COPY $s r3 // 96
	CAST r3 r3 #Struct // 99
	COPY r3 $my_array // 103
// Line 22:		
// Line 23:		local length :number = Array.length(my_array);
	ALIAS r3 $length // 106
	COPY $my_array r4 // 106
	COUNT r4 r4 // 109
	COPY r4 $length // 112
// Line 24:		local idx :number = 0;
	ALIAS r4 $idx // 115
	ALIAS r5 $literalexpression128 // 115
	LOAD $literalexpression128 0 // 115
	COPY $literalexpression128 $idx // 120
	@loop_start_whilestatement131: NOP // 124
// Line 25:		
// Line 26:		while (idx < length) {
	COPY $idx r5 // 124
	COPY $length r6 // 127
	LT r5 r6 r7 // 130
		JMPNOT r7 @loop_end_whilestatement131 // 134
// Line 27:			local ch : number = my_array[idx];
		ALIAS r5 $ch // 138
		COPY $idx r8 // 138
		GET $my_array r6 r8 // 141
		COPY r6 $ch // 145
// Line 28:			
// Line 29:			if (ch >= 97) {
		COPY $ch r6 // 148
		ALIAS r8 $literalexpression142 // 151
		LOAD $literalexpression142 97 // 151
		GTE r6 $literalexpression142 r9 // 156
			JMPNOT r9 @then_ifstatement140 // 160
// Line 30:				if (ch <= 122) {				
			COPY $ch r6 // 164
			ALIAS r8 $literalexpression147 // 167
			LOAD $literalexpression147 122 // 167
			LTE r6 $literalexpression147 r10 // 172
				JMPNOT r10 @then_ifstatement145 // 176
// Line 31:					my_array[idx] = ch - 32; 
				COPY $ch r6 // 180
				ALIAS r8 $literalexpression153 // 183
				LOAD $literalexpression153 32 // 183
				SUB r6 $literalexpression153 r11 // 188
				COPY $idx r6 // 192
				PUT r11 $my_array r6 // 195
				@then_ifstatement145: NOP // 200
			@then_ifstatement140: NOP // 201
// Line 32:				}
// Line 33:			}
// Line 34:						
// Line 35:			idx += 1;
		COPY $idx r6 // 201
		ALIAS r8 $literalexpression156 // 204
		LOAD $literalexpression156 1 // 204
		ADD r6 $literalexpression156 r9 // 209
		COPY r9 $idx // 213
		JMP @loop_start_whilestatement131 // 216
		@loop_end_whilestatement131: NOP // 220
// Line 36:		}
// Line 37:				
// Line 38:		// convert the array back into a unicode string
// Line 39:		local result:string = String.fromArray(my_array); 
	ALIAS r5 $result // 220
	COPY $my_array r6 // 220
	CAST r6 r6 #String // 223
	COPY r6 $result // 227
// Line 40:		return result;
	COPY $result r6 // 230
	PUSH r6 // 233
	JMP @exit_tomb_string_toUpper // 235
@exit_tomb_string_toUpper: // 238
RET // 239
// Line 41:	}		

// ********* tomb_string_toLower Method ***********
// #BUILTIN
// LIBRARY:string
// METHOD:toLower
// RETURN:String
// ARGS:
// s:String
@entry_tomb_string_toLower: // 240
// Line 42:
// Line 43:	private tomb_string_toLower(s:string):string 
ALIAS r1 $s // 241
POP $s // 241
// Line 44:	{        
// Line 45:		local my_array: array<number>;		
	ALIAS r2 $my_array // 243
// Line 46:		
// Line 47:		// extract chars from string into an array
// Line 48:		my_array = s.toArray();	
	COPY $s r3 // 243
	CAST r3 r3 #Struct // 246
	COPY r3 $my_array // 250
// Line 49:		
// Line 50:		local length :number = Array.length(my_array);
	ALIAS r3 $length // 253
	COPY $my_array r4 // 253
	COUNT r4 r4 // 256
	COPY r4 $length // 259
// Line 51:		local idx :number = 0;
	ALIAS r4 $idx // 262
	ALIAS r5 $literalexpression180 // 262
	LOAD $literalexpression180 0 // 262
	COPY $literalexpression180 $idx // 267
	@loop_start_whilestatement183: NOP // 271
// Line 52:		
// Line 53:		while (idx < length) {
	COPY $idx r5 // 271
	COPY $length r6 // 274
	LT r5 r6 r7 // 277
		JMPNOT r7 @loop_end_whilestatement183 // 281
// Line 54:			local ch : number = my_array[idx];
		ALIAS r5 $ch // 285
		COPY $idx r8 // 285
		GET $my_array r6 r8 // 288
		COPY r6 $ch // 292
// Line 55:			
// Line 56:			if (ch >= 65) {
		COPY $ch r6 // 295
		ALIAS r8 $literalexpression194 // 298
		LOAD $literalexpression194 65 // 298
		GTE r6 $literalexpression194 r9 // 303
			JMPNOT r9 @then_ifstatement192 // 307
// Line 57:				if (ch <= 90) {				
			COPY $ch r6 // 311
			ALIAS r8 $literalexpression199 // 314
			LOAD $literalexpression199 90 // 314
			LTE r6 $literalexpression199 r10 // 319
				JMPNOT r10 @then_ifstatement197 // 323
// Line 58:					my_array[idx] = ch + 32; 
				COPY $ch r6 // 327
				ALIAS r8 $literalexpression205 // 330
				LOAD $literalexpression205 32 // 330
				ADD r6 $literalexpression205 r11 // 335
				COPY $idx r6 // 339
				PUT r11 $my_array r6 // 342
				@then_ifstatement197: NOP // 347
			@then_ifstatement192: NOP // 348
// Line 59:				}
// Line 60:			}
// Line 61:						
// Line 62:			idx += 1;
		COPY $idx r6 // 348
		ALIAS r8 $literalexpression208 // 351
		LOAD $literalexpression208 1 // 351
		ADD r6 $literalexpression208 r9 // 356
		COPY r9 $idx // 360
		JMP @loop_start_whilestatement183 // 363
		@loop_end_whilestatement183: NOP // 367
// Line 63:		}
// Line 64:				
// Line 65:		// convert the array back into a unicode string
// Line 66:		local result:string = String.fromArray(my_array); 
	ALIAS r5 $result // 367
	COPY $my_array r6 // 367
	CAST r6 r6 #String // 370
	COPY r6 $result // 374
// Line 67:		return result;
	COPY $result r6 // 377
	PUSH r6 // 380
	JMP @exit_tomb_string_toLower // 382
@exit_tomb_string_toLower: // 385
RET // 386
// Line 68:	}		

// ********* tomb_string_indexOf Method ***********
// #BUILTIN
// LIBRARY:string
// METHOD:indexOf
// RETURN:Number
// ARGS:
// s:String
// x:Number
@entry_tomb_string_indexOf: // 387
// Line 69:	
// Line 70:	
// Line 71:	private tomb_string_indexOf(s:string, x:number):number 
ALIAS r1 $s // 388
POP $s // 388
ALIAS r2 $x // 390
POP $x // 390
CAST $x $x #Number // 392
// Line 72:	{        
// Line 73:		local my_array: array<number>;		
	ALIAS r3 $my_array // 396
// Line 74:		
// Line 75:		// extract chars from string into an array
// Line 76:		my_array = s.toArray();	
	COPY $s r4 // 396
	CAST r4 r4 #Struct // 399
	COPY r4 $my_array // 403
// Line 77:		
// Line 78:		local length :number = Array.length(my_array);
	ALIAS r4 $length // 406
	COPY $my_array r5 // 406
	COUNT r5 r5 // 409
	COPY r5 $length // 412
// Line 79:		local idx :number = 0;
	ALIAS r5 $idx // 415
	ALIAS r6 $literalexpression234 // 415
	LOAD $literalexpression234 0 // 415
	COPY $literalexpression234 $idx // 420
	@loop_start_whilestatement237: NOP // 424
// Line 80:		
// Line 81:		while (idx < length) {
	COPY $idx r6 // 424
	COPY $length r7 // 427
	LT r6 r7 r8 // 430
		JMPNOT r8 @loop_end_whilestatement237 // 434
// Line 82:			local ch : number = my_array[idx];
		ALIAS r6 $ch // 438
		COPY $idx r9 // 438
		GET $my_array r7 r9 // 441
		COPY r7 $ch // 445
// Line 83:			
// Line 84:			if (ch == x) {
		COPY $ch r7 // 448
		COPY $x r9 // 451
		EQUAL r7 r9 r10 // 454
			JMPNOT r10 @then_ifstatement246 // 458
// Line 85:				// found, return index
// Line 86:				return idx;
			COPY $idx r7 // 462
			PUSH r7 // 465
			JMP @exit_tomb_string_indexOf // 467
			@then_ifstatement246: NOP // 471
// Line 87:			}
// Line 88:									
// Line 89:			idx += 1;
		COPY $idx r7 // 471
		ALIAS r9 $literalexpression254 // 474
		LOAD $literalexpression254 1 // 474
		ADD r7 $literalexpression254 r10 // 479
		COPY r10 $idx // 483
		JMP @loop_start_whilestatement237 // 486
		@loop_end_whilestatement237: NOP // 490
// Line 90:		}
// Line 91:		
// Line 92:		return -1;		// not found
	ALIAS r6 $literalexpression257 // 490
	LOAD $literalexpression257 1 // 490
	NEGATE $literalexpression257 $literalexpression257 // 495
	PUSH $literalexpression257 // 498
	JMP @exit_tomb_string_indexOf // 500
@exit_tomb_string_indexOf: // 503
RET // 504
// Line 93:	}		
// Line 94:	
// Line 95:	const RND_A:number = 16807;
// Line 96:	const RND_M:number = 2147483647;	
// Line 97:	const RND_SEED_KEY:string = ""tomb_rnd_seed"";	

// ********* tomb_random_seed Method ***********
// #BUILTIN
// LIBRARY:random
// METHOD:seed
// RETURN:None
// ARGS:
// seed:Number
@entry_tomb_random_seed: // 505
// Line 98:				
// Line 99:	private tomb_random_seed(seed:number) 
ALIAS r1 $seed // 506
POP $seed // 506
CAST $seed $seed #Number // 508
// Line 100:	{
// Line 101:		Storage.write(RND_SEED_KEY, seed);
	ALIAS r2 $methodcallexpression268 // 512
	COPY $seed r3 // 512
	PUSH r3 // 515
	ALIAS r3 $RND_SEED_KEY // 517
	LOAD $RND_SEED_KEY ""tomb_rnd_seed"" // 517
	PUSH $RND_SEED_KEY // 534
	LOAD $methodcallexpression268 ""Data.Set"" // 536
	EXTCALL $methodcallexpression268 // 548
@exit_tomb_random_seed: // 550
RET // 551
// Line 102:	}

// ********* tomb_random_generate Method ***********
// #BUILTIN
// LIBRARY:random
// METHOD:generate
// RETURN:Number
// ARGS:
@entry_tomb_random_generate: // 552
// Line 103:
// Line 104:	private tomb_random_generate(): number
// Line 105:	{
// Line 106:		local seed: number;
// Line 107:		local context:string = Runtime.context();
	ALIAS r1 $context // 553
	ALIAS r2 $methodcallexpression276 // 553
	LOAD $methodcallexpression276 ""Runtime.Context"" // 553
	EXTCALL $methodcallexpression276 // 572
	POP $methodcallexpression276 // 574
	COPY $methodcallexpression276 $context // 576
	ALIAS r2 $seed // 579
// Line 108:		seed = Storage.read<number>(context, RND_SEED_KEY);
	LOAD r3 3 // field type // 579
	PUSH r3 // 584
	ALIAS r4 $RND_SEED_KEY // 586
	LOAD $RND_SEED_KEY ""tomb_rnd_seed"" // 586
	PUSH $RND_SEED_KEY // 603
	COPY $context r4 // 605
	PUSH r4 // 608
	LOAD r3 ""Data.Get"" // 610
	EXTCALL r3 // 622
	POP r3 // 624
	COPY r3 $seed // 626
// Line 109:		seed = (RND_A * seed) % RND_M;
	ALIAS r3 $RND_A // 629
	LOAD $RND_A 16807 // 629
	COPY $seed r4 // 635
	MUL $RND_A r4 r5 // 638
	ALIAS r3 $RND_M // 642
	LOAD $RND_M 2147483647 // 642
	MOD r5 $RND_M r4 // 650
	COPY r4 $seed // 654
// Line 110:		Storage.write(RND_SEED_KEY, seed);
	ALIAS r3 $methodcallexpression292 // 657
	COPY $seed r4 // 657
	PUSH r4 // 660
	ALIAS r4 $RND_SEED_KEY // 662
	LOAD $RND_SEED_KEY ""tomb_rnd_seed"" // 662
	PUSH $RND_SEED_KEY // 679
	LOAD $methodcallexpression292 ""Data.Set"" // 681
	EXTCALL $methodcallexpression292 // 693
// Line 111:		return seed;
	COPY $seed r3 // 695
	PUSH r3 // 698
	JMP @exit_tomb_random_generate // 700
@exit_tomb_random_generate: // 703
RET // 704
";
	}
}
