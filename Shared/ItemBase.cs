using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

namespace CustomBlazorApp.Shared
{
    /// <summary>
    /// Represents a unique id/handle which is used across the entire project for identification and referencing of unique objects.
    /// </summary>
    [JsonConverter(typeof(ItemIdConverter))]
    public struct ItemId : IComparable<ItemId>, IEquatable<ItemId>
    {
        /// <summary>
        /// Size in bytes
        /// </summary>
        public const int IDLENGTH = 8;
        public byte[]? Value { get; private set; } = new byte[IDLENGTH];
        public ItemId() { }
        public ItemId(byte[] value)
        {
            Trace.Assert(value.Length == IDLENGTH);
            Array.Copy(value, Value, IDLENGTH);
        }

        public static readonly ItemId SystemId = new ItemId(Convert.FromHexString(new string('f', IDLENGTH * 2)));

        /// <summary>
        /// Lexigraphically compares ItemIds
        /// </summary>
        public int CompareTo(ItemId other)
        {
            if (Value == null || other.Value == null)
            {
                int result = 0;
                result += (Value == null ? 1 : 0);
                result += (other.Value == null ? -1 : 0);
                return result;
            }
            if (object.ReferenceEquals(Value, other.Value))
            {
                return 0;
            }
            for (int i = 0; i < IDLENGTH; i++)
            {
                int comparison = Value[i].CompareTo(other.Value[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }
            return 0;
        }
        public bool Equals(ItemId other)
        {
            if (Value == null || other.Value == null)
            {
                return Value == other.Value;
            }
            if (object.ReferenceEquals(Value, other.Value))
            {
                return true;
            }
            return Value.SequenceEqual(other.Value);
        }
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is ItemId id)
            {
                return Equals(id);
            }
            if (obj is byte[] value)
            {
                return value.Length == IDLENGTH && Equals(new ItemId(value));
            }
            return false;
        }
        /// <summary>
        /// Calculates a basic hash code by splitting the 8 byte value into two 4 byte sequences intepreted as integer and XORing both.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            if (Value == null)
            {
                return 0;
            }
            int hash = 0;
            hash ^= BitConverter.ToInt32(Value, 00);
            hash ^= BitConverter.ToInt32(Value, 04);
            return hash;
        }

        public override string ToString()
        {
            return Value == null ? new string('0', IDLENGTH * 2) : Convert.ToHexString(Value).ToLowerInvariant();
        }

        /// <summary>
        /// Parses a string to an ItemId. Will throw exception on malformed input.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FormatException"></exception>
        public static ItemId Parse(string? value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            byte[] bytes = Convert.FromHexString(value);
            if (bytes.Length != IDLENGTH)
            {
                throw new FormatException($"The hex string input must be exactly ${IDLENGTH * 2} characters ({IDLENGTH} bytes) long!");
            }
            return new ItemId(bytes);
        }

        /// <summary>
        /// Attempts to parse ItemID
        /// </summary>
        /// <param name="value"></param>
        /// <param name="id"></param>
        /// <returns>True if parse succeeds. False otherwise</returns>
        public static bool TryParse(string? value, out ItemId id)
        {
            if (value != null)
            {
                try
                {
                    byte[] bytes = Convert.FromHexString(value);
                    if (bytes.Length == IDLENGTH)
                    {
                        id = new ItemId(bytes);
                        return true;
                    }
                }
                catch (FormatException)
                {
                }
            }
            id = default;
            return false;
        }

        /// <summary>
        /// Parses a string to ItemId. If parsing fails, returns zeroed ItemId
        /// </summary>
        public static ItemId ParseOrDefault(string? value)
        {
            if (value != null)
            {
                try
                {
                    byte[] bytes = Convert.FromHexString(value);
                    if (bytes.Length == IDLENGTH)
                    {
                        return new ItemId(bytes);
                    }
                }
                catch (FormatException)
                {
                }
            }
            return default;
        }

        public static bool operator ==(ItemId left, ItemId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemId left, ItemId right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(ItemId left, ItemId right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(ItemId left, ItemId right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(ItemId left, ItemId right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(ItemId left, ItemId right)
        {
            return left.CompareTo(right) >= 0;
        }

        /// <summary>
        /// Returns true, if Value represents a zero value (null or array of zeroes)
        /// </summary>
        [JsonIgnore]
        public bool IsZero
        {
            get
            {
                if (Value == null)
                {
                    return true;
                }
                foreach (var section in Value)
                {
                    if (section != default)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Base class for all unique items. Allows comparison and JSON en/decoding
    /// </summary>
    public class ItemBase : IComparable<ItemBase>, IEquatable<ItemBase>
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId Id { get; set; }
        public ItemBase() { Id = default; }
        public ItemBase(ulong id) { this.Id = new ItemId(BitConverter.GetBytes(id)); }
        public ItemBase(ItemId id) { this.Id = id; }

        public int CompareTo(ItemBase? other)
        {
            if (other == null)
            {
                return int.MaxValue;
            }
            return Id.CompareTo(other.Id);
        }

        public bool Equals(ItemBase? other)
        {
            if (other == null)
            {
                return false;
            }
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ItemBase);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return Id.ToString();
        }

        //public static bool TryParse(string? v, out ulong id)
        //{
        //    return ulong.TryParse(v, System.Globalization.NumberStyles.HexNumber, null, out id);
        //}
    }

    public class ItemIdConverter : JsonConverter<ItemId>
    {
        public override ItemId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string raw = reader.GetString()!;
            return ItemId.Parse(raw);
        }
        public override ItemId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string raw = reader.GetString()!;
            return ItemId.Parse(raw);
        }

        public override void Write(Utf8JsonWriter writer, ItemId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, ItemId value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(value.ToString());
        }
    }
}
