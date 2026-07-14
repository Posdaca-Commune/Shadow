using System.Reflection;
using System.Runtime.Loader;
var hostDir = @"C:\Users\ns992\Projects\Shadow\Shadow\bin\Debug\net10.0";
var host = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hostDir, "Shadow.dll"));
var plugin = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hostDir, @"Plugins\Hoi4Launcher\Shadow.Hoi4Launcher.dll"));
Console.WriteLine("host resources:");
foreach (var n in host.GetManifestResourceNames().OrderBy(x => x)) Console.WriteLine("  " + n);
Console.WriteLine("plugin resources:");
foreach (var n in plugin.GetManifestResourceNames().OrderBy(x => x)) Console.WriteLine("  " + n);
