using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Unispect.SDK
{
    [Serializable]
    public class TypeDefWrapper
    {
        [JsonIgnore]
        public TypeDefinition InnerDefinition;

        public TypeDefWrapper(TypeDefinition typeDef, bool isExtended = false/*, bool getSubField = true*/)
        {
            try
            {
                InnerDefinition = typeDef;
                FullName = InnerDefinition.GetFullName() ?? "<null FullName>";
                Name = InnerDefinition.Name ?? "<null Name>";
                Namespace = InnerDefinition.Namespace ?? "<null Namespace>";
                ClassType = InnerDefinition.GetClassType() ?? "<null ClassType>";

                var parent = InnerDefinition.GetParent();
                if (parent.HasValue)
                {
                    Parent = new TypeDefWrapper(parent.Value, true);
                    ParentName = Parent?.Name ?? "<null ParentName>";
                }

                if (isExtended)
                    return;

                var fields = InnerDefinition.GetFields();
                if (fields != null)
                {
                    foreach (var field in fields)
                    {
                        try
                        {
                            var name = field.Name ?? "<null Name>";
                            if (name == "<ErrorReadingField>")
                                continue;

                            var fieldType = field.GetFieldTypeString() ?? "<null FieldType>";
                            var offset = field.Offset;
                            var typeCode = field.TypeCode;
                            var isPointer = false;
                            switch (typeCode)
                            {
                                case TypeEnum.Class:
                                case TypeEnum.SzArray:
                                case TypeEnum.GenericInst:
                                    isPointer = true;
                                    break;
                            }
                            var isValueType = typeCode == TypeEnum.ValueType;
                            var hasValue = field.HasValue(out var valType);
                            var constantValueType = hasValue ? $" [{valType}]" : "";

                            var wrapper = new FieldDefWrapper(name, fieldType, offset, isPointer, isValueType, hasValue, constantValueType)
                            {
                                InnerDefinition = field
                            };
                            Fields.Add(wrapper);
                        }
                        catch (Exception ex)
                        {
                            Log.Add($"TypeDefWrapper: Error creating FieldDefWrapper: {ex.Message}");
                        }
                    }
                }

                var interfaces = InnerDefinition.GetInterfaces();
                if (interfaces != null)
                {
                    foreach (var iface in interfaces)
                    {
                        try { Interfaces.Add(new TypeDefWrapper(iface, true)); } catch (Exception ex) { Log.Add($"TypeDefWrapper: Error adding interface: {ex.Message}"); }
                    }
                }
                InterfacesText = Interfaces.Aggregate("", (current, iface) => current + $", {iface.Name}");

                foreach (var f in Fields)
                {
                    if (f == null) continue;
                    f.Parent = this;
                    if (InnerDefinition.IsValueType)
                    {
                        if (!f.HasValue)
                        {
                            f.Offset -= 0x10;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"TypeDefWrapper: Exception in constructor: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public string ClassType { get; }

        public string Namespace { get; }

        public string Name { get; }

        public string FullName { get; }
        public TypeDefWrapper Parent { get; }
        public string ParentName { get; }

        public List<FieldDefWrapper> Fields { get; } = new List<FieldDefWrapper>();
        public List<TypeDefWrapper> Interfaces { get; } = new List<TypeDefWrapper>();

        public string InterfacesText { get; }

        public static implicit operator TypeDefWrapper(TypeDefinition typeDef)
        {
            return new TypeDefWrapper(typeDef);
        }

        #region Formatters
        public string ToTreeString(bool skipValueTypes = true)
        {
            var sb = new StringBuilder();
            sb.Append($"[{ClassType}] ");
            sb.Append(FullName);

            var parent = Parent;
            if (parent != null)
            {
                sb.Append($" : {parent.Name}");
                var interfaceList = Interfaces;
                if (interfaceList.Count > 0)
                {
                    foreach (var iface in interfaceList)
                    {
                        sb.Append($", {iface.Name}");
                    }
                }
            }

            sb.AppendLine();

            foreach (var field in Fields)
            {
                if (skipValueTypes && field.HasValue)
                    continue;

                var fieldName = field.Name;
                var fieldType = field.FieldType;
                sb.AppendLine(field.HasValue
                    ? $"    [{field.Offset:X2}][{field.ConstantValueTypeShort}] {fieldName} : {fieldType}"
                    : $"    [{field.Offset:X2}] {fieldName} : {fieldType}");
            }

            return sb.ToString();
        }

        public string ToCSharpString(string ptrName = "ulong", bool skipValueTypes = true)
        {
            var sb = new StringBuilder();

            sb.Append($"public struct {Name}");

            var parent = Parent;
            if (parent != null)
            {
                sb.Append($" // {FullName} : {parent.Name}");
                var interfaceList = Interfaces;
                if (interfaceList.Count > 0)
                {
                    foreach (var iface in interfaceList)
                    {
                        sb.Append($", {iface.Name}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("{");

            foreach (var field in Fields)
            {
                if (skipValueTypes && field.HasValue)
                    continue;

                var fieldName = field.Name ?? "UnknownField";
                var fieldType = field.FieldType ?? "object";

                var isPointer = field.IsPointer || fieldType == "String";

                sb.AppendLine(isPointer
                    ? $"    [FieldOffset(0x{field.Offset:X2})] public {ptrName} {fieldName}; // {fieldType.GetSimpleTypeKeyword()}"
                    : $"    [FieldOffset(0x{field.Offset:X2})] public {fieldType.GetSimpleTypeKeyword()} {fieldName};");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }
        #endregion
    }
}