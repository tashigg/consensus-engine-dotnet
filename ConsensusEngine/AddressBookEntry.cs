using System;
using System.Net;
using Newtonsoft.Json;

namespace Tashi.ConsensusEngine
{
    static class GuidExtensions
    {
        public static byte[] ToBigEndianBytes(this Guid guid)
        {
            var bytes = guid.ToByteArray();

            if (!BitConverter.IsLittleEndian)
            {
                return bytes;
            }

            // Microsoft store the first 3 sections as multi-byte integers, but
            // the remaining part as a byte array.
            Array.Reverse(bytes, 0, 4);
            Array.Reverse(bytes, 4, 2);
            Array.Reverse(bytes, 6, 2);

            return bytes;
        }

        public static Guid FromBigEndianBytes(Span<byte> bytes)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return new Guid(bytes);
            }

            // Microsoft store the first 3 sections as multi-byte integers, but
            // the remaining part as a byte array.
            bytes.Slice(0, 4).Reverse();
            bytes.Slice(4, 2).Reverse();
            bytes.Slice(6, 2).Reverse();

            return new Guid(bytes);
        }
    }

    class IPAddressConverter : JsonConverter<IPAddress>
    {
        public override void WriteJson(JsonWriter writer, IPAddress? value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.ToString());
        }

        public override IPAddress? ReadJson(JsonReader reader, Type objectType, IPAddress? existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value is string s)
            {
                if (IPAddress.TryParse(s, out var address))
                {
                    return address;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// An entry in to the address book.
    /// </summary>
    public abstract class AddressBookEntry : IEquatable<AddressBookEntry>
    {
        /// <summary>
        /// The public key for the creator associated with this address book entry.
        /// </summary>
        public PublicKey PublicKey;

        /// <summary>
        /// If this entry is for a relay node.
        /// </summary>
        public bool IsRelay;

        private static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All,
            Converters = new JsonConverter[] { new IPAddressConverter() }
        };

        /// <summary>
        /// Create a new address book entry.
        /// </summary>
        /// <param name="publicKey"></param>
        /// <param name="isRelay"></param>
        protected AddressBookEntry(PublicKey publicKey, bool isRelay)
        {
            PublicKey = publicKey;
            IsRelay = isRelay;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, _jsonSerializerSettings);
        }

        public static AddressBookEntry? Deserialize(string? data)
        {
            return data is null ? null : JsonConvert.DeserializeObject<AddressBookEntry>(data, _jsonSerializerSettings);
        }

        /// <summary>
        /// Check if this is equal to other.
        /// </summary>
        /// <param name="other">The other address book entry</param>
        /// <returns>whether or not this is equal to other.</returns>
        public abstract bool Equals(AddressBookEntry other);
    }

    /// <summary>
    /// 
    /// </summary>
    public class DirectAddressBookEntry : AddressBookEntry
    {
        // `EndPoint` is already exposed, so we don't need to expose these too.

        [JsonProperty] private readonly IPAddress Address;
        [JsonProperty] private readonly int Port;

        /// <summary>
        /// The IP:port endpoint for this address book entry.
        /// </summary>
        [JsonIgnore] public IPEndPoint EndPoint => new IPEndPoint(Address, Port);


        [JsonConstructor]
        public DirectAddressBookEntry(IPAddress address, int port, PublicKey publicKey, bool isRelay = false) : base(
            publicKey, isRelay)
        {
            Address = address;
            Port = port;
        }


        public DirectAddressBookEntry(IPEndPoint endPoint, PublicKey publicKey, bool isRelay = false) : this(
            endPoint.Address, endPoint.Port, publicKey, isRelay)
        {
        }

        public new static DirectAddressBookEntry? Deserialize(string? data)
        {
            var entry = AddressBookEntry.Deserialize(data);
            if (entry is DirectAddressBookEntry direct)
            {
                return direct;
            }

            return null;
        }

        public override bool Equals(AddressBookEntry other)
        {
            if (other is DirectAddressBookEntry direct)
            {
                return Address.Equals(direct.Address) && PublicKey.Equals(direct.PublicKey);
            }

            return false;
        }
    }

    class GuidConverter : JsonConverter<Guid>
    {
        public override void WriteJson(JsonWriter writer, Guid value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override Guid ReadJson(JsonReader reader, Type objectType, Guid existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value is string s)
            {
                if (Guid.TryParse(s, out var guid))
                {
                    return guid;
                }
            }

            throw new Exception($"Failed to parse guid");
        }
    }

    public readonly struct RelayAllocation : IEquatable<RelayAllocation>
    {
        public DirectAddressBookEntry Address { get; }
        public Guid SessionId { get; }

        public RelayAllocation(DirectAddressBookEntry address, Guid sessionId)
        {
            Address = address;
            SessionId = sessionId;
        }

        private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All,
            Converters = new JsonConverter[] { new IPAddressConverter(), new GuidConverter() }
        };

        public static RelayAllocation Deserialize(string data)
        {
            return JsonConvert.DeserializeObject<RelayAllocation>(data, _jsonSerializerSettings);
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, _jsonSerializerSettings);
        }

        public bool Equals(RelayAllocation other)
        {
            return Address == other.Address && SessionId == other.SessionId;
        }
    }

    public class ExternalAddressBookEntry : AddressBookEntry
    {
        public readonly string RelayJoinCode;

        public ExternalAddressBookEntry(string relayJoinCode, PublicKey publicKey) : base(publicKey, false)
        {
            RelayJoinCode = relayJoinCode;
        }

        public override bool Equals(AddressBookEntry other)
        {
            if (other is ExternalAddressBookEntry external)
            {
                return RelayJoinCode.Equals(external.RelayJoinCode) && PublicKey.Equals(external.PublicKey);
            }

            return false;
        }
    }
}
