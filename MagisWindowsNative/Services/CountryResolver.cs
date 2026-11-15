namespace MagisWindowsNative.Services;

public static class CountryResolver
{
    private const string Unknown = "Sin país identificado";
    private const int MaxCachedLength = 80;
    private const int MaxCacheEntries = 20000;

    private static readonly (string Name, string[] Tokens)[] Countries =
    {
        ("Argentina", new[] { "ARGENTINA", "ARG" }), ("Bolivia", new[] { "BOLIVIA", "BOL" }),
        ("Brasil", new[] { "BRASIL", "BRAZIL", "BR" }), ("Chile", new[] { "CHILE", "CHI" }),
        ("Colombia", new[] { "COLOMBIA", "COL" }), ("Costa Rica", new[] { "COSTA RICA", "CRI" }),
        ("Cuba", new[] { "CUBA" }), ("Ecuador", new[] { "ECUADOR", "ECU" }),
        ("El Salvador", new[] { "EL SALVADOR", "SLV" }), ("España", new[] { "ESPAÑA", "SPAIN", "ESPANA", "ES" }),
        ("Estados Unidos", new[] { "USA", "US", "UNITED STATES", "ESTADOS UNIDOS", "EEUU", "EE.UU" }),
        ("Guatemala", new[] { "GUATEMALA", "GUA" }), ("Honduras", new[] { "HONDURAS", "HON" }),
        ("México", new[] { "MEXICO", "MÉXICO", "MEX" }), ("Nicaragua", new[] { "NICARAGUA", "NIC" }),
        ("Panamá", new[] { "PANAMA", "PANAMÁ", "PAN" }), ("Paraguay", new[] { "PARAGUAY", "PAR" }),
        ("Perú", new[] { "PERU", "PERÚ", "PER" }), ("Puerto Rico", new[] { "PUERTO RICO", "PRI" }),
        ("República Dominicana", new[] { "REPUBLICA DOMINICANA", "REPÚBLICA DOMINICANA", "DOMINICAN REPUBLIC", "DO" }),
        ("Uruguay", new[] { "URUGUAY", "URU" }), ("Venezuela", new[] { "VENEZUELA", "VEN" }),
        ("Reino Unido", new[] { "UNITED KINGDOM", "UK", "REINO UNIDO", "BRITAIN" })
    };

    // Category names repeat thousands of times in a provider catalog, so remembering
    // the answer avoids re-scanning them. An empty string means "no country found".
    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string Resolve(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var hit = Lookup(value);
            if (hit is not null) return hit;
        }
        return Unknown;
    }

    private static string? Lookup(string value)
    {
        var cacheable = value.Length <= MaxCachedLength;
        if (cacheable)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(value, out var cached))
                    return cached.Length == 0 ? null : cached;
            }
        }

        var found = Find(value.ToUpperInvariant());
        if (cacheable)
        {
            lock (Cache)
            {
                if (Cache.Count < MaxCacheEntries) Cache[value] = found ?? "";
            }
        }
        return found;
    }

    private static string? Find(string upper)
    {
        foreach (var (name, tokens) in Countries)
            foreach (var token in tokens)
                if (ContainsToken(upper, token))
                    return name;
        return null;
    }

    // Same rule as the old regex (?<![A-Z])TOKEN(?![A-Z]): the token must not be
    // glued to another ASCII letter on either side. No Regex objects are built.
    private static bool ContainsToken(string text, string token)
    {
        var start = 0;
        while (start <= text.Length - token.Length)
        {
            var index = text.IndexOf(token, start, StringComparison.Ordinal);
            if (index < 0) return false;
            var end = index + token.Length;
            var leftOk = index == 0 || !IsAsciiUpper(text[index - 1]);
            var rightOk = end >= text.Length || !IsAsciiUpper(text[end]);
            if (leftOk && rightOk) return true;
            start = index + 1;
        }
        return false;
    }

    private static bool IsAsciiUpper(char c) => c >= 'A' && c <= 'Z';
}
