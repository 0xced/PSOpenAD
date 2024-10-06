using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography;
using PSOpenAD;
using PSOpenAD.Module;
using PSOpenAD.Module.Commands;

if (args.Length < 1)
{
    return Usage();
}

new OnModuleImportAndRemove().OnImport();

IDebugCommand command = args[0] switch
{
    nameof(GetOpenADUser) => new DebugGetOpenADUser(),
    nameof(GetOpenADGroup) => new DebugGetOpenADGroup(),
    nameof(GetOpenADRootDSE) => new DebugGetOpenADRootDSE(),
    nameof(GetOpenADGroupMember) => new DebugGetOpenADGroupMember(),
    _ => throw new NotSupportedException($"Command {args[0]} is not supported."),
};

var properties = new Dictionary<PropertyInfo, object>();

foreach (var arg in args.Skip(1))
{
    var separatorIndex = arg.IndexOf('=');
    if (separatorIndex == -1)
    {
        return Usage();
    }

    var propertyName = arg[..separatorIndex];
    var propertyValue = arg[(separatorIndex + 1)..];
    var property = command.GetType().GetProperties().SingleOrDefault(p => p.Name == propertyName);
    if (property == null)
    {
        throw new InvalidOperationException($"The {command.GetType().BaseType?.Name} command does not have a '" + propertyName + "' property.");
    }

    if (property.PropertyType.IsArray)
    {
        if (properties.TryGetValue(property, out var value) && value is List<string> list)
        {
            list.Add(propertyValue);
        }
        else
        {
            properties[property] = new List<string> { propertyValue };
        }
    }
    else
    {
        properties[property] = propertyValue;
    }
}

foreach (var (property, value) in properties)
{
    if (value is List<string> list)
    {
        var converter = TypeDescriptor.GetConverter(property.PropertyType.GetElementType()!);
        property.SetValue(command, list.Select(e => converter.ConvertFromString(e)).Cast<string>().ToArray());
    }
    else if (value is string text)
    {
        object? converted;
        if (property.PropertyType.IsAssignableTo(typeof(ADPrincipalIdentity)))
        {
            converted = new ADPrincipalIdentity(text);
        }
        else
        {
            var converter = TypeDescriptor.GetConverter(property.PropertyType);
            converted = converter.ConvertFromString(text);
        }
        property.SetValue(command, converted);
    }
}

foreach (var result in command.Run())
{
    foreach (var (attributeName, isSingleValue) in result.AttributeDescriptors)
    {
        if (isSingleValue)
        {
            Console.WriteLine($"{attributeName}: {ToString(result.GetAttribute<object>(attributeName))}");
        }
        else
        {
            Console.WriteLine($"{attributeName}");
            foreach (var attributeValue in result.GetAttributes<object?>(attributeName))
            {
                Console.WriteLine($"  {ToString(attributeValue)}");
            }
        }
    }
}

return 0;

string? ToString(object? value)
{
    if (value is Oid oid)
        return string.IsNullOrEmpty(oid.FriendlyName) ? oid.Value : $"{oid.Value} ({oid.FriendlyName})";

    return value?.ToString();
}

int Usage()
{
    Console.WriteLine($"Usage: {Path.GetFileName(Environment.GetCommandLineArgs()[0])} <command> [arg1=value1] [arg2=value2] ...");
    return 1;
}
