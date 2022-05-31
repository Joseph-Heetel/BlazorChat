namespace CustomBlazorApp.Server.Services.DatabaseWrapper
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple =false, Inherited = true)]
    public class PartitionPropertyAttribute : Attribute
    {
        public bool GenerateFromId { get; set; } = false;

        public PartitionPropertyAttribute()
        {
        }
    }
}
