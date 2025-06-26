using System;
using System.Linq;
using Newtonsoft.Json;

namespace Unispect.SDK
{
    [Serializable]
    public class FieldDefWrapper
    {
        [JsonIgnore]
        public FieldDefinition InnerDefinition;

        [JsonConstructor]
        public FieldDefWrapper(string name, string fieldType, int offset, bool isPointer, bool isValueType, bool hasValue, string constantValueType)
        {
            Name = name;
            FieldType = fieldType;
            Offset = offset;
            IsPointer = isPointer;
            IsValueType = isValueType;
            HasValue = hasValue;
            ConstantValueType = constantValueType;
        }

        public FieldDefWrapper(FieldDefinition fieldDef/*, bool getFieldTypeDef = true*/)
        {
            try
            {
                InnerDefinition = fieldDef;
                Name = InnerDefinition.Name ?? "<null Name>";
                FieldType = InnerDefinition.GetFieldTypeString() ?? "<null FieldType>";
                Offset = InnerDefinition.Offset;
                var typeCode = InnerDefinition.TypeCode;
                var isPointer = false;
                switch (typeCode)
                {
                    case TypeEnum.Class:
                    case TypeEnum.SzArray:
                    case TypeEnum.GenericInst:
                        isPointer = true;
                        break;
                }
                IsPointer = isPointer;
                IsValueType = typeCode == TypeEnum.ValueType;
                HasValue = InnerDefinition.HasValue(out var valType);
                ConstantValueType = HasValue ? $" [{valType}]": "";
            }
            catch (Exception ex)
            {
                Log.Add($"FieldDefWrapper: Exception in constructor: {ex.Message}\n{ex.StackTrace}");
                throw;
            }

            // Todo: if 'FieldTypeDefinition' gets used elsewhere, consider re-implementing the following:
            //if (getFieldTypeDef)
            //{
            //    var fdType = InnerDefinition.GetFieldType();
            //    if (fdType.HasValue)
            //        FieldTypeDefinition = new TypeDefWrapper(fdType.Value, getSubField: false);
            //}

        }

        public string Name { get; }

        public string FieldType { get; }
        
        public bool HasValue { get; }
        public string ConstantValueType { get; }
        public string ConstantValueTypeShort => HasValue ? $"{ConstantValueType[2]}" : "";

        public bool IsValueType { get; }
        public bool IsPointer { get; }

        public TypeDefWrapper FieldTypeDefinition { get; }

        public TypeDefWrapper Parent { get; set; }

        public int Offset { get; set; }

        public string OffsetHex => $"[{Offset:X2}]";

        public static implicit operator FieldDefWrapper(FieldDefinition fieldDef)
        {
            return new FieldDefWrapper(fieldDef);
        }

        public override string ToString()
        {
            return $"[{Offset:X2}]{ConstantValueTypeShort} {Name} : {FieldType}";
        }
    }
}