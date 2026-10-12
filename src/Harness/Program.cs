using System; using Mono.Cecil;
class H { static void Main(string[] a){
  var res = new DefaultAssemblyResolver();
  res.AddSearchDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(a[0])));
  var asm = AssemblyDefinition.ReadAssembly(a[0], new ReaderParameters{ AssemblyResolver = res });
  OC2.EightPlayers.Patcher.Patch(asm);
  asm.Write(a[1]);
  // sanity: reload the written file
  var chk = AssemblyDefinition.ReadAssembly(a[1], new ReaderParameters{ AssemblyResolver = res });
  int bodies=0; foreach (var t in chk.MainModule.GetTypes()) foreach (var m in t.Methods) if (m.HasBody) { var _ = m.Body.Instructions.Count; bodies++; }
  foreach (var f in OC2.EightPlayers.Patcher.Failures) Console.WriteLine("PATCH FAILURE: " + f);
  Console.WriteLine("written + reloaded OK, method bodies=" + bodies);
}}
