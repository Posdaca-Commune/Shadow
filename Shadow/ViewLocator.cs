using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Shadow.ViewModels;

namespace Shadow;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var viewModelType = param.GetType();
        var name = viewModelType.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = viewModelType.Assembly.GetType(name)
                   ?? Type.GetType(name)
                   ?? AppDomain.CurrentDomain.GetAssemblies()
                       .Select(assembly => assembly.GetType(name))
                       .FirstOrDefault(candidate => candidate is not null);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase
               || data?.GetType().Name.EndsWith("ViewModel", StringComparison.Ordinal) == true;
    }
}
