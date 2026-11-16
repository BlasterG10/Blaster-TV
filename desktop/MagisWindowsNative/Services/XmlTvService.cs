using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using MagisWindowsNative.Models;

namespace MagisWindowsNative.Services;

public static class XmlTvService
{
    private static readonly string[] OffsetFormats =
    {
        "yyyyMMddHHmmss zzz", "yyyyMMddHHmm zzz",
        "yyyyMMddHHmmss", "yyyyMMddHHmm"
    };
    private static readonly string[] LocalFormats = { "yyyyMMddHHmmss", "yyyyMMddHHmm" };

    // The guide is parsed on a worker thread and streamed element by element, so
    // a very large XMLTV file neither freezes the window nor is held in memory twice.
    public static Task<List<EpgProgram>> LoadAsync(string path, CancellationToken ct = default)
        => Task.Run(() => Load(path, ct), ct);

    private static List<EpgProgram> Load(string path, CancellationToken ct)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        Stream input = file;
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            input = new GZipStream(file, CompressionMode.Decompress, leaveOpen: false);

        var channels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<EpgProgram>();

        using (input)
        using (var reader = XmlReader.Create(input, new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            IgnoreComments = true, IgnoreWhitespace = true
        }))
        {
            reader.MoveToContent();
            if (reader.NodeType != XmlNodeType.Element || reader.IsEmptyElement) return result;
            reader.Read(); // step into the root element

            while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
            {
                ct.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element) { reader.Skip(); continue; }

                if (reader.LocalName == "channel")
                {
                    var channel = (XElement)XNode.ReadFrom(reader);
                    var id = (string?)channel.Attribute("id") ?? "";
                    var display = (string?)channel.Elements("display-name").FirstOrDefault();
                    channels[id] = display ?? id;
                }
                else if (reader.LocalName == "programme")
                {
                    var item = (XElement)XNode.ReadFrom(reader);
                    if (!TryDate((string?)item.Attribute("start"), out var start) ||
                        !TryDate((string?)item.Attribute("stop"), out var stop)) continue;
                    result.Add(new EpgProgram {
                        ChannelId = (string?)item.Attribute("channel") ?? "",
                        Start = start, Stop = stop,
                        Title = (string?)item.Element("title") ?? "(Sin título)",
                        Description = (string?)item.Element("desc") ?? ""
                    });
                }
                else reader.Skip();
            }
        }

        // Channel names may be declared after the programmes, so resolve them last.
        foreach (var program in result)
            program.ChannelName = channels.TryGetValue(program.ChannelId, out var name) ? name : program.ChannelId;

        return result.OrderBy(x => x.Start).ToList();
    }

    private static bool TryDate(string? value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (TryFast(value.AsSpan().Trim(), out var fast))
        {
            date = fast.LocalDateTime;
            return true;
        }
        return TrySlow(value, out date);
    }

    // Hand-rolled parser for the usual "yyyyMMddHHmmss +0000" form; far cheaper than
    // TryParseExact when a guide holds hundreds of thousands of programmes.
    private static bool TryFast(ReadOnlySpan<char> s, out DateTimeOffset result)
    {
        result = default;
        if (s.Length < 12) return false;
        for (var i = 0; i < 12; i++)
            if (!char.IsAsciiDigit(s[i])) return false;

        var year = Two(s, 0) * 100 + Two(s, 2);
        var month = Two(s, 4);
        var day = Two(s, 6);
        var hour = Two(s, 8);
        var minute = Two(s, 10);
        var second = 0;
        var pos = 12;

        if (s.Length >= 14 && char.IsAsciiDigit(s[12]) && char.IsAsciiDigit(s[13]))
        {
            second = Two(s, 12);
            pos = 14;
        }
        else if (s.Length > 12 && char.IsAsciiDigit(s[12])) return false;

        var offset = TimeSpan.Zero;
        var rest = s.Slice(pos).Trim();
        if (rest.Length > 0)
        {
            if (rest.Length != 5 || (rest[0] != '+' && rest[0] != '-')) return false;
            for (var i = 1; i < 5; i++)
                if (!char.IsAsciiDigit(rest[i])) return false;
            var offsetHours = Two(rest, 1);
            var offsetMinutes = Two(rest, 3);
            if (offsetHours > 14 || offsetMinutes > 59) return false;
            offset = new TimeSpan(offsetHours, offsetMinutes, 0);
            if (rest[0] == '-') offset = offset.Negate();
        }

        if (year < 1 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) ||
            hour > 23 || minute > 59 || second > 59) return false;

        try
        {
            result = new DateTimeOffset(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified), offset);
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static int Two(ReadOnlySpan<char> s, int index) => (s[index] - '0') * 10 + (s[index + 1] - '0');

    private static bool TrySlow(string value, out DateTime date)
    {
        date = default;
        var normalized = value.Trim();
        // XMLTV commonly encodes offsets as +0000; .NET's zzz expects +00:00.
        if (normalized.Length >= 5)
        {
            var suffix = normalized[^5..];
            if ((suffix[0] == '+' || suffix[0] == '-') &&
                suffix[1..].All(char.IsDigit))
                normalized = normalized[..^2] + ":" + normalized[^2..];
        }

        if (DateTimeOffset.TryParseExact(normalized, OffsetFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var dto))
        { date = dto.LocalDateTime; return true; }

        if (DateTime.TryParseExact(normalized, LocalFormats,
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date))
        { date = date.ToLocalTime(); return true; }
        return false;
    }
}
