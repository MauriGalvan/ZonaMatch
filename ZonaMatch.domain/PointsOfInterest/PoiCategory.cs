using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.PointsOfInterest
{
    // taxonomía propia de dos niveles: categoría estándar de las pantallas -> subcategoría
    public class PoiCategory
    {
        public PoiCategory(string code, string name, short? parentId, short sortOrder)
        {
            Code = Guard.NotBlank(code, nameof(code));
            Name = Guard.NotBlank(name, nameof(name));
            ParentId = parentId;
            SortOrder = sortOrder;
        }

        public short Id { get; private set; }
        public string Code { get; private set; }
        public string Name { get; private set; }
        public short? ParentId { get; private set; }
        public short SortOrder { get; private set; }

        public bool IsRoot => ParentId is null;
    }

    // traduce un atributo de una fuente (amenity=school, nivel=Nivel Primario) a una subcategoría
    public class PoiMappingRule
    {
        public PoiMappingRule(short dataSourceId, string attribute, string value, short categoryId, int priority)
        {
            DataSourceId = Guard.Positive(dataSourceId, nameof(dataSourceId));
            Attribute = Guard.NotBlank(attribute, nameof(attribute));
            Value = Guard.NotBlank(value, nameof(value));
            CategoryId = Guard.Positive(categoryId, nameof(categoryId));
            Priority = Guard.NotNegative(priority, nameof(priority));
        }

        public int Id { get; private set; }
        public short DataSourceId { get; private set; }
        public string Attribute { get; private set; }
        public string Value { get; private set; }
        public short CategoryId { get; private set; }
        public int Priority { get; private set; }
    }
}
