using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OC2.EightPlayers
{
    /// <summary>Small Cecil helpers shared by all patch files. Every helper THROWS if its target is not found
    /// or the expected number of replacements is not made, so a game update cannot silently half-patch.</summary>
    public static class Il
    {
        public static TypeDefinition Type(AssemblyDefinition a, string fullOrShortName)
        {
            var t = a.MainModule.GetTypes().FirstOrDefault(x => x.FullName == fullOrShortName)
                 ?? a.MainModule.GetTypes().FirstOrDefault(x => x.Name == fullOrShortName);
            if (t == null) throw new Exception("type not found: " + fullOrShortName);
            return t;
        }

        public static MethodDefinition Method(AssemblyDefinition a, string type, string method, int paramCount = -1)
        {
            var t = Type(a, type);
            var m = t.Methods.FirstOrDefault(x => x.Name == method && (paramCount < 0 || x.Parameters.Count == paramCount));
            if (m == null) throw new Exception("method not found: " + type + "::" + method);
            return m;
        }

        /// <summary>Nested compiler-generated iterator/async state machine MoveNext of a method.</summary>
        public static MethodDefinition MoveNext(AssemblyDefinition a, string type, string iteratorName)
        {
            var t = Type(a, type).NestedTypes.FirstOrDefault(n => n.Name.StartsWith("<" + iteratorName + ">"));
            if (t == null) throw new Exception("iterator not found: " + type + "/" + iteratorName);
            return t.Methods.First(x => x.Name == "MoveNext");
        }

        public static bool IsLdc(Instruction i, int v)
        {
            switch (i.OpCode.Code)
            {
                case Code.Ldc_I4_0: return v == 0; case Code.Ldc_I4_1: return v == 1; case Code.Ldc_I4_2: return v == 2;
                case Code.Ldc_I4_3: return v == 3; case Code.Ldc_I4_4: return v == 4; case Code.Ldc_I4_5: return v == 5;
                case Code.Ldc_I4_6: return v == 6; case Code.Ldc_I4_7: return v == 7; case Code.Ldc_I4_8: return v == 8;
                case Code.Ldc_I4_S: return (sbyte)i.Operand == v;
                case Code.Ldc_I4: return (int)i.Operand == v;
            }
            return false;
        }

        public static void SetLdc(Instruction i, int v)
        {
            if (v >= -128 && v <= 127) { i.OpCode = OpCodes.Ldc_I4_S; i.Operand = (sbyte)v; }
            else { i.OpCode = OpCodes.Ldc_I4; i.Operand = v; }
        }

        /// <summary>Replace the Nth (0-based, in IL order) ldc.i4 <paramref name="from"/> with <paramref name="to"/>.</summary>
        public static void ReplaceNth(MethodDefinition m, int from, int to, int nth)
        {
            int n = 0;
            foreach (var i in m.Body.Instructions)
                if (IsLdc(i, from) && n++ == nth) { SetLdc(i, to); return; }
            throw new Exception($"{m.DeclaringType.Name}::{m.Name}: occurrence #{nth} of ldc {from} not found (have {n})");
        }

        /// <summary>Replace every ldc.i4 <paramref name="from"/> in the method; require exactly <paramref name="expected"/> hits.</summary>
        public static void ReplaceAll(MethodDefinition m, int from, int to, int expected)
        {
            int n = 0;
            foreach (var i in m.Body.Instructions) if (IsLdc(i, from)) { SetLdc(i, to); n++; }
            if (n != expected) throw new Exception($"{m.DeclaringType.Name}::{m.Name}: replaced {n} ldc {from}, expected {expected}");
        }

        /// <summary>Replace ldc.i4 <paramref name="from"/> only where the *next* instruction's opcode text matches (e.g. "newarr", "ceq", "bge").</summary>
        public static int ReplaceBeforeOp(MethodDefinition m, int from, int to, Func<Instruction, bool> nextMatches, int expected)
        {
            var ins = m.Body.Instructions; int n = 0;
            for (int k = 0; k + 1 < ins.Count; k++)
                if (IsLdc(ins[k], from) && nextMatches(ins[k + 1])) { SetLdc(ins[k], to); n++; }
            if (n != expected) throw new Exception($"{m.DeclaringType.Name}::{m.Name}: replaced {n} (ctx) ldc {from}, expected {expected}");
            return n;
        }

        public static Func<Instruction, bool> Op(params Code[] codes) { return i => codes.Contains(i.OpCode.Code); }

        public static string Dump(MethodDefinition m)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var i in m.Body.Instructions) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }
}
