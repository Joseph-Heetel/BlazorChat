using BlazorChat.Shared;
using System.Security.Cryptography;

namespace BlazorChat.Server.Services
{
    /// <summary>
    /// A service which generates random Ids
    /// </summary>
    public interface IIdGeneratorService
    {
        void Generate(ItemId[] output);
        ItemId Generate();
    }
    /// <remarks>
    /// This service implementation will not guarantee collision free!
    /// The collision chance is an issue for generating close to ~ 4 billion IDs (https://stackoverflow.com/a/22029380)
    /// Using a symmetric encryption algorithm with a 64bit blocksize would be a good alternative (somewhat slower, however no collisions until 2^64 ids generated),
    /// for example DES (Security concerns regarding DES security could be alleviated by using Triple-DES)
    /// </remarks>
    public class RandomIdGeneratorService : IIdGeneratorService
    {
        [ThreadStatic]
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        public void Generate(ItemId[] output)
        {
            byte[] bytes = new byte[ItemId.IDLENGTH];
            for (int i = 0; i < output.Length; i++)
            {
                _rng.GetBytes(bytes);
                output[i] = new ItemId(bytes);
            }
        }

        ItemId IIdGeneratorService.Generate()
        {
            byte[] values = new byte[ItemId.IDLENGTH];
            _rng.GetBytes(values);
            return new ItemId(values);
        }
    }
}
