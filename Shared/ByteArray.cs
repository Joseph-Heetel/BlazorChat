using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorChat.Shared
{
    /// <summary>
    /// Helper struct for dealing with variable size byte arrays. Used to transfer hashed passwords.
    /// </summary>
    [JsonConverter(typeof(ByteArrayConverter))]
    public struct ByteArray
    {
        public byte[]? Data { get; set; } = Array.Empty<byte>();

        public ByteArray(byte[]? data = null)
        {
            Data = data ?? Array.Empty<byte>();
        }

        public ByteArray(int size = 0)
        {
            if (size <= 0)
            {
                Data = Array.Empty<byte>();
            }
            else
            {
                Data = new byte[size];
            }
        }

        public bool Equals(ByteArray other)
        {
            if (Data == null || other.Data == null)
            {
                return false;
            }
            if (Data.Length == 0)
            {
                return false;
            }
            return Data.SequenceEqual(other.Data);
        }

        public override bool Equals(object? obj)
        {
            return obj is ByteArray token && Equals(token);
        }

        public override int GetHashCode()
        {
            return Data?.GetHashCode() ?? 0;
        }

        public static bool operator ==(ByteArray left, ByteArray right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ByteArray left, ByteArray right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            if (Data != null)
            {
                return Convert.ToHexString(Data);
            }
            return "";
        }
    }

    public class ByteArrayConverter : JsonConverter<ByteArray>
    {
        public override ByteArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new ByteArray(Convert.FromHexString(reader.GetString()!));
        }

        public override void Write(Utf8JsonWriter writer, ByteArray value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

}
