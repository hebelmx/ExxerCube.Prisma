using System;

namespace ExxerCube.Prisma.Web.UI.Components.Shared.Navigation;

/// <summary>
/// Marks a routed component so that it is excluded from navigation validation
/// and automatic discovery features (e.g., NotFound suggestions).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HideFromNavigationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HideFromNavigationAttribute"/> class.
    /// </summary>
    public HideFromNavigationAttribute()
    {
    }
}
