namespace BlazorChat.Server.Services.DatabaseWrapper
{
    /// <summary>
    /// Marks a model property that it contains the partition key
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple =false, Inherited = true)]
    public class PartitionPropertyAttribute : Attribute
    {
        /// <summary>
        /// If set to true, the partition key property may be set by hashing the id of the item
        /// </summary>
        public bool GenerateFromId { get; set; } = false;

        public PartitionPropertyAttribute()
        {
        }
    }
}
