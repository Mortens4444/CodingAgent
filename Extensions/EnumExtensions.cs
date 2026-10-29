using System.ComponentModel;
using System.Reflection;

namespace CodingAgent.Extensions;

internal static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        Type enumType = value.GetType();
        string? name = Enum.GetName(enumType, value);

        if (name != null)
        {
            FieldInfo? fieldInfo = enumType.GetField(name);
            DescriptionAttribute? descriptionAttribute = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();
            return descriptionAttribute?.Description ?? name;
        }

        return String.Empty;
    }
}