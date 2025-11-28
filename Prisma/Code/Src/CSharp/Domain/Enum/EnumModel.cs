// <copyright file="EnumModel.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using ExxerCube.Prisma.Domain.Enum.LookUpTable;
using ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Provides a base class for creating strongly-typed enumerations (ModelEnum pattern).
/// Production-tested implementation ported from IndTrace project.
/// Thread-safe with O(1) lookup performance after initial reflection warmup.
/// </summary>
public class EnumModel : IComparable, IEnumModel, ILookupEntity
{
    /// <summary>
    /// Gets the invalid enumeration instance.
    /// </summary>
    public static readonly EnumModel Invalid = new(1, "Invalid Value");

    // Note: The order of static and instance members is intentional for readability and maintainability.
    // Static fields (SA1204: static before instance)
    /// <summary>
    /// Thread-safe singleton lookup cache for enum values.
    /// Key: Type, Value: Dictionary of int->EnumModel for fast O(1) lookup.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Dictionary<int, EnumModel>> LookupCache = new();

    // Instance fields
    private string displayName = string.Empty;

    // Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="EnumModel"/> class.
    /// </summary>
    public EnumModel()
    {
        this.Name = null!;
        this.displayName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnumModel"/> class with specified values.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="name">The name.</param>
    /// <param name="displayName">The display name.</param>
    protected EnumModel(int value, string name, string displayName = "")
    {
        this.Value = value;
        this.Name = name;
        this.displayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : this.Name;
    }

    // Properties (SA1201: before methods)
    /// <inheritdoc/>
    IEnumModel IEnumModel.Invalid => Invalid;

    /// <summary>
    /// Gets the integer value of the enumeration.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Gets or sets the name of the enumeration.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the display name of the enumeration.
    /// </summary>
    public string DisplayName => this.displayName;

    /// <inheritdoc/>
    int IEnumModel.Value { get; set; }

    /// <inheritdoc/>
    string IEnumModel.DisplayName { get => this.displayName; set => this.displayName = value; }

    // Static methods (SA1204: static before instance)
    /// <summary>
    /// Implicitly converts an enumeration to its integer value.
    /// </summary>
    /// <param name="d">The enumeration to convert.</param>
    public static implicit operator int(EnumModel d) => d.Value;

    /// <summary>
    /// Implicitly converts an enumeration to its display name (fallback to name).
    /// Helps in logging/UI bindings without forcing callers to access properties explicitly.
    /// </summary>
    /// <param name="d">The enumeration to convert.</param>
    public static implicit operator string(EnumModel d) => d.DisplayName ?? d.Name ?? string.Empty;

    /// <summary>
    /// Checks if a given integer value corresponds to any of the defined enumerations.
    /// </summary>
    /// <typeparam name="TEnum">The derived EnumModel type.</typeparam>
    /// <param name="value">The integer value to check.</param>
    /// <returns>True if the value corresponds to a defined enumeration, otherwise False.</returns>
    public static bool Exists<TEnum>(int value)
        where TEnum : EnumModel
    {
        // Using reflection to get all static readonly fields of the derived type
        var definedValues = typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .Cast<EnumModel>()
            .Select(e => e.Value);

        // Check if the given value exists in the definedValues
        return definedValues.Contains(value);
    }

    /// <summary>
    /// Retrieves all instances of a particular EnumModel-derived type.
    /// </summary>
    /// <typeparam name="TEnumeration">Type of the EnumModel.</typeparam>
    /// <returns>An IEnumerable containing all instances of the EnumModel-derived type.</returns>
    public static IEnumerable<TEnumeration> GetAll<TEnumeration>()
        where TEnumeration : EnumModel, new()
    {
        var type = typeof(TEnumeration);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        foreach (var fieldInfo in fields)
        {
            if (fieldInfo.GetValue(null) is TEnumeration instance)
            {
                yield return instance;
            }
        }
    }

    /// <summary>
    /// Calculates the absolute difference between two enumeration values.
    /// </summary>
    /// <param name="firstValue">The first enumeration value.</param>
    /// <param name="secondValue">The second enumeration value.</param>
    /// <returns>The absolute difference between the two values.</returns>
    public static int AbsoluteDifference(EnumModel firstValue, EnumModel secondValue)
    {
        var absoluteDifference = Math.Abs(firstValue.Value - secondValue.Value);
        return absoluteDifference;
    }

    /// <summary>
    /// Converts all enum values to a typed lookup table collection.
    /// </summary>
    /// <typeparam name="TLookUpTable">The lookup table type.</typeparam>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <returns>A list of lookup table entries.</returns>
    public static IList<TLookUpTable> ToLookUpTable<TLookUpTable, TEnumeration>()
        where TLookUpTable : EnumLookUpTable, ILookUpTable, new()
        where TEnumeration : EnumModel, new()
    {
        var type = typeof(TEnumeration);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        return [.. from info in fields
                let instance = new TEnumeration()
                select info.GetValue(null)
            into enumeration
                where enumeration != null
                let value = enumeration.GetType().GetProperty("Value")?.GetValue(enumeration, null)
                let name = enumeration.GetType().GetProperty("Name")?.GetValue(enumeration, null)
                let displayName = enumeration.GetType().GetProperty("DisplayName")?.GetValue(enumeration, null)
                where value != null && (int)value >= 0
                select new EnumLookUpTable((int)value, (string)name!, (string)displayName!) into lookUpTable
                select EnumLookUpTable.ToUpperClass<TLookUpTable>(lookUpTable)];
    }

    /// <summary>
    /// Converts all enum values to a base lookup table collection.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <returns>A list of lookup table entries.</returns>
    public static IList<EnumLookUpTable> ToLookUpTable<TEnumeration>()
        where TEnumeration : EnumModel, new()
    {
        var type = typeof(TEnumeration);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        return [.. from info in fields
                let instance = new TEnumeration()
                select info.GetValue(null)
            into enumeration
                where enumeration != null
                let value = enumeration.GetType().GetProperty("Value")?.GetValue(enumeration, null)
                let name = enumeration.GetType().GetProperty("Name")?.GetValue(enumeration, null)
                let displayName = enumeration.GetType().GetProperty("DisplayName")?.GetValue(enumeration, null)
                where value != null && (int)value >= 0
                select new EnumLookUpTable((int)value, (string)name!, (string)displayName!)];
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current enumeration.
    /// </summary>
    /// <param name="obj">The object to compare with the current enumeration.</param>
    /// <returns>True if equal, otherwise false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not EnumModel otherValue)
        {
            return false;
        }

        var typeMatches = this.GetType() == obj.GetType();
        var valueMatches = this.Value.Equals(otherValue.Value);

        return typeMatches && valueMatches;
    }

    /// <summary>
    /// Returns a hash code for the enumeration.
    /// </summary>
    /// <returns>A hash code for the enumeration.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.GetType(), this.Value);
    }

    /// <summary>
    /// Creates an enumeration instance from an integer value using lazy reflection singleton pattern.
    /// First call per type builds cached lookup table, subsequent calls are O(1) dictionary lookups.
    /// Always returns a value (never throws) - Invalid instance returned for unrecognized values.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of enumeration to create.</typeparam>
    /// <param name="value">The integer value.</param>
    /// <returns>Matching enumeration instance or Invalid instance for unrecognized values.</returns>
    public static TEnumeration FromValue<TEnumeration>(int value)
        where TEnumeration : EnumModel, new()
    {
        try
        {
            // Get or build lookup table (happens once per enum type, then cached)
            var lookup = LookupCache.GetOrAdd(typeof(TEnumeration), BuildLookupTable);

            // Fast O(1) dictionary lookup (no reflection after warmup)
            return lookup.TryGetValue(value, out var result) ? (TEnumeration)result : InvalidValue<TEnumeration>();
        }
        catch (Exception)
        {
            // Production-safe: always return a value, never throw
            return InvalidValue<TEnumeration>();
        }
    }

    /// <summary>
    /// Creates an enumeration instance from a display name.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of enumeration to create.</typeparam>
    /// <param name="displayName">The display name.</param>
    /// <returns>A new enumeration instance.</returns>
    public static TEnumeration FromDisplayName<TEnumeration>(string displayName)
        where TEnumeration : EnumModel, new()
    {
        try
        {
            var matchingItem = Parse<TEnumeration, string>(displayName, "display name", item => item.displayName == displayName);
            return matchingItem;
        }
        catch (Exception)
        {
            return InvalidValue<TEnumeration>();
        }
    }

    /// <summary>
    /// Creates an enumeration instance from a nullable integer value.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of enumeration to create.</typeparam>
    /// <param name="value">The nullable integer value.</param>
    /// <returns>A new enumeration instance.</returns>
    public static TEnumeration FromValue<TEnumeration>(int? value)
        where TEnumeration : EnumModel, new()
    {
        try
        {
            var matchingItem = Parse<TEnumeration, int?>(value, "value", item => item.Value == value);
            return matchingItem;
        }
        catch (Exception)
        {
            return InvalidValue<TEnumeration>();
        }
    }

    /// <summary>
    /// Creates an enumeration instance from a name.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of enumeration to create.</typeparam>
    /// <param name="name">The name.</param>
    /// <returns>A new enumeration instance.</returns>
    public static TEnumeration FromName<TEnumeration>(string name)
        where TEnumeration : EnumModel, new()
    {
        try
        {
            var matchingItem = Parse<TEnumeration, string>(name, "name", item => item.Name == name);
            return matchingItem;
        }
        catch (Exception)
        {
            return InvalidValue<TEnumeration>();
        }
    }

    /// <summary>
    /// Creates an invalid enumeration instance.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of enumeration to create.</typeparam>
    /// <returns>An invalid enumeration instance.</returns>
    public static TEnumeration InvalidValue<TEnumeration>()
        where TEnumeration : EnumModel, new()
    {
        // Create a new instance of the derived class
        var newEnumeration = new TEnumeration();

        // Return the static Invalid instance from the derived class if it exists.
        var invalidField = typeof(TEnumeration).GetField("Invalid", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (invalidField != null && invalidField.GetValue(null) is TEnumeration invalidInstance)
        {
            return invalidInstance;
        }

        // If the derived class does not have an Invalid field, create a default instance.
        return new TEnumeration { Name = "Invalid Value" };
    }

    /// <summary>
    /// Compares the current enumeration with another object.
    /// </summary>
    /// <param name="other">The object to compare with.</param>
    /// <returns>A value indicating the relative order of the objects.</returns>
    public int CompareTo(object? other)
    {
        return other is EnumModel enumeration ? this.Value.CompareTo(enumeration.Value) : default;
    }

    // Instance methods

    /// <summary>
    /// Returns a string representation of the enumeration.
    /// </summary>
    /// <returns>The display name or name of the enumeration.</returns>
    public override string ToString()
    {
        return this.displayName ?? this.Name;
    }

    /// <summary>
    /// Deconstructs the enumeration into its components.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="name">The name.</param>
    /// <param name="displayName">The display name.</param>
    public void Deconstruct(out int value, out string name, out string displayName)
    {
        value = this.Value;
        name = this.Name;
        displayName = this.displayName ?? this.Name;
    }

    /// <summary>
    /// Builds lookup table for an enum type using reflection (called once per type, then cached).
    /// Discovers all public static readonly fields of the enum type and indexes them by Value.
    /// </summary>
    /// <param name="enumType">The enum type to build lookup table for.</param>
    /// <returns>Dictionary mapping int values to EnumModel instances.</returns>
    private static Dictionary<int, EnumModel> BuildLookupTable(Type enumType)
    {
        var lookup = new Dictionary<int, EnumModel>();

        try
        {
            // Get all public static fields declared on this specific enum type
            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var field in fields)
            {
                // Only process fields that are instances of EnumModel
                if (field.GetValue(null) is EnumModel enumValue)
                {
                    // Index by Value for O(1) lookup (handles duplicate values - last one wins)
                    lookup[enumValue.Value] = enumValue;
                }
            }
        }
        catch (Exception)
        {
            // Production-safe: always return a value, never throw
            return new Dictionary<int, EnumModel>();
        }

        // Reflection failure: return empty lookup (will fall back to InvalidValue)
        // Production-safe: never throw, always return usable lookup table
        return lookup;
    }

    private static TEnumeration Parse<TEnumeration, TU>(TU value, string description, Func<TEnumeration, bool> predicate)
        where TEnumeration : EnumModel, new()
    {
        try
        {
            var matchingItem = GetAll<TEnumeration>().FirstOrDefault(predicate);

            if (matchingItem != null)
            {
                return matchingItem;
            }
            else
            {
                // return Invalid TEnumeration();
                return InvalidValue<TEnumeration>();
            }
        }
        catch (Exception)
        {
            return InvalidValue<TEnumeration>();
        }
    }
}
