using System.Reflection;
var asm = Assembly.LoadFrom(args[0]);
foreach (var type in asm.GetTypes().Where(t => t.Name.Contains("NavigationView") && !t.Name.Contains("Automation")).OrderBy(t => t.FullName))
{
    var props = type.GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static)
        .Where(p => p.Name.Contains("Setting", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Footer", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Menu", StringComparison.OrdinalIgnoreCase))
        .Select(p => p.Name);
    var methods = type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
        .Where(m => m.Name.Contains("Setting", StringComparison.OrdinalIgnoreCase))
        .Select(m => m.Name);
    if (props.Any() || methods.Any())
    {
        Console.WriteLine(type.FullName);
        foreach (var p in props) Console.WriteLine("  P " + p + " : " + type.GetProperty(p)!.PropertyType.Name);
        foreach (var m in methods.Distinct()) Console.WriteLine("  M " + m);
    }
}
