using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomBlazorApp.Shared
{
    /// <summary>
    /// Helper struct for dealing with variable size byte arrays
    /// </summary>
    public struct ByteArray
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();

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
            return Data.GetHashCode();
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
            return Convert.ToHexString(Data);
        }
    }
    // https://stackoverflow.com/a/30353296
    public class ByteArrayComparer : EqualityComparer<ByteArray>
    {
        public override bool Equals(ByteArray first, ByteArray second)
        {
            if (first.Data == null || second.Data == null)
            {
                // null == null returns true.
                // non-null == null returns false.
                return first.Data == second.Data;
            }
            if (ReferenceEquals(first.Data, second.Data))
            {
                return true;
            }
            if (first.Data.Length != second.Data.Length)
            {
                return false;
            }
            // Linq extension method is based on IEnumerable, must evaluate every item.
            return first.Data.SequenceEqual(second.Data);
        }
        public override int GetHashCode(ByteArray obj)
        {
            if (obj.Data == null || obj.Data.Length < 4)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            return BitConverter.ToInt32(obj.Data, 0);
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
            writer.WriteStringValue(Convert.ToHexString(value.Data));
        }
    }

}
