using System;
using FluentAvalonia.UI.Controls;

namespace Shadow.ViewModels;

internal static class IconKeyResolver
{
    public static FASymbol Resolve(string iconKey, FASymbol fallback)
    {
        return Enum.TryParse<FASymbol>(iconKey, ignoreCase: true, out var symbol)
            ? symbol
            : fallback;
    }
}
