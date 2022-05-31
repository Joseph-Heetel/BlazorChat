using System.Text;
using System.Security.Cryptography;
using BlazorChat.Shared;

namespace BlazorChat.Server.Services
{
    /// <summary>
    /// A service for generating stable hashes (c# object.GetHashCode() is not guaranteed stable!)
    /// </summary>
    /// <remarks>
    /// Use for perfomance optimisation only. These hashes are insecure!
    /// </remarks>
    public interface IHashService
    {
        uint GetHashShort(byte[] value);
        uint GetHashShort(string value)
        {
            return GetHashShort(Encoding.Unicode.GetBytes(value));
        }

        byte[] GetHashLong(byte[] value);
        byte[] GetHashLong(string value)
        {
            return GetHashLong(Encoding.Unicode.GetBytes(value));
        }
    }
    public class SHAHashService : IHashService
    {
        private readonly HashAlgorithm _hash;
        private readonly SemaphoreSlim _semaphore;

        public SHAHashService()
        {
            _hash = HashAlgorithm.Create(HashAlgorithmName.SHA1.ToString())!;
            _semaphore = new SemaphoreSlim(1);
        }

        public byte[] GetHashLong(byte[] value)
        {
            _semaphore.Wait();
            byte[] bytes = _hash.ComputeHash(value);
            _semaphore.Release();
            return bytes;
        }

        public uint GetHashShort(byte[] value)
        {
            _semaphore.Wait();
            byte[] hash = _hash.ComputeHash(value);
            _semaphore.Release();
            uint result = 0;
            for (int i = 0; ((i * 4) + 3) < hash.Length; i++)
            {
                result ^= BitConverter.ToUInt32(hash, i * 4);
            }
            return result;
        }
    }
}
