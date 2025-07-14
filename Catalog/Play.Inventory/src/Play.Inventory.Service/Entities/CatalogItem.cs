using MongoDB.Bson.Serialization.Attributes;
using Play.Common;

namespace Play.Inventory.Service.Entities
{
    public class CatalogItem : IEntity
    {
        [BsonId]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

    }
}