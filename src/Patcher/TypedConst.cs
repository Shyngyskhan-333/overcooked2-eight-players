using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OC2.EightPlayers
{
    /// <summary>Finds integer constants that are *typed* as a given enum (e.g. EngagementSlot.Count inlined as ldc.i4.4)
    /// by tracking what consumes them, and rewrites only those. Avoids touching unrelated 4s (MachineID.Count etc.).</summary>
    public class TypedConst
    {
        readonly string typeName; readonly int from, to;
        public readonly List<string> Rewritten = new List<string>();
        public readonly List<string> Unresolved = new List<string>();

        public TypedConst(string enumTypeName, int from, int to) { typeName = enumTypeName; this.from = from; this.to = to; }

        bool Is(TypeReference t)
        {
            if (t == null) return false;
            if (t is ByReferenceType br) t = br.ElementType;
            return t.Name == typeName;
        }

        public void Run(ModuleDefinition mod)
        {
            foreach (var t in mod.GetTypes())
                foreach (var m in t.Methods)
                {
                    foreach (var p in m.Parameters)
                        if (Is(p.ParameterType) && p.HasConstant && p.Constant is int c && c == from)
                        { p.Constant = to; Rewritten.Add(t.FullName + "::" + m.Name + " default " + p.Name); }
                    if (m.HasBody) Scan(m);
                }
        }

        void Scan(MethodDefinition m)
        {
            var ins = m.Body.Instructions;
            for (int k = 0; k < ins.Count; k++)
            {
                if (!Il.IsLdc(ins[k], from) || k + 1 >= ins.Count) continue;
                var consumer = ins[k + 1];
                string how = null;
                switch (consumer.OpCode.Code)
                {
                    case Code.Stfld: case Code.Stsfld:
                        if (Is(((FieldReference)consumer.Operand).FieldType)) how = "store field"; break;
                    case Code.Stloc: case Code.Stloc_S: case Code.Stloc_0: case Code.Stloc_1: case Code.Stloc_2: case Code.Stloc_3:
                        if (Is(LocalOf(m, consumer)?.VariableType)) how = "store local"; break;
                    case Code.Starg: case Code.Starg_S:
                        if (Is(((ParameterDefinition)consumer.Operand).ParameterType)) how = "store arg"; break;
                    case Code.Ret:
                        if (Is(m.ReturnType)) how = "return"; break;
                    case Code.Ceq: case Code.Beq: case Code.Beq_S: case Code.Bne_Un: case Code.Bne_Un_S:
                    case Code.Blt: case Code.Blt_S: case Code.Bge: case Code.Bge_S: case Code.Ble: case Code.Ble_S:
                    case Code.Bgt: case Code.Bgt_S: case Code.Clt: case Code.Cgt: case Code.Clt_Un: case Code.Cgt_Un:
                        if (k > 0 && Is(ProducedType(m, ins[k - 1]))) how = "compare"; break;
                    case Code.Call: case Code.Callvirt: case Code.Newobj:
                    {
                        var cm = (MethodReference)consumer.Operand;
                        if (cm.Parameters.Count > 0 && Is(cm.Parameters[cm.Parameters.Count - 1].ParameterType)) how = "last call arg";
                        break;
                    }
                }
                if (how == null)
                {
                    var pt = ParamTypeOfArg(m, k);
                    if (pt != null && Is(pt)) how = "call arg";
                }
                if (how != null)
                {
                    Il.SetLdc(ins[k], to);
                    Rewritten.Add(m.DeclaringType.FullName + "::" + m.Name + " IL_" + ins[k].Offset.ToString("X4") + " (" + how + ")");
                }
                else if (MentionsType(m))
                    Unresolved.Add(m.DeclaringType.FullName + "::" + m.Name + " IL_" + ins[k].Offset.ToString("X4") + " next=" + consumer);
            }
        }

        bool MentionsType(MethodDefinition m)
        {
            return m.Body.Instructions.Any(i =>
                (i.Operand is FieldReference f && (f.FieldType.Name == typeName || f.DeclaringType.Name == typeName)) ||
                (i.Operand is MethodReference mm && (mm.ReturnType.Name == typeName || mm.Parameters.Any(p => p.ParameterType.Name == typeName))));
        }

        static VariableDefinition LocalOf(MethodDefinition m, Instruction i)
        {
            switch (i.OpCode.Code)
            {
                case Code.Stloc_0: case Code.Ldloc_0: return m.Body.Variables[0];
                case Code.Stloc_1: case Code.Ldloc_1: return m.Body.Variables[1];
                case Code.Stloc_2: case Code.Ldloc_2: return m.Body.Variables[2];
                case Code.Stloc_3: case Code.Ldloc_3: return m.Body.Variables[3];
                default: return i.Operand as VariableDefinition;
            }
        }

        static TypeReference ArgType(MethodDefinition m, int index)
        {
            if (m.HasThis) { if (index == 0) return m.DeclaringType; index--; }
            return index >= 0 && index < m.Parameters.Count ? m.Parameters[index].ParameterType : null;
        }

        TypeReference ProducedType(MethodDefinition m, Instruction i)
        {
            switch (i.OpCode.Code)
            {
                case Code.Ldfld: case Code.Ldsfld: case Code.Ldflda: return ((FieldReference)i.Operand).FieldType;
                case Code.Ldloc: case Code.Ldloc_S: case Code.Ldloc_0: case Code.Ldloc_1: case Code.Ldloc_2: case Code.Ldloc_3:
                    return LocalOf(m, i)?.VariableType;
                case Code.Ldarg: case Code.Ldarg_S: return (i.Operand as ParameterDefinition)?.ParameterType;
                case Code.Ldarg_0: return ArgType(m, 0);
                case Code.Ldarg_1: return ArgType(m, 1);
                case Code.Ldarg_2: return ArgType(m, 2);
                case Code.Ldarg_3: return ArgType(m, 3);
                case Code.Call: case Code.Callvirt: return ((MethodReference)i.Operand).ReturnType;
                default: return null;
            }
        }

        static int Pops(Instruction i)
        {
            var op = i.OpCode;
            if (op.Code == Code.Call || op.Code == Code.Callvirt || op.Code == Code.Newobj)
            {
                var mr = (MethodReference)i.Operand; int n = mr.Parameters.Count;
                if (mr.HasThis && op.Code != Code.Newobj) n++;
                return n;
            }
            switch (op.StackBehaviourPop)
            {
                case StackBehaviour.Pop0: return 0;
                case StackBehaviour.Pop1: case StackBehaviour.Popi: case StackBehaviour.Popref: return 1;
                case StackBehaviour.Pop1_pop1: case StackBehaviour.Popi_pop1: case StackBehaviour.Popi_popi: case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4: case StackBehaviour.Popi_popr8: case StackBehaviour.Popref_pop1: case StackBehaviour.Popref_popi: return 2;
                case StackBehaviour.Popi_popi_popi: case StackBehaviour.Popref_popi_popi: case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4: case StackBehaviour.Popref_popi_popr8: case StackBehaviour.Popref_popi_popref: return 3;
                default: return 0;
            }
        }

        static int Pushes(Instruction i)
        {
            var op = i.OpCode;
            if (op.Code == Code.Call || op.Code == Code.Callvirt) return ((MethodReference)i.Operand).ReturnType.FullName == "System.Void" ? 0 : 1;
            if (op.Code == Code.Newobj) return 1;
            switch (op.StackBehaviourPush)
            {
                case StackBehaviour.Push0: return 0;
                case StackBehaviour.Push1_push1: return 2;
                default: return 1;
            }
        }

        /// <summary>If ins[k] is an argument of a later call in straight-line code, return that parameter's type (else null).</summary>
        TypeReference ParamTypeOfArg(MethodDefinition m, int k)
        {
            var ins = m.Body.Instructions;
            int depth = 1; // our constant is on the stack
            for (int q = k + 1; q < ins.Count && q < k + 60; q++)
            {
                var i = ins[q];
                var fc = i.OpCode.FlowControl;
                if (fc == FlowControl.Branch || fc == FlowControl.Cond_Branch || fc == FlowControl.Return || fc == FlowControl.Throw) return null;
                if (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt || i.OpCode.Code == Code.Newobj)
                {
                    var mr = (MethodReference)i.Operand;
                    int total = mr.Parameters.Count + (mr.HasThis && i.OpCode.Code != Code.Newobj ? 1 : 0);
                    int fromTop = depth - 1; // 0 = the last argument
                    if (fromTop >= total) return null; // constant is not consumed by this call (belongs to a nested one)
                    if (depth <= total)
                    {
                        int idx = mr.Parameters.Count - 1 - fromTop;
                        return idx >= 0 ? mr.Parameters[idx].ParameterType : null;
                    }
                    return null;
                }
                depth -= Pops(i); if (depth < 1) return null;
                depth += Pushes(i);
            }
            return null;
        }
    }
}
