using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

/// <summary>
/// A test-only writer and reader for Steam's binary KeyValues (shortcuts.vdf), written by hand so the
/// tests don't depend on the code under test. Each node is a type byte (0x00 object, 0x01 string,
/// 0x02 int32), a NUL-terminated UTF-8 key and its value; 0x08 ends an object, and one more 0x08 ends the file.
/// </summary>
internal sealed class BinaryVdf
{
    private const byte ObjectType = 0x00;
    private const byte StringType = 0x01;
    private const byte Int32Type = 0x02;
    private const byte End = 0x08;

    private readonly List<byte> _bytes = [];

    /// <summary>A whole file: the root object named <paramref name="rootName"/> plus the closing end byte.</summary>
    public static byte[] Document(string rootName, Action<BinaryVdf> body)
    {
        var writer = new BinaryVdf();
        writer.Object(rootName, body);
        writer._bytes.Add(End);
        return [.. writer._bytes];
    }

    public BinaryVdf Object(string key, Action<BinaryVdf> body)
    {
        Key(ObjectType, key);
        body(this);
        _bytes.Add(End);
        return this;
    }

    public BinaryVdf String(string key, string value)
    {
        Key(StringType, key);
        Text(value);
        return this;
    }

    public BinaryVdf Int(string key, int value)
    {
        Key(Int32Type, key);
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        _bytes.AddRange(buffer.ToArray());
        return this;
    }

    private void Key(byte type, string key)
    {
        _bytes.Add(type);
        Text(key);
    }

    private void Text(string value)
    {
        _bytes.AddRange(Encoding.UTF8.GetBytes(value));
        _bytes.Add(0);
    }

    /// <summary>Parses a whole file into its root node.</summary>
    public static VdfNode Parse(byte[] bytes)
    {
        var position = 0;
        var root = Assert.Single(ReadChildren(bytes, ref position));
        Assert.Equal(End, bytes[position]);
        Assert.Equal(bytes.Length, position + 1);
        return root;
    }

    private static List<VdfNode> ReadChildren(byte[] bytes, ref int position)
    {
        var nodes = new List<VdfNode>();
        while (bytes[position] != End)
        {
            var type = bytes[position++];
            var key = ReadText(bytes, ref position);
            nodes.Add(type switch
            {
                ObjectType => new VdfNode(key, ReadChildren(bytes, ref position)),
                StringType => new VdfNode(key, ReadText(bytes, ref position)),
                Int32Type => new VdfNode(key, ReadInt(bytes, ref position)),
                _ => throw new InvalidDataException($"Unexpected type 0x{type:x2} at {position - 1}"),
            });
        }

        position++;
        return nodes;
    }

    private static string ReadText(byte[] bytes, ref int position)
    {
        var end = Array.IndexOf(bytes, (byte)0, position);
        var text = Encoding.UTF8.GetString(bytes, position, end - position);
        position = end + 1;
        return text;
    }

    private static int ReadInt(byte[] bytes, ref int position)
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position, 4));
        position += 4;
        return value;
    }
}

/// <summary>One parsed node: <see cref="Value"/> is a string, an int, or a list of child nodes.</summary>
internal sealed record VdfNode(string Key, object Value)
{
    public IReadOnlyList<VdfNode> Children => (IReadOnlyList<VdfNode>)Value;

    public IReadOnlyList<string> Keys => [.. Children.Select(child => child.Key)];

    public VdfNode this[string key] => Children.Single(child => child.Key == key);

    /// <summary>Every leaf as "path=value", in file order, for comparing whole subtrees.</summary>
    public IReadOnlyList<string> Flatten() => [.. FlattenUnder("")];

    private IEnumerable<string> FlattenUnder(string prefix) =>
        Value switch
        {
            List<VdfNode> { Count: 0 } => [$"{prefix.TrimEnd('/')}={{}}"],
            List<VdfNode> children => children.SelectMany(child => child.FlattenUnder($"{prefix}{child.Key}/")),
            _ => [$"{prefix.TrimEnd('/')}={Value} ({Value.GetType().Name})"],
        };
}
